using System.Collections.Generic;
using UnityEngine;

/// Round, cute hypercasual spider built from simple meshes on top of the original rig:
/// modelled on the 3D-printed chibi toy spiders: big round abdomen, smaller round head, two big glossy eyes
/// (+2 tiny side eyes), tiny fangs and 8 short chunky bent legs.
/// Skins only change colours/pattern, back spikes and the body shape. The old realistic mesh is hidden,
/// its rig/animator still drives jump/death motion which we follow (position) and stylise (squash, flip).
public class TCCuteSpider : MonoBehaviour
{
    const string RootName = "__cute";
    public Transform bone; Vector3 boneRest; Animator anim; playerInteract pi;
    Transform body; readonly List<Transform> legs = new List<Transform>(); readonly List<Quaternion> legRest = new List<Quaternion>();
    Vector3 bodyRest, bodyScale; float flip, t, blink = 2.5f;
    Transform head; Vector3 headRest; readonly List<Transform> eyes = new List<Transform>(); readonly List<Vector3> eyeRest = new List<Vector3>();

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Mesh cone;

    public static void Build(GameObject spider, SkinDef def, int skin)
    {
        var st = spider.transform;
        // Hide the realistic mesh (once), remember its framing.
        var smrs = spider.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Bounds b = new Bounds(st.position, Vector3.one * 0.3f); bool hasB = false;
        foreach (var r in smrs) { if (r.enabled) { if (!hasB) { b = r.bounds; hasB = true; } else b.Encapsulate(r.bounds); } r.enabled = false; }
        var old = st.Find(RootName);
        var cs = spider.GetComponent<TCCuteSpider>();
        if (old) { if (cs && cs.cachedBounds.size != Vector3.zero) { b = cs.cachedBounds; } old.name = "__old"; old.gameObject.SetActive(false); Destroy(old.gameObject); }
        if (!cs) cs = spider.AddComponent<TCCuteSpider>();
        if (hasB || cs.cachedBounds.size == Vector3.zero) cs.cachedBounds = b; else b = cs.cachedBounds;

        float scale = Mathf.Max(0.0001f, st.lossyScale.x);
        float R = b.size.magnitude * 0.16f / scale;               // body radius, local units
        Vector3 center = st.InverseTransformPoint(b.center);
        // Up = spider's back. On the trunk that is radially outward, in the menu it is world up.
        Vector3 upW = spider.GetComponentInParent<player>() ? new Vector3(st.position.x, 0, st.position.z).normalized : Vector3.up;
        if (upW.sqrMagnitude < 0.01f) upW = Vector3.up;
        Vector3 up = st.InverseTransformDirection(upW).normalized;
        var head = TCSkins.FindDeep(st, "Spider_Head");
        bool inGame = spider.GetComponentInParent<player>();
        // Face: in game the spider looks up the trunk (climb direction); in menu previews it faces the preview camera (-Z).
        Vector3 fwdW = Vector3.up;
        Vector3 fwd = inGame ? st.InverseTransformDirection(fwdW) : Vector3.back; // menu preview camera sits at -Z
        fwd = Vector3.ProjectOnPlane(fwd, up); if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
        fwd.Normalize();

        var root = new GameObject(RootName).transform; root.SetParent(st, false);
        root.localPosition = center - up * Vector3.Dot(center, up) + up * Mathf.Min(Vector3.Dot(center, up), R * 0.2f);
        root.localRotation = Quaternion.LookRotation(fwd, up);
        cs.legs.Clear(); cs.legRest.Clear(); cs.eyeRest.Clear();

        var bodyMat = Resources.Load<Material>("Skins/Skin_" + skin);
        if (!bodyMat) bodyMat = Flat(def.body, null, 0, def.metallic, def.gloss);
        // Photo look (3D-printed chibi spider): one solid plastic colour for head + legs, the abdomen carries the skin pattern.
        var plain = Flat(def.body, bodyMat, 0, def.metallic, def.gloss);
        var legMat = Flat(def.body * 0.93f, bodyMat, 0, def.metallic, def.gloss);
        var spikeMat = Flat(def.accent, bodyMat, def.emission, def.metallic, def.gloss);
        var eyeMat = Flat(def.eye, bodyMat, def.eyeGlow, 0.1f, 0.92f);
        var shine = Flat(Color.white, bodyMat, 0.6f, 0, 0.9f);
        var fangMat = Flat(def.body * 0.8f, bodyMat, 0, def.metallic, def.gloss);

        Vector3 s = def.BodyScale;
        float pivotY = R * 0.95f;
        var bodyT = new GameObject("body").transform; bodyT.SetParent(root, false); bodyT.localPosition = new Vector3(0, pivotY, 0);
        cs.body = bodyT; cs.bodyRest = bodyT.localPosition; cs.bodyScale = Vector3.one;
        Vector3 P(float x, float y, float z) => new Vector3(x * R, y * R - pivotY, z * R);
        // Big round abdomen at the back (shape per skin), smaller round head in front.
        Vector3 ab = Vector3.Scale(new Vector3(1.02f, 0.95f, 1.0f), s) * R;
        var abd = Part(PrimitiveType.Sphere, bodyT, P(0, 1.08f, -0.55f) , ab * 2f, bodyMat).transform; abd.name = "abdomen";
        abd.localRotation = Quaternion.Euler(-12, 0, 0);
        float hr = 0.8f * Mathf.Lerp(1f, s.x, 0.35f);
        var headT = Part(PrimitiveType.Sphere, bodyT, P(0, 0.80f, 0.55f), new Vector3(2f, 1.85f, 1.9f) * hr * R, plain).transform; headT.name = "head";
        cs.head = headT; cs.headRest = headT.localPosition;
        Vector3 HS(Vector3 dir, float lift) { dir.Normalize(); return headT.localPosition + Vector3.Scale(dir, new Vector3(1f, 0.925f, 0.95f) * hr * R) + dir * lift * R; }
        cs.eyes.Clear();
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            // two very big glossy round eyes with a small white highlight
            var d = new Vector3(0.42f * sgn, 0.14f, 0.9f).normalized;
            var e = Part(PrimitiveType.Sphere, bodyT, HS(d, -0.05f), new Vector3(0.52f, 0.52f, 0.22f) * R, eyeMat).transform;
            e.localRotation = Quaternion.LookRotation(d, Vector3.up); e.name = "eye";
            Part(PrimitiveType.Sphere, e, new Vector3(0.22f, 0.24f, 0.42f), new Vector3(0.24f, 0.24f, 0.3f), shine);
            cs.eyes.Add(e); cs.eyeRest.Add(e.localScale);
            // tiny side eye
            var sd = new Vector3(0.86f * sgn, 0.22f, 0.46f).normalized;
            var se = Part(PrimitiveType.Sphere, bodyT, HS(sd, -0.02f), new Vector3(0.15f, 0.15f, 0.08f) * R, eyeMat).transform;
            se.localRotation = Quaternion.LookRotation(sd, Vector3.up);
            cs.eyes.Add(se); cs.eyeRest.Add(se.localScale);
            // tiny fangs / pedipalps under the face
            var f = Part(PrimitiveType.Capsule, bodyT, P(0.17f * sgn, 0.22f, 1.10f), new Vector3(0.17f, 0.15f, 0.17f) * R, fangMat).transform;
            f.localRotation = Quaternion.Euler(18, 0, -8 * sgn);
        }

        // 8 short, chunky, rounded legs with a slight bend (two capsules + rounded knee), 4 per side.
        for (int sgn = -1; sgn <= 1; sgn += 2)
            for (int k = 0; k < 4; k++)
            {
                float z = Mathf.Lerp(0.85f, -0.55f, k / 3f);
                var hip = new GameObject("leg").transform; hip.SetParent(bodyT, false);
                hip.localPosition = P(sgn * 0.55f, 0.62f, z);
                hip.localRotation = Quaternion.Euler(0, sgn * (90 - 32 * (k - 1.5f)), 0);
                float th = 0.36f;
                // upper: out and slightly up
                var up1 = Part(PrimitiveType.Capsule, hip, new Vector3(0, 0.08f * R, 0.26f * R), new Vector3(th, 0.32f, th) * R, legMat).transform;
                up1.localRotation = Quaternion.Euler(72, 0, 0);
                Part(PrimitiveType.Sphere, hip, new Vector3(0, 0.15f * R, 0.55f * R), Vector3.one * th * 1.04f * R, legMat);
                // lower: down to the ground, chunky rounded foot
                var lo = Part(PrimitiveType.Capsule, hip, new Vector3(0, -0.15f * R, 0.68f * R), new Vector3(th * 1.08f, 0.34f, th * 1.08f) * R, legMat).transform;
                lo.localRotation = Quaternion.Euler(-20, 0, 0);
                cs.legs.Add(hip); cs.legRest.Add(hip.localRotation);
            }
        Vector3 radii = ab; // spikes sit on the abdomen
        Vector3 abC = abd.localPosition;
        Vector3 Surf(Vector3 dir, float lift = 0) { dir.Normalize(); return abC + Vector3.Scale(dir, radii) + dir * lift * R; }

        // Spikes along the back ridge (skin feature)
        if (def.spikes > 0)
        {
            if (!cone) cone = MakeCone();
            for (int k = 0; k < def.spikes; k++)
            {
                float a = Mathf.Lerp(-10f, 125f, def.spikes == 1 ? 0.5f : k / (float)(def.spikes - 1)) * Mathf.Deg2Rad; // from forehead over the back
                var n = new Vector3(0, Mathf.Cos(a), -Mathf.Sin(a));
                float sideJ = def.spikes > 5 ? ((k % 2) * 2 - 1) * 0.18f : 0f;
                n.x = sideJ; n.Normalize();
                var g = new GameObject("spike"); g.transform.SetParent(bodyT, false);
                g.transform.localPosition = Surf(n, -0.06f);
                g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, n);
                float len = def.spikeLength * R * (k == 0 || k == def.spikes - 1 ? 0.7f : 1f);
                g.transform.localScale = new Vector3(R * 0.28f, len, R * 0.28f);
                g.AddComponent<MeshFilter>().sharedMesh = cone;
                var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = spikeMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        cs.anim = spider.GetComponentInChildren<Animator>();
        cs.pi = spider.GetComponent<playerInteract>();
        var smr0 = smrs.Length > 0 ? smrs[0] : null;
        cs.bone = smr0 && smr0.rootBone ? smr0.rootBone : head;
        cs.boneRest = cs.bone ? st.InverseTransformPoint(cs.bone.position) : Vector3.zero;
        cs.restRoot = root.localPosition; cs.root = root; cs.flip = 0;
    }

    [System.NonSerialized] public float wave; // 0..1, menu wave (front leg up)
    public Bounds cachedBounds; Transform root; Vector3 restRoot;

    void LateUpdate()
    {
        if (!root) return;
        t += Time.deltaTime;
        var st = transform;
        // Follow the rig's root motion (jump hop etc.) but keep our own clean orientation.
        Vector3 off = bone ? st.InverseTransformPoint(bone.position) - boneRest : Vector3.zero;
        if (off.magnitude > 5f) off = Vector3.zero;
        root.localPosition = restRoot + off;
        float hop = Mathf.Clamp01(off.magnitude * 4f);
        // Squash & stretch + scuttle bounce
        float bob = Mathf.Sin(t * 24f) * 0.035f;
        body.localScale = new Vector3(1 + bob - hop * 0.08f, 1 - bob + hop * 0.15f, 1 + bob - hop * 0.08f);
        if (head) head.localPosition = headRest + new Vector3(Mathf.Sin(t * 12f) * 0.02f, Mathf.Abs(Mathf.Sin(t * 12f)) * 0.03f, 0) * head.localScale.y;
        // Legs: alternating tetrapod gait (scuttle / climb): swing forward-back, lift on the swing phase.
        for (int i = 0; i < legs.Count; i++)
        {
            int side = i < 4 ? 0 : 1, k = i % 4;
            float ph = t * 12f + (((k + side) & 1) == 0 ? 0 : Mathf.PI);
            float swing = Mathf.Sin(ph) * 22f, lift = Mathf.Max(0, Mathf.Cos(ph)) * 18f;
            legs[i].localRotation = Quaternion.Euler(0, swing, 0) * legRest[i] * Quaternion.Euler(-lift, 0, 0);
            if (i == 4 && wave > 0) legs[i].localRotation = legRest[i] * Quaternion.Euler(-70f * wave, 0, Mathf.Sin(t * 16f) * 25f * wave); // hi!
        }
        // Eyes: blink every few seconds
        blink -= Time.deltaTime;
        if (blink < -0.14f) blink = Random.Range(1.8f, 4f);
        float lid = blink < 0 ? 0.12f : 1f;
        for (int i = 0; i < eyes.Count; i++) if (eyes[i]) { var r = eyeRest[i]; eyes[i].localScale = new Vector3(r.x, r.y * lid, r.z); }
        // Death: roll on the back, cartoon style
        bool dead = pi ? pi.life <= 0 : anim && anim.isActiveAndEnabled && HasBool(anim, "death") && anim.GetBool("death");
        flip = Mathf.MoveTowards(flip, dead ? 1 : 0, Time.deltaTime * 3f);
        body.localRotation = Quaternion.Euler(0, 0, 180f * Mathf.SmoothStep(0, 1, flip));
    }

    static bool HasBool(Animator a, string n) { foreach (var p in a.parameters) if (p.name == n) return true; return false; }

    static Material Flat(Color c, Material src, float emission = 0, float metallic = 0, float gloss = 0.55f)
    {
        string key = ColorUtility.ToHtmlStringRGBA(c) + emission.ToString("0.00") + metallic.ToString("0.00") + gloss.ToString("0.00");
        if (mats.TryGetValue(key, out var m) && m) return m;
        m = src ? new Material(src) : new Material(Shader.Find("Standard"));
        m.mainTexture = null; m.color = c;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (emission > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * emission); m.SetTexture("_EmissionMap", null); }
        else m.DisableKeyword("_EMISSION");
        mats[key] = m; return m;
    }

    static GameObject Part(PrimitiveType t, Transform p, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(t);
        var col = g.GetComponent<Collider>(); if (col) Object.DestroyImmediate(col);
        g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        if (rot.HasValue) g.transform.localRotation = rot.Value;
        var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    /// Unit cone: base radius 0.5 at y=0, tip at y=1 (smooth, rounded look via normals).
    static Mesh MakeCone()
    {
        const int N = 14; var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            v.Add(d * 0.5f); n.Add((d + Vector3.up * 0.5f).normalized);
            v.Add(Vector3.up); n.Add((d + Vector3.up * 0.5f).normalized);
        }
        for (int i = 0; i < N; i++) { int a = i * 2; tri.AddRange(new[] { a, a + 1, a + 2, a, a + 2, a + 1 }); }
        var m = new Mesh { name = "spike" }; m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tri, 0); m.RecalculateBounds();
        return m;
    }
}
