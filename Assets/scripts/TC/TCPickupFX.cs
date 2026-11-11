using System.Collections.Generic;
using UnityEngine;

public enum TCPickupKind { Chilli, Health, SlowMo, Magnet, Coin, BoostStart }

/// Power-up pickup juice (polish2): bob + swing/spin of the model, camera-facing halo and rotating rays,
/// sonar ping, twinkles and a themed particle layer per type (flames, hearts, snowflakes, coins + bolts).
/// The effect objects are baked into the prefab (Tools > Tall Crawl > Polish2 > Bake Pickup FX) so they can be
/// tweaked in the editor; this component only animates them and hides them when the pickup is collected.
/// Also owns the pooled collect bursts (TCPickupFX.Burst).
[ExecuteAlways]
public class TCPickupFX : MonoBehaviour
{
    public TCPickupKind kind;
    [Tooltip("Tagged pickup object (collider). FX hide when it is collected.")] public Transform item;
    [Tooltip("Model that bobs and swings (defaults to the item).")] public Transform visual;
    public Transform fxRoot;
    public Renderer halo, rays;
    public Color color = Color.white, color2 = Color.white;
    [Header("Motion")]
    public float bobHeight = 0.055f, bobSpeed = 2.6f;
    [Tooltip("Degrees of left/right swing (keeps the icon facing the player). 0 = keep the model's own spin.")] public float swing = 32f;
    public float swingSpeed = 2.2f;
    public float pulse = 0.07f;
    public float raysSpin = 40f;
    [Tooltip("Push the halo away from the camera so it sits behind the model.")] public float haloBehind = 0.16f;

    Vector3 itemBase, visualBaseScale; Quaternion visualBase; bool captured; float phase;
    MaterialPropertyBlock mpb; Vector3 haloBase, raysBase; Camera cam;

    void OnEnable()
    {
        if (!captured && item)
        {
            itemBase = item.localPosition;
            var v = visual ? visual : item;
            visualBase = v.localRotation; visualBaseScale = v.localScale;
            if (halo) haloBase = halo.transform.localScale;
            if (rays) raysBase = rays.transform.localScale;
            captured = true;
            if (Application.isPlaying && swing > 0)
                foreach (var cr in GetComponentsInChildren<continuousRotation>(true)) cr.enabled = false;
        }
        phase = Application.isPlaying ? Random.value * 10f : 0f;
        ApplyTint();
    }

    void OnDisable()
    {
        if (!captured || !item) return;
        item.localPosition = itemBase;
        var v = visual ? visual : item; v.localRotation = visualBase; v.localScale = visualBaseScale;
    }

    void ApplyTint()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        if (halo) { halo.GetPropertyBlock(mpb); mpb.SetColor("_TintColor", color); halo.SetPropertyBlock(mpb); }
        if (rays) { rays.GetPropertyBlock(mpb); mpb.SetColor("_TintColor", new Color(color2.r, color2.g, color2.b, 0.75f)); rays.SetPropertyBlock(mpb); }
    }

    void LateUpdate()
    {
        if (!item || !fxRoot) return;
        bool on = item.gameObject.activeSelf;
        if (fxRoot.gameObject.activeSelf != on) fxRoot.gameObject.SetActive(on);
        if (!on) return;
        if (!Application.isPlaying) { ApplyTint(); return; }
        float t = Time.time + phase;

        // bob along the trunk axis + gentle squash so it feels alive
        item.localPosition = itemBase + Vector3.up * Mathf.Sin(t * bobSpeed) * bobHeight;
        var v = visual ? visual : item;
        if (swing > 0)
        {
            var up = v.parent ? v.parent.InverseTransformDirection(transform.up) : Vector3.up;
            v.localRotation = Quaternion.AngleAxis(Mathf.Sin(t * swingSpeed) * swing, up) * visualBase;
        }
        float s = 1f + Mathf.Sin(t * bobSpeed * 2f) * 0.035f;
        v.localScale = new Vector3(visualBaseScale.x * (2f - s), visualBaseScale.y * s, visualBaseScale.z * (2f - s));

        // camera-facing halo behind the model
        if (!cam) cam = Camera.main;
        var centre = item.TransformPoint(Vector3.zero);
        if (visual) { var r = visual.GetComponentInChildren<Renderer>(); if (r) centre = r.bounds.center; }
        if (cam)
        {
            var toCam = (cam.transform.position - centre).normalized;
            fxRoot.position = centre - toCam * haloBehind;
            fxRoot.rotation = Quaternion.LookRotation(-toCam, cam.transform.up);
        }
        float p = 1f + Mathf.Sin(t * 3.1f) * pulse;
        if (halo) halo.transform.localScale = haloBase * p;
        if (rays)
        {
            rays.transform.localScale = raysBase * (2f - p);
            rays.transform.localRotation = Quaternion.Euler(0, 0, t * raysSpin);
        }
    }

    // ------------------------------------------------------------------ prefab builder (editor bake)
    public static void Palette(TCPickupKind k, out Color c1, out Color c2)
    {
        switch (k)
        {
            case TCPickupKind.Chilli: c1 = TCFxLib.Hex("#FF4B2B"); c2 = TCFxLib.Hex("#FFC93C"); break;
            case TCPickupKind.Health: c1 = TCFxLib.Hex("#41E58C"); c2 = TCFxLib.Hex("#FF6F9C"); break;
            case TCPickupKind.SlowMo: c1 = TCFxLib.Hex("#47C6FF"); c2 = TCFxLib.Hex("#E4FAFF"); break;
            case TCPickupKind.Magnet: c1 = TCFxLib.Hex("#FFD23F"); c2 = TCFxLib.Hex("#FFF4B0"); break;
            case TCPickupKind.BoostStart: c1 = TCFxLib.Hex("#FF6A1A"); c2 = TCFxLib.Hex("#FFE066"); break;
            default: c1 = TCFxLib.Hex("#FFD54F"); c2 = Color.white; break;
        }
    }

    /// Builds the FX hierarchy under a pickup prefab root. Idempotent: removes an earlier "PickupFX".
    public static TCPickupFX Build(GameObject root, TCPickupKind k, Transform item, Transform visual, Vector3 centreLocal, float size)
    {
        var old = root.transform.Find("PickupFX"); if (old) Object.DestroyImmediate(old.gameObject);
        Palette(k, out var c1, out var c2);
        var fx = new GameObject("PickupFX").transform;
        fx.SetParent(root.transform, false); fx.localPosition = centreLocal;

        var halo = Quad(fx, "Halo", "Glow", size * 2.3f, 0f);
        var rays = Quad(fx, "Rays", "Rays", size * 2.9f, -0.01f);

        // sonar ping ring
        var ping = TCFxLib.PS(fx, "Ping", "Ring", new Vector3(0, 0, -0.02f), 4);
        { var m = ping.main; m.startLifetime = 1.1f; m.startSize = size * 3.2f; m.startColor = new Color(c1.r, c1.g, c1.b, 0.9f);
          var em = ping.emission; em.rateOverTime = 0.95f; TCFxLib.SizeLife(ping, 0, 0.25f, 1, 1); TCFxLib.ColorLife(ping, TCFxLib.Fade(Color.white, c2, 0.1f, 0.4f)); }

        // twinkles
        var tw = TCFxLib.PS(fx, "Twinkles", "Star", new Vector3(0, 0, -0.26f), 16);
        { var m = tw.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f); m.startSize = new ParticleSystem.MinMaxCurve(size * 0.25f, size * 0.55f); m.startColor = new ParticleSystem.MinMaxGradient(Color.white, c2);
          var em = tw.emission; em.rateOverTime = 7; TCFxLib.Sphere(tw, size * 0.95f, 0.35f); TCFxLib.SizeLife(tw, 0, 0, 0.3f, 1, 0.6f, 0.4f, 1, 0); TCFxLib.SpinRandom(tw, 90); }

        switch (k)
        {
            case TCPickupKind.Chilli:
            {
                var fl = TCFxLib.PS(fx, "Flames", "Flame", new Vector3(0, -size * 0.25f, -0.12f), 30);
                var m = fl.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f); m.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 0.85f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.5f); m.startColor = Color.white;
                var em = fl.emission; em.rateOverTime = 16; TCFxLib.Sphere(fl, size * 0.45f, 1f);
                TCFxLib.SizeLife(fl, 0, 0.5f, 0.3f, 1f, 1, 0.1f); TCFxLib.ColorLife(fl, TCFxLib.Fade(Color.white, c1, 0.1f, 0.5f));
                var vel = fl.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.y = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); vel.x = new ParticleSystem.MinMaxCurve(0, 0); vel.z = new ParticleSystem.MinMaxCurve(0, 0);
                var emb = TCFxLib.PS(fx, "Embers", "Spark", new Vector3(0, 0, -0.26f), 20);
                var me = emb.main; me.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f); me.startSize = new ParticleSystem.MinMaxCurve(size * 0.08f, size * 0.16f); me.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f); me.startColor = new ParticleSystem.MinMaxGradient(c2, TCFxLib.Hex("#FF8A2B"));
                var ee = emb.emission; ee.rateOverTime = 10; TCFxLib.Sphere(emb, size * 0.6f, 1f);
                var ev = emb.velocityOverLifetime; ev.enabled = true; ev.space = ParticleSystemSimulationSpace.World; ev.y = new ParticleSystem.MinMaxCurve(0.4f, 0.8f); ev.x = new ParticleSystem.MinMaxCurve(0, 0); ev.z = new ParticleSystem.MinMaxCurve(0, 0);
                var nz = emb.noise; nz.enabled = true; nz.strength = 0.4f; nz.frequency = 1.5f; nz.quality = ParticleSystemNoiseQuality.Low;
                TCFxLib.ColorLife(emb, TCFxLib.Fade(Color.white, c1, 0.05f, 0.6f));
                break;
            }
            case TCPickupKind.Health:
            {
                var h = TCFxLib.PS(fx, "Hearts", "Heart", new Vector3(0, 0, -0.26f), 10);
                var m = h.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.4f); m.startSize = new ParticleSystem.MinMaxCurve(size * 0.28f, size * 0.42f); m.startColor = new ParticleSystem.MinMaxGradient(c2, TCFxLib.Hex("#FF4F7B"));
                var em = h.emission; em.rateOverTime = 3.2f; TCFxLib.Sphere(h, size * 0.7f, 0.2f);
                var v = h.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.y = new ParticleSystem.MinMaxCurve(0.25f, 0.4f); v.x = new ParticleSystem.MinMaxCurve(0, 0); v.z = new ParticleSystem.MinMaxCurve(0, 0);
                TCFxLib.SizeLife(h, 0, 0, 0.2f, 1.15f, 0.35f, 1, 1, 0.6f); TCFxLib.ColorLife(h, TCFxLib.Fade(Color.white, Color.white, 0.12f, 0.6f));
                var plus = TCFxLib.PS(fx, "Cross", "Spark", new Vector3(0, 0, -0.26f), 12);
                var mp = plus.main; mp.startLifetime = 0.8f; mp.startSize = new ParticleSystem.MinMaxCurve(size * 0.12f, size * 0.2f); mp.startColor = TCFxLib.Hex("#B8FFD6");
                var ep = plus.emission; ep.rateOverTime = 6; TCFxLib.Sphere(plus, size * 0.85f, 0.3f);
                var vp = plus.velocityOverLifetime; vp.enabled = true; vp.space = ParticleSystemSimulationSpace.World; vp.y = new ParticleSystem.MinMaxCurve(0.15f, 0.3f); vp.x = new ParticleSystem.MinMaxCurve(0, 0); vp.z = new ParticleSystem.MinMaxCurve(0, 0);
                TCFxLib.ColorLife(plus, TCFxLib.Fade(Color.white, c1, 0.1f, 0.5f));
                break;
            }
            case TCPickupKind.SlowMo:
            {
                var s = TCFxLib.PS(fx, "Snowflakes", "Snow", new Vector3(0, 0, -0.26f), 14);
                var m = s.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.0f); m.startSize = new ParticleSystem.MinMaxCurve(size * 0.18f, size * 0.32f); m.startColor = new ParticleSystem.MinMaxGradient(Color.white, c1);
                var em = s.emission; em.rateOverTime = 4.5f; TCFxLib.Sphere(s, size * 1.0f, 0.15f); TCFxLib.SpinRandom(s, 60);
                var v = s.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local; v.orbitalZ = new ParticleSystem.MinMaxCurve(0.9f, 1.3f); v.y = new ParticleSystem.MinMaxCurve(-0.08f, -0.03f); v.x = new ParticleSystem.MinMaxCurve(0, 0); v.z = new ParticleSystem.MinMaxCurve(0, 0);
                TCFxLib.ColorLife(s, TCFxLib.Fade(Color.white, c2, 0.15f, 0.65f)); TCFxLib.SizeLife(s, 0, 0.3f, 0.2f, 1, 1, 0.7f);
                var mist = TCFxLib.PS(fx, "FrostMist", "GlowSoft", new Vector3(0, 0, 0.02f), 8);
                var mm = mist.main; mm.startLifetime = 1.8f; mm.startSize = new ParticleSystem.MinMaxCurve(size * 1.2f, size * 1.8f); mm.startColor = new Color(c2.r, c2.g, c2.b, 0.35f);
                var me = mist.emission; me.rateOverTime = 2; TCFxLib.Sphere(mist, size * 0.4f, 1); TCFxLib.ColorLife(mist, TCFxLib.Fade(Color.white, c1, 0.3f, 0.6f, 0.6f)); TCFxLib.SpinRandom(mist, 20);
                break;
            }
            case TCPickupKind.Magnet:
            {
                var c = TCFxLib.PS(fx, "CoinsIn", "Coin", new Vector3(0, 0, -0.26f), 14);
                var m = c.main; m.startLifetime = 0.9f; m.startSize = new ParticleSystem.MinMaxCurve(size * 0.2f, size * 0.3f); m.startColor = Color.white;
                var em = c.emission; em.rateOverTime = 5; TCFxLib.Sphere(c, size * 1.5f, 0f);
                var v = c.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local; v.radial = new ParticleSystem.MinMaxCurve(-1.4f, -1.1f); v.orbitalZ = new ParticleSystem.MinMaxCurve(1.5f, 2.0f); v.x = new ParticleSystem.MinMaxCurve(0, 0); v.y = new ParticleSystem.MinMaxCurve(0, 0); v.z = new ParticleSystem.MinMaxCurve(0, 0);
                TCFxLib.SizeLife(c, 0, 0.6f, 0.2f, 1, 0.8f, 0.8f, 1, 0); TCFxLib.ColorLife(c, TCFxLib.Fade(Color.white, Color.white, 0.15f, 0.8f));
                var b = TCFxLib.PS(fx, "Bolts", "Bolt", new Vector3(0, 0, -0.26f), 6);
                var mb = b.main; mb.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.2f); mb.startSize = new ParticleSystem.MinMaxCurve(size * 0.4f, size * 0.6f); mb.startColor = new ParticleSystem.MinMaxGradient(c2, TCFxLib.Hex("#7FD8FF"));
                mb.startRotation = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
                var eb = b.emission; eb.rateOverTime = 5; TCFxLib.Sphere(b, size * 0.85f, 0f);
                break;
            }
        }

        var comp = root.GetComponent<TCPickupFX>(); if (!comp) comp = root.AddComponent<TCPickupFX>();
        comp.kind = k; comp.item = item; comp.visual = visual; comp.fxRoot = fx; comp.color = c1; comp.color2 = c2;
        comp.halo = halo; comp.rays = rays;
        comp.swing = k == TCPickupKind.Chilli ? 26f : 34f;
        return comp;
    }

    static Renderer Quad(Transform parent, string name, string mat, float size, float z)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var col = q.GetComponent<Collider>(); if (col) Object.DestroyImmediate(col);
        q.name = name; q.transform.SetParent(parent, false); q.transform.localPosition = new Vector3(0, 0, z); q.transform.localScale = Vector3.one * size;
        var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = TCFxLib.Mat(mat);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return r;
    }

    // ------------------------------------------------------------------ collect bursts (runtime, pooled)
    static readonly Dictionary<TCPickupKind, List<ParticleSystem>> pools = new Dictionary<TCPickupKind, List<ParticleSystem>>();
    static readonly Dictionary<TCPickupKind, int> next = new Dictionary<TCPickupKind, int>();
    static Transform burstHost;

    /// Collect burst: flash + shock ring + stars + themed particles. Follows the climbing spider (parented to the player).
    public static void Burst(TCPickupKind k, Vector3 pos)
    {
        if (!Application.isPlaying) return;
        if (!burstHost)
        {
            pools.Clear(); next.Clear();
            var pl = Object.FindObjectOfType<player>();
            burstHost = new GameObject("TC_PickupBursts").transform;
            if (pl) burstHost.SetParent(pl.p1 ? pl.p1 : pl.transform, false);
        }
        if (!pools.TryGetValue(k, out var list)) { list = new List<ParticleSystem>(); pools[k] = list; next[k] = 0; }
        int cap = k == TCPickupKind.Coin ? 6 : 2;
        ParticleSystem ps;
        if (list.Count < cap) { ps = BuildBurst(k, burstHost); list.Add(ps); }
        else { ps = list[next[k]]; next[k] = (next[k] + 1) % cap; }
        ps.transform.position = pos;
        var cam = Camera.main; if (cam) ps.transform.rotation = Quaternion.LookRotation(cam.transform.position - pos, cam.transform.up);
        ps.Clear(true); ps.Play(true);
    }

    static ParticleSystem BuildBurst(TCPickupKind k, Transform host)
    {
        Palette(k, out var c1, out var c2);
        bool coin = k == TCPickupKind.Coin, big = k == TCPickupKind.BoostStart;
        float S = coin ? 0.45f : big ? 1.5f : 1f;
        var root = TCFxLib.PS(host, "Burst_" + k, "Glow", Vector3.zero, 2);
        // flash
        { TCFxLib.OneShot(root, 0.22f); var m = root.main; m.startSize = 1.5f * S; m.startColor = new Color(c2.r, c2.g, c2.b, 1); TCFxLib.Burst(root, 1); TCFxLib.SizeLife(root, 0, 0.4f, 0.3f, 1, 1, 1.15f); TCFxLib.ColorLife(root, TCFxLib.Fade(Color.white, c1, 0.02f, 0.3f)); }
        var ring = TCFxLib.PS(root.transform, "Shock", "Ring", Vector3.zero, 2);
        { TCFxLib.OneShot(ring, 0.42f); var m = ring.main; m.startSize = 2.2f * S; m.startColor = c1; TCFxLib.Burst(ring, coin ? 1 : 2); var em = ring.emission; if (!coin) em.SetBursts(new[] { new ParticleSystem.Burst(0, 1), new ParticleSystem.Burst(0.08f, 1) });
          TCFxLib.SizeLife(ring, 0, 0.15f, 1, 1); TCFxLib.ColorLife(ring, TCFxLib.Fade(Color.white, c1, 0.02f, 0.35f)); }
        var stars = TCFxLib.PS(root.transform, "Stars", "Star", Vector3.zero, 24);
        { TCFxLib.OneShot(stars, 0.35f, 0.6f); var m = stars.main; m.startSpeed = new ParticleSystem.MinMaxCurve(1.6f * S, 3.2f * S); m.startSize = new ParticleSystem.MinMaxCurve(0.1f * S, 0.22f * S); m.startColor = new ParticleSystem.MinMaxGradient(Color.white, c2);
          TCFxLib.Burst(stars, coin ? 5 : 16); TCFxLib.Sphere(stars, 0.1f); var lim = stars.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.3f; lim.dampen = 0.18f;
          TCFxLib.SizeLife(stars, 0, 1, 1, 0); TCFxLib.SpinRandom(stars, 200); }
        if (coin) return root;

        string theme = k == TCPickupKind.Health ? "Heart" : k == TCPickupKind.SlowMo ? "Snow" : k == TCPickupKind.Magnet ? "Coin" : "Flame";
        var th = TCFxLib.PS(root.transform, "Theme", theme, Vector3.zero, 24);
        { TCFxLib.OneShot(th, 0.55f, 0.85f); var m = th.main; m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * S, 2.6f * S); m.startSize = new ParticleSystem.MinMaxCurve(0.16f * S, 0.3f * S);
          m.startColor = k == TCPickupKind.Health ? new ParticleSystem.MinMaxGradient(c2, TCFxLib.Hex("#FF4F7B")) : k == TCPickupKind.SlowMo ? new ParticleSystem.MinMaxGradient(Color.white, c1) : new ParticleSystem.MinMaxGradient(Color.white);
          TCFxLib.Burst(th, big ? 22 : 12); TCFxLib.Sphere(th, 0.15f); var lim = th.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.4f; lim.dampen = 0.12f;
          if (k == TCPickupKind.Health || k == TCPickupKind.Chilli || big) { m.gravityModifier = -0.25f; }
          TCFxLib.SizeLife(th, 0, 0.4f, 0.15f, 1.1f, 1, 0); if (k != TCPickupKind.Health) TCFxLib.SpinRandom(th, 180);
          TCFxLib.ColorLife(th, TCFxLib.Fade(Color.white, k == TCPickupKind.Chilli || big ? c1 : Color.white, 0.03f, 0.55f)); }
        if (k == TCPickupKind.Magnet || k == TCPickupKind.Chilli || big)
        {
            var extra = TCFxLib.PS(root.transform, "Extra", k == TCPickupKind.Magnet ? "Bolt" : "Spark", Vector3.zero, 24);
            TCFxLib.OneShot(extra, 0.25f, 0.5f); var m = extra.main; m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f * S, 4.5f * S); m.startSize = new ParticleSystem.MinMaxCurve(0.06f * S, 0.12f * S); m.startColor = new ParticleSystem.MinMaxGradient(c2, c1);
            if (k == TCPickupKind.Magnet) { m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1f); m.startColor = new ParticleSystem.MinMaxGradient(TCFxLib.Hex("#FFF4B0"), TCFxLib.Hex("#7FD8FF")); m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); }
            else { var r = extra.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1.5f; }
            TCFxLib.Burst(extra, k == TCPickupKind.Magnet ? 6 : 18); TCFxLib.Sphere(extra, 0.1f);
            TCFxLib.ColorLife(extra, TCFxLib.Fade(Color.white, c1, 0.02f, 0.4f));
        }
        return root;
    }

    public static TCPickupKind? KindFromTag(string tag)
    {
        switch (tag) { case "boost": return TCPickupKind.Chilli; case "health": return TCPickupKind.Health; case "cooldown": return TCPickupKind.SlowMo; case "magnet": return TCPickupKind.Magnet; case "points": return TCPickupKind.Coin; }
        return null;
    }
}
