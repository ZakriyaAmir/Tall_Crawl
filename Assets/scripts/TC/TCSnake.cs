using UnityEngine;

/// Cute low-poly snake that spirals DOWN the trunk on a helix (like a spring), head first.
/// The body follows the head's exact path (path-following slither) with a travelling serpentine wave, so the whole
/// snake lies on one static helical track: at any lane the player meets it as ONE thin diagonal band that a normal
/// jump clears. It covers well under one turn of the trunk, so there is never a second coil to land on.
/// Damage, shield, invincibility, WEB bonus and chilli smash all go through the shared TCEnemy / playerInteract path.
public class TCSnake : TCEnemy
{
    // ---- shape & motion tuning (world units; trunk radius 0.603) ----
    public const float TrunkR = 0.603f;
    public const float Slope = 0.42f;          // rise per unit of horizontal travel around the trunk (helix pitch 1.59u / turn)
    public const float BodyLen = 1.45f;        // head-to-tail-tip arc length (covers ~127 deg of the trunk)
    public const float MaxRadius = 0.078f;     // thickest body radius
    public const float HitR = 0.59f;           // axis distance of the hit spheres (spider hit capsule centre ~0.564)
    public const float PathSpeed = 0.97f;      // slither speed along the helix (~85 deg/s around, 0.37 u/s downward)
    public const float WaveLen = 0.52f, WaveAmp = 0.032f;
    public const int Rings = 26, Sides = 7;
    static readonly float K = Slope * TrunkR;                          // dy per radian
    static readonly float Ell = Mathf.Sqrt(TrunkR * TrunkR + K * K);     // arc length per radian

    public Transform head, tongue;
    public Transform[] tongueJoints;
    public const int TongueJoints = 4; public const float TongueSeg = 0.022f;
    public Transform[] hitPoints;              // sphere colliders along the body
    public MeshFilter bodyFilter, hullFilter;

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
    static readonly float[] HitU = { 0.10f, 0.24f, 0.38f, 0.52f, 0.66f, 0.80f };

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
        dir = Random.value < 0.5f ? 1 : -1;
        SetupHelix(startAngle * Mathf.Deg2Rad - dir * BodyLen / Ell, startY + K * BodyLen / Ell, dir, BodyLen);
        base.Init(startY, startAngle, sweep, spd);
    }

    public void SetupHelix(float th0Rad, float yTop, int direction, float sHeadStart)
    {
        theta0 = th0Rad; y0 = yTop; dir = direction; sHead = sHeadStart;
        hissed = false; tongueClock = Random.Range(0f, TongueCycle); isSmashed = false;
        Pose(0f);
    }

    /// Path coordinate (from the helix origin) of the head; exposed for the jump-clearance harness.
    public static float ArcPerRadian => Ell;
    public static float RisePerRadian => K;

    protected override void UpdateTransform() { transform.position = Vector3.zero; transform.rotation = Quaternion.identity; }

    void Update()
    {
        if (isSmashed) return;
        float dt = Time.deltaTime;
        sHead += PathSpeed * (IsPlayerInChilliMode() ? 1.25f : 1f) * dt;
        Pose(Time.time);

        // warning cue: one hiss as it slides into view from above, tongue flicks faster when the spider is near
        var pl = player.Instance;
        float dy = pl ? y - pl.transform.position.y : 99f;
        if (!hissed && dy < 5.6f && dy > -1f) { hissed = true; TCSfx.Play("hiss", 0.75f, Random.Range(0.95f, 1.08f)); }
        tongueClock += dt * (dy < 2.5f ? 1.6f : 1f); // flicks faster when the spider is close
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
            head.position = centers[0] + f * 0.07f + o * 0.012f;
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

    // ---------------- dynamic faceted tube ----------------
    void EnsureMesh()
    {
        if (bodyMesh) return;
        int ringV = Sides;
        int smoothCount = Rings * ringV + 1; // + tail tip
        hullV = new Vector3[smoothCount]; hullN = new Vector3[smoothCount];
        var smoothTris = new System.Collections.Generic.List<int>();
        var stripeTris = new System.Collections.Generic.List<int>();
        var flatList = new System.Collections.Generic.List<int>();
        var flatStripe = new System.Collections.Generic.List<int>();
        for (int i = 0; i < Rings - 1; i++)
        {
            bool stripe = (i % 4 == 2);
            for (int s = 0; s < Sides; s++)
            {
                int a = i * ringV + s, b = i * ringV + (s + 1) % Sides, c = a + ringV, d = b + ringV;
                int[] q = { a, d, c, a, b, d };
                smoothTris.AddRange(q);
                (stripe ? flatStripe : flatList).AddRange(q);
            }
        }
        int tip = smoothCount - 1; int last = (Rings - 1) * ringV;
        for (int s = 0; s < Sides; s++) { int[] q = { last + s, last + (s + 1) % Sides, tip }; smoothTris.AddRange(q); flatList.AddRange(q); }
        // front cap (hidden by the head, avoids a see-through neck)
        for (int s = 1; s < Sides - 1; s++) { int[] q = { 0, s + 1, s }; smoothTris.AddRange(q); flatList.AddRange(q); }

        flatSrc = new int[flatList.Count + flatStripe.Count];
        flatList.CopyTo(flatSrc, 0); flatStripe.CopyTo(flatSrc, flatList.Count);
        flatV = new Vector3[flatSrc.Length]; flatN = new Vector3[flatSrc.Length];
        bodyMesh = new Mesh { name = "SnakeBody" }; bodyMesh.MarkDynamic();
        bodyMesh.vertices = flatV; bodyMesh.subMeshCount = 2;
        var t0 = new int[flatList.Count]; for (int i = 0; i < t0.Length; i++) t0[i] = i;
        var t1 = new int[flatStripe.Count]; for (int i = 0; i < t1.Length; i++) t1[i] = flatList.Count + i;
        bodyMesh.SetTriangles(t0, 0); bodyMesh.SetTriangles(t1, 1);
        hullMesh = new Mesh { name = "SnakeHull" }; hullMesh.MarkDynamic();
        hullMesh.vertices = hullV; hullMesh.normals = hullN; hullMesh.triangles = smoothTris.ToArray();
        if (bodyFilter) bodyFilter.sharedMesh = bodyMesh;
        if (hullFilter) hullFilter.sharedMesh = hullMesh;
    }

    void BuildMesh()
    {
        EnsureMesh();
        var smooth = new Vector3[hullV.Length];
        for (int i = 0; i < Rings; i++)
        {
            float u = (float)i / (Rings - 1); float r = RadiusAt(u);
            Vector3 f = fwd[i], up = Vector3.ProjectOnPlane(outN[i], f).normalized, side = Vector3.Cross(f, up);
            for (int s = 0; s < Sides; s++)
            {
                float th = 2f * Mathf.PI * (s + (i % 2) * 0.5f) / Sides;
                Vector3 d = up * Mathf.Cos(th) * 0.82f + side * Mathf.Sin(th); // slightly flattened against the bark
                smooth[i * Sides + s] = centers[i] + d * r;
                hullV[i * Sides + s] = centers[i] + d * (r - 0.004f);
                hullN[i * Sides + s] = d.normalized;
            }
        }
        Vector3 tipP = centers[Rings - 1] - fwd[Rings - 1] * 0.05f;
        smooth[hullV.Length - 1] = tipP; hullV[hullV.Length - 1] = tipP; hullN[hullV.Length - 1] = -fwd[Rings - 1];
        for (int i = 0; i < flatSrc.Length; i += 3)
        {
            Vector3 a = smooth[flatSrc[i]], b = smooth[flatSrc[i + 1]], c = smooth[flatSrc[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            // keep every facet wound outward (tube sides away from the spine, neck cap forward)
            int ia = flatSrc[i], ib = flatSrc[i + 1], ic = flatSrc[i + 2];
            Vector3 want;
            if (ia < Sides && ib < Sides && ic < Sides) want = fwd[0];
            else { int ring = Mathf.Min(Rings - 1, Mathf.Min(ia, Mathf.Min(ib, ic)) / Sides); want = (a + b + c) / 3f - centers[ring]; }
            if (Vector3.Dot(n, want) < 0f) { var tmp = b; b = c; c = tmp; n = -n; }
            flatV[i] = a; flatV[i + 1] = b; flatV[i + 2] = c;
            flatN[i] = flatN[i + 1] = flatN[i + 2] = n;
        }
        bodyMesh.vertices = flatV; bodyMesh.normals = flatN; bodyMesh.RecalculateBounds();
        hullMesh.vertices = hullV; hullMesh.normals = hullN;
        if (!hullChecked)
        {
            hullChecked = true; var tr = hullMesh.triangles;
            Vector3 n0 = Vector3.Cross(hullV[tr[1]] - hullV[tr[0]], hullV[tr[2]] - hullV[tr[0]]);
            if (Vector3.Dot(n0, hullN[tr[0]] + hullN[tr[1]] + hullN[tr[2]]) < 0f)
            { for (int i = 0; i < tr.Length; i += 3) { int t = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = t; } hullMesh.triangles = tr; }
        }
        hullMesh.RecalculateBounds();
    }

    // ---------------- prefab ----------------
    public static readonly Color Green = new Color(0.45f, 0.74f, 0.30f), GreenDark = new Color(0.26f, 0.52f, 0.22f),
        Belly = new Color(0.98f, 0.86f, 0.42f), Blush = new Color(0.85f, 0.48f, 0.40f), TongueRed = new Color(0.651f, 0.012f, 0.129f); // palette crimson #A60321

    public static GameObject CreateSnakePrefab(Transform parent)
    {
        var root = new GameObject("Snake_Enemy");
        if (parent) root.transform.SetParent(parent, false);
        var sn = root.AddComponent<TCSnake>();
        sn.enemyType = TCEnemyType.Snake; sn.radius = TrunkR;
        var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;

        var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
        sn.bodyFilter = body.AddComponent<MeshFilter>();
        var mr = body.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { TCLowPoly.Mat(Green, 0.45f, 1.05f), TCLowPoly.Mat(Belly, 0.40f, 1.05f) };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        var hull = new GameObject("BodyHull"); hull.transform.SetParent(root.transform, false);
        sn.hullFilter = hull.AddComponent<MeshFilter>();
        var hr = hull.AddComponent<MeshRenderer>(); hr.sharedMaterial = TCLowPoly.HullMat(TCLowPoly.Ink, 1.25f);
        hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; hr.receiveShadows = false;
        sn.model = body.transform;

        // head (local: +Z forward, +Y away from bark)
        var head = new GameObject("Head").transform; head.SetParent(root.transform, false); sn.head = head;
        var skull = TCLowPoly.Ellipsoid(8, 6, new Vector3(0.072f, 0.05f, 0.098f), 0.03f, 7, t => Mathf.Lerp(1f, 0.92f, t));
        TCLowPoly.Part("Skull", head, skull, TCLowPoly.Mat(Green, 0.45f, 1.05f), new Vector3(0, 0, 0.0f), Vector3.zero);
        var jaw = TCLowPoly.Ellipsoid(7, 5, new Vector3(0.06f, 0.026f, 0.08f), 0f, 3);
        TCLowPoly.Part("Chin", head, jaw, TCLowPoly.Mat(Belly, 0.35f, 1.05f), new Vector3(0, -0.022f, 0.012f), Vector3.zero);
        var patch = TCLowPoly.Ellipsoid(6, 4, new Vector3(0.03f, 0.012f, 0.05f), 0f, 5);
        TCLowPoly.Part("Crown", head, patch, TCLowPoly.Mat(GreenDark, 0.4f, 1.0f), new Vector3(0, 0.045f, -0.02f), Vector3.zero, null, false);
        var eyeW = TCLowPoly.Ellipsoid(8, 6, Vector3.one * 0.03f, 0f, 2);
        var eyeB = TCLowPoly.Ellipsoid(7, 5, Vector3.one * 0.019f, 0f, 2);
        var spark = TCLowPoly.Ellipsoid(5, 4, Vector3.one * 0.0075f, 0f, 2);
        var cheek = TCLowPoly.Ellipsoid(6, 4, new Vector3(0.016f, 0.008f, 0.012f), 0f, 4);
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            var e = TCLowPoly.Part(sgn < 0 ? "EyeL" : "EyeR", head, eyeW, TCLowPoly.Mat(Color.white, 0.2f, 1.1f), new Vector3(sgn * 0.034f, 0.042f, 0.036f), Vector3.zero);
            TCLowPoly.Part("Pupil", e, eyeB, TCLowPoly.Mat(new Color(0.08f, 0.05f, 0.05f), 0.1f, 1f), new Vector3(sgn * 0.004f, 0.014f, 0.01f), Vector3.zero, null, false);
            TCLowPoly.Part("Spark", e, spark, TCLowPoly.Mat(Color.white, 0f, 1.2f), new Vector3(sgn * 0.006f + 0.004f, 0.03f, 0.006f), Vector3.zero, null, false);
            TCLowPoly.Part("Cheek", head, cheek, TCLowPoly.Mat(Blush, 0.2f, 1.05f), new Vector3(sgn * 0.058f, 0.006f, 0.05f), new Vector3(0, sgn * 30f, 0), null, false);
        }
        // forked tongue
        var tg = new GameObject("Tongue").transform; tg.SetParent(head, false); tg.localPosition = new Vector3(0, -0.012f, 0.092f); sn.tongue = tg;
        // flexible tongue: 4-joint chain (each joint bends up/down with a lagging sine) ending in a forked tip
        var tm = TCLowPoly.Mat(TongueRed, 0.2f, 1.1f);
        Transform link = tg; var joints = new Transform[TongueJoints];
        for (int j = 0; j < TongueJoints; j++)
        {
            var jt = new GameObject("TJ" + j).transform; jt.SetParent(link, false); jt.localPosition = j == 0 ? Vector3.zero : new Vector3(0, 0, TongueSeg);
            float r0 = Mathf.Lerp(0.0078f, 0.0052f, j / (float)TongueJoints), r1 = Mathf.Lerp(0.0078f, 0.0052f, (j + 1) / (float)TongueJoints);
            TCLowPoly.Part("Seg", jt, TCLowPoly.Cone(4, r0, r1, TongueSeg + 0.002f), tm, Vector3.zero, new Vector3(90, 0, 0), null, false);
            joints[j] = jt; link = jt;
        }
        var fork = TCLowPoly.Cone(4, 0.0055f, 0.0f, 0.036f);
        var tipT = new GameObject("Fork").transform; tipT.SetParent(link, false); tipT.localPosition = new Vector3(0, 0, TongueSeg);
        TCLowPoly.Part("ForkL", tipT, fork, tm, Vector3.zero, new Vector3(90, 0, 32), null, false);
        TCLowPoly.Part("ForkR", tipT, fork, tm, Vector3.zero, new Vector3(90, 0, -32), null, false);
        sn.tongueJoints = joints;

        // hit spheres: head + body (tail tip excluded so only the visible thick body hurts)
        var hp = new Transform[HitU.Length];
        var hc = head.gameObject.AddComponent<SphereCollider>(); hc.isTrigger = true; hc.radius = 0.056f; hc.center = new Vector3(0, -0.085f, 0.01f); // local up = away from bark: sink to spider hit radius
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
