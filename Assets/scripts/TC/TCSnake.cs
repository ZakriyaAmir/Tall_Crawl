using UnityEngine;

/// Cute low-poly snake that spirals DOWN the trunk on a helix (like a spring), head first.
/// The body follows the head's exact path (path-following slither) with a travelling serpentine wave, so the whole
/// snake lies on one static helical track: at any lane the player meets it as ONE thin diagonal band that a normal
/// jump clears. It covers well under one turn of the trunk, so there is never a second coil to land on.
/// Damage, shield, invincibility, WEB bonus and chilli smash all go through the shared TCEnemy / playerInteract path.
public class TCSnake : TCEnemy
{
    // ---- shape & motion tuning (world units; trunk cylinder radius 0.50) ----
    public const float TrunkR = 0.50f;
    public const float Slope = 0.42f;          // rise per unit of horizontal travel around the trunk (helix pitch 1.32u / turn)
    public const float BodyLen = 1.45f;        // head-to-tail-tip arc length (covers ~153 deg of the trunk)
    public const float MaxRadius = 0.078f;     // thickest body radius
    public const float HitR = 0.56f;           // axis distance of the hit spheres (spider hit capsule centre ~0.564)
    public const float PathSpeed = 0.97f;      // slither speed along the helix (~85 deg/s around, 0.37 u/s downward)
    public const float WaveLen = 0.52f, WaveAmp = 0.032f;
    public const int Rings = 52, Sides = 18;
    static readonly float K = Slope * TrunkR;                          // dy per radian
    static readonly float Ell = Mathf.Sqrt(TrunkR * TrunkR + K * K);     // arc length per radian

    public Transform head, tongue;
    public Transform[] tongueJoints;
    public const int TongueJoints = 4; public const float TongueSeg = 0.022f;
    public Transform[] hitPoints;              // sphere colliders along the body
    public MeshFilter bodyFilter, hullFilter;
    public enum Variant { Green, Yellow, Red }
    public Variant variant = Variant.Green;
    [Tooltip("Slither speed multiplier: yellow 0.6, green 1, red 2.2")] public float speedMul = 1f;
    public float Speed => PathSpeed * speedMul;
    public static float SpeedMulFor(Variant v) => v == Variant.Yellow ? 0.6f : v == Variant.Red ? 2.2f : 1f;

    float theta0, y0, sHead; int dir = 1;
    float tongueTimer, tongueClock, hissTimer; bool hissed;
    public const float TongueCycle = 1.25f;

    /// 0 = in, 1 = fully out. Two quick flicks (0.18s each) then a pause, looping every TongueCycle seconds.
    public static float TongueOut(float clock)
    {
        float c = Mathf.Repeat(clock, TongueCycle);
        if (c < 0.18f) return Mathf.Sin(c / 0.18f * Mathf.PI);
        if (c > 0.24f && c < 0.42f) return Mathf.Sin((c - 0.24f) / 0.18f * Mathf.PI);
        return 0f;
    }
    Mesh bodyMesh, hullMesh;
    Vector3[] centers = new Vector3[Rings], fwd = new Vector3[Rings], outN = new Vector3[Rings];
    Vector3[] flatV, hullV, hullN, flatN;
    int[] flatSrc; bool hullChecked;                              // flat vertex -> smooth vertex index
    static readonly float[] HitU = { 0.08f, 0.15f, 0.22f, 0.29f, 0.36f, 0.43f, 0.50f, 0.57f, 0.64f, 0.71f, 0.78f }; // ~0.1u apart: no gaps the spider could slip through

    public override bool CanJumpOver => true;

    public static float RadiusAt(float u) // u: 0 = neck (behind head), 1 = tail tip
    {
        float swell = Mathf.SmoothStep(0.70f, 1f, Mathf.Clamp01(u / 0.22f));
        float taper = u < 0.45f ? 1f : Mathf.Lerp(1f, 0.10f, Mathf.Pow((u - 0.45f) / 0.55f, 1.15f));
        return MaxRadius * swell * taper;
    }

    /// Point on the (unwaved) helical track at path coordinate s, radial offset h above the bark.
    Vector3 Track(float s, float h, out Vector3 outward)
    {
        float th = theta0 + dir * s / Ell; float y = y0 - K * s / Ell;
        outward = new Vector3(Mathf.Sin(th), 0, Mathf.Cos(th));
        return outward * (TrunkR + h) + Vector3.up * y;
    }

    /// Head placed at (y, angle) with the body trailing up-trunk behind it.
    public override void Init(float startY, float startAngle, float sweep = 40f, float spd = 2.2f)
    {
        // Randomly decide clockwise (dir = 1) or anticlockwise (dir = -1) spin direction
        dir = Random.value < 0.5f ? 1 : -1;
        SetupHelix(startAngle * Mathf.Deg2Rad - dir * BodyLen / Ell, startY + K * BodyLen / Ell, dir, BodyLen);
        base.Init(startY, startAngle, sweep, spd);
    }

    public void SetupHelix(float th0Rad, float yTop, int direction, float sHeadStart)
    {
        theta0 = th0Rad; y0 = yTop; dir = (direction == 0) ? (Random.value < 0.5f ? 1 : -1) : direction; sHead = sHeadStart; sweepPrimed = false;
        hissed = false; tongueClock = Random.Range(0f, TongueCycle); isSmashed = false;
        Pose(0f);
    }

    /// Path coordinate (from the helix origin) of the head; exposed for the jump-clearance harness.
    public static float ArcPerRadian => Ell;
    public static float RisePerRadian => K;

    protected override void UpdateTransform() { transform.position = Vector3.zero; transform.rotation = Quaternion.identity; }

    void Update()
    {
        if (isSmashed || IsGameOverOrDead()) return;
        bool isTutorial = TCGameUI.Instance != null && TCGameUI.Instance.IsTutorialRun;
        if (!isTutorial)
        {
            var p = player.Instance;
            if (p && !p.GameOver && p.paused) return;
            if (p && p.currentScore < 200f) return;
            if (TCGameUI.Instance && !TCGameUI.Instance.Started) return;
        }

        float dt = Time.deltaTime;
        sHead += Speed * (IsPlayerInChilliMode() ? 1.25f : 1f) * dt;
        Pose(Time.time);
        SweepHitCheck();

        // warning cue: one hiss as it slides into view from above, tongue flicks faster when the spider is near
        var pl = player.Instance;
        float dy = pl ? y - pl.transform.position.y : 99f;
        if (!hissed && dy < 5.6f && dy > -1f) { hissed = true; TCSfx.Play("hiss", 0.75f, Random.Range(0.95f, 1.08f)); }
        tongueClock += dt * (dy < 2.5f ? 1.6f : 1f) * Mathf.Lerp(1f, speedMul, 0.6f); // faster near the spider and for quicker variants
    }

    // ---- swept hit check: at 2.5x the spider climbs ~4 u/s, so on a slow frame (low-end phone, hitch) it can step clean
    // over the thin body between two physics updates and the trigger never fires. Sweep the spider's hit capsule from
    // last frame to this one and deliver the same trigger the physics would have, so a no-jump crossing always hurts.
    static playerInteract sweepPI;
    Vector3 sweepLast; bool sweepPrimed;
    SphereCollider[] sweepCols;
    void SweepHitCheck()
    {
        if (!sweepPI) sweepPI = FindFirstObjectByType<playerInteract>();
        var hcap = sweepPI ? sweepPI.HitCollider : null;
        if (!hcap || !hcap.enabled || !hcap.gameObject.activeInHierarchy) { sweepPrimed = false; return; }
        Vector3 c1 = hcap.bounds.center;
        if (!sweepPrimed) { sweepLast = c1; sweepPrimed = true; return; }
        Vector3 c0 = sweepLast; sweepLast = c1;
        float step = Vector3.Distance(c0, c1);
        if (step < 0.04f || step > 1.5f || sweepPI.Invulnerable) return; // small steps: physics triggers are reliable; big jumps = teleport
        if (sweepCols == null || sweepCols.Length == 0) sweepCols = GetComponentsInChildren<SphereCollider>(true);
        Vector3 ext = hcap.bounds.extents; float pr = Mathf.Min(ext.x, ext.z); float half = Mathf.Max(0f, ext.y - pr);
        int n = Mathf.CeilToInt(step / 0.02f);
        for (int i = 1; i < n; i++) // interior samples only: both end poses are covered by the physics engine
        {
            Vector3 c = Vector3.Lerp(c0, c1, (float)i / n);
            foreach (var sc in sweepCols)
            {
                if (!sc || !sc.enabled || !sc.gameObject.activeInHierarchy) continue;
                Vector3 sp = sc.bounds.center; float sr = sc.bounds.extents.x;
                Vector3 q = new Vector3(c.x, Mathf.Clamp(sp.y, c.y - half, c.y + half), c.z); // closest point on the capsule core
                if ((sp - q).sqrMagnitude < (sr + pr) * (sr + pr)) { sweepPI.SendMessage("OnTriggerEnter", sc, SendMessageOptions.DontRequireReceiver); return; }
            }
        }
    }

    void Pose(float t)
    {
        float wavePhase = 0f;
        // ring centres along the body, following the path behind the head with a world-fixed serpentine wave
        for (int i = 0; i < Rings; i++)
        {
            float u = (float)i / (Rings - 1);
            float s = sHead - 0.07f - u * (BodyLen - 0.07f);
            float r = RadiusAt(u);
            Vector3 o; Vector3 p = Track(s, r * 0.78f, out o);
            Vector3 o2; Vector3 p2 = Track(s + 0.02f, r * 0.78f, out o2);
            Vector3 tan = (p2 - p).normalized;
            Vector3 lat = Vector3.Cross(o, tan).normalized;
            float amp = WaveAmp * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(u / 0.3f)) * Mathf.Lerp(1f, 0.6f, u);
            centers[i] = p + lat * (amp * Mathf.Sin(2f * Mathf.PI * s / WaveLen + wavePhase));
            outN[i] = o;
        }
        for (int i = 0; i < Rings; i++)
        {
            Vector3 f = i == 0 ? centers[0] - centers[1] : (i == Rings - 1 ? centers[i - 1] - centers[i] : centers[i - 1] - centers[i + 1]);
            fwd[i] = f.normalized;
        }
        y = centers[0].y;
        if (pooled) pooled.y = Mathf.Max(centers[Rings - 1].y, y);
        BuildMesh();

        // head: at the neck front, facing the direction of travel, up = away from the bark
        if (head)
        {
            Vector3 o; Vector3 hp = Track(sHead, MaxRadius * 0.85f, out o);
            Vector3 f = (hp - centers[0]); if (f.sqrMagnitude < 1e-6f) f = fwd[0];
            f = Vector3.ProjectOnPlane(f.normalized, o).normalized;
            float yaw = Mathf.Sin(2f * Mathf.PI * sHead / WaveLen) * 9f;
            head.position = centers[0] + f * 0.035f + o * 0.012f; // neck end tucked inside the skull (no visible cut)
            head.rotation = Quaternion.AngleAxis(yaw, o) * Quaternion.LookRotation(f, o);
        }
        if (tongue)
        {
            // real-snake flick loop: out-in, out-in (quick double flick), short pause, repeat
            float flick = TongueOut(tongueClock);
            // extend/retract along the mouth axis, and flex like a whip: each joint bends up/down (and a little sideways)
            // with a phase lag down the chain, so the forked tip tastes the air instead of poking out as a stick
            tongue.localScale = new Vector3(1f, 1f, Mathf.Max(0.03f, flick));
            tongue.localRotation = Quaternion.Euler(4f, 0, 0);
            if (tongueJoints != null)
                for (int j = 0; j < tongueJoints.Length; j++)
                {
                    float lag = j * 0.95f;
                    float pitch = Mathf.Sin(t * 30f - lag) * (10f + 9f * j) * flick;
                    float yawJ = Mathf.Sin(t * 17f - lag * 0.7f) * 5f * j * flick;
                    tongueJoints[j].localRotation = Quaternion.Euler(pitch, yawJ, 0);
                }
            tongue.gameObject.SetActive(flick > 0.04f);
        }
        if (hitPoints != null)
            for (int k = 0; k < hitPoints.Length && k < HitU.Length; k++)
            {
                float fi = HitU[k] * (Rings - 1); int i0 = Mathf.FloorToInt(fi); int i1 = Mathf.Min(Rings - 1, i0 + 1);
                Vector3 hcP = Vector3.Lerp(centers[i0], centers[i1], fi - i0);
                // pull the hit sphere in to the spider's hit radius (its capsule rides ~0.56-0.6u from the axis,
                // while the visual body sits on top of the bark): visible overlap == damage, no grazing misses
                Vector2 hz = new Vector2(hcP.x, hcP.z); if (hz.sqrMagnitude > 1e-6f) hz = hz.normalized * HitR;
                hitPoints[k].position = new Vector3(hz.x, hcP.y, hz.y);
            }
        transform.position = Vector3.zero; transform.rotation = Quaternion.identity;
    }

    // ---------------- dynamic smooth tube (spider-style: smooth normals, skin texture, shader outline) ----------------
    Vector2[] tubeUV;
    void EnsureMesh()
    {
        if (bodyMesh) return;
        int row = Sides + 1; // duplicated seam column for UVs (normals welded)
        int count = Rings * row + 1;
        hullV = new Vector3[count]; hullN = new Vector3[count]; tubeUV = new Vector2[count];
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < Rings - 1; i++)
            for (int k = 0; k < Sides; k++)
            {
                int a = i * row + k, b = a + 1, c = a + row, d = b + row;
                tris.Add(a); tris.Add(d); tris.Add(c); tris.Add(a); tris.Add(b); tris.Add(d);
            }
        int tip = count - 1, last = (Rings - 1) * row;
        for (int k = 0; k < Sides; k++) { tris.Add(last + k); tris.Add(last + k + 1); tris.Add(tip); }
        for (int k = 1; k < Sides - 1; k++) { tris.Add(0); tris.Add(k + 1); tris.Add(k); } // neck cap (inside the head)
        for (int i = 0; i < Rings; i++) for (int k = 0; k <= Sides; k++) tubeUV[i * row + k] = new Vector2(k / (float)Sides, i / (float)(Rings - 1));
        tubeUV[tip] = new Vector2(0.5f, 1f);
        bodyMesh = new Mesh { name = "SnakeBody" }; bodyMesh.MarkDynamic();
        bodyMesh.vertices = hullV; bodyMesh.normals = hullN; bodyMesh.uv = tubeUV; bodyMesh.SetTriangles(tris, 0);
        if (bodyFilter) bodyFilter.sharedMesh = bodyMesh;
    }

    void BuildMesh()
    {
        EnsureMesh();
        int row = Sides + 1;
        for (int i = 0; i < Rings; i++)
        {
            float u = (float)i / (Rings - 1); float r = RadiusAt(u);
            Vector3 f = fwd[i], up = Vector3.ProjectOnPlane(outN[i], f).normalized, side = Vector3.Cross(f, up);
            for (int k = 0; k <= Sides; k++)
            {
                float th = 2f * Mathf.PI * k / Sides;
                Vector3 d = up * Mathf.Cos(th) * 0.84f + side * Mathf.Sin(th); // slightly flattened against the bark
                hullV[i * row + k] = centers[i] + d * r;
                hullN[i * row + k] = (up * Mathf.Cos(th) / 0.84f + side * Mathf.Sin(th)).normalized;
            }
        }
        int tip = hullV.Length - 1;
        hullV[tip] = centers[Rings - 1] - fwd[Rings - 1] * 0.045f; hullN[tip] = -fwd[Rings - 1];
        bodyMesh.vertices = hullV; bodyMesh.normals = hullN;
        if (!hullChecked)
        {
            hullChecked = true; var tr = bodyMesh.triangles;
            Vector3 n0 = Vector3.Cross(hullV[tr[1]] - hullV[tr[0]], hullV[tr[2]] - hullV[tr[0]]);
            if (Vector3.Dot(n0, hullN[tr[0]] + hullN[tr[1]] + hullN[tr[2]]) < 0f)
            { for (int i = 0; i < tr.Length; i += 3) { int t = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = t; } bodyMesh.triangles = tr; }
        }
        bodyMesh.RecalculateBounds();
    }

    // ---------------- prefab ----------------
    public static readonly Color TongueRed = new Color(0.651f, 0.012f, 0.129f); // palette crimson #A60321
    struct Skin { public Color back, blotch, belly, head, pupil, blush; }
    static Skin SkinFor(Variant v)
    {
        switch (v)
        {
            case Variant.Yellow: return new Skin { back = new Color(1f, 0.84f, 0.20f), blotch = new Color(0.88f, 0.50f, 0.08f), belly = new Color(1f, 0.97f, 0.78f), head = new Color(1f, 0.82f, 0.22f), pupil = new Color(0.30f, 0.78f, 1f), blush = new Color(1f, 0.55f, 0.35f) };
            case Variant.Red: return new Skin { back = new Color(0.93f, 0.22f, 0.17f), blotch = new Color(0.48f, 0.05f, 0.08f), belly = new Color(1f, 0.82f, 0.56f), head = new Color(0.92f, 0.24f, 0.18f), pupil = new Color(1f, 0.88f, 0.22f), blush = new Color(1f, 0.55f, 0.55f) };
            default: return new Skin { back = new Color(0.50f, 0.82f, 0.30f), blotch = new Color(0.20f, 0.50f, 0.20f), belly = new Color(1f, 0.92f, 0.52f), head = new Color(0.52f, 0.83f, 0.32f), pupil = new Color(1f, 0.82f, 0.16f), blush = new Color(1f, 0.55f, 0.45f) };
        }
    }

    public static GameObject CreateSnakePrefab(Transform parent) => CreateSnakePrefab(parent, Variant.Green);

    public static GameObject CreateSnakePrefab(Transform parent, Variant variant)
    {
        var sk = SkinFor(variant); string vn = variant.ToString();
        var root = new GameObject("Snake_" + vn);
        if (parent) root.transform.SetParent(parent, false);
        var sn = root.AddComponent<TCSnake>();
        sn.enemyType = TCEnemyType.Snake; sn.radius = TrunkR; sn.variant = variant; sn.speedMul = SpeedMulFor(variant);
        var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;

        var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
        sn.bodyFilter = body.AddComponent<MeshFilter>();
        var mr = body.AddComponent<MeshRenderer>();
        mr.sharedMaterial = TCToon.Mat("snake_body_" + vn, Color.white, TCToon.SnakeSkin("tc_snake_skin_" + vn, sk.back, sk.blotch, sk.belly, 8));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        sn.model = body.transform;

        // head (local: +Z forward, +Y away from bark): rounded spade skull, spider eyes on top, smiling chin
        var headMat = TCToon.Mat("snake_head_" + vn, sk.head);
        var bellyMat = TCToon.Mat("snake_belly_" + vn, sk.belly);
        var darkMat = TCToon.Mat("snake_dark_" + vn, sk.blotch);
        var head = new GameObject("Head").transform; head.SetParent(root.transform, false); sn.head = head;
        TCToon.Part("Skull", head, TCToon.Lathe("tc_snake_skull", -0.065f, 0.112f,
            a => 0.066f * Mathf.Pow(Mathf.Sin(a * Mathf.PI), 0.42f) * Mathf.Lerp(1.0f, 0.72f, a * a), 1.16f, 0.62f, a => new Vector2(0, 0.010f * Mathf.Sin(a * Mathf.PI))),
            headMat, new Vector3(0, 0.006f, 0f), Vector3.zero, Vector3.one);
        TCToon.Part("Chin", head, TCToon.Lathe("tc_snake_chin", -0.05f, 0.09f, a => Mathf.Pow(Mathf.Sin(a * Mathf.PI), 0.55f) * 0.058f, 1.05f, 0.45f),
            bellyMat, new Vector3(0, -0.024f, 0.012f), Vector3.zero, Vector3.one);
        TCToon.Ball("Crown", head, darkMat, new Vector3(0, 0.04f, -0.035f), new Vector3(0.022f, 0.008f, 0.03f));
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            string side = sgn < 0 ? "L" : "R";
            TCToon.Ball("Brow" + side, head, headMat, new Vector3(sgn * 0.037f, 0.036f, 0.028f), new Vector3(0.034f, 0.026f, 0.034f));
            TCToon.Eye("Eye" + side, head, new Vector3(sgn * 0.037f, 0.058f, 0.03f), new Vector3(sgn * 0.36f, 0.86f, 0.38f), 0.030f, sk.pupil, 0.6f);
            TCToon.Ball("Nostril" + side, head, TCToon.Mat("snake_nostril", TCToon.EyeBall, null, 0f, 0f, 0f), new Vector3(sgn * 0.016f, 0.022f, 0.098f), new Vector3(0.0055f, 0.004f, 0.004f));
            TCToon.Ball("Cheek" + side, head, TCToon.Mat("snake_blush_" + vn, sk.blush, null, 0f, 0f, 0f), new Vector3(sgn * 0.058f, 0.004f, 0.05f), new Vector3(0.016f, 0.008f, 0.012f), new Vector3(0, sgn * 30f, 0));
        }
        // forked tongue: 4-joint flexible chain (each joint bends up/down with a lagging sine) ending in a forked tip
        var tg = new GameObject("Tongue").transform; tg.SetParent(head, false); tg.localPosition = new Vector3(0, -0.012f, 0.098f); sn.tongue = tg;
        var tm = TCToon.Mat("snake_tongue", TongueRed, null, 0.1f, 1.0f, 0f, null, 0.95f);
        Transform link = tg; var joints = new Transform[TongueJoints];
        for (int j = 0; j < TongueJoints; j++)
        {
            var jt = new GameObject("TJ" + j).transform; jt.SetParent(link, false); jt.localPosition = j == 0 ? Vector3.zero : new Vector3(0, 0, TongueSeg);
            float r0 = Mathf.Lerp(0.0078f, 0.0052f, j / (float)TongueJoints), r1 = Mathf.Lerp(0.0078f, 0.0052f, (j + 1) / (float)TongueJoints);
            TCToon.Part("Seg", jt, TCToon.Lathe("tc_snake_tseg" + j, -0.002f, TongueSeg + 0.002f, a => Mathf.Lerp(r0, r1, a), 1f, 0.75f, null, 10, 4), tm, Vector3.zero, Vector3.zero, Vector3.one);
            joints[j] = jt; link = jt;
        }
        var fork = TCToon.Lathe("tc_snake_fork", 0f, 0.036f, a => 0.0055f * Mathf.Pow(1f - a, 0.8f), 1f, 0.75f, null, 10, 6);
        var tipT = new GameObject("Fork").transform; tipT.SetParent(link, false); tipT.localPosition = new Vector3(0, 0, TongueSeg);
        TCToon.Part("ForkL", tipT, fork, tm, Vector3.zero, new Vector3(0, -30f, 0), Vector3.one);
        TCToon.Part("ForkR", tipT, fork, tm, Vector3.zero, new Vector3(0, 30f, 0), Vector3.one);
        sn.tongueJoints = joints;
        root.AddComponent<TCBlink>();

        // hit spheres: head + body (tail tip excluded so only the visible thick body hurts)
        var hp = new Transform[HitU.Length];
        var hc = head.gameObject.AddComponent<SphereCollider>(); hc.isTrigger = true; hc.radius = 0.056f; hc.center = new Vector3(0, 0f, 0.01f);
        for (int k = 0; k < HitU.Length; k++)
        {
            var g = new GameObject("Hit" + k); g.transform.SetParent(root.transform, false);
            var c = g.AddComponent<SphereCollider>(); c.isTrigger = true; c.radius = Mathf.Max(0.03f, RadiusAt(HitU[k]) * 0.85f);
            hp[k] = g.transform;
        }
        sn.hitPoints = hp;
        root.tag = "branch"; // same convention as the squirrel; TCEnemy handling runs first in playerInteract
        root.SetActive(false);
        return root;
    }
}
