using System.Collections.Generic;
using UnityEngine;

/// Tiny procedural low-poly toolkit for the cartoon enemies (snake, squirrel, bat).
/// Every part is built twice: a flat-shaded faceted mesh (the visible body, cel-lit) and a smooth-normal
/// "hull" copy shrunk a hair inside it that only draws the ink outline, so faceted meshes get a crack-free
/// silhouette stroke. Built-in pipeline shader TallCrawl/SpiderPreBaked (same as the other enemies) so it never goes pink.
public static class TCLowPoly
{
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    public static readonly Color Ink = new Color(0.16f, 0.09f, 0.05f, 1f);

    static Shader CelShader() => Shader.Find("TallCrawl/SpiderPreBaked") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Standard");

    /// Faceted body material: cel bands on, no outline pass (the hull does the stroke).
    public static Material Mat(Color c, float cel = 0.42f, float bright = 1.0f, Color? emission = null)
    {
        string key = "m" + ColorUtility.ToHtmlStringRGBA(c) + cel + bright + (emission.HasValue ? ColorUtility.ToHtmlStringRGBA(emission.Value) : "");
        if (mats.TryGetValue(key, out var m) && m) return m;
        m = new Material(CelShader()) { name = "TC_LP_" + ColorUtility.ToHtmlStringRGB(c) };
        m.color = c;
        Set(m, "_Brightness", bright); Set(m, "_ShadowDull", 1f); Set(m, "_CelIntensity", cel);
        Set(m, "_EdgeThreshold", 0f); Set(m, "_OutlineWidth", 0f);
        if (emission.HasValue && m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", emission.Value); m.EnableKeyword("_EMISSION"); }
        mats[key] = m; return m;
    }

    /// Outline-only material for hull meshes (its base pass is hidden inside the faceted body).
    public static Material HullMat(Color ink, float width = 1.25f, Color? baseColor = null)
    {
        string key = "h" + ColorUtility.ToHtmlStringRGBA(ink) + width + (baseColor.HasValue ? ColorUtility.ToHtmlStringRGBA(baseColor.Value) : "");
        if (mats.TryGetValue(key, out var m) && m) return m;
        m = new Material(CelShader()) { name = "TC_LP_Hull" };
        m.color = baseColor ?? ink; Set(m, "_Brightness", 1f); Set(m, "_ShadowDull", 1f); Set(m, "_CelIntensity", 0f); Set(m, "_EdgeThreshold", 0f);
        Set(m, "_OutlineWidth", width); if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", ink);
        mats[key] = m; return m;
    }

    static void Set(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }

    // ---------------- mesh primitives (smooth, indexed) ----------------

    /// Low-poly ellipsoid (UV sphere) with optional per-ring radius profile and deterministic jitter.
    public static Mesh Ellipsoid(int sides, int rings, Vector3 radii, float jitter = 0f, int seed = 1, System.Func<float, float> profile = null)
    {
        var v = new List<Vector3>(); var tris = new List<int>();
        var rnd = new System.Random(seed);
        v.Add(new Vector3(0, -radii.y, 0));
        for (int r = 1; r < rings; r++)
        {
            float t = (float)r / rings; float phi = Mathf.PI * t - Mathf.PI * 0.5f;
            float k = profile != null ? profile(t) : 1f;
            for (int s = 0; s < sides; s++)
            {
                float th = 2f * Mathf.PI * (s + (r % 2) * 0.5f) / sides;
                float j = 1f + (float)(rnd.NextDouble() * 2 - 1) * jitter;
                v.Add(new Vector3(Mathf.Cos(phi) * Mathf.Cos(th) * radii.x * k * j, Mathf.Sin(phi) * radii.y * j, Mathf.Cos(phi) * Mathf.Sin(th) * radii.z * k * j));
            }
        }
        v.Add(new Vector3(0, radii.y, 0));
        int top = v.Count - 1;
        for (int s = 0; s < sides; s++) { int a = 1 + s, b = 1 + (s + 1) % sides; tris.Add(0); tris.Add(a); tris.Add(b); }
        for (int r = 0; r < rings - 2; r++)
            for (int s = 0; s < sides; s++)
            {
                int a = 1 + r * sides + s, b = 1 + r * sides + (s + 1) % sides, c = a + sides, d = b + sides;
                tris.Add(a); tris.Add(c); tris.Add(d); tris.Add(a); tris.Add(d); tris.Add(b);
            }
        int lr = 1 + (rings - 2) * sides;
        for (int s = 0; s < sides; s++) { int a = lr + s, b = lr + (s + 1) % sides; tris.Add(a); tris.Add(top); tris.Add(b); }
        return Build(v, tris);
    }

    /// Cone/tapered prism along +Y from base radius r0 (at y=0) to r1 (at y=h).
    public static Mesh Cone(int sides, float r0, float r1, float h, float squashZ = 1f)
    {
        var v = new List<Vector3>(); var tris = new List<int>();
        for (int s = 0; s < sides; s++) { float th = 2 * Mathf.PI * s / sides; v.Add(new Vector3(Mathf.Cos(th) * r0, 0, Mathf.Sin(th) * r0 * squashZ)); }
        for (int s = 0; s < sides; s++) { float th = 2 * Mathf.PI * s / sides; v.Add(new Vector3(Mathf.Cos(th) * r1, h, Mathf.Sin(th) * r1 * squashZ)); }
        int bc = v.Count; v.Add(Vector3.zero); int tc = v.Count; v.Add(new Vector3(0, h, 0));
        for (int s = 0; s < sides; s++)
        {
            int a = s, b = (s + 1) % sides, c = a + sides, d = b + sides;
            tris.Add(a); tris.Add(c); tris.Add(d); tris.Add(a); tris.Add(d); tris.Add(b);
            tris.Add(bc); tris.Add(a); tris.Add(b);
            if (r1 > 0.0001f) { tris.Add(tc); tris.Add(d); tris.Add(c); }
        }
        return Build(v, tris);
    }

    /// Flat triangle-fan membrane (bat wing) from a polygon outline in the XY plane, with slight thickness.
    public static Mesh Membrane(Vector2[] outline, float thick = 0.006f, float sag = 0.0f)
    {
        var v = new List<Vector3>(); var tris = new List<int>();
        Vector2 c = Vector2.zero; foreach (var p in outline) c += p; c /= outline.Length;
        int n = outline.Length;
        v.Add(new Vector3(c.x, c.y, -thick - sag)); v.Add(new Vector3(c.x, c.y, thick - sag));
        foreach (var p in outline) { v.Add(new Vector3(p.x, p.y, -thick)); }
        foreach (var p in outline) { v.Add(new Vector3(p.x, p.y, thick)); }
        for (int i = 0; i < n; i++)
        {
            int a = 2 + i, b = 2 + (i + 1) % n;
            tris.Add(0); tris.Add(b); tris.Add(a);           // back
            tris.Add(1); tris.Add(a + n); tris.Add(b + n);   // front
            tris.Add(a); tris.Add(b); tris.Add(b + n); tris.Add(a); tris.Add(b + n); tris.Add(a + n); // rim
        }
        return Build(v, tris);
    }

    public static Mesh Build(List<Vector3> v, List<int> t)
    {
        var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    /// Unwelds every triangle so each face gets its own normal (the faceted low-poly look).
    public static Mesh Flat(Mesh src)
    {
        var sv = src.vertices; var st = src.triangles;
        var v = new Vector3[st.Length]; var t = new int[st.Length];
        for (int i = 0; i < st.Length; i++) { v[i] = sv[st[i]]; t[i] = i; }
        var m = new Mesh(); m.vertices = v; m.triangles = t; m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    /// Smooth hull copy pushed inward along its normals so only its outline pass shows.
    public static Mesh Hull(Mesh src, float inset = 0.004f)
    {
        var m = Object.Instantiate(src); var v = m.vertices; var nn = m.normals;
        for (int i = 0; i < v.Length; i++) v[i] -= nn[i] * inset;
        m.vertices = v; m.RecalculateBounds(); return m;
    }

    /// Adds a faceted part (+ outline hull) under parent. Returns the part transform.
    public static Transform Part(string name, Transform parent, Mesh smooth, Material mat, Vector3 pos, Vector3 euler, Vector3? scale = null, bool outline = true, float inset = 0.004f)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(euler); g.transform.localScale = scale ?? Vector3.one;
        AddRenderer(g, Flat(smooth), mat);
        if (outline)
        {
            var h = new GameObject("Hull"); h.transform.SetParent(g.transform, false);
            AddRenderer(h, Hull(smooth, inset), HullMat(Ink, 1.25f, mat ? mat.color : (Color?)null));
        }
        return g.transform;
    }

    public static MeshRenderer AddRenderer(GameObject g, Mesh mesh, Material mat)
    {
        var mf = g.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return mr;
    }
}
