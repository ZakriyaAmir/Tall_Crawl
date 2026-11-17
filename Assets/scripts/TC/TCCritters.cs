using UnityEngine;

/// polish3 low-poly remakes of the squirrel and the bat (faceted, cel-lit, ink outline hulls via TCLowPoly).
/// Same pivots / names / sizes as the old models so TCSquirrel and TCWasp animation + hitboxes are untouched.
public static class TCCritters
{
    // ---------------- squirrel ----------------
    static readonly Color Fur = new Color(0.92f, 0.52f, 0.20f), FurDark = new Color(0.62f, 0.30f, 0.12f), FurLight = new Color(0.98f, 0.66f, 0.32f),
        Cream = new Color(1f, 0.92f, 0.78f), Pink = new Color(0.96f, 0.62f, 0.60f), Nose = new Color(0.30f, 0.15f, 0.12f), EyeBlack = new Color(0.07f, 0.05f, 0.06f);

    /// Builds the squirrel under bodyPivot (local +Z = head / down the trunk, +Y = away from the bark, X = across).
    /// Returns head, tail and the four leg pivots at the same spots as the old OBJ rig.
    public static void BuildSquirrel(Transform bodyPivot, out Transform head, out Transform tail, out Transform fl, out Transform fr, out Transform bl, out Transform br)
    {
        var fur = TCLowPoly.Mat(Fur, 0.42f, 1.05f); var dark = TCLowPoly.Mat(FurDark, 0.4f, 1.0f); var light = TCLowPoly.Mat(FurLight, 0.4f, 1.05f);
        var cream = TCLowPoly.Mat(Cream, 0.3f, 1.05f); var pink = TCLowPoly.Mat(Pink, 0.25f, 1.05f);

        // torso: chubby pear (wider haunches at the back), cream tummy, chipmunk back stripes
        TCLowPoly.Part("Torso", bodyPivot, TCLowPoly.Ellipsoid(9, 7, new Vector3(0.115f, 0.175f, 0.095f), 0.05f, 11, t => Mathf.Lerp(0.88f, 1.05f, t)),
            fur, new Vector3(0, 0.06f, -0.01f), new Vector3(-90, 0, 0));
        TCLowPoly.Part("Haunch", bodyPivot, TCLowPoly.Ellipsoid(8, 6, new Vector3(0.125f, 0.09f, 0.10f), 0.05f, 12), fur, new Vector3(0, 0.055f, -0.11f), Vector3.zero);
        TCLowPoly.Part("Tummy", bodyPivot, TCLowPoly.Ellipsoid(8, 5, new Vector3(0.08f, 0.05f, 0.14f), 0.03f, 13), cream, new Vector3(0, 0.0f, 0.02f), Vector3.zero, null, false);
        TCLowPoly.Part("Stripe", bodyPivot, TCLowPoly.Ellipsoid(6, 5, new Vector3(0.026f, 0.018f, 0.15f), 0.02f, 14), dark, new Vector3(0, 0.145f, -0.02f), Vector3.zero, null, false);
        // (side stripes dropped: they read as pale bars from the gameplay camera)

        // head
        head = new GameObject("HeadPivot").transform; head.SetParent(bodyPivot, false);
        head.localPosition = new Vector3(0, 0.05f, 0.20f); head.localRotation = Quaternion.Euler(10f, 0, 0);
        TCLowPoly.Part("Skull", head, TCLowPoly.Ellipsoid(9, 7, new Vector3(0.115f, 0.10f, 0.11f), 0.04f, 21), fur, new Vector3(0, 0.07f, 0.10f), Vector3.zero);
        TCLowPoly.Part("Muzzle", head, TCLowPoly.Ellipsoid(8, 6, new Vector3(0.068f, 0.05f, 0.06f), 0.03f, 22), cream, new Vector3(0, 0.05f, 0.19f), Vector3.zero);
        TCLowPoly.Part("Nose", head, TCLowPoly.Ellipsoid(6, 4, new Vector3(0.022f, 0.016f, 0.016f), 0f, 23), TCLowPoly.Mat(Nose, 0.2f, 1f), new Vector3(0, 0.085f, 0.243f), Vector3.zero, null, false);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            TCLowPoly.Part(sg < 0 ? "CheekL" : "CheekR", head, TCLowPoly.Ellipsoid(7, 5, new Vector3(0.05f, 0.04f, 0.045f), 0.03f, 24), cream, new Vector3(sg * 0.062f, 0.04f, 0.16f), Vector3.zero);
            TCLowPoly.Part(sg < 0 ? "BlushL" : "BlushR", head, TCLowPoly.Ellipsoid(6, 4, new Vector3(0.018f, 0.01f, 0.016f), 0f, 25), pink, new Vector3(sg * 0.075f, 0.075f, 0.17f), Vector3.zero, null, false);
            // big glossy eyes on the top-front of the face (the camera sees the face as it runs down at you)
            var eye = TCLowPoly.Part(sg < 0 ? "EyeL" : "EyeR", head, TCLowPoly.Ellipsoid(8, 6, new Vector3(0.034f, 0.04f, 0.03f), 0f, 26), TCLowPoly.Mat(EyeBlack, 0.1f, 1f),
                new Vector3(sg * 0.055f, 0.135f, 0.14f), new Vector3(-30, sg * 18f, 0));
            TCLowPoly.Part("Spark", eye, TCLowPoly.Ellipsoid(5, 4, Vector3.one * 0.011f, 0f, 27), TCLowPoly.Mat(Color.white, 0f, 1.25f), new Vector3(sg * 0.01f, 0.022f, 0.018f), Vector3.zero, null, false);
            TCLowPoly.Part("Spark2", eye, TCLowPoly.Ellipsoid(4, 3, Vector3.one * 0.005f, 0f, 28), TCLowPoly.Mat(Color.white, 0f, 1.25f), new Vector3(-sg * 0.008f, 0.004f, 0.028f), Vector3.zero, null, false);
            // ears with pink inner + fluffy tuft
            var ear = TCLowPoly.Part(sg < 0 ? "EarL" : "EarR", head, TCLowPoly.Cone(5, 0.035f, 0.004f, 0.085f, 0.55f), fur, new Vector3(sg * 0.07f, 0.13f, 0.04f), new Vector3(-35, 0, sg * -28f));
            TCLowPoly.Part("Inner", ear, TCLowPoly.Cone(4, 0.02f, 0.002f, 0.06f, 0.4f), pink, new Vector3(0, 0.008f, 0.012f), Vector3.zero, null, false);
            TCLowPoly.Part("Tuft", ear, TCLowPoly.Cone(4, 0.012f, 0.0f, 0.04f), dark, new Vector3(0, 0.075f, 0), new Vector3(0, 0, sg * 12f), null, false);
        }
        TCLowPoly.Part("Teeth", head, TCLowPoly.Cone(4, 0.014f, 0.01f, 0.018f, 0.5f), TCLowPoly.Mat(Color.white, 0.1f, 1.15f), new Vector3(0, 0.03f, 0.235f), new Vector3(180, 0, 0), null, false);

        // big bushy S-curved tail: chained faceted puffs, lighter tip
        tail = new GameObject("TailPivot").transform; tail.SetParent(bodyPivot, false); tail.localPosition = new Vector3(0, 0.03f, -0.22f);
        // one continuous plume: overlapping puffs along an S-curve (up from the rump, curling forward over the back)
        int nP = 9;
        for (int i = 0; i < nP; i++)
        {
            float u = i / (float)(nP - 1);
            // rises off the rump, lies back up the trunk (-Z) and flicks its tip outward: a soft S
            float curl = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, u));
            var p = new Vector3((i % 2 == 0 ? 1f : -1f) * 0.018f * Mathf.Sin(u * Mathf.PI),
                                0.06f + 0.16f * Mathf.Sin(u * Mathf.PI * 0.7f) + 0.16f * curl,
                                -0.03f - 0.30f * u + 0.12f * curl);
            float sz = Mathf.Lerp(0.08f, 0.15f, Mathf.Sin(Mathf.Min(1f, u * 1.4f) * Mathf.PI * 0.5f)) * (u > 0.82f ? Mathf.Lerp(1f, 0.62f, (u - 0.82f) / 0.18f) : 1f);
            var m = u > 0.9f ? cream : (i % 2 == 0 ? fur : light);
            TCLowPoly.Part("Puff" + i, tail, TCLowPoly.Ellipsoid(8, 6, new Vector3(sz * 1.05f, sz, sz * 0.95f), 0.06f, 40 + i), m, p, new Vector3(i * 23f, i * 37f, 0));
        }

        // legs (pivot at the hip/shoulder; animation swings them about X)
        fl = Leg("LegFL", bodyPivot, new Vector3(-0.11f, 0.01f, 0.12f), fur, dark, false);
        fr = Leg("LegFR", bodyPivot, new Vector3(0.11f, 0.01f, 0.12f), fur, dark, false);
        bl = Leg("LegBL", bodyPivot, new Vector3(-0.14f, 0.04f, -0.11f), fur, dark, true);
        br = Leg("LegBR", bodyPivot, new Vector3(0.14f, 0.04f, -0.11f), fur, dark, true);
    }

    static Transform Leg(string name, Transform parent, Vector3 pos, Material fur, Material paw, bool hind)
    {
        var pivot = new GameObject(name + "_Pivot").transform; pivot.SetParent(parent, false); pivot.localPosition = pos;
        float s = hind ? 1.25f : 1f;
        TCLowPoly.Part("Thigh", pivot, TCLowPoly.Ellipsoid(6, 5, new Vector3(0.04f, 0.065f, 0.045f) * s, 0.04f, hind ? 51 : 52), fur, new Vector3(0, -0.04f * s, 0.0f), Vector3.zero);
        TCLowPoly.Part("Paw", pivot, TCLowPoly.Ellipsoid(6, 4, new Vector3(0.032f, 0.02f, 0.042f) * s, 0.04f, 53), paw, new Vector3(0, -0.10f * s, 0.018f), Vector3.zero);
        return pivot;
    }

    // ---------------- bat ----------------
    static readonly Color BatFur = new Color(0.47f, 0.39f, 0.62f), BatBelly = new Color(0.72f, 0.62f, 0.80f), BatWing = new Color(0.33f, 0.26f, 0.45f),
        BatBone = new Color(0.22f, 0.16f, 0.30f), BatInk = new Color(0.95f, 0.92f, 1f);

    /// Chubby cartoon bat facing +Z (out of the bark), wings in the X/Y plane. Keeps the old rig names
    /// ("Body", "BatWingPivot_L/R") so TCWasp still finds it; TCBatFlap drives a two-joint wing flap.
    public static GameObject BuildBat(Transform parent)
    {
        var root = new GameObject("Bat_Visual"); if (parent) root.transform.SetParent(parent, false);
        var fur = TCLowPoly.Mat(BatFur, 0.42f, 1.08f); var belly = TCLowPoly.Mat(BatBelly, 0.35f, 1.05f);
        var wing = TCLowPoly.Mat(BatWing, 0.35f, 1.05f); var bone = TCLowPoly.Mat(BatBone, 0.2f, 1f);
        var pink = TCLowPoly.Mat(Pink, 0.25f, 1.05f);
        var ink = TCLowPoly.HullMat(BatInk, 1.3f); // light rim like the old bat so it reads against the night sky

        var body = new GameObject("Body").transform; body.SetParent(root.transform, false);
        P("Torso", body, TCLowPoly.Ellipsoid(8, 7, new Vector3(0.075f, 0.092f, 0.068f), 0.05f, 61), fur, new Vector3(0, 0.03f, 0), Vector3.zero, ink);
        P("Belly", body, TCLowPoly.Ellipsoid(7, 5, new Vector3(0.05f, 0.062f, 0.03f), 0.03f, 62), belly, new Vector3(0, 0.015f, 0.05f), Vector3.zero, null);
        var head = new GameObject("Head").transform; head.SetParent(body, false); head.localPosition = new Vector3(0, 0.14f, 0.01f);
        P("Skull", head, TCLowPoly.Ellipsoid(9, 7, new Vector3(0.085f, 0.072f, 0.072f), 0.04f, 63), fur, Vector3.zero, Vector3.zero, ink);
        P("Snout", head, TCLowPoly.Ellipsoid(7, 5, new Vector3(0.04f, 0.028f, 0.03f), 0.02f, 64), belly, new Vector3(0, -0.018f, 0.058f), Vector3.zero, null);
        P("Nose", head, TCLowPoly.Ellipsoid(5, 4, new Vector3(0.013f, 0.009f, 0.008f), 0f, 65), pink, new Vector3(0, -0.006f, 0.088f), Vector3.zero, null);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            var eye = P(sg < 0 ? "EyeL" : "EyeR", head, TCLowPoly.Ellipsoid(8, 6, new Vector3(0.024f, 0.027f, 0.018f), 0f, 66), TCLowPoly.Mat(Color.white, 0.1f, 1.15f),
                new Vector3(sg * 0.033f, 0.014f, 0.058f), new Vector3(0, sg * 14f, 0), null);
            P("Pupil", eye, TCLowPoly.Ellipsoid(6, 5, new Vector3(0.015f, 0.018f, 0.01f), 0f, 67), TCLowPoly.Mat(EyeBlack, 0.1f, 1f), new Vector3(0, -0.002f, 0.012f), Vector3.zero, null);
            P("Spark", eye, TCLowPoly.Ellipsoid(4, 3, Vector3.one * 0.0055f, 0f, 68), TCLowPoly.Mat(Color.white, 0f, 1.3f), new Vector3(sg * 0.005f, 0.008f, 0.021f), Vector3.zero, null);
            var ear = P(sg < 0 ? "EarL" : "EarR", head, TCLowPoly.Cone(4, 0.032f, 0.003f, 0.085f, 0.5f), fur, new Vector3(sg * 0.048f, 0.05f, -0.005f), new Vector3(-8, 0, sg * -24f), ink);
            P("Inner", ear, TCLowPoly.Cone(4, 0.018f, 0.002f, 0.06f, 0.35f), pink, new Vector3(0, 0.006f, 0.01f), Vector3.zero, null);
            P(sg < 0 ? "FangL" : "FangR", head, TCLowPoly.Cone(3, 0.006f, 0.0f, 0.016f), TCLowPoly.Mat(Color.white, 0.05f, 1.2f), new Vector3(sg * 0.012f, -0.04f, 0.075f), new Vector3(180, 0, 0), null);
            P(sg < 0 ? "FootL" : "FootR", body, TCLowPoly.Ellipsoid(5, 4, new Vector3(0.016f, 0.022f, 0.014f), 0f, 69), bone, new Vector3(sg * 0.03f, -0.065f, 0.01f), Vector3.zero, null);
        }

        for (int sg = -1; sg <= 1; sg += 2)
        {
            var pv = new GameObject(sg < 0 ? "BatWingPivot_L" : "BatWingPivot_R").transform; pv.SetParent(root.transform, false);
            pv.localPosition = new Vector3(sg * 0.065f, 0.055f, 0.002f);
            // inner membrane + arm bone (shoulder -> wrist)
            P("Arm", pv, TCLowPoly.Cone(4, 0.011f, 0.007f, 0.19f), bone, Vector3.zero, new Vector3(0, 0, sg * -78f), null);
            var inner = new[] { new Vector2(0, 0.012f), new Vector2(0.10f, 0.045f), new Vector2(0.19f, 0.045f), new Vector2(0.17f, -0.03f), new Vector2(0.11f, -0.055f), new Vector2(0.05f, -0.07f), new Vector2(0, -0.075f) };
            P("Membrane", pv, TCLowPoly.Membrane(Mirror(inner, sg), 0.005f, 0.006f), wing, Vector3.zero, Vector3.zero, null); // no hull: a flat membrane would flood with rim colour
            // wing tip (folds at the wrist) with two finger bones and a scalloped edge
            var tip = new GameObject("WingTip").transform; tip.SetParent(pv, false); tip.localPosition = new Vector3(sg * 0.19f, 0.045f, 0);
            var outer = new[] { new Vector2(0, 0), new Vector2(0.09f, 0.05f), new Vector2(0.18f, 0.035f), new Vector2(0.19f, -0.035f), new Vector2(0.13f, -0.03f), new Vector2(0.10f, -0.09f), new Vector2(0.05f, -0.065f), new Vector2(-0.02f, -0.075f) };
            P("Membrane", tip, TCLowPoly.Membrane(Mirror(outer, sg), 0.005f, 0.006f), wing, Vector3.zero, Vector3.zero, null);
            P("Finger1", tip, TCLowPoly.Cone(3, 0.007f, 0.003f, 0.19f), bone, Vector3.zero, new Vector3(0, 0, sg * -82f), null);
            P("Finger2", tip, TCLowPoly.Cone(3, 0.006f, 0.002f, 0.15f), bone, Vector3.zero, new Vector3(0, 0, sg * -118f), null);
            P("Claw", tip, TCLowPoly.Cone(3, 0.006f, 0.0f, 0.02f), TCLowPoly.Mat(Color.white, 0.05f, 1.1f), new Vector3(0, 0.004f, 0), new Vector3(0, 0, sg * 20f), null);
        }
        root.AddComponent<TCBatFlap>();
        return root;
    }

    static Vector2[] Mirror(Vector2[] pts, int sg) { var r = new Vector2[pts.Length]; for (int i = 0; i < pts.Length; i++) r[i] = new Vector2(pts[i].x * sg, pts[i].y); if (sg < 0) System.Array.Reverse(r); return r; }

    /// Part with an optional custom outline material (null = no outline).
    static Transform P(string name, Transform parent, Mesh m, Material mat, Vector3 pos, Vector3 euler, Material hull)
    {
        var t = TCLowPoly.Part(name, parent, m, mat, pos, euler, null, hull != null);
        if (hull != null) { var h = t.Find("Hull"); if (h) h.GetComponent<MeshRenderer>().sharedMaterial = TCLowPoly.HullMat(hull.GetColor("_OutlineColor"), hull.GetFloat("_OutlineWidth"), mat.color); }
        return t;
    }
}

/// Two-joint flap for the low-poly bat: shoulder stroke + lagging wrist fold, body bob and ear twitch.
/// Runs in LateUpdate so it overrides TCWasp's generic wing rotation.
public class TCBatFlap : MonoBehaviour
{
    Transform wl, wr, tl, tr, body, head; float phase;
    void Awake()
    {
        wl = transform.Find("BatWingPivot_L"); wr = transform.Find("BatWingPivot_R");
        tl = wl ? wl.Find("WingTip") : null; tr = wr ? wr.Find("WingTip") : null;
        body = transform.Find("Body"); head = body ? body.Find("Head") : null; phase = Random.value * 10f;
    }
    void LateUpdate()
    {
        float t = Time.time * 2f * Mathf.PI * 5.2f + phase;
        float stroke = Mathf.Sin(t);                         // +1 up, -1 down
        float flap = stroke * 48f + 8f;
        float fold = Mathf.Sin(t - 0.9f) * 34f + 12f;        // tip lags behind the shoulder
        float sweep = Mathf.Cos(t) * 14f;
        if (wl) wl.localRotation = Quaternion.Euler(sweep * 0.5f, -sweep, -flap);
        if (wr) wr.localRotation = Quaternion.Euler(sweep * 0.5f, sweep, flap);
        if (tl) tl.localRotation = Quaternion.Euler(0, -fold * 0.35f, -fold);
        if (tr) tr.localRotation = Quaternion.Euler(0, fold * 0.35f, fold);
        if (body) body.localPosition = new Vector3(0, -stroke * 0.012f, 0);
        if (head) head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.25f) * 6f, Mathf.Sin(t * 0.13f) * 10f, 0);
    }
}
