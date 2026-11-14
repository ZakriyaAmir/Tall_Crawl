using UnityEngine;

/// Spawns and manages a magical bioluminescent swarm of firefly particles
/// hovering and meandering organically around the spider during CloudyCrown_Midnight.
public class TCFireflySwarm : MonoBehaviour
{
    public static TCFireflySwarm Instance { get; private set; }

    Transform spiderTransform;
    TCCuteSpider cuteSpider;
    ParticleSystem fireflyPS;
    ParticleSystem.EmissionModule emission;
    ParticleSystem.MainModule mainModule;

    float targetIntensity;
    float currentIntensity;
    const float BaseEmissionRate = 15f;

    public static TCFireflySwarm Ensure(Transform parent = null)
    {
        if (Instance != null) return Instance;
        var existing = FindObjectOfType<TCFireflySwarm>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var go = new GameObject("TCFireflySwarm");
        if (parent != null) go.transform.SetParent(parent, false);
        Instance = go.AddComponent<TCFireflySwarm>();
        return Instance;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        BuildParticleSystem();
    }

    void Start()
    {
        ResolveSpider();
    }

    void BuildParticleSystem()
    {
        if (fireflyPS != null) return;

        fireflyPS = gameObject.AddComponent<ParticleSystem>();
        fireflyPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        mainModule = fireflyPS.main;
        mainModule.loop = true;
        mainModule.playOnAwake = false;
        mainModule.maxParticles = 60;
        mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
        mainModule.scalingMode = ParticleSystemScalingMode.Hierarchy;
        mainModule.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        mainModule.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.2f);
        mainModule.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        mainModule.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.16f);

        // Bioluminescent warm yellow-green to golden firefly colors
        var col1 = new Color(0.82f, 1.0f, 0.25f, 0.95f);
        var col2 = new Color(1.0f, 0.94f, 0.38f, 0.95f);
        mainModule.startColor = new ParticleSystem.MinMaxGradient(col1, col2);

        emission = fireflyPS.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        // Spherical cloud volume around the spider
        var shape = fireflyPS.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.15f;
        shape.radiusThickness = 0.85f;

        // Organic wandering motion (noise module)
        var noise = fireflyPS.noise;
        noise.enabled = true;
        noise.strength = 0.38f;
        noise.frequency = 0.65f;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        // Rhythmic firefly blinking / pulsing over lifetime
        var colLife = fireflyPS.colorOverLifetime;
        colLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.98f, 0.8f), 0.5f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.15f), // Flash 1 peak
                new GradientAlphaKey(0.18f, 0.35f), // Dim dip
                new GradientAlphaKey(1.0f, 0.55f), // Flash 2 peak
                new GradientAlphaKey(0.20f, 0.72f), // Dim dip
                new GradientAlphaKey(0.85f, 0.88f), // Flash 3 peak
                new GradientAlphaKey(0.0f, 1.0f)   // Gentle fade
            }
        );
        colLife.color = grad;

        // Luminous size breathing curve
        var sizeLife = fireflyPS.sizeOverLifetime;
        sizeLife.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.2f);
        curve.AddKey(0.15f, 1.0f);
        curve.AddKey(0.35f, 0.45f);
        curve.AddKey(0.55f, 1.0f);
        curve.AddKey(0.72f, 0.5f);
        curve.AddKey(0.88f, 0.9f);
        curve.AddKey(1f, 0f);
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // Renderer setup with soft glow texture
        var rend = fireflyPS.GetComponent<ParticleSystemRenderer>();
        var mat = TCFxLib.Mat("GlowSoft") ?? TCFxLib.Mat("Spark");
        if (mat) rend.sharedMaterial = mat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.alignment = ParticleSystemRenderSpace.View;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    Transform ResolveSpider()
    {
        if (spiderTransform != null) return spiderTransform;
        var sp = FindObjectOfType<playerInteract>();
        if (sp != null)
        {
            spiderTransform = sp.transform;
            cuteSpider = sp.GetComponentInChildren<TCCuteSpider>();
        }
        else
        {
            var pl = FindObjectOfType<player>();
            if (pl != null)
            {
                spiderTransform = pl.transform;
                cuteSpider = pl.GetComponentInChildren<TCCuteSpider>();
            }
        }
        return spiderTransform;
    }

    public Vector3 GetSpiderCenter()
    {
        var sp = ResolveSpider();
        if (sp == null) return transform.position;

        if (cuteSpider == null) cuteSpider = sp.GetComponentInChildren<TCCuteSpider>();
        if (cuteSpider != null)
        {
            return cuteSpider.VisualCenter();
        }

        var cuteRoot = sp.Find("__cute");
        if (cuteRoot != null)
        {
            var body = cuteRoot.Find("body");
            if (body != null) return body.position;
            return cuteRoot.position;
        }

        return sp.position + sp.up * 0.1f;
    }

    public void SetIntensity(float intensity)
    {
        targetIntensity = Mathf.Clamp01(intensity);
    }

    void LateUpdate()
    {
        // Follow spider's 3D position (including jumps)
        transform.position = GetSpiderCenter();

        // Smoothly adjust intensity
        currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, Time.deltaTime * 1.5f);

        if (fireflyPS == null) return;

        if (currentIntensity > 0.01f)
        {
            if (!fireflyPS.isPlaying) fireflyPS.Play();
            emission.rateOverTime = BaseEmissionRate * currentIntensity;
        }
        else
        {
            emission.rateOverTime = 0f;
            if (fireflyPS.isPlaying && fireflyPS.particleCount == 0)
            {
                fireflyPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
