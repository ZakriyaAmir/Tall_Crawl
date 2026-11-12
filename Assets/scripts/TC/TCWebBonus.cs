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
    // Letter cadence lives in TCTrack (half the health-pickup frequency).
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

    LineRenderer webGlow;
    float fovKick, dutchNow;
    Vector3 camPosRest, camPosClose;
    Quaternion camRotRest, camRotClose;
    float fovRest;
    Camera swingCam;
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
        if (IsSwinging && webLine && webLine.enabled && silkLive)
            DrawSilk(silkTip, silkAnchor, 0.85f, silkAge);
        if (IsSwinging) silkAge += Time.deltaTime;
        TickFadingSilk();
        TickGhosts();
        if (IsSwinging) UpdateSwingHud();
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

        TCTrack.Instance?.ClearHazardsAhead(80f);
        TCTrack.Instance?.ReserveClearRun(DistanceBonusMetres);

        EnsureWeb();
        EnsureSwingHud();
        TCFx.Shake(0.18f);
        TCSfx.Play("celebrate", 0.85f);

        var cam = Camera.main;
        swingCam = cam;
        camPosRest = cam ? cam.transform.localPosition : Vector3.zero;
        camRotRest = cam ? cam.transform.localRotation : Quaternion.identity;
        fovRest = cam ? cam.fieldOfView : 64f;
        // Pull IN along the same line of sight so the spider reads 2–3× larger,
        // with the same look-up-the-trunk framing.
        Vector3 look = new Vector3(0f, -0.78f, 0.66f);
        Vector3 outDir = camPosRest - look;
        camPosClose = look + outDir * (1f / 2.15f);
        // Pitch down so the spider sits mid-low in the frame, with room under it,
        // and the silk running up the trunk is visible above.
        camRotClose = camRotRest;
        dutchNow = 0f;
        fovKick = 0f;

        if (cam)
        {
            float z = 0f;
            while (z < 0.28f)
            {
                z += Time.unscaledDeltaTime;
                float k = Smooth(z / 0.28f);
                cam.transform.localPosition = Vector3.Lerp(camPosRest, camPosClose, k);
                cam.transform.localRotation = Quaternion.Slerp(camRotRest, camRotClose, k);
                cam.fieldOfView = Mathf.Lerp(fovRest, fovRest - 4f, k);
                yield return null;
            }
            ApplySwingCam(0f, 0f);
        }

        float mpu = TCConfig.I != null ? Mathf.Max(0.01f, TCConfig.I.metresPerUnit) : 20f;
        float worldUnits = DistanceBonusMetres / mpu;
        Vector3 axis = pl && pl.p1 ? pl.p1.position : Vector3.zero;
        axis.y = 0f;
        float y0 = pl && pl.p1 ? pl.p1.position.y : (sp ? sp.position.y : 0f);
        float yEnd = y0 + worldUnits;
        float yaw = pl ? pl.CurrentYaw : (sp ? sp.eulerAngles.y : 0f);

        var trail = EnsureWind(sp);
        if (trail) { trail.Clear(); trail.Play(); }

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
            float yawAnchor = yawStart + OrbitPerSwing * 0.62f;
            float yAnchor = yStart + 2.15f + (yLand - yStart) * 0.35f;
            Vector3 anchorFixed = OnTrunk(axis, yawAnchor, yAnchor, TrunkRadius + 0.02f);

            // --- 1. Thwip: visible flying arc, then taut ---
            silkLive = true;
            silkAge = 0f;
            if (webLine) webLine.enabled = true;
            if (webGlow) webGlow.enabled = true;
            PlayThwip(shot);
            fovKick = 18f;
            TCFx.Shake(0.12f);
            if (cute) cute.SetSwingPose(-28f, 10f, 0f, 0.32f, -1f); // splay + squash on the shot
            if (shot == 1 || shot == 6) TCSfx.PlayVoice(false, 0.62f, 1.04f + shot * 0.03f);

            float t = 0f;
            while (t < ThwipDur)
            {
                t += Time.deltaTime;
                float ext = Mathf.Clamp01(t / ThwipDur);
                ext = ext * ext * (3f - 2f * ext);
                silkTip = SpinneretWorld(cute, sp);
                silkAnchor = Vector3.Lerp(silkTip, anchorFixed, ext);
                if (webAnchor) webAnchor.position = silkAnchor;
                DrawSilk(silkTip, silkAnchor, 0.15f + 0.2f * ext, silkAge); // loose flying arc
                if (pl && pl.p1)
                {
                    var pos = pl.p1.position; pos.y = yStart;
                    pl.ApplySwingTransform(pos, yawStart);
                    yPrev = yStart;
                }
                ApplySwingCam(dutchNow, fovKick);
                yield return null;
            }
            silkAnchor = anchorFixed;
            if (webAnchor) webAnchor.position = silkAnchor;
            DrawSilk(silkTip, silkAnchor, 1f, silkAge);
            SpawnAnchorBurst(anchorFixed, axis);
            if (cute) cute.SetSwingPose(-22f, 8f, 0f, 0.38f, -0.85f); // catch squash
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

                silkTip = SpinneretWorld(cute, sp);
                silkAnchor = anchorFixed;
                if (webAnchor) webAnchor.position = silkAnchor;
                DrawSilk(silkTip, silkAnchor, taut, silkAge);

                if (cute)
                {
                    float lean = -22f - 20f * Mathf.Sin(swingU * Mathf.PI);
                    float roll = 26f * Mathf.Sin(swingU * Mathf.PI);
                    float twist = 16f * pend;
                    // Mid-swing: legs tucked. Stretch slightly through the bottom.
                    float squash = 0.08f - 0.16f * Mathf.Sin(swingU * Mathf.PI);
                    cute.SetSwingPose(lean, roll, twist, squash, 0.95f);
                }

                dutchNow = 11f * Mathf.Sin(swingU * Mathf.PI) * (shot % 2 == 0 ? 1f : -1f);
                if (pl && pl.p1)
                {
                    var pos = pl.p1.position; pos.y = y;
                    pl.ApplySwingTransform(pos, yaw);
                    float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                    ClimbSpeed = (y - yPrev) / dt;
                    yPrev = y;
                }
                ApplySwingCam(dutchNow, fovKick);
                yield return null;
            }
            yaw = yawLand;

            // --- 3. Release: splay, stretch, full flip, then the next thwip ---
            LingerCurrentSilk();
            silkLive = false;
            if (webLine) webLine.enabled = false;
            if (webGlow) webGlow.enabled = false;

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
                    float y = landY + Mathf.Sin(k * Mathf.PI) * 0.28f;
                    if (cute) cute.SetSwingPose(-8f, 16f, 360f * Smooth(k), -0.14f * Mathf.Sin(k * Mathf.PI), -0.7f + k * 0.4f);
                    if (pl && pl.p1)
                    {
                        var pos = pl.p1.position; pos.y = y;
                        pl.ApplySwingTransform(pos, yawLand);
                        ClimbSpeed = (y - yPrev) / Mathf.Max(Time.deltaTime, 1e-4f);
                        yPrev = y;
                    }
                    dutchNow = Mathf.Lerp(dutchNow, 0f, k);
                    ApplySwingCam(dutchNow, fovKick);
                        yield return null;
                }
                yaw = yawLand;
                if (pl && pl.p1)
                {
                    var pos = pl.p1.position; pos.y = landY;
                    pl.ApplySwingTransform(pos, yaw);
                }
            }
            else
            {
                if (cute) cute.SetSwingPose(-6f, 10f, 0f, -0.22f, -1f); // stretch + splay on release
                float flip = 0f;
                while (flip < FlipDur)
                {
                    flip += Time.deltaTime;
                    float k = Mathf.Clamp01(flip / FlipDur);
                    float y = yLand + Mathf.Sin(k * Mathf.PI) * 0.22f;
                    if (cute) cute.SetSwingPose(-10f, 18f, 360f * k, -0.16f * Mathf.Sin(k * Mathf.PI), -0.85f);
                    if (pl && pl.p1)
                    {
                        var pos = pl.p1.position; pos.y = y;
                        pl.ApplySwingTransform(pos, yawLand);
                        ClimbSpeed = (y - yPrev) / Mathf.Max(Time.deltaTime, 1e-4f);
                        yPrev = y;
                    }
                    dutchNow = Mathf.Lerp(dutchNow, 0f, k);
                    ApplySwingCam(dutchNow, fovKick);
                        yield return null;
                }
            }
        }

        if (trail) trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        // Keep the last strand visible through the landing hold, then fade it.
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
            if (cam) ApplySwingCam(0f, 0f);
            yield return null;
        }
        ShowcaseHold = false;
        LingerCurrentSilk();
        silkLive = false;
        if (webLine) webLine.enabled = false;
        if (webGlow) webGlow.enabled = false;

        if (cam)
        {
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

    void ApplySwingCam(float dutch, float kick)
    {
        var cam = swingCam ? swingCam : Camera.main;
        if (!cam) return;
        fovKick = Mathf.MoveTowards(fovKick, 0f, Time.unscaledDeltaTime * 48f);
        cam.transform.localPosition = camPosClose;
        cam.transform.localRotation = camRotClose * Quaternion.AngleAxis(dutch, Vector3.forward);
        cam.fieldOfView = (fovRest - 4f) + fovKick;
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

    void LingerCurrentSilk()
    {
        if (!webLine || webLine.positionCount < 2) return;
        void Copy(LineRenderer src, float widthMul, Color col, bool glow)
        {
            var go = new GameObject(glow ? "TCWebFadeGlow" : "TCWebFade");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = src.positionCount;
            var pts = new Vector3[src.positionCount];
            src.GetPositions(pts);
            lr.SetPositions(pts);
            lr.startWidth = src.startWidth * widthMul;
            lr.endWidth = src.endWidth * widthMul;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            var mat = glow ? MakeSilkMat(col, true, true) : MakeSilkMat(col, false, false);
            lr.material = mat;
            lr.startColor = col;
            lr.endColor = new Color(col.r, col.g, col.b, col.a * 0.55f);
            fadingSilk.Add(lr);
            fadingAge.Add(0f);
            fadingLife.Add(glow ? 0.55f : 0.70f);
            fadingMat.Add(mat);
        }
        Copy(webLine, 0.85f, new Color(0.9f, 0.97f, 1f, 0.8f), false);
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
                // Linger, then a quick thickness snap (no retract, which read as white shards).
                float snap = u > 0.6f ? Smooth((u - 0.6f) / 0.4f) : 0f;
                if (snap > 0f)
                {
                    lr.startWidth = 0.05f * (1f - snap);
                    lr.endWidth = 0.04f * (1f - snap);
                }
                var col = fadingMat[i] && fadingMat[i].HasProperty("_Color")
                    ? fadingMat[i].color : new Color(1f, 1f, 1f, 1f);
                if (fadingMat[i]) fadingMat[i].color = new Color(col.r, col.g, col.b, 0.9f * a);
                lr.startColor = new Color(1f, 1f, 1f, 0.95f * a);
                lr.endColor = new Color(0.85f, 0.95f, 1f, 0.5f * a);
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
        if (!webLine) return;
        Vector3 dir = anchor - tip;
        float rawLen = dir.magnitude;
        if (rawLen > 0.12f) tip = tip + dir / rawLen * 0.22f; // start ahead of the body so it does not bloom in the lens
        const int N = 22;
        webLine.positionCount = N;
        if (webGlow) webGlow.positionCount = N;
        float len = Vector3.Distance(tip, anchor);
        float sag = Mathf.Lerp(0.28f, 0.04f, Mathf.Clamp01(taut)) * Mathf.Max(0.4f, len);
        Vector3 side = Vector3.Cross(anchor - tip, Vector3.up);
        if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
        side.Normalize();
        for (int i = 0; i < N; i++)
        {
            float u = i / (N - 1f);
            Vector3 p = Vector3.Lerp(tip, anchor, u);
            float bow = 4f * u * (1f - u);
            float wob = Mathf.Sin(age * 18f + u * 10f) * 0.04f * (1.1f - taut);
            p += Vector3.down * (sag * bow) + side * (wob * bow);
            webLine.SetPosition(i, p);
            if (webGlow) webGlow.SetPosition(i, p);
        }
        // Thick, bright core + a wider additive glow shell. Pulse a shimmer along the length.
        float pulse = 1f + 0.08f * Mathf.Sin(age * 22f);
        webLine.widthMultiplier = pulse;
        webLine.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.028f),
            new Keyframe(0.18f, 0.070f),
            new Keyframe(1f, 0.060f));
        webLine.startColor = Color.white;
        webLine.endColor = new Color(0.85f, 0.95f, 1f, 1f);
        if (webLine.material && silkShimmerTex)
            webLine.material.mainTextureOffset = new Vector2(-age * 1.8f, 0f);
        if (webGlow)
        {
            webGlow.widthMultiplier = pulse;
            webGlow.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.06f),
                new Keyframe(0.18f, 0.15f),
                new Keyframe(1f, 0.12f));
            webGlow.startColor = new Color(0.7f, 0.92f, 1f, 0.32f);
            webGlow.endColor = new Color(0.55f, 0.85f, 1f, 0.22f);
            if (webGlow.material)
                webGlow.material.mainTextureOffset = new Vector2(-age * 1.8f, 0f);
        }
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
        // Short silk splat on the bark: radial strands that fade quickly.
        Vector3 normal = (pos - new Vector3(axis.x, pos.y, axis.z));
        if (normal.sqrMagnitude < 1e-6f) normal = Vector3.forward;
        normal.Normalize();
        for (int i = 0; i < 7; i++)
        {
            float ang = i / 7f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.right;
            tangent.Normalize();
            Vector3 bitan = Vector3.Cross(normal, tangent);
            Vector3 dir = (tangent * Mathf.Cos(ang) + bitan * Mathf.Sin(ang) + normal * 0.15f).normalized;
            float len = Random.Range(0.18f, 0.38f);
            var go = new GameObject("AnchorSplat");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, pos);
            lr.SetPosition(1, pos + dir * len);
            lr.startWidth = 0.028f;
            lr.endWidth = 0.008f;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var mat = MakeSilkMat(new Color(1f, 1f, 1f, 0.95f), false, false);
            lr.material = mat;
            fadingSilk.Add(lr);
            fadingAge.Add(0f);
            fadingLife.Add(0.35f);
            fadingMat.Add(mat);
        }
        BurstParticles(pos, normal, false);
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
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, celebrate ? 0.06f : 0.05f);
        main.startColor = celebrate ? new Color(1f, 0.85f, 0.25f, 1f) : new Color(0.85f, 1f, 0.7f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = celebrate ? 0.35f : 0.8f;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(celebrate ? 48 : 22)) });
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
        webLine.startWidth = 0.085f;
        webLine.endWidth = 0.055f;
        webLine.useWorldSpace = true;
        webLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        webLine.receiveShadows = false;
        webLine.numCapVertices = 4;
        webLine.numCornerVertices = 2;
        webLine.textureMode = LineTextureMode.Tile;
        webLine.material = MakeSilkMat(Color.white, false, false);
        if (webLine.material && silkShimmerTex)
        {
            webLine.material.mainTexture = silkShimmerTex;
            webLine.material.mainTextureScale = new Vector2(3f, 1f);
        }
        webLine.startColor = Color.white;
        webLine.endColor = new Color(0.85f, 0.95f, 1f, 1f);
        webLine.enabled = false;

        var glowGo = new GameObject("TCWebGlow");
        glowGo.transform.SetParent(transform, false);
        webGlow = glowGo.AddComponent<LineRenderer>();
        webGlow.positionCount = 2;
        webGlow.startWidth = 0.20f;
        webGlow.endWidth = 0.12f;
        webGlow.useWorldSpace = true;
        webGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        webGlow.receiveShadows = false;
        webGlow.numCapVertices = 4;
        webGlow.textureMode = LineTextureMode.Tile;
        webGlow.material = MakeSilkMat(new Color(0.7f, 0.92f, 1f, 0.65f), true, true);
        webGlow.startColor = new Color(0.7f, 0.92f, 1f, 0.42f);
        webGlow.endColor = new Color(0.55f, 0.85f, 1f, 0.28f);
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


    /// Procedural W/E/B pickup. Parent is the track bonus root; caller pools or destroys after collect.
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
        col.radius = 0.55f;
        var letterComp = item.AddComponent<TCWebLetter>();
        letterComp.letter = letter;

        // Billboard letter card — large, brightly colored, easy to read on the trunk
        var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(card.GetComponent<Collider>());
        card.name = "Glyph";
        card.transform.SetParent(item.transform, false);
        card.transform.localScale = Vector3.one * 1.05f;
        var mr = card.GetComponent<MeshRenderer>();
        mr.sharedMaterial = LetterMaterial(letter);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(glow.GetComponent<Collider>());
        glow.name = "Glow";
        glow.transform.SetParent(item.transform, false);
        glow.transform.localPosition = new Vector3(0, 0, 0.03f);
        glow.transform.localScale = Vector3.one * 1.75f;
        var gr = glow.GetComponent<MeshRenderer>();
        gr.sharedMaterial = LetterGlowMaterial(letter);
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        gr.receiveShadows = false;

        var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(halo.GetComponent<Collider>());
        halo.name = "Halo";
        halo.transform.SetParent(item.transform, false);
        halo.transform.localPosition = new Vector3(0, 0, 0.05f);
        halo.transform.localScale = Vector3.one * 2.35f;
        var hr = halo.GetComponent<MeshRenderer>();
        hr.sharedMaterial = LetterGlowMaterial(letter);
        hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hr.receiveShadows = false;

        var bill = item.AddComponent<TCWebLetterBillboard>();
        bill.visual = item.transform;
        ApplyLetterVisual(root, letter);
        return root;
    }

    public static Color LetterColor(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        if (letter == 'W') return new Color(1f, 0.78f, 0.12f, 1f);       // gold
        if (letter == 'E') return new Color(0.12f, 0.95f, 0.48f, 1f);     // emerald
        return new Color(0.28f, 0.52f, 1f, 1f);                           // azure B
    }

    public static void ApplyLetterVisual(GameObject root, char letter)
    {
        if (!root) return;
        letter = char.ToUpperInvariant(letter);
        var glyph = LetterMaterial(letter);
        var glow = LetterGlowMaterial(letter);
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!r) continue;
            if (r.name == "Glyph") r.sharedMaterial = glyph;
            else if (r.name == "Glow" || r.name == "Halo") r.sharedMaterial = glow;
        }
        var bill = root.GetComponentInChildren<TCWebLetterBillboard>(true);
        if (bill) bill.PlayPop();
    }

    static readonly Dictionary<char, Material> letterMats = new Dictionary<char, Material>();
    static readonly Dictionary<char, Material> glowMats = new Dictionary<char, Material>();
    public static Material LetterMaterial(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        if (letterMats.TryGetValue(letter, out var m) && m) return m;
        var tex = LetterTexture(letter);
        m = MakeLetterUnlit("WEB_" + letter, tex, Color.white, true);
        letterMats[letter] = m;
        return m;
    }

    public static Material LetterGlowMaterial(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        if (glowMats.TryGetValue(letter, out var m) && m) return m;
        var c = LetterColor(letter);
        m = MakeLetterUnlit("WEBGlow_" + letter, null, new Color(c.r, c.g, c.b, 0.42f), true);
        glowMats[letter] = m;
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

    static Texture2D LetterTexture(char letter)
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        var col = LetterColor(letter);
        var card = Color.Lerp(col, Color.white, 0.22f);
        var rim = Color.Lerp(col, Color.white, 0.7f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2 - 1, v = (y + 0.5f) / N * 2 - 1;
                float ax = Mathf.Abs(u) - 0.70f, ay = Mathf.Abs(v) - 0.70f;
                float d = Mathf.Min(Mathf.Max(ax, ay), 0) + new Vector2(Mathf.Max(ax, 0), Mathf.Max(ay, 0)).magnitude - 0.20f;
                float a = Mathf.Clamp01(-d * 36f);
                float edge = Mathf.Clamp01(1f - Mathf.Abs(d) * 18f);
                var fill = Color.Lerp(card, rim, edge * 0.85f);
                fill.a = a;
                px[y * N + x] = fill;
            }
        tex.SetPixels(px);
        StampLetter(tex, letter);
        tex.Apply(false, false);
        return tex;
    }

    static void StampLetter(Texture2D tex, char letter)
    {
        int N = tex.width;
        // Very small bitmap-ish strokes for W, E, B so we don't depend on a font at runtime.
        void Ink(int x, int y, float a = 1)
        {
            if ((uint)x >= (uint)N || (uint)y >= (uint)N) return;
            var c = tex.GetPixel(x, y);
            var ink = new Color(0.08f, 0.07f, 0.12f, 1);
            c = Color.Lerp(c, ink, a * 0.95f);
            c.a = Mathf.Max(c.a, a);
            tex.SetPixel(x, y, c);
        }
        void Disc(float cx, float cy, float r)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a > 0) Ink(x, y, a);
                }
        }
        void Capsule(float x0, float y0, float x1, float y1, float r)
        {
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
        float r = 5.2f * s;
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

/// Keeps the letter card facing the camera and gives it a gentle bob.
public class TCWebLetterBillboard : MonoBehaviour
{
    public Transform visual;
    Vector3 basePos;
    float pop = 1f;
    float age;

    void OnEnable()
    {
        if (visual) basePos = visual.localPosition;
        PlayPop();
    }

    public void PlayPop()
    {
        pop = 0f;
        age = 0f;
        if (visual) visual.localScale = Vector3.zero;
    }

    void LateUpdate()
    {
        if (!visual) return;
        age += Time.deltaTime;
        pop = Mathf.MoveTowards(pop, 1f, Time.deltaTime * 5.5f);
        // Short overshoot pop-in, then a lively bob + pulse.
        float over = 1f + Mathf.Sin(Mathf.Clamp01(pop) * Mathf.PI) * 0.22f * (1f - pop);
        float s = Mathf.SmoothStep(0f, 1f, pop) * over;
        visual.localScale = Vector3.one * s;
        var cam = Camera.main;
        if (cam) visual.rotation = Quaternion.LookRotation(visual.position - cam.transform.position, Vector3.up);
        visual.localPosition = basePos + Vector3.up * (Mathf.Sin(age * 3.6f) * 0.11f);
        var glow = visual.Find("Glow");
        if (glow) glow.localScale = Vector3.one * (1.75f * (1f + 0.08f * Mathf.Sin(age * 5f)));
        var halo = visual.Find("Halo");
        if (halo) halo.localScale = Vector3.one * (2.35f * (1f + 0.10f * Mathf.Sin(age * 4.2f + 1f)));
    }
}
