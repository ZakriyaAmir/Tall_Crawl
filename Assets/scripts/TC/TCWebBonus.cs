using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// WEB letter collectible + 5000 m invincible web-swing bonus.
/// Letters W, E, B spawn sparsely along the trunk. Collecting all three fires a
/// cinematic upward swing: the spider shoots a web, is immune to hits, and is
/// carried 5000 m in a few seconds. Play then resumes with a short grace period
/// and a clear stretch of trunk ahead.
public class TCWebBonus : MonoBehaviour
{
    public static TCWebBonus Instance { get; private set; }

    public const float DistanceBonusMetres = 5000f;
    public const int SwingCount = 10;
    public const float SwingSeconds = 9f; // 0.9 s per thwip-and-swing cycle
    public const float GraceSeconds = 1.8f;
    // Letter cadence lives in TCTrack (25% every 20s, missed letters retry in 8–12s).
    // The WEB swing is earned at most once per run; after that no more letters spawn.

    public bool HasW { get; private set; }
    public bool HasE { get; private set; }
    public bool HasB { get; private set; }
    public bool AllCollected => HasW && HasE && HasB;
    public bool IsSwinging { get; private set; }
    public bool OwnsMotion { get; private set; }
    /// True once this run has started (or finished) its one WEB swing. No further letters spawn.
    public bool SpentThisRun { get; private set; }
    public bool IsInvincible => IsSwinging || graceLeft > 0f;
    public float ClimbSpeed { get; private set; } // world units / sec during the swing
    public bool ShowcaseHold { get; private set; }

    public static System.Action OnLettersChanged, OnSwingStart, OnSwingEnd;

    float graceLeft;
    float lastLetterMetres = -999f;
    LineRenderer webLine, webGlow;
    Transform webAnchor;
    Coroutine swingCo;
    GameObject letterProto;
    AudioSource whoosh;
    ParticleSystem windTrail;
    float silkAge;
    Vector3 silkAnchor, silkTip;
    bool silkLive;
    readonly List<LineRenderer> fadingSilk = new List<LineRenderer>();
    readonly List<float> fadingAge = new List<float>();
    readonly List<float> fadingLife = new List<float>();
    readonly List<Material> fadingMat = new List<Material>();
    const float TrunkRadius = 0.603f;
    const int SwingBeats = 18;
    const float OrbitPerBeat = 720f / SwingBeats; // 18 swings * 40 deg = 720 deg (2 full swirls, lands at exact start facing)

    Vector3 camPosRest, camWorldOffsetRest;
    Quaternion camRotRest, camWorldRotRest;
    float fovRest, camBaselineY;
    Camera swingCam;
    Animator camAnimRest;
    bool camAnimWasEnabled;
    Transform camParentRest;
    Vector3 camScaleRest;
    bool swingCamActive;
    Transform swingBody;
    Vector3 swingBodyRest;
    readonly List<(TrailRenderer tr, bool enabled, bool emitting)> pausedTrails = new List<(TrailRenderer, bool, bool)>();
    GameObject splatDecal;
    float silkTaut = 1f;

    TextMeshProUGUI swingMeterLabel, swingPopLabel;
    RectTransform swingHudRt, swingPopRt;
    CanvasGroup swingHudCg, swingPopCg;
    TrailRenderer bodyTrail;
    float ghostTimer;
    readonly List<GameObject> ghosts = new List<GameObject>();
    readonly List<float> ghostAge = new List<float>();
    const float GhostLife = 0.22f;
    int currentThwip;
    float metresAtSwingStart;
    Texture2D silkShimmerTex;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (graceLeft > 0f) graceLeft = Mathf.Max(0f, graceLeft - Time.deltaTime);
        if (IsSwinging && silkLive)
            DrawSilk(silkTip, silkAnchor, silkTaut, silkAge);
        if (IsSwinging) silkAge += Time.deltaTime;
        TickFadingSilk();
        TickGhosts();
        if (IsSwinging) UpdateSwingHud();
    }

    void LateUpdate()
    {
        // Drive the cam after all motion so a screenshot at end-of-frame
        // cannot land between a transform write and the previous cam update.
        if (IsSwinging && swingCamActive)
            DriveSwingCam(0f);
    }

    Transform ResolveSpider()
    {
        var p = player.Instance;
        if (p && p.p1) return p.GetComponentInChildren<playerInteract>() ? p.GetComponentInChildren<playerInteract>().transform : p.p1;
        var sp = FindObjectOfType<playerInteract>();
        return sp ? sp.transform : null;
    }

    public bool HasLetter(char c)
    {
        c = char.ToUpperInvariant(c);
        return c == 'W' ? HasW : c == 'E' ? HasE : c == 'B' && HasB;
    }

    public bool CanSpawnLetter(float metres)
    {
        if (SpentThisRun || IsSwinging || AllCollected) return false;
        return true;
    }

    public char NextLetterToSpawn()
    {
        if (!HasW) return 'W';
        if (!HasE) return 'E';
        return 'B';
    }

    public void MarkLetterSpawned(float metres)
    {
        lastLetterMetres = metres;
    }

    public void CollectLetter(char c, Vector3 pos)
    {
        c = char.ToUpperInvariant(c);
        bool changed = false;
        if (c == 'W' && !HasW) { HasW = true; changed = true; }
        else if (c == 'E' && !HasE) { HasE = true; changed = true; }
        else if (c == 'B' && !HasB) { HasB = true; changed = true; }
        if (!changed) return;
        TCSfx.Play("coin", 0.9f);
        TCFx.Shake(0.08f);
        TCPickupFX.Burst(TCPickupKind.Letter, pos);
        OnLettersChanged?.Invoke();
        if (AllCollected && !IsSwinging) BeginSwing();
    }

    public void ResetLetters()
    {
        HasW = HasE = HasB = false;
        lastLetterMetres = -999f;
        OnLettersChanged?.Invoke();
    }

    public void BeginSwing()
    {
        if (IsSwinging) return;
        if (swingCo != null) StopCoroutine(swingCo);
        swingCo = StartCoroutine(SwingRoutine());
    }

    IEnumerator SwingRoutine()
    {
        IsSwinging = true;
        OwnsMotion = true;
        SpentThisRun = true;
        currentThwip = 0;
        ShowcaseHold = false;
        OnSwingStart?.Invoke();
        var pl = player.Instance;
        var sp = ResolveSpider();
        var cute = ResolveCute(sp);
        if (pl) pl.ResetInputState();
        metresAtSwingStart = pl ? pl.currentScore : 0f;

        float mpuEarly = TCConfig.I != null ? Mathf.Max(0.01f, TCConfig.I.metresPerUnit) : 20f;
        float clearWorld = DistanceBonusMetres / mpuEarly + 40f;
        TCTrack.Instance?.ClearHazardsAhead(clearWorld);
        TCTrack.Instance?.ReserveClearRun(DistanceBonusMetres + 600f);

        EnsureWeb();
        EnsureSwingHud();
        TCFx.Shake(0.18f);
        TCSfx.Play("celebrate", 0.85f);

        BindSwingBody(sp);
        PauseExistingTrails(sp);

        var cam = Camera.main;
        swingCam = cam;
        camParentRest = cam ? cam.transform.parent : null;
        camPosRest = cam ? cam.transform.localPosition : Vector3.zero;
        camRotRest = cam ? cam.transform.localRotation : Quaternion.identity;
        camScaleRest = cam ? cam.transform.localScale : Vector3.one;
        fovRest = cam ? cam.fieldOfView : 64f;

        float mpu = TCConfig.I != null ? Mathf.Max(0.01f, TCConfig.I.metresPerUnit) : 20f;
        float worldUnits = DistanceBonusMetres / mpu;
        Vector3 axis = pl && pl.p1 ? pl.p1.position : Vector3.zero;
        axis.y = 0f;
        float y0 = pl && pl.p1 ? pl.p1.position.y : (sp ? sp.position.y : 0f);
        float yEnd = y0 + worldUnits;
        float yaw = pl ? pl.CurrentYaw : (sp ? sp.eulerAngles.y : 0f);

        // Unparent Main Camera during the swing so p1's 360-degree trunk orbit does not
        // spin the camera, while preserving the EXACT original camera angle, offset, and FOV.
        if (cam)
        {
            camAnimRest = cam.GetComponent<Animator>();
            camAnimWasEnabled = camAnimRest && camAnimRest.enabled;
            if (camAnimRest) camAnimRest.enabled = false;
            camWorldRotRest = cam.transform.rotation;
            camBaselineY = y0;
            camWorldOffsetRest = cam.transform.position - new Vector3(axis.x, y0, axis.z);
            cam.transform.SetParent(null, true);
            swingCamActive = true;
            ApplyPendulumRadius(0f, 0f);
            DriveSwingCam(0f);
        }

        BindBodyTrail(sp);
        if (bodyTrail) { bodyTrail.Clear(); bodyTrail.emitting = true; }
        var trail = EnsureWind(sp);
        if (trail) { trail.Clear(); trail.Play(true); }

        const int Beats = SwingBeats;
        float beatCycle = SwingSeconds / Beats;
        float yPrev = y0;
        float yaw0 = yaw;
        float totalElapsed = 0f;
        float totalDur = SwingSeconds;

        for (int beat = 0; beat < Beats; beat++)
        {
            bool last = beat == Beats - 1;
            currentThwip = Mathf.Clamp(Mathf.CeilToInt((beat + 1) * (SwingCount / (float)Beats)), 1, SwingCount);
            float yawStart = yaw0 + beat * OrbitPerBeat;
            float beatDur = last ? beatCycle * 1.15f : beatCycle * 0.98f;
            float baseFrac0 = beat / (float)Beats;
            float baseFrac1 = (beat + 1) / (float)Beats;

            const float shootFrac = 0.14f;
            const float releaseFrac = 0.56f;
            float releaseCut = last ? 0.64f : releaseFrac;

            // Pin the web splat genuinely in world space on the upper bark ahead of the spider.
            // Because the world bark moves naturally past the camera at ~28 wu/s, the splat and
            // taut web sweep realistically from top of screen past the spider before releasing.
            float shootTargetYaw = yawStart + OrbitPerBeat * 0.62f + 16f;
            Vector3 anchorWorld = OnTrunk(axis, shootTargetYaw, camBaselineY + 2.25f, TrunkRadius + 0.03f);
            bool splatSpawned = false;
            bool whooshPlayed = false;
            bool released = false;
            bool doCorkscrew = !last && (beat % 2 == 1);

            silkLive = true;
            silkAge = 0f;
            PlayThwip(beat);
            if (beat == 2 || beat == 11) TCSfx.PlayVoice(false, 0.62f, 1.04f + (beat % 4) * 0.03f);

            float t = 0f;
            while (t < beatDur)
            {
                float dt = Time.deltaTime;
                t += dt;
                totalElapsed += dt;
                float u = Mathf.Clamp01(t / beatDur);
                float masterU = Mathf.Clamp01(totalElapsed / Mathf.Max(0.01f, totalDur));

                // Smoothly swing out from trunk at launch and land flush onto bark at 5000m.
                float launchEnv = Smooth(Mathf.Clamp01(masterU / 0.055f));
                float landEnv = last ? 1f - Smooth(Mathf.Clamp01((u - 0.66f) / 0.34f)) : 1f;
                float radialEnv = launchEnv * landEnv;

                // Continuous orbital swirl with a pendulum whip surge during web pull.
                float whipSin = Mathf.Sin(2f * Mathf.PI * u);
                float yawEase = u + 0.045f * whipSin;
                yaw = yawStart + OrbitPerBeat * yawEase;
                float relYaw = yaw - yaw0;

                // Smooth climb baseline + organic pendulum arc dip under the taut web & soar on release.
                float climbFrac = Mathf.Lerp(baseFrac0, baseFrac1, u);
                camBaselineY = Mathf.Lerp(y0, yEnd, climbFrac);
                float pullArc = -0.20f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u / releaseCut));
                float soarArc = 0.16f * Mathf.Sin(Mathf.PI * u);
                float y = camBaselineY + (pullArc + soarArc) * radialEnv;
                if (last && u >= 0.84f) y = Mathf.Lerp(y, yEnd, Smooth((u - 0.84f) / 0.16f));

                // Keep a wide, dramatic distance around the tree trunk while staying cleanly framed.
                float orbitExtraZ = SwingOrbitExtraZ(relYaw, u) * radialEnv;
                float orbitExtraY = (0.26f + 0.10f * Mathf.Sin(Mathf.PI * u)) * radialEnv;

                if (u >= shootFrac && !splatSpawned)
                {
                    splatSpawned = true;
                    silkAnchor = anchorWorld;
                    if (webAnchor) webAnchor.position = silkAnchor;
                    SpawnAnchorBurst(anchorWorld, axis);
                }
                if (u >= shootFrac && !whooshPlayed)
                {
                    whooshPlayed = true;
                    PlayWhoosh(beat);
                }
                if (u >= releaseCut && !released)
                {
                    released = true;
                    LingerCurrentSilk();
                    silkLive = false;
                    SetSilkVisible(false);
                    if (last)
                    {
                        TCSfx.Play("celebrate", 1f);
                        TCSfx.PlayVoice(false, 0.7f, 1.12f);
                    }
                }

                // Cohesive hero pose: reach up-left -> banked pull -> aerial corkscrew/glide.
                // On __cute: -lean pitches head outward away from trunk; -twist aims head into +yaw swirl.
                if (cute)
                {
                    if (u < shootFrac)
                    {
                        float k = Smooth(u / shootFrac);
                        cute.SetSwingPose(-24f, Mathf.Lerp(10f, -14f, k), -32f, 0.12f * k, -0.95f);
                    }
                    else if (u < releaseCut)
                    {
                        float su = (u - shootFrac) / Mathf.Max(0.01f, releaseCut - shootFrac);
                        float arc = Mathf.Sin(Mathf.PI * su);
                        float lean = -24f - 16f * arc;
                        float roll = -14f - 24f * arc;
                        float twist = -34f - 8f * arc;
                        float squash = 0.06f - 0.15f * arc;
                        cute.SetSwingPose(lean, roll, twist, squash, 0.85f);
                    }
                    else
                    {
                        float fu = (u - releaseCut) / Mathf.Max(0.01f, 1f - releaseCut);
                        float sf = Smooth(fu);
                        if (last)
                        {
                            float lean = Mathf.Lerp(-24f, 0f, sf);
                            float roll = Mathf.Lerp(-16f, 0f, sf);
                            float twist = Mathf.Lerp(-34f, -360f, sf);
                            float tuck = Mathf.Lerp(-0.7f, 0f, sf);
                            cute.SetSwingPose(lean, roll, twist, -0.10f * Mathf.Sin(fu * Mathf.PI), tuck);
                        }
                        else if (doCorkscrew)
                        {
                            float lean = Mathf.Lerp(-22f, -24f, fu);
                            float roll = 18f * Mathf.Sin(fu * Mathf.PI);
                            float twist = Mathf.Lerp(-34f, -394f, sf);
                            cute.SetSwingPose(lean, roll, twist, -0.14f * Mathf.Sin(fu * Mathf.PI), -0.78f);
                        }
                        else
                        {
                            float lean = Mathf.Lerp(-24f, -24f, fu);
                            float roll = Mathf.Lerp(-14f, 12f, Mathf.Sin(fu * Mathf.PI));
                            float twist = Mathf.Lerp(-34f, -32f, sf);
                            float tuck = Mathf.Lerp(0.45f, -0.90f, sf);
                            cute.SetSwingPose(lean, roll, twist, -0.08f * Mathf.Sin(fu * Mathf.PI), tuck);
                        }
                    }
                }

                TickSwing(pl, y, yaw, orbitExtraZ, orbitExtraY, ref yPrev);

                if (silkLive && u < releaseCut)
                {
                    silkTip = FrontWebOrigin(cute, sp);
                    if (u < shootFrac)
                    {
                        float ext = Smooth(u / shootFrac);
                        silkAnchor = Vector3.Lerp(silkTip, anchorWorld, ext);
                        if (webAnchor) webAnchor.position = silkAnchor;
                        DrawSilk(silkTip, silkAnchor, 0.35f + 0.35f * ext, silkAge);
                    }
                    else
                    {
                        silkAnchor = anchorWorld;
                        if (webAnchor) webAnchor.position = silkAnchor;
                        DrawSilk(silkTip, silkAnchor, 1f, silkAge);
                    }
                }
                yield return null;
            }

            yaw = yaw0 + (beat + 1) * OrbitPerBeat;
            if (last)
            {
                camBaselineY = yEnd;
                TickSwing(pl, yEnd, yaw, 0f, 0f, ref yPrev);
            }
        }

        if (trail)
        {
            trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(trail.gameObject);
            windTrail = null;
        }
        if (bodyTrail)
        {
            bodyTrail.emitting = false;
            bodyTrail.Clear();
            Destroy(bodyTrail.gameObject);
            bodyTrail = null;
        }
        if (cute) { cute.ClearSwingPose(); cute.TriggerLandBounce(); }
        SpawnLandingBurst(sp ? sp.position : Vector3.zero);
        ShowFiveThousandPop();
        TCSfx.Play("chime", 0.9f, 1.05f);
        TCSfx.Play("celebrate", 1f);

        // Brief settle while +5000m pop appears, keeping the exact default camera untouched.
        ShowcaseHold = true;
        float hold = 0f;
        while (hold < 0.30f)
        {
            hold += Time.unscaledDeltaTime;
            ApplyPendulumRadius(0f, 0f);
            DriveSwingCam(0f);
            yield return null;
        }
        ShowcaseHold = false;
        LingerCurrentSilk();
        silkLive = false;
        SetSilkVisible(false);

        RestoreSwingBody();
        RestorePausedTrails();
        if (splatDecal) { Destroy(splatDecal); splatDecal = null; }

        if (cam && swingCamActive)
        {
            cam.transform.SetParent(camParentRest, true);
            cam.transform.localScale = camScaleRest;
            swingCamActive = false;
            if (camAnimRest) camAnimRest.enabled = camAnimWasEnabled;
            cam.transform.localPosition = camPosRest;
            cam.transform.localRotation = camRotRest;
            cam.fieldOfView = fovRest;
            cam.transform.localScale = camScaleRest;
        }

        HideSwingHud();

        ClimbSpeed = 0f;
        OwnsMotion = false;
        IsSwinging = false;

        TCTrack.Instance?.ClearHazardsAhead(12f);
        TCTrack.Instance?.ReserveClearRun(400f);
        graceLeft = GraceSeconds;
        if (pl) pl.ResetInputState();

        var pi = FindObjectOfType<playerInteract>();
        if (pi) pi.GrantGrace(GraceSeconds);

        ResetLetters();
        OnSwingEnd?.Invoke();
        swingCo = null;
    }

    float SwingOrbitExtraZ(float relYawDeg, float swingU)
    {
        float side = Mathf.Abs(Mathf.Sin(relYawDeg * Mathf.Deg2Rad));
        float baseExtra = Mathf.Lerp(0.76f, 0.30f, Mathf.Pow(side, 1.35f));
        return baseExtra + 0.09f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(swingU));
    }

    void BindSwingBody(Transform sp)
    {
        swingBody = sp;
        swingBodyRest = swingBody ? swingBody.localPosition : Vector3.zero;
    }

    void ApplyPendulumRadius(float extraZ, float extraY = 0f)
    {
        if (!swingBody) return;
        var lp = swingBodyRest;
        lp.z += extraZ;
        lp.y += extraY;
        swingBody.localPosition = lp;
    }

    void RestoreSwingBody()
    {
        if (swingBody) swingBody.localPosition = swingBodyRest;
    }

    void PauseExistingTrails(Transform sp)
    {
        pausedTrails.Clear();
        var roots = new List<Transform>();
        if (sp) roots.Add(sp);
        var pl = player.Instance;
        if (pl)
        {
            if (pl.p1) roots.Add(pl.p1);
            roots.Add(pl.transform);
        }
        var cute = ResolveCute(sp);
        if (cute) roots.Add(cute.transform);
        var seen = new HashSet<TrailRenderer>();
        foreach (var root in roots)
        {
            if (!root) continue;
            foreach (var tr in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (!tr || !seen.Add(tr) || tr == bodyTrail) continue;
                pausedTrails.Add((tr, tr.enabled, tr.emitting));
                tr.Clear();
                tr.emitting = false;
                tr.enabled = false;
            }
        }
    }

    void RestorePausedTrails()
    {
        for (int i = 0; i < pausedTrails.Count; i++)
        {
            var entry = pausedTrails[i];
            if (!entry.tr) continue;
            entry.tr.Clear();
            entry.tr.enabled = entry.enabled;
            entry.tr.emitting = entry.emitting;
        }
        pausedTrails.Clear();
    }

    void SetSilkVisible(bool on)
    {
        if (webLine) webLine.enabled = on;
        if (webGlow) webGlow.enabled = on;
    }

    void TickSwing(player pl, float y, float yaw, float extraZ, float extraY, ref float yPrev)
    {
        if (pl && pl.p1)
        {
            var pos = pl.p1.position; pos.y = y;
            pl.ApplySwingTransform(pos, yaw);
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            ClimbSpeed = (y - yPrev) / dt;
            yPrev = y;
        }
        ApplyPendulumRadius(extraZ, extraY);
        DriveSwingCam(0f);
    }

    Vector3 VisualSpiderPos()
    {
        var cute = ResolveCute(swingBody);
        if (cute) return cute.VisualCenter();
        if (swingBody) return swingBody.position;
        var sp = ResolveSpider();
        return sp ? sp.position : Vector3.zero;
    }

    void DriveSwingCam(float dutch, float blend = 1f, Vector3 fromPos = default, Quaternion fromRot = default)
    {
        var cam = swingCam ? swingCam : Camera.main;
        if (!cam || !swingCamActive) return;

        var pl = player.Instance;
        Vector3 spiderPos = VisualSpiderPos();
        Vector3 axis = pl && pl.p1 ? pl.p1.position : spiderPos;

        // Preserve the exact gameplay camera angle, offset from the trunk, and FOV.
        cam.transform.position = new Vector3(axis.x, camBaselineY, axis.z) + camWorldOffsetRest;
        cam.transform.rotation = camWorldRotRest;
        cam.fieldOfView = fovRest;
    }


    static Vector3 OnTrunk(Vector3 axis, float yawDeg, float y, float radius)
    {
        float a = yawDeg * Mathf.Deg2Rad;
        return new Vector3(axis.x + Mathf.Sin(a) * radius, y, axis.z + Mathf.Cos(a) * radius);
    }

    void PlayThwip(int shot)
    {
        float pitch = 0.90f + (shot % 4) * 0.07f;
        TCSfx.Play("thwip", 1f, pitch);
    }

    void PlayWhoosh(int shot)
    {
        float pitch = 0.88f + (shot % 3) * 0.08f;
        TCSfx.Play("whoosh", 0.8f, pitch);
        if (whoosh && TCAssets.I && TCAssets.I.whoosh) whoosh.PlayOneShot(TCAssets.I.whoosh, 0.7f);
    }

    Vector3 lingerTip, lingerAnchor;

    void LingerCurrentSilk()
    {
        if (!webLine || !webLine.enabled) return;
        Vector3 dir = lingerAnchor - lingerTip;
        if (dir.sqrMagnitude < 0.005f) return;

        var go = new GameObject("TCSilkLinger");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, lingerTip);
        lr.SetPosition(1, lingerAnchor);
        lr.startWidth = 0.042f;
        lr.endWidth = 0.026f;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.numCapVertices = 3;
        var mat = MakeSilkMat(new Color(0.92f, 0.97f, 1f, 0.65f), true, true);
        if (silkShimmerTex) mat.mainTexture = silkShimmerTex;
        lr.sharedMaterial = mat;
        lr.startColor = new Color(0.94f, 0.98f, 1f, 0.65f);
        lr.endColor = new Color(0.75f, 0.92f, 1f, 0.35f);

        fadingSilk.Add(lr);
        fadingAge.Add(0f);
        fadingLife.Add(0.26f);
        fadingMat.Add(mat);
    }

    void ClearFadingSilk()
    {
        for (int i = 0; i < fadingSilk.Count; i++)
            if (fadingSilk[i]) Destroy(fadingSilk[i].gameObject);
        fadingSilk.Clear();
        fadingAge.Clear();
        fadingLife.Clear();
        fadingMat.Clear();
    }

    void TickFadingSilk()
    {
        for (int i = fadingSilk.Count - 1; i >= 0; i--)
        {
            fadingAge[i] += Time.deltaTime;
            float u = Mathf.Clamp01(fadingAge[i] / Mathf.Max(0.01f, fadingLife[i]));
            float a = 1f - u;
            a *= a;
            var lr = fadingSilk[i];
            if (lr)
            {
                if (fadingMat[i])
                    SetMatColor(fadingMat[i], new Color(0.94f, 0.97f, 1f, 0.55f * a));
                lr.startColor = new Color(0.94f, 0.97f, 1f, 0.55f * a);
                lr.endColor = new Color(0.85f, 0.94f, 1f, 0.30f * a);
            }
            if (fadingAge[i] >= fadingLife[i])
            {
                if (fadingSilk[i]) Destroy(fadingSilk[i].gameObject);
                fadingSilk.RemoveAt(i);
                fadingAge.RemoveAt(i);
                fadingLife.RemoveAt(i);
                fadingMat.RemoveAt(i);
            }
        }
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    TCCuteSpider ResolveCute(Transform sp)
    {
        if (sp)
        {
            var c = sp.GetComponentInParent<TCCuteSpider>() ?? sp.GetComponentInChildren<TCCuteSpider>();
            if (c) return c;
        }
        var pl = player.Instance;
        if (pl) return pl.GetComponentInChildren<TCCuteSpider>();
        return FindObjectOfType<TCCuteSpider>();
    }

    static Vector3 FrontWebOrigin(TCCuteSpider cute, Transform sp)
    {
        if (cute) return cute.WebOrigin();
        if (sp) return sp.position + Vector3.up * 0.10f;
        return Vector3.zero;
    }

    void DrawSilk(Vector3 tip, Vector3 anchor, float taut, float age)
    {
        EnsureWeb();
        Vector3 dir = anchor - tip;
        float len = dir.magnitude;
        if (len < 0.04f)
        {
            SetSilkVisible(false);
            return;
        }
        lingerTip = tip;
        lingerAnchor = anchor;
        silkTaut = taut;

        // Subtle whip-wave while shooting out; snaps dead taut under swing tension.
        var cam = swingCam ? swingCam : Camera.main;
        Vector3 camFwd = cam ? cam.transform.forward : Vector3.forward;
        Vector3 side = Vector3.Cross(dir / len, camFwd);
        if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
        side.Normalize();
        float whipAmp = (1f - Mathf.Clamp01(taut)) * Mathf.Min(0.14f, len * 0.09f);

        const int Segs = 10;
        if (webLine.positionCount != Segs) webLine.positionCount = Segs;
        if (webGlow.positionCount != Segs) webGlow.positionCount = Segs;
        for (int i = 0; i < Segs; i++)
        {
            float s = i / (float)(Segs - 1);
            Vector3 p = Vector3.Lerp(tip, anchor, s);
            if (whipAmp > 1e-4f)
                p += side * (Mathf.Sin(Mathf.PI * s) * Mathf.Sin(s * Mathf.PI * 2f - age * 42f) * whipAmp);
            webLine.SetPosition(i, p);
            webGlow.SetPosition(i, p);
        }
        if (webLine.material && silkShimmerTex)
            webLine.material.mainTextureOffset = new Vector2(-age * 4.5f, 0f);

        webLine.enabled = true;
        webGlow.enabled = true;
        TickSplat(anchor);
    }

    ParticleSystem EnsureWind(Transform sp)
    {
        if (windTrail) return windTrail;
        if (!sp) return null;
        var go = new GameObject("SwingWind");
        go.transform.SetParent(sp, false);
        go.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        windTrail = go.AddComponent<ParticleSystem>();
        var main = windTrail.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new Color(0.88f, 0.96f, 1f, 0.85f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = windTrail.emission;
        em.rateOverTime = 55f;
        var sh = windTrail.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.12f;
        var col = windTrail.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.65f, 0.88f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sol = windTrail.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (rend)
        {
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.06f;
            rend.lengthScale = 2.6f;
            rend.sharedMaterial = TCFxLib.Mat("Spark");
        }
        windTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return windTrail;
    }

    void BindBodyTrail(Transform sp)
    {
        if (bodyTrail || !sp) return;
        var host = sp;
        var cute = ResolveCute(sp);
        if (cute) host = cute.transform;
        var go = new GameObject("TCSwingRibbon");
        go.transform.SetParent(host, false);
        go.transform.localPosition = Vector3.zero;
        bodyTrail = go.AddComponent<TrailRenderer>();
        bodyTrail.time = 0.34f;
        bodyTrail.minVertexDistance = 0.04f;
        bodyTrail.startWidth = 0.18f;
        bodyTrail.endWidth = 0.01f;
        bodyTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bodyTrail.receiveShadows = false;
        var tmat = MakeSilkMat(new Color(0.65f, 0.92f, 1f, 0.65f), true, true);
        if (silkShimmerTex) tmat.mainTexture = silkShimmerTex;
        bodyTrail.material = tmat;
        bodyTrail.startColor = new Color(0.85f, 0.97f, 1f, 0.72f);
        bodyTrail.endColor = new Color(0.45f, 0.82f, 1f, 0f);
        bodyTrail.emitting = false;
        bodyTrail.Clear();
    }

    void TickGhostClock(Transform src)
    {
        if (!src) return;
        ghostTimer += Time.deltaTime;
        if (ghostTimer < 0.05f) return;
        ghostTimer = 0f;
        SpawnGhost(src);
    }

    void SpawnGhost(Transform src)
    {
        if (!src) return;
        var go = new GameObject("SwingGhost");
        go.transform.position = src.position;
        go.transform.rotation = src.rotation;
        var ghostMat = MakeSilkMat(new Color(0.65f, 0.9f, 1f, 0.35f), true, true);
        foreach (var mf in src.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf || !mf.sharedMesh) continue;
            var child = new GameObject("g");
            child.transform.SetParent(go.transform, false);
            child.transform.position = mf.transform.position;
            child.transform.rotation = mf.transform.rotation;
            child.transform.localScale = mf.transform.lossyScale;
            var cmf = child.AddComponent<MeshFilter>();
            cmf.sharedMesh = mf.sharedMesh;
            var mr = child.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ghostMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        ghosts.Add(go);
        ghostAge.Add(0f);
        if (ghosts.Count > 8)
        {
            if (ghosts[0]) Destroy(ghosts[0]);
            ghosts.RemoveAt(0);
            ghostAge.RemoveAt(0);
        }
    }

    void TickGhosts()
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            ghostAge[i] += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(ghostAge[i] / GhostLife);
            if (ghosts[i])
            {
                float s = 1f - 0.15f * (1f - a);
                ghosts[i].transform.localScale = Vector3.one * s;
                foreach (var r in ghosts[i].GetComponentsInChildren<MeshRenderer>())
                    if (r && r.sharedMaterial) r.sharedMaterial.color = new Color(0.65f, 0.9f, 1f, 0.35f * a * a);
            }
            if (ghostAge[i] >= GhostLife)
            {
                if (ghosts[i]) Destroy(ghosts[i]);
                ghosts.RemoveAt(i);
                ghostAge.RemoveAt(i);
            }
        }
    }

    void SpawnAnchorBurst(Vector3 pos, Vector3 axis)
    {
        // Small radial web decal + a few soft particles. No LineRenderer bars.
        Vector3 normal = pos - new Vector3(axis.x, pos.y, axis.z);
        if (normal.sqrMagnitude < 1e-6f) normal = Vector3.forward;
        normal.Normalize();
        if (splatDecal) Destroy(splatDecal);
        splatDecal = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var col = splatDecal.GetComponent<Collider>();
        if (col) Destroy(col);
        splatDecal.name = "WebSplat";
        splatDecal.transform.position = pos + normal * 0.02f;
        splatDecal.transform.rotation = Quaternion.LookRotation(normal);
        splatDecal.transform.localScale = Vector3.one * 0.52f;
        var mr = splatDecal.GetComponent<MeshRenderer>();
        if (mr)
        {
            var mat = MakeSilkMat(new Color(0.95f, 0.97f, 1f, 0.85f), false, true);
            mat.mainTexture = MakeWebDecalTex();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mat.mainTexture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mat.mainTexture);
            mat.renderQueue = 2450;
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        StartCoroutine(FadeSplat(splatDecal, 1.15f));
        BurstParticles(pos, normal, false);
    }

    void TickSplat(Vector3 pos)
    {
        if (!splatDecal) return;
        var pl = player.Instance;
        Vector3 axis = pl && pl.p1 ? pl.p1.position : pos;
        Vector3 normal = pos - new Vector3(axis.x, pos.y, axis.z);
        if (normal.sqrMagnitude < 1e-6f) normal = Vector3.forward;
        normal.Normalize();
        splatDecal.transform.position = pos + normal * 0.03f;
        splatDecal.transform.rotation = Quaternion.LookRotation(normal);
    }

    IEnumerator FadeSplat(GameObject go, float life)
    {
        if (!go) yield break;
        var mr = go.GetComponent<MeshRenderer>();
        var mat = mr ? mr.material : null;
        float t = 0f;
        while (t < life && go)
        {
            t += Time.deltaTime;
            float a = 1f - Smooth(t / life);
            if (mat)
                SetMatColor(mat, new Color(0.95f, 0.97f, 1f, 0.85f * a));
            yield return null;
        }
        if (go) Destroy(go);
        if (splatDecal == go) splatDecal = null;
    }

    static Texture2D MakeWebDecalTex()
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color[N * N];
        float cx = (N - 1) * 0.5f, cy = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float dx = x - cx, dy = y - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy) / (N * 0.5f);
            float ang = Mathf.Atan2(dy, dx);
            float a = 0f;
            // a few radial threads
            for (int i = 0; i < 8; i++)
            {
                float t = i * Mathf.PI / 4f;
                float ad = Mathf.Abs(Mathf.Sin(ang - t));
                a = Mathf.Max(a, Mathf.Clamp01(1f - ad * 28f) * Mathf.Clamp01(1.05f - r) * 0.85f);
            }
            // two rings
            a = Mathf.Max(a, Mathf.Clamp01(1f - Mathf.Abs(r - 0.38f) * 22f) * 0.7f);
            a = Mathf.Max(a, Mathf.Clamp01(1f - Mathf.Abs(r - 0.68f) * 22f) * 0.55f);
            a *= Mathf.Clamp01(1.05f - r);
            px[y * N + x] = new Color(0.95f, 0.97f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }

    void SpawnLandingBurst(Vector3 pos)
    {
        BurstParticles(pos + Vector3.up * 0.2f, Vector3.up, true);
    }

    void BurstParticles(Vector3 pos, Vector3 normal, bool celebrate)
    {
        var go = new GameObject(celebrate ? "SwingCelebrate" : "AnchorBurst");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.duration = 0.4f;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(celebrate ? 1.6f : 0.8f, celebrate ? 4.2f : 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, celebrate ? 0.06f : 0.035f);
        main.startColor = celebrate ? new Color(1f, 0.85f, 0.25f, 1f) : new Color(0.92f, 0.97f, 1f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = celebrate ? 0.35f : 0.8f;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(celebrate ? 48 : 10)) });
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = celebrate ? 50f : 35f;
        sh.radius = 0.05f;
        sh.rotation = Quaternion.LookRotation(normal).eulerAngles;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        if (celebrate)
        {
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        }
        else
        {
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.85f, 1f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.6f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
        }
        col.color = g;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (rend)
        {
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            var pmat = MakeSilkMat(Color.white, true, true);
            pmat.mainTexture = SoftParticleTex();
            if (pmat.HasProperty("_BaseMap")) pmat.SetTexture("_BaseMap", SoftParticleTex());
            rend.material = pmat;
        }
        ps.Play(true);
        Object.Destroy(go, 2.2f);
    }

    static Texture2D _softTex;
    static Texture2D SoftParticleTex()
    {
        if (_softTex) return _softTex;
        const int N = 32;
        _softTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[N * N];
        float c = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / (c * 0.92f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        _softTex.SetPixels(px);
        _softTex.Apply(false, false);
        return _softTex;
    }

    Canvas MakeOverlay(string name, int sort)
    {
        var canvasGo = new GameObject(name);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sort;
        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>().enabled = false;
        return canvas;
    }

    void EnsureSwingHud()
    {
        if (swingHudRt) { if (swingHudCg) swingHudCg.alpha = 1f; return; }
        var overlay = MakeOverlay("TCWebSwingCanvas", 90);
        var parent = overlay.transform;
        var bg = TCUI.Img("WebSwingMeter", parent, new Color(0.08f, 0.10f, 0.16f, 0.62f));
        swingHudRt = bg.rectTransform;
        TCUI.Place(swingHudRt, new Vector2(0.5f, 0.88f), Vector2.zero, new Vector2(620, 92));
        swingHudCg = swingHudRt.gameObject.AddComponent<CanvasGroup>();
        swingHudCg.blocksRaycasts = false;
        swingMeterLabel = TCUI.Label("Text", swingHudRt, "THWIP 0 / 10", 44, Color.white);
        TCUI.Stretch(swingMeterLabel.rectTransform, 16, 16, 6, 6);
        TCUI.Outline(swingMeterLabel, 0.28f);
        swingMeterLabel.enableAutoSizing = true;
        swingMeterLabel.fontSizeMax = 44;
        swingMeterLabel.fontSizeMin = 24;

        var pop = TCUI.Img("WebSwingPop", parent, new Color(1f, 1f, 1f, 0f));
        swingPopRt = pop.rectTransform;
        TCUI.Place(swingPopRt, new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(820, 200));
        swingPopCg = swingPopRt.gameObject.AddComponent<CanvasGroup>();
        swingPopCg.alpha = 0f;
        swingPopCg.blocksRaycasts = false;
        swingPopLabel = TCUI.Label("Text", swingPopRt, "+5000m", 96, new Color(1f, 0.9f, 0.25f));
        TCUI.Stretch(swingPopLabel.rectTransform, 10, 10, 10, 10);
        TCUI.Outline(swingPopLabel, 0.4f);
        swingPopLabel.enableAutoSizing = true;
        swingPopLabel.fontSizeMax = 110;
        swingPopLabel.fontSizeMin = 48;
    }

    void UpdateSwingHud()
    {
        if (!swingMeterLabel) return;
        var pl = player.Instance;
        int gained = pl ? Mathf.Max(0, Mathf.RoundToInt(pl.currentScore - metresAtSwingStart)) : 0;
        swingMeterLabel.text = "THWIP  " + currentThwip + " / " + SwingCount + "   +" + gained + "m";
    }

    void HideSwingHud()
    {
        if (swingHudCg) swingHudCg.alpha = 0f;
    }

    void ShowFiveThousandPop()
    {
        if (!swingPopCg || !swingPopRt) return;
        if (swingPopLabel) swingPopLabel.text = "+5000m";
        StartCoroutine(PopFiveThousand());
    }

    IEnumerator PopFiveThousand()
    {
        swingPopCg.alpha = 1f;
        swingPopRt.localScale = Vector3.one * 0.4f;
        float t = 0f;
        while (t < 1.15f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 1.15f);
            float punch = k < 0.18f ? Smooth(k / 0.18f) * 1.2f : Mathf.Lerp(1.2f, 1f, Smooth(Mathf.Clamp01((k - 0.18f) / 0.18f)));
            swingPopRt.localScale = Vector3.one * punch;
            if (k > 0.72f) swingPopCg.alpha = 1f - Smooth((k - 0.72f) / 0.28f);
            yield return null;
        }
        swingPopCg.alpha = 0f;
    }

    void EnsureWeb()
    {
        if (webLine) return;
        silkShimmerTex = MakeShimmerTex();
        var go = new GameObject("TCWebStrand");
        go.transform.SetParent(transform, false);
        webLine = go.AddComponent<LineRenderer>();
        webLine.positionCount = 10;
        webLine.startWidth = 0.048f;
        webLine.endWidth = 0.032f;
        webLine.useWorldSpace = true;
        webLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        webLine.receiveShadows = false;
        webLine.numCapVertices = 4;
        webLine.numCornerVertices = 2;
        webLine.textureMode = LineTextureMode.Tile;
        webLine.material = MakeSilkMat(new Color(0.96f, 0.99f, 1f, 0.92f), false, true);
        if (webLine.material && silkShimmerTex)
        {
            webLine.material.mainTexture = silkShimmerTex;
            webLine.material.mainTextureScale = new Vector2(3f, 1f);
        }
        webLine.startColor = new Color(0.98f, 0.99f, 1f, 0.95f);
        webLine.endColor = new Color(0.85f, 0.95f, 1f, 0.78f);
        webLine.enabled = false;

        var glowGo = new GameObject("TCWebGlow");
        glowGo.transform.SetParent(transform, false);
        webGlow = glowGo.AddComponent<LineRenderer>();
        webGlow.positionCount = 10;
        webGlow.startWidth = 0.115f;
        webGlow.endWidth = 0.075f;
        webGlow.useWorldSpace = true;
        webGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        webGlow.receiveShadows = false;
        webGlow.numCapVertices = 4;
        webGlow.textureMode = LineTextureMode.Tile;
        webGlow.material = MakeSilkMat(new Color(0.55f, 0.90f, 1f, 0.35f), true, true);
        if (webGlow.material && silkShimmerTex)
        {
            webGlow.material.mainTexture = silkShimmerTex;
            webGlow.material.mainTextureScale = new Vector2(2f, 1f);
        }
        webGlow.startColor = new Color(0.65f, 0.92f, 1f, 0.38f);
        webGlow.endColor = new Color(0.45f, 0.85f, 1f, 0.16f);
        webGlow.enabled = false;

        var anchor = new GameObject("TCWebAnchor");
        webAnchor = anchor.transform;
        webAnchor.SetParent(transform, false);
        whoosh = gameObject.GetComponent<AudioSource>();
        if (!whoosh) whoosh = gameObject.AddComponent<AudioSource>();
        whoosh.playOnAwake = false;
    }

    static Texture2D MakeShimmerTex()
    {
        const int W = 64, H = 8;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        for (int x = 0; x < W; x++)
        {
            float u = x / (float)(W - 1);
            float hot = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2.4f), 2f);
            var c = Color.Lerp(new Color(0.85f, 0.92f, 1f, 1f), Color.white, hot);
            c.a = 0.75f + 0.25f * hot;
            for (int y = 0; y < H; y++) tex.SetPixel(x, y, c);
        }
        tex.Apply(false, false);
        return tex;
    }

    static void SetMatColor(Material m, Color c)
    {
        if (!m) return;
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static Material MakeSilkMat(Color color, bool additive, bool transparent)
    {
        Shader sh = Shader.Find("TallCrawl/FX Particle");
        if (!sh && additive)
            sh = Shader.Find("Mobile/Particles/Additive")
              ?? Shader.Find("Legacy Shaders/Particles/Additive");
        if (!sh)
            sh = Shader.Find("Mobile/Particles/Alpha Blended")
              ?? Shader.Find("Sprites/Default")
              ?? Shader.Find("Universal Render Pipeline/Unlit")
              ?? Shader.Find("Unlit/Color");
        var m = new Material(sh) { name = additive ? "TCSilkGlow" : "TCSilk" };
        SetMatColor(m, color);
        if (m.HasProperty("_Additive")) m.SetFloat("_Additive", additive ? 1f : 0f);
        if (m.HasProperty("_Boost")) m.SetFloat("_Boost", additive ? 1.4f : 1.15f);
        if (transparent || additive)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f);
            if (m.HasProperty("_SrcBlend"))
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend"))
                m.SetInt("_DstBlend", additive
                    ? (int)UnityEngine.Rendering.BlendMode.One
                    : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.renderQueue = additive ? 3100 : 3000;
        }
        return m;
    }


    /// Procedural W/E/B pickup. Matches power-up visual size (~0.28) and FX
    /// (halo, rays, ripple ring, twinkles, bob/sway) with a white glow theme.
    /// The glyph is a compact 3D-looking letter — no flat gold card.
    public static GameObject CreateLetterPrefab(Transform parent, char letter)
    {
        letter = char.ToUpperInvariant(letter);
        var root = new GameObject("WebLetter_" + letter);
        if (parent) root.transform.SetParent(parent, false);
        root.tag = "Untagged";

        var item = new GameObject("LetterItem");
        item.transform.SetParent(root.transform, false);
        // Sit on the trunk surface (root is at the axis, yawed to the lane).
        item.transform.localPosition = new Vector3(0f, 0f, 0.72f);
        var col = item.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.22f;
        var letterComp = item.AddComponent<TCWebLetter>();
        letterComp.letter = letter;

        var face = new GameObject("Face");
        face.transform.SetParent(item.transform, false);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(face.transform, false);

        var faceMat = LetterMaterial(letter);
        var shadeMat = LetterShadeMaterial(letter);
        const float glyphSize = 0.28f;
        for (int i = 6; i >= 1; i--)
            LetterQuad(visual.transform, "Extrude" + i, shadeMat, glyphSize, new Vector3(0f, 0f, 0.011f * i));
        LetterQuad(visual.transform, "Glyph", faceMat, glyphSize, Vector3.zero);

        var bill = item.AddComponent<TCWebLetterBillboard>();
        bill.face = face.transform;
        bill.visual = visual.transform;

        TCPickupFX.Build(root, TCPickupKind.Letter, item.transform, visual.transform, item.transform.localPosition, glyphSize);
        ApplyLetterVisual(root, letter);
        return root;
    }

    static MeshRenderer LetterQuad(Transform parent, string name, Material mat, float size, Vector3 localPos)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var c = q.GetComponent<Collider>();
        if (c) Object.DestroyImmediate(c);
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = localPos;
        q.transform.localScale = Vector3.one * size;
        var mr = q.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    public static Color LetterColor(char letter)
    {
        // Shared white theme for every letter — identity is the glyph, not a tinted card.
        return new Color(0.96f, 0.97f, 1f, 1f);
    }

    public static void ApplyLetterVisual(GameObject root, char letter)
    {
        if (!root) return;
        letter = char.ToUpperInvariant(letter);
        var glyph = LetterMaterial(letter);
        var shade = LetterShadeMaterial(letter);
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!r) continue;
            if (r.name == "Glyph") r.sharedMaterial = glyph;
            else if (r.name.StartsWith("Extrude")) r.sharedMaterial = shade;
        }
        var letterComp = root.GetComponentInChildren<TCWebLetter>(true);
        if (letterComp) letterComp.letter = letter;
        var fx = root.GetComponent<TCPickupFX>();
        if (fx)
        {
            fx.kind = TCPickupKind.Letter;
            TCPickupFX.Palette(TCPickupKind.Letter, out var c1, out var c2);
            fx.color = c1; fx.color2 = c2;
        }
        var bill = root.GetComponentInChildren<TCWebLetterBillboard>(true);
        if (bill) bill.PlayPop();
    }

    static readonly Dictionary<char, Material> letterMats = new Dictionary<char, Material>();
    static readonly Dictionary<char, Material> shadeMats = new Dictionary<char, Material>();
    public static Material LetterMaterial(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        if (letterMats.TryGetValue(letter, out var m) && m) return m;
        var tex = LetterTexture(letter);
        m = MakeLetterUnlit("WEB_" + letter, tex, Color.white, true);
        m.renderQueue = 3002;
        letterMats[letter] = m;
        return m;
    }

    public static Material LetterShadeMaterial(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        if (shadeMats.TryGetValue(letter, out var m) && m) return m;
        var tex = LetterTexture(letter);
        m = MakeLetterUnlit("WEBShade_" + letter, tex, new Color(0.42f, 0.46f, 0.55f, 1f), true);
        m.renderQueue = 3001;
        shadeMats[letter] = m;
        return m;
    }

    static Material MakeLetterUnlit(string name, Texture tex, Color color, bool transparent)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit")
              ?? Shader.Find("Unlit/Transparent")
              ?? Shader.Find("Unlit/Texture")
              ?? Shader.Find("Sprites/Default");
        var m = new Material(sh) { name = name, color = color };
        if (tex)
        {
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (transparent)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
        }
        return m;
    }

    static readonly Dictionary<char, Texture2D> letterTex = new Dictionary<char, Texture2D>();
    static Texture2D LetterTexture(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        if (letterTex.TryGetValue(letter, out var cached) && cached) return cached;
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(new Color[N * N]); // transparent, no card
        // Dark outline, offset shade for a beveled 3D read, cream face, top-left highlight.
        StampLetter(tex, letter, new Color(0.10f, 0.12f, 0.18f, 1f), 8.4f, 0f, 0f);
        StampLetter(tex, letter, new Color(0.36f, 0.40f, 0.50f, 1f), 6.6f, 3.2f, -3.2f);
        StampLetter(tex, letter, new Color(0.95f, 0.96f, 1f, 1f), 6.1f, 0f, 0f);
        StampLetter(tex, letter, new Color(1f, 1f, 1f, 1f), 3.4f, -1.8f, 1.8f);
        tex.Apply(false, false);
        letterTex[letter] = tex;
        return tex;
    }

    static void StampLetter(Texture2D tex, char letter, Color ink, float radius, float ox, float oy)
    {
        int N = tex.width;
        void Ink(int x, int y, float a = 1)
        {
            if ((uint)x >= (uint)N || (uint)y >= (uint)N) return;
            var c = tex.GetPixel(x, y);
            c = Color.Lerp(c, ink, a * 0.95f);
            c.a = Mathf.Max(c.a, a * ink.a);
            tex.SetPixel(x, y, c);
        }
        void Capsule(float x0, float y0, float x1, float y1, float r)
        {
            x0 += ox; x1 += ox; y0 += oy; y1 += oy;
            Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1);
            int minx = Mathf.FloorToInt(Mathf.Min(x0, x1) - r - 1), maxx = Mathf.CeilToInt(Mathf.Max(x0, x1) + r + 1);
            int miny = Mathf.FloorToInt(Mathf.Min(y0, y1) - r - 1), maxy = Mathf.CeilToInt(Mathf.Max(y0, y1) + r + 1);
            Vector2 ba = b - a; float den = Vector2.Dot(ba, ba);
            for (int y = miny; y <= maxy; y++)
                for (int x = minx; x <= maxx; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = den > 1e-4f ? Mathf.Clamp01(Vector2.Dot(p - a, ba) / den) : 0f;
                    float d = (p - (a + ba * t)).magnitude;
                    float al = Mathf.Clamp01(r - d + 0.5f);
                    if (al > 0) Ink(x, y, al);
                }
        }
        float s = N / 128f;
        float C(float v) => v * s;
        float r = radius * s;
        if (letter == 'W')
        {
            Capsule(C(36), C(96), C(46), C(36), r);
            Capsule(C(46), C(36), C(64), C(70), r);
            Capsule(C(64), C(70), C(82), C(36), r);
            Capsule(C(82), C(36), C(92), C(96), r);
        }
        else if (letter == 'E')
        {
            Capsule(C(40), C(34), C(40), C(96), r);
            Capsule(C(40), C(96), C(92), C(96), r);
            Capsule(C(40), C(65), C(82), C(65), r);
            Capsule(C(40), C(34), C(92), C(34), r);
        }
        else // B
        {
            Capsule(C(40), C(34), C(40), C(96), r);
            Capsule(C(40), C(96), C(78), C(96), r);
            Capsule(C(40), C(65), C(76), C(65), r);
            Capsule(C(40), C(34), C(78), C(34), r);
            Capsule(C(78), C(96), C(88), C(80), r);
            Capsule(C(88), C(80), C(78), C(65), r);
            Capsule(C(76), C(65), C(90), C(50), r);
            Capsule(C(90), C(50), C(78), C(34), r);
        }
    }

}

/// Trigger volume on a W/E/B pickup.
public class TCWebLetter : MonoBehaviour
{
    public char letter = 'W';
    bool taken;
    void OnEnable() { taken = false; }
    public void Collect(Vector3 pos)
    {
        if (taken) return;
        taken = true;
        TCWebBonus.Instance?.CollectLetter(letter, pos);
        var pooled = GetComponentInParent<TCPooled>();
        if (pooled) pooled.Release();
        else
        {
            var root = transform.parent ? transform.parent.gameObject : gameObject;
            root.SetActive(false);
        }
    }
}

/// Faces the letter glyph toward the camera. Bob, sway and FX live on TCPickupFX.
public class TCWebLetterBillboard : MonoBehaviour
{
    public Transform face;
    public Transform visual;

    public void PlayPop() { }

    void LateUpdate()
    {
        var t = face ? face : (visual ? visual : null);
        if (!t) return;
        var cam = Camera.main;
        if (!cam) return;
        Vector3 toCam = t.position - cam.transform.position;
        if (toCam.sqrMagnitude < 1e-8f) return;
        t.rotation = Quaternion.LookRotation(toCam, Vector3.up);
    }
}
