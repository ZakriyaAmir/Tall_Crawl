using UnityEngine;

/// <summary>
/// Chilli Mode Fire Effects:
/// - Smooth aerodynamic convex fire shield positioned ahead of the spider.
/// - The convex shield is formed by curved energy dome surfaces, a protective glowing rim,
///   pulsing forward ramming crest, and sweeping flame arcs.
/// - Clean high-speed ember/spark particle trail along the rim and behind the shield.
/// </summary>
public class TCFireEffects : MonoBehaviour
{
    // polish2: transparent fresnel fire shield (hex energy + scrolling flame noise), full-body shield bubble,
    // flames streaming back from the shield rim, a world-space fire + smoke + ember trail, heat glow,
    // pop-in / fade-out and an "about to end" flicker.
    playerInteract pi;
    GameObject fireRoot;
    ParticleSystem rimFireParticles, sparkParticles, trailFlames, trailSmoke, trailEmbers, heatGlow;
    Transform shieldRoot, outerDomeTransform, innerDomeTransform, bubbleTransform;
    Material outerDomeMat, innerDomeMat, bubbleMat;
    bool wasActive; float spinAngle, fade, popT;
    static Mesh convexShieldMesh;

    void Awake()
    {
        pi = GetComponentInParent<playerInteract>();
        BuildFireEffects();
    }

    static Material ShieldMat(string name, Color rim, Color core, float fill, float hex, float opacity, float power)
    {
        var m = new Material(TCFxLib.Shield) { name = name };
        m.SetColor("_Color", rim); m.SetColor("_CoreColor", core); m.SetFloat("_Fill", fill); m.SetFloat("_HexStrength", hex);
        m.SetFloat("_Opacity", opacity); m.SetFloat("_RimPower", power); return m;
    }

    void BuildFireEffects()
    {
        fireRoot = new GameObject("Chilli_Fire_Effects");
        fireRoot.transform.SetParent(transform, false);

        shieldRoot = new GameObject("ConvexFireShield").transform;
        shieldRoot.SetParent(fireRoot.transform, false);
        shieldRoot.localPosition = new Vector3(0, 0.16f, 0.46f);
        if (!convexShieldMesh) convexShieldMesh = CreateConvexShieldMesh(14, 36, 0.62f, 0.36f);

        outerDomeMat = ShieldMat("FireShield_Outer", TCFxLib.Hex("#FF5A12"), TCFxLib.Hex("#FFD84A"), 0.22f, 0.8f, 0.85f, 1.6f);
        innerDomeMat = ShieldMat("FireShield_Inner", TCFxLib.Hex("#FFB02E"), TCFxLib.Hex("#FFF6C8"), 0.35f, 0.3f, 0.6f, 1.2f);
        innerDomeMat.SetVector("_Scroll", new Vector4(0, -0.9f, -0.2f, 2.2f));
        bubbleMat = ShieldMat("FireShield_Bubble", TCFxLib.Hex("#FF7A1E"), TCFxLib.Hex("#FFE08A"), 0.0f, 0.55f, 0.75f, 2.6f);
        bubbleMat.SetTextureScale("_HexTex", new Vector2(5, 3));

        outerDomeTransform = MeshObj("OuterConvexDome", shieldRoot, convexShieldMesh, outerDomeMat, Vector3.zero, Vector3.one).transform;
        innerDomeTransform = MeshObj("InnerCoreDome", shieldRoot, convexShieldMesh, innerDomeMat, new Vector3(0, 0, 0.02f), Vector3.one * 0.74f).transform;

        // full-body bubble (shield read): sphere mesh around the spider
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var sm = sphere.GetComponent<MeshFilter>().sharedMesh; Destroy(sphere);
        bubbleTransform = MeshObj("ShieldBubble", fireRoot.transform, sm, bubbleMat, new Vector3(0, 0.14f, 0.02f), new Vector3(1.05f, 0.75f, 1.35f)).transform;

        // flames streaming back from the shield rim (comet look), local to the spider
        rimFireParticles = TCFxLib.PS(shieldRoot, "RimFireParticles", "Flame", new Vector3(0, 0, 0.02f), 90);
        {
            var m = rimFireParticles.main; m.playOnAwake = false; m.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
            m.startSpeed = 0; m.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.42f); m.startColor = Color.white;
            m.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var em = rimFireParticles.emission; em.rateOverTime = 110;
            var sh = rimFireParticles.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.56f; sh.radiusThickness = 0.15f;
            var v = rimFireParticles.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(0, 0); v.y = new ParticleSystem.MinMaxCurve(0, 0); v.z = new ParticleSystem.MinMaxCurve(-2.6f, -1.6f);
            TCFxLib.SizeLife(rimFireParticles, 0, 0.6f, 0.25f, 1f, 1, 0.15f);
            TCFxLib.ColorLife(rimFireParticles, TCFxLib.Fade3(Color.white, TCFxLib.Hex("#FFB43A"), TCFxLib.Hex("#FF3A12")));
        }
        // forward sparks off the ramming crest
        sparkParticles = TCFxLib.PS(shieldRoot, "Sparks", "Spark", new Vector3(0, 0, 0.3f), 40);
        {
            var m = sparkParticles.main; m.playOnAwake = false; m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            m.startColor = new ParticleSystem.MinMaxGradient(Color.white, TCFxLib.Hex("#FFD56A"));
            var em = sparkParticles.emission; em.rateOverTime = 40;
            var sh = sparkParticles.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 55f; sh.radius = 0.3f;
            var r = sparkParticles.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1.2f;
            TCFxLib.ColorLife(sparkParticles, TCFxLib.Fade(Color.white, TCFxLib.Hex("#FF8A2B"), 0.02f, 0.5f));
        }
        // heat glow behind everything
        heatGlow = TCFxLib.PS(fireRoot.transform, "HeatGlow", "Glow", new Vector3(0, 0.15f, 0.2f), 6);
        {
            var m = heatGlow.main; m.playOnAwake = false; m.startLifetime = 0.45f; m.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.1f);
            m.startColor = new Color(1f, 0.55f, 0.15f, 0.55f);
            var em = heatGlow.emission; em.rateOverTime = 9; TCFxLib.ColorLife(heatGlow, TCFxLib.Fade(Color.white, TCFxLib.Hex("#FF7A2A"), 0.25f, 0.6f));
        }
        // world-space trail left on the trunk: flames, smoke, embers
        trailFlames = TCFxLib.PS(fireRoot.transform, "TrailFlames", "Flame", new Vector3(0, 0.12f, -0.15f), 120);
        {
            var m = trailFlames.main; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f); m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            m.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f); m.startColor = Color.white;
            var em = trailFlames.emission; em.rateOverTime = 55; em.rateOverDistance = 10;
            TCFxLib.Sphere(trailFlames, 0.18f, 1);
            TCFxLib.SizeLife(trailFlames, 0, 1f, 0.5f, 0.75f, 1, 0.05f);
            TCFxLib.ColorLife(trailFlames, TCFxLib.Fade3(Color.white, TCFxLib.Hex("#FFA030"), TCFxLib.Hex("#E8280E")));
            var r = trailFlames.GetComponent<ParticleSystemRenderer>(); r.alignment = ParticleSystemRenderSpace.View; r.sortingFudge = 2;
        }
        trailSmoke = TCFxLib.PS(fireRoot.transform, "TrailSmoke", "Smoke", new Vector3(0, 0.12f, -0.3f), 40);
        {
            var m = trailSmoke.main; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f); m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            m.startColor = new Color(0.45f, 0.32f, 0.3f, 0.45f);
            var em = trailSmoke.emission; em.rateOverTime = 16;
            TCFxLib.Sphere(trailSmoke, 0.2f, 1); TCFxLib.SpinRandom(trailSmoke, 60);
            TCFxLib.SizeLife(trailSmoke, 0, 0.5f, 1, 1.6f);
            TCFxLib.ColorLife(trailSmoke, TCFxLib.Fade(TCFxLib.Hex("#FFB070"), TCFxLib.Hex("#6B5A58"), 0.2f, 0.4f, 0.8f));
            var r = trailSmoke.GetComponent<ParticleSystemRenderer>(); r.sortingFudge = 6;
        }
        trailEmbers = TCFxLib.PS(fireRoot.transform, "TrailEmbers", "Spark", new Vector3(0, 0.12f, -0.1f), 60);
        {
            var m = trailEmbers.main; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f); m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f); m.startColor = new ParticleSystem.MinMaxGradient(TCFxLib.Hex("#FFF2A0"), TCFxLib.Hex("#FF7A1E"));
            var em = trailEmbers.emission; em.rateOverTime = 45;
            TCFxLib.Sphere(trailEmbers, 0.3f, 1);
            var nz = trailEmbers.noise; nz.enabled = true; nz.strength = 0.8f; nz.frequency = 2f; nz.quality = ParticleSystemNoiseQuality.Low;
            var r = trailEmbers.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.08f; r.lengthScale = 1.5f;
            TCFxLib.ColorLife(trailEmbers, TCFxLib.Fade(Color.white, TCFxLib.Hex("#FF4A12"), 0.02f, 0.6f));
        }
        fireRoot.SetActive(false);
    }

    static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    void SetEmit(bool on)
    {
        foreach (var ps in new[] { rimFireParticles, sparkParticles, trailFlames, trailSmoke, trailEmbers, heatGlow })
        {
            if (!ps) continue;
            if (on) { ps.Clear(true); ps.Play(true); } else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void Update()
    {
        if (!pi) pi = GetComponentInParent<playerInteract>();
        bool active = pi != null && pi.boostDuration > 0 && !pi.IsDead;
        float dt = Time.deltaTime;

        if (active != wasActive)
        {
            wasActive = active;
            if (!fireRoot) return;
            if (active) { fireRoot.SetActive(true); SetEmit(true); spinAngle = 0f; popT = 0f; }
            else SetEmit(false);
        }

        fade = Mathf.MoveTowards(fade, active ? 1f : 0f, dt * (active ? 6f : 3.5f));
        if (!active && fade <= 0f) { if (fireRoot && fireRoot.activeSelf && (!trailFlames || trailFlames.particleCount == 0)) fireRoot.SetActive(false); }
        if (!fireRoot || !fireRoot.activeSelf) return;

        // about-to-end warning flicker
        float warn = (active && pi.boostDuration < 0.9f) ? (0.55f + 0.45f * Mathf.Sign(Mathf.Sin(Time.time * 28f))) : 1f;
        float a = fade * warn;
        outerDomeMat.SetFloat("_Alpha", a); innerDomeMat.SetFloat("_Alpha", a); bubbleMat.SetFloat("_Alpha", a * 0.9f);

        popT += dt;
        float pop = popT < 0.35f ? Mathf.LerpUnclamped(0.2f, 1f, TCUI.EaseOutBack(Mathf.Clamp01(popT / 0.35f))) : 1f;
        spinAngle += dt * 220f;
        float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 20f);
        if (shieldRoot)
        {
            shieldRoot.localPosition = new Vector3(0, 0.16f, 0.48f + 0.04f * Mathf.Sin(Time.time * 26f));
            shieldRoot.localScale = new Vector3(pulse, pulse, 1f + 0.1f * Mathf.Sin(Time.time * 18f)) * pop;
        }
        if (bubbleTransform) bubbleTransform.localScale = new Vector3(1.05f, 0.75f, 1.35f) * (pop * (1f + 0.03f * Mathf.Sin(Time.time * 9f)));
        if (outerDomeTransform) outerDomeTransform.localRotation = Quaternion.Euler(0, 0, spinAngle);
        if (innerDomeTransform) innerDomeTransform.localRotation = Quaternion.Euler(0, 0, -spinAngle * 1.4f);
    }

    /// <summary>
    /// Generates a smooth, aerodynamic convex dome mesh that bulges forward toward +Z.
    /// Perfectly rounded front profile with smooth normals for pristine cel-shading.
    /// </summary>
    static Mesh CreateConvexShieldMesh(int rings, int segments, float radius, float depth)
    {
        var mesh = new Mesh { name = "ConvexFireShieldMesh" };
        int vertCount = (rings + 1) * (segments + 1);
        var vertices = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        int v = 0;
        for (int r = 0; r <= rings; r++)
        {
            float phi = (Mathf.PI * 0.5f) * ((float)r / rings); // 0 at forward apex, PI/2 at base rim
            // Convex parabolic curve forward along +Z
            float z = Mathf.Cos(phi) * depth;
            float curRadius = Mathf.Sin(phi) * radius;

            for (int s = 0; s <= segments; s++)
            {
                float theta = s * (Mathf.PI * 2f / segments);
                float x = Mathf.Cos(theta) * curRadius;
                float y = Mathf.Sin(theta) * curRadius;

                vertices[v] = new Vector3(x, y, z);
                // Outward normal calculated from spherical / ellipsoidal surface
                normals[v] = new Vector3(x / radius, y / radius, z / depth).normalized;
                // Circular polar UVs
                uvs[v] = new Vector2(0.5f + (x / radius) * 0.5f, 0.5f + (y / radius) * 0.5f);
                v++;
            }
        }

        // Triangles
        int triCount = rings * segments * 6;
        var triangles = new int[triCount];
        int t = 0;
        int rowStride = segments + 1;

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int current = r * rowStride + s;
                int next = current + rowStride;

                triangles[t++] = current;
                triangles[t++] = current + 1;
                triangles[t++] = next;

                triangles[t++] = current + 1;
                triangles[t++] = next + 1;
                triangles[t++] = next;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}
