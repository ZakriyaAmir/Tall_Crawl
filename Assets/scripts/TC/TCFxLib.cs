using System.Collections.Generic;
using UnityEngine;

/// Shared VFX kit (polish2): materials on the TallCrawl/FX shaders, particle-system builders and
/// colour helpers. Used at runtime (bursts, boost fire, speed lines) and by the editor baker that
/// writes the power-up pickup effects into the prefabs (Tools > Tall Crawl > Polish2 > Bake Pickup FX).
public static class TCFxLib
{
    // name -> texture (Resources path), additive, brightness
    static readonly Dictionary<string, (string tex, float add, float boost)> defs = new Dictionary<string, (string, float, float)>
    {
        { "Glow",  ("FX/fx_glow", 0.8f, 1f) },
        { "Halo",  ("FX/fx_halo", 0.7f, 1.2f) },
        { "GlowSoft", ("FX/fx_glow", 0.25f, 1f) },
        { "Star",  ("FX/fx_star", 0.85f, 1.35f) },
        { "Ring",  ("FX/fx_ring", 0.55f, 1.1f) },
        { "Rays",  ("FX/fx_rays", 0.9f, 1.2f) },
        { "Spark", ("FX/fx_spark", 0.9f, 1.5f) },
        { "Heart", ("FX/fx_heart", 0f, 1f) },
        { "Snow",  ("FX/fx_snow", 0.35f, 1.1f) },
        { "Bolt",  ("FX/fx_bolt", 0.75f, 1.4f) },
        { "Flame", ("FX/fx_flame", 0.55f, 1.25f) },
        { "Smoke", ("FX/fx_smoke", 0f, 1f) },
        { "Coin",  ("TCUI/icon_coin", 0f, 1f) },
    };
    public static IEnumerable<string> Names => defs.Keys;
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    /// Material asset Resources/FX/Materials/FX_{name} (baked by the editor so the shaders ship in builds);
    /// falls back to a runtime material when the asset is missing.
    public static Material Mat(string name)
    {
        if (cache.TryGetValue(name, out var m) && m) return m;
        m = Resources.Load<Material>("FX/Materials/FX_" + name);
        if (!m) m = Create(name);
        cache[name] = m; return m;
    }

    public static Material Create(string name)
    {
        if (name == "Shield") return CreateShield();
        var d = defs[name];
        var sh = Shader.Find("TallCrawl/FX Particle") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply") ?? Shader.Find("Sprites/Default");
        var m = new Material(sh) { name = "FX_" + name };
        var t = Resources.Load<Texture2D>(d.tex);
        if (t) m.mainTexture = t;
        if (m.HasProperty("_Additive")) m.SetFloat("_Additive", d.add);
        if (m.HasProperty("_Boost")) m.SetFloat("_Boost", d.boost);
        // pickup halo/rays/rings/themed sprites sit on the curved trunk: test depth a bit closer so the bark doesn't slice them
        if (m.HasProperty("_DepthPull")) m.SetFloat("_DepthPull", name == "Halo" || name == "Rays" ? 0.4f : name == "Ring" || name == "Snow" || name == "Bolt" || name == "Star" ? 0.3f : 0f);
        return m;
    }

    static Material CreateShield()
    {
        var sh = Shader.Find("TallCrawl/FX Shield") ?? Shader.Find("TallCrawl/FX Particle") ?? Shader.Find("Sprites/Default");
        var m = new Material(sh) { name = "FX_Shield" };
        if (m.HasProperty("_HexTex")) { m.SetTexture("_HexTex", Resources.Load<Texture2D>("FX/fx_hex")); m.SetTextureScale("_HexTex", new Vector2(3, 3)); }
        if (m.HasProperty("_NoiseTex")) m.SetTexture("_NoiseTex", Resources.Load<Texture2D>("FX/fx_noise"));
        return m;
    }
    public static Material Shield => Mat("Shield");

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    public static Gradient Fade(Color a, Color b, float inT = 0.12f, float outStart = 0.55f, float peak = 1f)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(peak, inT), new GradientAlphaKey(peak * 0.85f, outStart), new GradientAlphaKey(0, 1) });
        return g;
    }
    public static Gradient Fade3(Color a, Color b, Color c)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 0.35f), new GradientColorKey(c, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0, 1) });
        return g;
    }
    public static AnimationCurve Curve(params float[] tv) { var c = new AnimationCurve(); for (int i = 0; i + 1 < tv.Length; i += 2) c.AddKey(tv[i], tv[i + 1]); return c; }

    /// Creates a child particle system with sane mobile defaults (local space, billboard, no shape, no emission).
    public static ParticleSystem PS(Transform parent, string name, string mat, Vector3 localPos, int max = 48)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startSpeed = 0; main.startLifetime = 1; main.startSize = 0.2f;
        main.cullingMode = ParticleSystemCullingMode.Automatic;
        var em = ps.emission; em.rateOverTime = 0;
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mat(mat);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.alignment = ParticleSystemRenderSpace.View;
        r.minParticleSize = 0f; r.maxParticleSize = 2f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return ps;
    }

    public static void ColorLife(ParticleSystem ps, Gradient g) { var c = ps.colorOverLifetime; c.enabled = true; c.color = g; }
    public static void SizeLife(ParticleSystem ps, params float[] tv) { var s = ps.sizeOverLifetime; s.enabled = true; s.size = new ParticleSystem.MinMaxCurve(1f, Curve(tv)); }
    public static void Burst(ParticleSystem ps, int count, float time = 0f) { var em = ps.emission; em.enabled = true; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(time, (short)count) }); }
    public static void Sphere(ParticleSystem ps, float radius, float thickness = 1f) { var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius; sh.radiusThickness = thickness; }
    public static void SpinRandom(ParticleSystem ps, float degPerSec)
    {
        var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        var r = ps.rotationOverLifetime; r.enabled = true; r.z = new ParticleSystem.MinMaxCurve(-degPerSec * Mathf.Deg2Rad, degPerSec * Mathf.Deg2Rad);
    }
    public static void OneShot(ParticleSystem ps, float life, float lifeMax = -1)
    {
        var m = ps.main; m.loop = false; m.playOnAwake = false; m.duration = 1f;
        m.startLifetime = lifeMax > 0 ? new ParticleSystem.MinMaxCurve(life, lifeMax) : new ParticleSystem.MinMaxCurve(life);
    }
}
