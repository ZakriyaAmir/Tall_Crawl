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
    LineRenderer webLine;
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
    const float OrbitPerSwing = 108f; // degrees around the trunk; 10 swings = 3 full revolutions

    LineRenderer webGlow, webOutline;
    Transform silkCore, silkGlow, silkEdge;
    Light swingRim;
    Transform swingOutlineRoot;
    float fovKick, dutchNow;
    Vector3 camPosRest, camPosClose;
    Quaternion camRotRest, camRotClose;
    float fovRest;
    Camera swingCam;
    Animator camAnimRest;
    bool camAnimWasEnabled;
    Transform camParentRest;
    Vector3 camScaleRest;
    bool swingCamActive;
    Transform swingBody;
    Vector3 swingBodyRest;
    readonly List<Renderer> spiderRenderers = new List<Renderer>();
    readonly List<int> spiderQueues = new List<int>();
    GameObject splatDecal;

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
            DrawSilk(silkTip, silkAnchor, 0.85f, silkAge);
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
            DriveSwingCam(dutchNow);
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
        LiftSpiderSort(cute, true);
        SetSpiderTrails(sp, false);
        SetSpiderOutline(cute, true);

        var cam = Camera.main;
        swingCam = cam;
        camParentRest = cam ? cam.transform.parent : null;
        camPosRest = cam ? cam.transform.localPosition : Vector3.zero;
        camRotRest = cam ? cam.transform.localRotation : Quaternion.identity;
        camScaleRest = cam ? cam.transform.localScale : Vector3.one;
        fovRest = cam ? cam.fieldOfView : 64f;
        dutchNow = 0f;
        fovKick = 0f;

        float mpu = TCConfig.I != null ? Mathf.Max(0.01f, TCConfig.I.metresPerUnit) : 20f;
        float worldUnits = DistanceBonusMetres / mpu;
        Vector3 axis = pl && pl.p1 ? pl.p1.position : Vector3.zero;
        axis.y = 0f;
        float y0 = pl && pl.p1 ? pl.p1.position.y : (sp ? sp.position.y : 0f);
        float yEnd = y0 + worldUnits;
        float yaw = pl ? pl.CurrentYaw : (sp ? sp.eulerAngles.y : 0f);

        // Dedicated world-space swing camera: unparent so p1 yaw no longer
        // cancels the orbit (that was why every frame looked like the same straight-up view).
        if (cam)
        {
            Vector3 fromPos = cam.transform.position;
            Quaternion fromRot = cam.transform.rotation;
            camAnimRest = cam.GetComponent<Animator>();
            camAnimWasEnabled = camAnimRest && camAnimRest.enabled;
            if (camAnimRest) camAnimRest.enabled = false;
            cam.transform.SetParent(null, true);
            swingCamActive = true;
            ApplyPendulumRadius(HangRadius());
            float z = 0f;
            while (z < 0.32f)
            {
                z += Time.unscaledDeltaTime;
                float k = Smooth(z / 0.32f);
                DriveSwingCam(0f, k, fromPos, fromRot);
                yield return null;
            }
        }

        var trail = EnsureWind(sp);
        if (trail) { trail.Clear(); trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }

        const int Shots = SwingCount;
        float cycle = SwingSeconds / Shots;
        float yPrev = y0;
        const float ThwipDur = 0.08f;
        const float FlipDur = 0.22f;

        for (int shot = 0; shot < Shots; shot++)
        {
            bool last = shot == Shots - 1;
            currentThwip = shot + 1;
            float yawStart = yaw;
            float yawLand = yawStart + OrbitPerSwing;
            float yStart = Mathf.Lerp(y0, yEnd, shot / (float)Shots);
            float yLand = Mathf.Lerp(y0, yEnd, (shot + 1) / (float)Shots);
            // Keep the attach point in the upper frame: a short strand (~1.2 wu)
            // on the visible face of the trunk. A climb-tied 10 wu web put the
            // splat off-screen and ran the chord through the camera.
            // Anchor is relative to the spinnerets, not p1.y (the root sits ~0.85
            // above the spider child, which previously put the splat off-screen).
            Vector3 AnchorNow()
            {
                Vector3 tip = SpinneretWorld(cute, sp);
                float yawNow = (pl ? pl.CurrentYaw : yaw) + 6f;
                return OnTrunk(axis, yawNow, tip.y + 0.40f, TrunkRadius + 0.03f);
            }
            Vector3 anchorFixed = AnchorNow();

            // --- 1. Thwip: visible flying arc, then taut ---
            silkLive = true;
            silkAge = 0f;
            PlayThwip(shot);
            fovKick = 2f;
            TCFx.Shake(0.12f);
            if (cute) cute.SetSwingPose(-28f, 10f, 0f, 0.32f, -1f);
            if (shot == 1 || shot == 6) TCSfx.PlayVoice(false, 0.62f, 1.04f + shot * 0.03f);

            float t = 0f;
            while (t < ThwipDur)
            {
                t += Time.deltaTime;
                float ext = Mathf.Clamp01(t / ThwipDur);
                ext = ext * ext * (3f - 2f * ext);
                TickSwing(pl, yStart, yawStart, HangRadius(), dutchNow, ref yPrev);
                silkTip = SpinneretWorld(cute, sp);
                silkAnchor = Vector3.Lerp(silkTip, anchorFixed, ext);
                if (webAnchor) webAnchor.position = silkAnchor;
                DrawSilk(silkTip, silkAnchor, 0.15f + 0.2f * ext, silkAge);
                yield return null;
            }
            silkAnchor = anchorFixed;
            if (webAnchor) webAnchor.position = silkAnchor;
            DrawSilk(silkTip, silkAnchor, 1f, silkAge);
            SpawnAnchorBurst(anchorFixed, axis);
            if (cute) cute.SetSwingPose(-22f, 8f, 0f, 0.38f, -0.85f);
            TCFx.Shake(0.08f);

            // --- 2. Pendulum while orbiting the trunk ---
            PlayWhoosh(shot);
            float swingDur = Mathf.Max(0.32f, cycle - ThwipDur - (last ? 0.50f : FlipDur));
            t = 0f;
            while (t < swingDur)
            {
                t += Time.deltaTime;
                float swingU = Mathf.Clamp01(t / swingDur);
                float pend = 0.5f - 0.5f * Mathf.Cos(swingU * Mathf.PI);
                yaw = yawStart + OrbitPerSwing * pend;
                float dip = 0.55f * Mathf.Sin(swingU * Mathf.PI);
                float y = Mathf.Lerp(yStart, yLand, Smooth(pend)) - dip;
                float taut = 0.55f + 0.45f * Mathf.Abs(Mathf.Cos(swingU * Mathf.PI));

                if (cute)
                {
                    float lean = -22f - 20f * Mathf.Sin(swingU * Mathf.PI);
                    float roll = 26f * Mathf.Sin(swingU * Mathf.PI);
                    float twist = 16f * pend;
                    float squash = 0.08f - 0.16f * Mathf.Sin(swingU * Mathf.PI);
                    cute.SetSwingPose(lean, roll, twist, squash, 0.95f);
                }

                dutchNow = 8f * Mathf.Sin(swingU * Mathf.PI) * (shot % 2 == 0 ? 1f : -1f);
                TickSwing(pl, y, yaw, PendulumExtra(swingU), dutchNow, ref yPrev);

                silkTip = SpinneretWorld(cute, sp);
                silkAnchor = AnchorNow();
                anchorFixed = silkAnchor;
                if (webAnchor) webAnchor.position = silkAnchor;
                DrawSilk(silkTip, silkAnchor, taut, silkAge);
                yield return null;
            }
            yaw = yawLand;

            // --- 3. Release: splay, stretch, full flip, then the next thwip ---
            LingerCurrentSilk();
            silkLive = false;
            SetSilkVisible(false);

            if (last)
            {
                if (cute) cute.SetSwingPose(-10f, 12f, 0f, -0.18f, -0.9f);
                TCSfx.Play("celebrate", 1f);
                TCSfx.PlayVoice(false, 0.7f, 1.12f);
                float flip = 0f;
                float landY = yLand;
                while (flip < 0.55f)
                {
                    flip += Time.deltaTime;
                    float k = Mathf.Clamp01(flip / 0.55f);
                    float y = landY + Mathf.Sin(k * Mathf.PI) * 0.14f;
                    if (cute) cute.SetSwingPose(-8f, 16f, 360f * Smooth(k), -0.14f * Mathf.Sin(k * Mathf.PI), -0.7f + k * 0.4f);
                    dutchNow = Mathf.Lerp(dutchNow, 0f, k);
                    TickSwing(pl, y, yawLand, HangRadius(), dutchNow, ref yPrev);
                    yield return null;
                }
                yaw = yawLand;
                TickSwing(pl, landY, yaw, HangRadius(), 0f, ref yPrev);
            }
            else
            {
                if (cute) cute.SetSwingPose(-6f, 10f, 0f, -0.22f, -1f);
                float flip = 0f;
                while (flip < FlipDur)
                {
                    flip += Time.deltaTime;
                    float k = Mathf.Clamp01(flip / FlipDur);
                    float y = yLand + Mathf.Sin(k * Mathf.PI) * 0.12f;
                    if (cute) cute.SetSwingPose(-10f, 18f, 360f * k, -0.16f * Mathf.Sin(k * Mathf.PI), -0.85f);
                    dutchNow = Mathf.Lerp(dutchNow, 0f, k);
                    TickSwing(pl, y, yawLand, HangRadius(), dutchNow, ref yPrev);
                    yield return null;
                }
            }
        }

        if (trail) trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (cute) { cute.ClearSwingPose(); cute.TriggerLandBounce(); }
        SpawnLandingBurst(sp ? sp.position : Vector3.zero);
        ShowFiveThousandPop();
        TCSfx.Play("chime", 0.9f, 1.05f);
        TCSfx.Play("celebrate", 1f);

        // Hold the close camera so the landing pose and +5000m pop can be read.
        ShowcaseHold = true;
        float hold = 0f;
        while (hold < 1.0f)
        {
            hold += Time.unscaledDeltaTime;
            ApplyPendulumRadius(HangRadius());
            DriveSwingCam(0f);
            yield return null;
        }
        ShowcaseHold = false;
        LingerCurrentSilk();
        silkLive = false;
        SetSilkVisible(false);

        RestoreSwingBody();
        LiftSpiderSort(cute, false);
        SetSpiderTrails(sp, true);
        SetSpiderOutline(cute, false);
        if (splatDecal) { Destroy(splatDecal); splatDecal = null; }

        if (cam && swingCamActive)
        {
            cam.transform.SetParent(camParentRest, true);
            cam.transform.localScale = camScaleRest;
            swingCamActive = false;
            if (camAnimRest) camAnimRest.enabled = camAnimWasEnabled;
            float z = 0f;
            var fromPos = cam.transform.localPosition;
            var fromRot = cam.transform.localRotation;
            float fromFov = cam.fieldOfView;
            while (z < 0.42f)
            {
                z += Time.unscaledDeltaTime;
                float k = Smooth(z / 0.42f);
                cam.transform.localPosition = Vector3.Lerp(fromPos, camPosRest, k);
                cam.transform.localRotation = Quaternion.Slerp(fromRot, camRotRest, k);
                cam.fieldOfView = Mathf.Lerp(fromFov, fovRest, k);
                yield return null;
            }
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

    float HangRadius() => 0.18f;

    float PendulumExtra(float swingU)
    {
        float mid = Mathf.Sin(Mathf.PI * Mathf.Clamp01(swingU));
        return Mathf.Lerp(HangRadius(), 0.70f, mid);
    }

    void BindSwingBody(Transform sp)
    {
        swingBody = sp;
        swingBodyRest = swingBody ? swingBody.localPosition : Vector3.zero;
    }

    void ApplyPendulumRadius(float extraZ)
    {
        if (!swingBody) return;
        var lp = swingBodyRest;
        lp.z += extraZ;
        swingBody.localPosition = lp;
    }

    void RestoreSwingBody()
    {
        if (swingBody) swingBody.localPosition = swingBodyRest;
    }

    void SetSpiderTrails(Transform sp, bool on)
    {
        // The skin trail (and any leftover swing trail) reads as fat white bars
        // once the spider starts moving quickly. Kill them for the showcase.
        var roots = new System.Collections.Generic.List<Transform>();
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
                if (!tr || !seen.Add(tr)) continue;
                tr.Clear();
                tr.emitting = on;
                tr.enabled = on;
            }
        }
    }

    void SetSilkVisible(bool on)
    {
        if (webLine) webLine.enabled = false;
        if (webGlow) webGlow.enabled = false;
        if (webOutline) webOutline.enabled = false;
        if (silkCore) silkCore.gameObject.SetActive(on);
        if (silkGlow) silkGlow.gameObject.SetActive(on);
        if (silkEdge) silkEdge.gameObject.SetActive(on);
    }

    void SetSpiderOutline(TCCuteSpider cute, bool on)
    {
        if (!on)
        {
            if (swingRim)
            {
                Destroy(swingRim.gameObject);
                swingRim = null;
            }
            if (swingOutlineRoot)
            {
                Destroy(swingOutlineRoot.gameObject);
                swingOutlineRoot = null;
            }
            return;
        }
        var host = cute ? cute.transform : swingBody;
        if (!host) return;
        if (swingOutlineRoot) Destroy(swingOutlineRoot.gameObject);
        swingOutlineRoot = new GameObject("SwingOutline").transform;
        swingOutlineRoot.SetParent(host, false);
        var dark = MakeSilkMat(new Color(0.07f, 0.06f, 0.10f, 1f), false, false);
        dark.renderQueue = 1990;
        if (dark.HasProperty("_ZWrite")) dark.SetInt("_ZWrite", 1);
        if (dark.HasProperty("_Cull")) dark.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
        dark.SetInt("_CullMode", (int)UnityEngine.Rendering.CullMode.Front);
        foreach (var mf in host.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf || !mf.sharedMesh) continue;
            if (mf.transform == swingOutlineRoot || mf.transform.IsChildOf(swingOutlineRoot)) continue;
            var mr0 = mf.GetComponent<MeshRenderer>();
            if (!mr0 || !mr0.enabled) continue;
            AddOutlineHull(mf.transform, mf.sharedMesh, dark, 1.10f, "o");
        }
        if (!swingRim)
        {
            var lg = new GameObject("SwingRim");
            swingRim = lg.AddComponent<Light>();
            swingRim.type = LightType.Point;
            swingRim.color = new Color(0.85f, 0.95f, 1f, 1f);
            swingRim.intensity = 1.0f;
            swingRim.range = 1.6f;
            swingRim.shadows = LightShadows.None;
        }
    }

    static void AddOutlineHull(Transform src, Mesh mesh, Material mat, float scale, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(src, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * scale;
        var cmf = go.AddComponent<MeshFilter>();
        cmf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
    }

    void LiftSpiderSort(TCCuteSpider cute, bool lift)
    {
        if (lift)
        {
            spiderRenderers.Clear();
            spiderQueues.Clear();
            var host = cute ? cute.transform : swingBody;
            if (!host) return;
            foreach (var r in host.GetComponentsInChildren<Renderer>(true))
            {
                if (!r) continue;
                spiderRenderers.Add(r);
                int q = 2000;
                var sm = r.sharedMaterial;
                if (sm) q = sm.renderQueue;
                spiderQueues.Add(q);
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i]) mats[i].renderQueue = 3200;
            }
        }
        else
        {
            for (int i = 0; i < spiderRenderers.Count; i++)
            {
                var r = spiderRenderers[i];
                if (!r) continue;
                int q = i < spiderQueues.Count ? spiderQueues[i] : 2000;
                var mats = r.materials;
                for (int j = 0; j < mats.Length; j++)
                    if (mats[j]) mats[j].renderQueue = q;
            }
            spiderRenderers.Clear();
            spiderQueues.Clear();
        }
    }

    void TickSwing(player pl, float y, float yaw, float extraZ, float dutch, ref float yPrev)
    {
        if (pl && pl.p1)
        {
            var pos = pl.p1.position; pos.y = y;
            pl.ApplySwingTransform(pos, yaw);
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            ClimbSpeed = (y - yPrev) / dt;
            yPrev = y;
        }
        ApplyPendulumRadius(extraZ);
        DriveSwingCam(dutch);
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
        fovKick = Mathf.MoveTowards(fovKick, 0f, Time.unscaledDeltaTime * 48f);

        var pl = player.Instance;
        Vector3 spiderPos = VisualSpiderPos();
        Vector3 axis = pl && pl.p1 ? pl.p1.position : spiderPos;
        axis.y = spiderPos.y;
        float yawRad = (pl ? pl.CurrentYaw : 0f) * Mathf.Deg2Rad;
        Vector3 outward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
        Vector3 tangent = Vector3.Cross(Vector3.up, outward);

        // Bias the look toward the trunk so the spider stays on the bark side of
        // the frame (never isolated against empty sky) and fully in view.
        Vector3 trunkSide = axis + outward * (TrunkRadius + 0.10f);
        trunkSide.y = spiderPos.y;
        Vector3 lookAt = Vector3.Lerp(spiderPos, trunkSide, 0.22f) + Vector3.up * 0.08f;
        Vector3 camPos = lookAt + outward * 0.68f + tangent * 1.20f + Vector3.up * 0.06f;
        Vector3 fromAxis = camPos; fromAxis.x -= axis.x; fromAxis.z -= axis.z; fromAxis.y = 0f;
        float radial = fromAxis.magnitude;
        const float minRadial = TrunkRadius + 1.10f;
        if (radial < minRadial)
        {
            Vector3 push = radial > 1e-4f ? fromAxis.normalized : (outward + tangent).normalized;
            camPos += push * (minRadial - radial);
        }
        if (swingRim)
        {
            Vector3 back = (camPos - lookAt);
            if (back.sqrMagnitude < 1e-6f) back = outward;
            swingRim.transform.position = spiderPos - back.normalized * 0.30f + Vector3.up * 0.22f;
        }

        Vector3 lookDir = lookAt - camPos;
        if (lookDir.sqrMagnitude < 1e-6f) lookDir = -outward;
        Quaternion camRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                          * Quaternion.AngleAxis(dutch, Vector3.forward);

        if (blend < 0.999f)
        {
            cam.transform.position = Vector3.Lerp(fromPos, camPos, blend);
            cam.transform.rotation = Quaternion.Slerp(fromRot, camRot, blend);
        }
        else
        {
            cam.transform.position = camPos;
            cam.transform.rotation = camRot;
        }
        cam.fieldOfView = 42f + fovKick;
        EnforceSpiderInFrame(cam, axis, dutch);
        // Last-ditch snap: never ship a frame where the body is off-screen.
        Vector3 mid = VisualSpiderPos();
        var vp = cam.WorldToViewportPoint(mid);
        if (vp.z <= 0.12f || vp.x < 0.16f || vp.x > 0.82f || vp.y < 0.18f || vp.y > 0.84f)
        {
            Vector3 snap = mid + outward * 0.62f + tangent * 1.08f + Vector3.up * 0.08f;
            Vector3 xz = snap; xz.x -= axis.x; xz.z -= axis.z; xz.y = 0f;
            float rad = xz.magnitude;
            const float minR = TrunkRadius + 1.02f;
            if (rad < minR && rad > 1e-4f) snap += xz.normalized * (minR - rad);
            cam.transform.position = snap;
            Vector3 look = mid - snap;
            if (look.sqrMagnitude > 1e-6f)
                cam.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up)
                                      * Quaternion.AngleAxis(dutch, Vector3.forward);
        }
    }

    void EnforceSpiderInFrame(Camera cam, Vector3 axis, float dutch)
    {
        if (!cam) return;
        // Use a stable world-radius sphere around the visual center. Mesh AABB
        // corners explode during 360 flips and were panning the camera into empty sky.
        Vector3 mid = VisualSpiderPos();
        const float radius = 0.34f;
        for (int pass = 0; pass < 4; pass++)
        {
            var vp = cam.WorldToViewportPoint(mid);
            if (vp.z <= 0.08f)
            {
                cam.transform.position -= cam.transform.forward * 0.35f;
            }
            else
            {
                float dist = Mathf.Max(0.5f, Vector3.Distance(cam.transform.position, mid));
                float halfH = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float halfW = halfH * cam.aspect;
                float ur = radius / (2f * halfW);
                float vr = radius / (2f * halfH);
                float minX = vp.x - ur, maxX = vp.x + ur;
                float minY = vp.y - vr, maxY = vp.y + vr;
                Vector3 shift = Vector3.zero;
                const float x0 = 0.14f, x1 = 0.80f, y0 = 0.18f, y1 = 0.84f;
                if (minX < x0) shift -= cam.transform.right * Mathf.Min(0.45f, (x0 - minX) * 2f * halfW);
                if (maxX > x1) shift += cam.transform.right * Mathf.Min(0.45f, (maxX - x1) * 2f * halfW);
                if (minY < y0) shift -= cam.transform.up * Mathf.Min(0.45f, (y0 - minY) * 2f * halfH);
                if (maxY > y1) shift += cam.transform.up * Mathf.Min(0.45f, (maxY - y1) * 2f * halfH);
                if (shift.sqrMagnitude < 1e-8f) break;
                cam.transform.position += shift;
            }
            Vector3 xz = cam.transform.position - axis; xz.y = 0f;
            float radial = xz.magnitude;
            const float minRadial = TrunkRadius + 1.02f;
            if (radial < minRadial && radial > 1e-4f)
                cam.transform.position += xz.normalized * (minRadial - radial);
            // Re-aim at the spider so a pan can never leave us staring at empty sky.
            Vector3 lookDir = mid - cam.transform.position;
            if (lookDir.sqrMagnitude > 1e-6f)
                cam.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                                      * Quaternion.AngleAxis(dutch, Vector3.forward);
        }
    }

    Vector3[] VisualSpiderCorners()
    {
        var cute = ResolveCute(swingBody);
        Transform host = cute ? cute.transform : swingBody;
        if (!host) return System.Array.Empty<Vector3>();
        bool init = false;
        Bounds b = new Bounds();
        foreach (var r in host.GetComponentsInChildren<MeshRenderer>())
        {
            if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (swingOutlineRoot && (r.transform == swingOutlineRoot || r.transform.IsChildOf(swingOutlineRoot))) continue;
            if (!init) { b = r.bounds; init = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!init) return new[] { host.position };
        var e = b.extents; var c = b.center;
        return new[]
        {
            c + new Vector3( e.x,  e.y,  e.z), c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3( e.x, -e.y,  e.z), c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3(-e.x,  e.y,  e.z), c + new Vector3(-e.x,  e.y, -e.z),
            c + new Vector3(-e.x, -e.y,  e.z), c + new Vector3(-e.x, -e.y, -e.z)
        };
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
        ClearFadingSilk();
        if (!silkCore || !silkCore.gameObject.activeSelf) return;
        StartCoroutine(FadeRibbonLinger(lingerTip, lingerAnchor, 14f, new Color(0.08f, 0.09f, 0.14f, 0.85f), false, 2990, 0.4f));
        StartCoroutine(FadeRibbonLinger(lingerTip, lingerAnchor, 10f, new Color(0.98f, 0.99f, 1f, 0.85f), false, 3020, 0.4f));
    }

    IEnumerator FadeRibbonLinger(Vector3 tip, Vector3 anchor, float pixels, Color color, bool additive, int queue, float life)
    {
        var ribbon = MakeRibbon("TCSilkFade", color, additive, queue);
        var mr = ribbon.GetComponent<MeshRenderer>();
        var mat = mr ? mr.material : null;
        var baseCol = color;
        float t = 0f;
        while (t < life && ribbon)
        {
            t += Time.deltaTime;
            PlaceRibbon(ribbon, tip, anchor, pixels);
            float a = 1f - Smooth(t / life);
            if (mat)
            {
                var c = baseCol; c.a = baseCol.a * a;
                mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            }
            yield return null;
        }
        if (ribbon) Destroy(ribbon.gameObject);
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
                var col = fadingMat[i] && fadingMat[i].HasProperty("_Color")
                    ? fadingMat[i].color : new Color(0.94f, 0.97f, 1f, 0.55f);
                if (fadingMat[i]) fadingMat[i].color = new Color(col.r, col.g, col.b, 0.55f * a);
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

    static Vector3 SpinneretWorld(TCCuteSpider cute, Transform sp)
    {
        if (cute) return cute.SpinneretTip();
        if (sp) return sp.position + sp.up * 0.05f - sp.forward * 0.2f;
        return Vector3.zero;
    }

    void DrawSilk(Vector3 tip, Vector3 anchor, float taut, float age)
    {
        EnsureSilkRibbons();
        if (webLine) webLine.enabled = false;
        if (webGlow) webGlow.enabled = false;
        if (webOutline) webOutline.enabled = false;

        Vector3 dir = anchor - tip;
        float rawLen = dir.magnitude;
        if (rawLen > 0.10f) tip = tip + dir / rawLen * 0.14f;
        lingerTip = tip;
        lingerAnchor = anchor;
        // Camera-facing ribbons with a screen-space width cap. Mesh tubes (and
        // LineRenderers) bloom into opaque bars when the strand comes near the
        // lens; a view-facing card stays a thread and goes edge-on when aligned.
        PlaceRibbon(silkEdge, tip, anchor, 14f);
        PlaceRibbon(silkGlow, tip, anchor, 20f);
        PlaceRibbon(silkCore, tip, anchor, 10f);
        TickSplat(anchor);
    }

    void EnsureSilkRibbons()
    {
        if (silkCore) return;
        silkEdge = MakeRibbon("TCSilkEdge", new Color(0.08f, 0.09f, 0.14f, 1f), false, 2990);
        silkGlow = MakeRibbon("TCSilkGlow", new Color(0.40f, 0.82f, 1f, 0.28f), true, 3010);
        silkCore = MakeRibbon("TCSilkCore", new Color(0.98f, 0.99f, 1f, 1f), false, 3020);
    }

    Transform MakeRibbon(string name, Color color, bool additive, int queue)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);
        go.name = name;
        go.transform.SetParent(transform, false);
        var mr = go.GetComponent<MeshRenderer>();
        var mat = MakeSilkMat(color, additive, true);
        mat.renderQueue = queue;
        if (mr)
        {
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        go.SetActive(false);
        return go.transform;
    }

    void PlaceRibbon(Transform t, Vector3 a, Vector3 b, float pixels)
    {
        if (!t) return;
        var cam = swingCam ? swingCam : Camera.main;
        if (cam && !ClipAwayFromPoint(cam.transform.position, 1.20f, ref a, ref b))
        {
            t.gameObject.SetActive(false);
            return;
        }
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.06f || !cam)
        {
            t.gameObject.SetActive(false);
            return;
        }
        Vector3 mid = (a + b) * 0.5f;
        Vector3 toCam = cam.transform.position - mid;
        if (toCam.sqrMagnitude < 1e-6f)
        {
            t.gameObject.SetActive(false);
            return;
        }
        Vector3 view = toCam.normalized;
        Vector3 dir = d / len;
        // Hide when looking nearly along the strand (ribbon would be edge-on or unstable).
        if (Mathf.Abs(Vector3.Dot(dir, view)) > 0.94f)
        {
            t.gameObject.SetActive(false);
            return;
        }
        float dist = Mathf.Min(Vector3.Distance(cam.transform.position, a), Vector3.Distance(cam.transform.position, b));
        dist = Mathf.Max(0.85f, dist);
        float worldPerPx = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, cam.pixelHeight);
        float width = worldPerPx * pixels;
        t.gameObject.SetActive(true);
        t.position = mid;
        t.rotation = Quaternion.LookRotation(view, dir);
        t.localScale = new Vector3(width, len, 1f);
    }

    static bool ClipAwayFromPoint(Vector3 p, float r, ref Vector3 a, ref Vector3 b)
    {
        Vector3 ab = b - a;
        float len = ab.magnitude;
        if (len < 1e-5f) return false;
        Vector3 n = ab / len;
        Vector3 ap = a - p;
        float B = 2f * Vector3.Dot(ap, n);
        float C = ap.sqrMagnitude - r * r;
        float disc = B * B - 4f * C;
        if (disc < 0f)
            return ap.sqrMagnitude >= r * r && (b - p).sqrMagnitude >= r * r;

        float sqrt = Mathf.Sqrt(disc);
        float tEnter = (-B - sqrt) * 0.5f;
        float tExit = (-B + sqrt) * 0.5f;

        float best0 = 0f, best1 = -1f;
        void Consider(float u0, float u1)
        {
            u0 = Mathf.Max(0f, u0);
            u1 = Mathf.Min(len, u1);
            if (u1 - u0 < 0.05f) return;
            if (u1 > best1) { best0 = u0; best1 = u1; }
        }
        Consider(0f, tEnter);
        Consider(tExit, len);
        if (best1 < 0f) return false;
        Vector3 orig = a;
        a = orig + n * best0;
        b = orig + n * best1;
        return true;
    }

    ParticleSystem EnsureWind(Transform sp)
    {
        if (windTrail) return windTrail;
        if (!sp) return null;
        var go = new GameObject("SwingWind");
        go.transform.SetParent(sp, false);
        go.transform.localPosition = new Vector3(0f, 0.1f, -0.15f);
        windTrail = go.AddComponent<ParticleSystem>();
        var main = windTrail.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        main.startColor = new Color(0.92f, 0.97f, 1f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = windTrail.emission;
        em.rateOverTime = 90f;
        var sh = windTrail.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 18f;
        sh.radius = 0.08f;
        var col = windTrail.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sol = windTrail.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (rend)
        {
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.08f;
            rend.lengthScale = 3.2f;
            rend.material = MakeSilkMat(new Color(0.95f, 0.98f, 1f, 0.8f), true, true);
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
        bodyTrail = host.gameObject.GetComponent<TrailRenderer>();
        if (!bodyTrail) bodyTrail = host.gameObject.AddComponent<TrailRenderer>();
        bodyTrail.time = 0.28f;
        bodyTrail.minVertexDistance = 0.04f;
        bodyTrail.startWidth = 0.22f;
        bodyTrail.endWidth = 0.02f;
        bodyTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bodyTrail.receiveShadows = false;
        bodyTrail.material = MakeSilkMat(new Color(0.75f, 0.95f, 1f, 0.55f), true, true);
        bodyTrail.startColor = new Color(0.9f, 0.98f, 1f, 0.7f);
        bodyTrail.endColor = new Color(0.6f, 0.85f, 1f, 0f);
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
            {
                var c = mat.color;
                c.a = 0.85f * a;
                mat.color = c;
                if (mat.HasProperty("_BaseColor"))
                {
                    var b = mat.GetColor("_BaseColor");
                    b.a = c.a;
                    mat.SetColor("_BaseColor", b);
                }
            }
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
        webLine.positionCount = 2;
        webLine.startWidth = 0.045f;
        webLine.endWidth = 0.036f;
        webLine.useWorldSpace = true;
        webLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        webLine.receiveShadows = false;
        webLine.numCapVertices = 4;
        webLine.numCornerVertices = 2;
        webLine.textureMode = LineTextureMode.Tile;
        webLine.material = MakeSilkMat(new Color(0.95f, 0.97f, 1f, 0.70f), false, true);
        if (webLine.material && silkShimmerTex)
        {
            webLine.material.mainTexture = silkShimmerTex;
            webLine.material.mainTextureScale = new Vector2(3f, 1f);
        }
        webLine.startColor = new Color(0.96f, 0.98f, 1f, 0.70f);
        webLine.endColor = new Color(0.88f, 0.95f, 1f, 0.50f);
        webLine.enabled = false;

        var glowGo = new GameObject("TCWebGlow");
        glowGo.transform.SetParent(transform, false);
        webGlow = glowGo.AddComponent<LineRenderer>();
        webGlow.positionCount = 2;
        webGlow.startWidth = 0.09f;
        webGlow.endWidth = 0.07f;
        webGlow.useWorldSpace = true;
        webGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        webGlow.receiveShadows = false;
        webGlow.numCapVertices = 4;
        webGlow.textureMode = LineTextureMode.Tile;
        webGlow.material = MakeSilkMat(new Color(0.7f, 0.92f, 1f, 0.22f), true, true);
        webGlow.startColor = new Color(0.7f, 0.92f, 1f, 0.16f);
        webGlow.endColor = new Color(0.55f, 0.85f, 1f, 0.10f);
        webGlow.enabled = false;

        var outlineGo = new GameObject("TCWebOutline");
        outlineGo.transform.SetParent(transform, false);
        webOutline = outlineGo.AddComponent<LineRenderer>();
        webOutline.positionCount = 2;
        webOutline.startWidth = 0.082f;
        webOutline.endWidth = 0.070f;
        webOutline.useWorldSpace = true;
        webOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        webOutline.receiveShadows = false;
        webOutline.numCapVertices = 0;
        webOutline.material = MakeSilkMat(new Color(0.10f, 0.11f, 0.16f, 0.90f), false, true);
        webOutline.startColor = new Color(0.10f, 0.11f, 0.16f, 0.90f);
        webOutline.endColor = new Color(0.10f, 0.11f, 0.16f, 0.70f);
        webOutline.enabled = false;

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

    static Material MakeSilkMat(Color color, bool additive, bool transparent)
    {
        Shader sh = null;
        if (additive)
            sh = Shader.Find("Mobile/Particles/Additive")
              ?? Shader.Find("Legacy Shaders/Particles/Additive")
              ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
              ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (!sh)
            sh = Shader.Find("Universal Render Pipeline/Unlit")
              ?? Shader.Find("Unlit/Color")
              ?? Shader.Find("Sprites/Default");
        var m = new Material(sh) { color = color, name = additive ? "TCSilkGlow" : "TCSilk" };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", color);
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
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
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
