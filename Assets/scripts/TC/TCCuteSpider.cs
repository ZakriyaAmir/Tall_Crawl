using System.Collections.Generic;
using UnityEngine;

/// Round, cute hypercasual spider built from simple meshes on top of the original rig:
/// a ball body (shape per skin), big shiny eyes, blush, a small smile and 8 short stubby legs.
/// Skins only change colours/pattern, back spikes and the body shape. The old realistic mesh is hidden,
/// its rig/animator still drives jump/death motion which we follow (position) and stylise (squash, flip).
public class TCCuteSpider : MonoBehaviour
{
    const string RootName = "__cute";
    public Transform bone; Vector3 boneRest; Animator anim; playerInteract pi;
    Transform body; readonly List<Transform> legs = new List<Transform>(); readonly List<Quaternion> legRest = new List<Quaternion>();
    Vector3 bodyRest, bodyScale; float flip, t;

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
        Vector3 fwd = head ? st.InverseTransformPoint(head.position) - center : Vector3.forward;
        if (!head && spider.GetComponentInParent<player>()) fwd = st.InverseTransformDirection(Vector3.up); // climbing direction
        fwd = Vector3.ProjectOnPlane(fwd, up); if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
        fwd.Normalize();

        var root = new GameObject(RootName).transform; root.SetParent(st, false);
        root.localPosition = center - up * Vector3.Dot(center, up) + up * Mathf.Min(Vector3.Dot(center, up), R * 0.2f);
        root.localRotation = Quaternion.LookRotation(fwd, up);
        cs.legs.Clear(); cs.legRest.Clear();

        var bodyMat = Resources.Load<Material>("Skins/Skin_" + skin);
        if (!bodyMat) bodyMat = Flat(def.body, null);
        var legMat = Flat(Color.Lerp(def.body, def.accent, 0.55f) * 0.92f, bodyMat);
        var spikeMat = Flat(def.accent, bodyMat, def.emission);
        var white = Flat(Color.white, bodyMat); var black = Flat(new Color(0.13f, 0.11f, 0.12f), bodyMat);
        var blush = Flat(new Color(1f, 0.55f, 0.6f), bodyMat); var mouth = Flat(new Color(0.55f, 0.18f, 0.22f), bodyMat);

        Vector3 s = def.BodyScale; Vector3 radii = s * R;
        var bodyT = new GameObject("body").transform; bodyT.SetParent(root, false); bodyT.localPosition = new Vector3(0, radii.y * 0.95f, 0);
        cs.body = bodyT; cs.bodyRest = bodyT.localPosition; cs.bodyScale = Vector3.one;
        var ball = Part(PrimitiveType.Sphere, bodyT, Vector3.zero, radii * 2f, bodyMat);
        ball.name = "ball";
        Vector3 Surf(Vector3 dir, float lift = 0) { dir.Normalize(); var p = Vector3.Scale(dir, radii); return p + dir * lift * R; }

        // Eyes: big whites, dark pupils, tiny highlights.
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            var d = new Vector3(0.36f * sgn, 0.32f, 0.88f);
            var e = Part(PrimitiveType.Sphere, bodyT, Surf(d, -0.08f), Vector3.one * R * 0.62f, white).transform;
            e.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            Part(PrimitiveType.Sphere, e, new Vector3(0, 0, 0.28f), new Vector3(0.62f, 0.66f, 0.5f), black);
            Part(PrimitiveType.Sphere, e, new Vector3(0.14f, 0.18f, 0.48f), Vector3.one * 0.2f, white);
            // blush
            var bd = new Vector3(0.62f * sgn, -0.02f, 0.78f);
            var bl = Part(PrimitiveType.Sphere, bodyT, Surf(bd, -0.03f), new Vector3(0.3f, 0.17f, 0.08f) * R, blush).transform;
            bl.localRotation = Quaternion.LookRotation(bd.normalized, Vector3.up);
        }
        // Smile
        var md = new Vector3(0, 0.02f, 1f);
        var m = Part(PrimitiveType.Sphere, bodyT, Surf(md, -0.02f), new Vector3(0.26f, 0.13f, 0.08f) * R, mouth).transform;
        m.localRotation = Quaternion.LookRotation(md, Vector3.up);

        // 8 short stubby legs (4 per side)
        for (int sgn = -1; sgn <= 1; sgn += 2)
            for (int k = 0; k < 4; k++)
            {
                float z = Mathf.Lerp(0.55f, -0.6f, k / 3f);
                var hip = new GameObject("leg").transform; hip.SetParent(root, false);
                hip.localPosition = new Vector3(sgn * radii.x * 0.78f, radii.y * 0.55f, z * radii.z);
                hip.localRotation = Quaternion.Euler(0, sgn * (90 - 25 * (k - 1.5f)), 0) * Quaternion.Euler(0, 0, 0);
                var seg = Part(PrimitiveType.Capsule, hip, new Vector3(0, -R * 0.12f, R * 0.32f), new Vector3(0.3f, 0.3f, 0.3f) * R, legMat).transform;
                seg.localRotation = Quaternion.Euler(110, 0, 0); seg.localScale = new Vector3(0.32f, 0.38f, 0.32f) * R;
                var foot = Part(PrimitiveType.Sphere, hip, new Vector3(0, -R * 0.36f, R * 0.56f), Vector3.one * R * 0.36f, legMat);
                cs.legs.Add(hip); cs.legRest.Add(hip.localRotation);
            }

        // Spikes along the back ridge (skin feature)
        if (def.spikes > 0)
        {
            if (!cone) cone = MakeCone();
            for (int k = 0; k < def.spikes; k++)
            {
                float a = Mathf.Lerp(-35f, 120f, def.spikes == 1 ? 0.5f : k / (float)(def.spikes - 1)) * Mathf.Deg2Rad; // from forehead over the back
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
        // Squash & stretch + idle bounce
        float bob = Mathf.Sin(t * 12f) * 0.04f;
        body.localScale = new Vector3(1 + bob - hop * 0.08f, 1 - bob + hop * 0.15f, 1 + bob - hop * 0.08f);
        // Wiggle legs (scuttle)
        for (int i = 0; i < legs.Count; i++)
        {
            float ph = t * 14f + i * 1.7f;
            legs[i].localRotation = legRest[i] * Quaternion.Euler(Mathf.Sin(ph) * 14f, Mathf.Cos(ph) * 10f, 0);
        }
        // Death: roll on the back, cartoon style
        bool dead = pi ? pi.life <= 0 : anim && anim.isActiveAndEnabled && HasBool(anim, "death") && anim.GetBool("death");
        flip = Mathf.MoveTowards(flip, dead ? 1 : 0, Time.deltaTime * 3f);
        body.localRotation = Quaternion.Euler(0, 0, 180f * Mathf.SmoothStep(0, 1, flip));
    }

    static bool HasBool(Animator a, string n) { foreach (var p in a.parameters) if (p.name == n) return true; return false; }

    static Material Flat(Color c, Material src, float emission = 0)
    {
        string key = ColorUtility.ToHtmlStringRGBA(c) + emission.ToString("0.00");
        if (mats.TryGetValue(key, out var m) && m) return m;
        m = src ? new Material(src) : new Material(Shader.Find("Standard"));
        m.mainTexture = null; m.color = c;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.55f);
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
