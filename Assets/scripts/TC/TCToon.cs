using System.Collections.Generic;
using UnityEngine;

/// polish3 "spider style" toolkit for the enemies (squirrel, bat, wasp, snake).
/// Matches TCCuteSpider: smooth high-res meshes, TallCrawl/SpiderPreBaked cel shader, ink outline 1.4
/// (0.12,0.08,0.10), inner edge contour 0.22, shadow dull 0.55, and the spider's eye recipe
/// (dark glossy eyeball + coloured glowing pupil + white anime gleam). Soft procedural textures add
/// fur / scale detail (baked into texture assets when the enemy prefabs are saved).
public static class TCToon
{
    public static readonly Color Ink = new Color(0.12f, 0.08f, 0.10f, 1f);
    public static readonly Color EyeBall = new Color(0.08f, 0.07f, 0.09f, 1f);
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Texture2D> texs = new Dictionary<string, Texture2D>();

    static Shader Cel() => Shader.Find("TallCrawl/SpiderPreBaked") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Standard");

    /// Spider-identical cel material. outline/edge 0 for tiny bits (gleams, pupils) like the spider does.
    public static Material Mat(string name, Color c, Texture2D tex = null, float emission = 0f, float outline = 1.4f, float edge = 0.22f, Color? ink = null, float bright = 0.75f)
    {
        string key = name + ColorUtility.ToHtmlStringRGBA(c) + (tex ? tex.name : "") + emission + outline + edge + (ink.HasValue ? ColorUtility.ToHtmlStringRGBA(ink.Value) : "") + bright;
        if (mats.TryGetValue(key, out var m) && m) return m;
        m = new Material(Cel()) { name = "TCToon_" + name };
        m.mainTexture = tex ? tex : Texture2D.whiteTexture; m.color = c;
        S(m, "_Brightness", bright); S(m, "_ShadowDull", 0.55f); S(m, "_Glossiness", 0f); S(m, "_Metallic", 0f);
        S(m, "_CelIntensity", 0.30f); S(m, "_OutlineWidth", outline); S(m, "_EdgeThreshold", edge);
        if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", ink ?? Ink);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission > 0 ? c * emission : Color.black);
        if (emission > 0) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
        mats[key] = m; return m;
    }
    static void S(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }

    /// See-through wing film (same shader family as the spider's bee / wasp skin wings).
    public static Material Film(string name, Color c)
    {
        string key = "film" + name + ColorUtility.ToHtmlStringRGBA(c);
        if (mats.TryGetValue(key, out var m) && m) return m;
        var sh = Shader.Find("TallCrawl/TCFadeEntity") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
        m = new Material(sh) { name = "TCToon_" + name };
        m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        mats[key] = m; return m;
    }

    // ------------------------------------------------------------------ meshes
    /// Smooth lathe along +Z (v: 0 = z0 .. 1 = z1). radius(v) -> radius scale; section squash sx/sy;
    /// spine(v) -> (x,y) offset of the centre line. UV: u around (0 = +Y top), v along. Seam/pole normals welded
    /// so the inverted-hull outline never cracks.
    public static Mesh Lathe(string key, float z0, float z1, System.Func<float, float> radius, float sx = 1f, float sy = 1f,
                             System.Func<float, Vector2> spine = null, int seg = 32, int rings = 28)
    {
        if (meshes.TryGetValue(key, out var cached) && cached) return cached;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int r = 0; r <= rings; r++)
        {
            float a = r / (float)rings;
            float z = Mathf.Lerp(z0, z1, a); float rad = SafeR(radius(a));
            Vector2 c = spine != null ? spine(a) : Vector2.zero;
            for (int s = 0; s <= seg; s++)
            {
                float th = s / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(c.x + Mathf.Sin(th) * rad * sx, c.y + Mathf.Cos(th) * rad * sy, z));
                uv.Add(new Vector2(s / (float)seg, a));
            }
        }
        int row = seg + 1;
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < seg; s++)
            {
                int i0 = r * row + s, i1 = i0 + 1, i2 = i0 + row, i3 = i2 + 1;
                t.Add(i0); t.Add(i2); t.Add(i1); t.Add(i1); t.Add(i2); t.Add(i3);
            }
        var m = new Mesh { name = key }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateNormals();
        var n = m.normals;
        for (int r = 0; r <= rings; r++) { int a = r * row, b = a + seg; var avg = (n[a] + n[b]).normalized; n[a] = avg; n[b] = avg; }
        // poles: average whole ring (or point along the axis when it collapses)
        for (int pole = 0; pole < 2; pole++)
        {
            int r = pole == 0 ? 0 : rings; float rad = SafeR(radius(pole == 0 ? 0f : 1f));
            if (rad > 1e-4f) continue;
            Vector3 dir = pole == 0 ? Vector3.back : Vector3.forward;
            for (int s = 0; s <= seg; s++) n[r * row + s] = dir;
        }
        m.normals = n; m.RecalculateBounds();
        // flip check: normals should point away from the centre line
        var tri = m.triangles; Vector3 fn = Vector3.Cross(v[tri[1]] - v[tri[0]], v[tri[2]] - v[tri[0]]);
        if (Vector3.Dot(fn, n[tri[0]] + n[tri[1]] + n[tri[2]]) < 0) { for (int i = 0; i < tri.Length; i += 3) { int x = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = x; } m.triangles = tri; }
        meshes[key] = m; return m;
    }

    /// GLSL-style smoothstep edge (Mathf.SmoothStep interpolates between its first two args instead).
    public static float Edge(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

    /// Pow(sin(pi), k) is NaN in float (sin(PI) < 0): clamp every radius sample.
    static float SafeR(float r) => float.IsNaN(r) || r < 0f ? 0f : r;

    /// Unit-radius smooth sphere (radius 1, scale it) with welded seams.
    public static Mesh Ball() => Lathe("tc_ball", -1f, 1f, a => Mathf.Sin(a * Mathf.PI), 1f, 1f, null, 32, 24);
    /// Round-ended egg / bean: length along Z, fat end at the back when taper > 0.
    public static Mesh Egg(string key, float len, float rad, float taper = 0.25f) =>
        Lathe(key, -len * 0.5f, len * 0.5f, a => rad * Mathf.Pow(Mathf.Sin(a * Mathf.PI), 0.62f) * (1f - taper * (a - 0.5f)), 1f, 1f, null, 32, 24);

    /// Thin smooth membrane with soft rounded rim (good outline): outline polygon in X/Y, extruded +-thick/2 with
    /// a bulged centre. Double-sided by construction.
    public static Mesh Membrane(string key, Vector2[] outline, float thick = 0.008f, float bulge = 0.01f)
    {
        if (meshes.TryGetValue(key, out var cached) && cached) return cached;
        int n = outline.Length; Vector2 cen = Vector2.zero; foreach (var p in outline) cen += p; cen /= n;
        var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
        // rings from rim (k=0) to centre (k=K) on both faces
        const int K = 4;
        for (int face = 0; face < 2; face++)
        {
            float sgn = face == 0 ? 1f : -1f;
            for (int k = 0; k <= K; k++)
            {
                float f = k / (float)K; float h = sgn * (thick * 0.5f + bulge * Mathf.Sin(f * Mathf.PI * 0.5f));
                for (int i = 0; i < n; i++) { var p = Vector2.Lerp(outline[i], cen, f * 0.92f); v.Add(new Vector3(p.x, p.y, h)); uv.Add(new Vector2(i / (float)n, f)); }
            }
        }
        int faceStride = (K + 1) * n;
        for (int face = 0; face < 2; face++)
            for (int k = 0; k < K; k++)
                for (int i = 0; i < n; i++)
                {
                    int a = face * faceStride + k * n + i, b = face * faceStride + k * n + (i + 1) % n, c = a + n, d = b + n;
                    if (face == 0) { t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d); }
                    else { t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c); }
                }
        // centre fans
        for (int face = 0; face < 2; face++)
        {
            int ci = v.Count; float sgn = face == 0 ? 1f : -1f; v.Add(new Vector3(cen.x, cen.y, sgn * (thick * 0.5f + bulge))); uv.Add(new Vector2(0.5f, 1f));
            int baseK = face * faceStride + K * n;
            for (int i = 0; i < n; i++) { int a = baseK + i, b = baseK + (i + 1) % n; if (face == 0) { t.Add(a); t.Add(ci); t.Add(b); } else { t.Add(a); t.Add(b); t.Add(ci); } }
        }
        // rim strip joining the two faces
        for (int i = 0; i < n; i++) { int a = i, b = (i + 1) % n, c = faceStride + i, d = faceStride + (i + 1) % n; t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c); }
        var m = new Mesh { name = key }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals();
        // rim normals point outward in-plane so the outline hugs the edge
        var nn = m.normals;
        for (int face = 0; face < 2; face++)
            for (int i = 0; i < n; i++)
            {
                Vector2 e = (outline[(i + 1) % n] - outline[(i + n - 1) % n]); Vector2 o = new Vector2(e.y, -e.x).normalized;
                if (Vector2.Dot(o, outline[i] - cen) < 0) o = -o;
                nn[face * faceStride + i] = (new Vector3(o.x, o.y, 0) + new Vector3(0, 0, face == 0 ? 0.35f : -0.35f)).normalized;
            }
        m.normals = nn; m.RecalculateBounds();
        meshes[key] = m; return m;
    }

    // ------------------------------------------------------------------ textures
    /// Soft vertical gradient (lathe v) with a belly band (lathe u around 0.5 = underside) and fine fur noise.
    public static Texture2D Fur(string key, Color top, Color belly, float bellyWidth = 0.22f, Color? stripe = null, float stripeWidth = 0f, float noise = 0.06f)
    {
        if (texs.TryGetValue(key, out var tx) && tx) return tx;
        const int W = 128, H = 128; tx = new Texture2D(W, H, TextureFormat.RGB24, true) { name = key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H]; var rnd = new System.Random(key.GetHashCode());
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)(W - 1), vv = y / (float)(H - 1);
                float du = Mathf.Abs(u - 0.5f); // 0 at underside
                float b = 1f - Edge(bellyWidth * 0.6f, bellyWidth, du);
                Color c = Color.Lerp(top, belly, b);
                if (stripe.HasValue && stripeWidth > 0f) { float ds = Mathf.Min(u, 1f - u); c = Color.Lerp(c, stripe.Value, 1f - Edge(stripeWidth * 0.55f, stripeWidth, ds)); }
                // soft top-light: slightly lighter along the back
                c *= 1f + 0.06f * (1f - Mathf.Clamp01(Mathf.Min(u, 1 - u) * 4f));
                float nz = (float)(rnd.NextDouble() - 0.5) * noise + Mathf.Sin(x * 0.9f + y * 2.3f) * noise * 0.35f;
                px[y * W + x] = new Color(c.r + nz, c.g + nz, c.b + nz * 0.8f);
            }
        tx.SetPixels(px); tx.Apply(true); texs[key] = tx; return tx;
    }

    /// Snake skin: back colour with rounded saddle blotches along v, light belly/side band, tiny scale speckle.
    public static Texture2D SnakeSkin(string key, Color back, Color blotch, Color belly, int blotches = 9)
    {
        if (texs.TryGetValue(key, out var tx) && tx) return tx;
        const int W = 64, H = 512; tx = new Texture2D(W, H, TextureFormat.RGB24, true) { name = key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)(W - 1), vv = y / (float)(H - 1);
                float du = Mathf.Abs(u - 0.5f);
                Color c = Color.Lerp(back, belly, 1f - Edge(0.17f, 0.26f, du));
                // saddles on the back (u near 0/1), rounded, fading out toward the tail
                float dTop = Mathf.Min(u, 1f - u);
                float ph = Mathf.Repeat(vv * blotches + 0.3f, 1f); float dv = Mathf.Abs(ph - 0.5f) * 2f; // 0 centre
                float saddle = 1f - Edge(0.55f, 0.75f, Mathf.Sqrt(dv * dv * 0.8f + (dTop / 0.24f) * (dTop / 0.24f)));
                saddle *= Mathf.Lerp(1f, 0.55f, vv);
                c = Color.Lerp(c, blotch, saddle);
                // scale speckle
                float sc = Mathf.Sin(x * 2.4f) * Mathf.Sin(y * 0.55f + (x % 2) * 1.5f);
                c *= 1f + sc * 0.035f;
                px[y * W + x] = c;
            }
        tx.SetPixels(px); tx.Apply(true); texs[key] = tx; return tx;
    }

    // ------------------------------------------------------------------ parts
    public static Transform Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(euler); g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return g.transform;
    }
    public static Transform Ball(string name, Transform parent, Material mat, Vector3 pos, Vector3 radii, Vector3? euler = null) =>
        Part(name, parent, Ball(), mat, pos, euler ?? Vector3.zero, radii);
    public static Transform Pivot(string name, Transform parent, Vector3 pos, Vector3? euler = null)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = pos; g.localRotation = Quaternion.Euler(euler ?? Vector3.zero); return g;
    }

    /// The spider's eye: dark glossy eyeball, coloured glowing pupil, white anime gleam (top-right).
    /// size = eyeball radius; dir = look direction in parent space. Returns the eyeball (scale Y to blink).
    public static Transform Eye(string name, Transform parent, Vector3 pos, Vector3 dir, float size, Color pupil, float depth = 0.55f)
    {
        var e = Pivot(name, parent, pos); e.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        var ball = Ball("Ball", e, Mat("eyeball", EyeBall, null, 0f, 1.4f, 0f), Vector3.zero, new Vector3(size, size, size * depth));
        Ball("Pupil", ball, Mat("pupil", pupil, null, 0.3f, 0f, 0f), new Vector3(0f, -0.04f, 0.62f), new Vector3(0.56f, 0.56f, 0.45f));
        Ball("Shine", ball, Mat("shine", new Color(0.97f, 0.97f, 0.97f), null, 0.55f, 0f, 0f, null, 1.1f), new Vector3(0.30f, 0.34f, 0.80f), new Vector3(0.26f, 0.26f, 0.22f));
        Ball("Shine2", ball, Mat("shine", new Color(0.97f, 0.97f, 0.97f), null, 0.55f, 0f, 0f, null, 1.1f), new Vector3(-0.26f, -0.30f, 0.84f), new Vector3(0.12f, 0.12f, 0.12f));
        return e;
    }

    /// Bands along lathe v (wasp abdomen): alternating colours with soft edges + fuzz noise.
    public static Texture2D Bands(string key, Color a, Color b, int bands, float soft = 0.08f, float startV = 0f)
    {
        if (texs.TryGetValue(key, out var tx) && tx) return tx;
        const int W = 32, H = 256; tx = new Texture2D(W, H, TextureFormat.RGB24, true) { name = key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H]; var rnd = new System.Random(key.GetHashCode());
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float vv = y / (float)(H - 1);
                float ph = Mathf.Repeat((vv - startV) * bands, 1f);
                float k = Edge(0.5f - soft, 0.5f + soft, ph) * (1f - Edge(1f - soft, 1f, ph)) + (ph < soft ? 1f - Edge(0f, soft, ph) : 0f);
                Color c = Color.Lerp(a, b, Mathf.Clamp01(k));
                float nz = (float)(rnd.NextDouble() - 0.5) * 0.05f; px[y * W + x] = new Color(c.r + nz, c.g + nz, c.b + nz);
            }
        tx.SetPixels(px); tx.Apply(true); texs[key] = tx; return tx;
    }
}
