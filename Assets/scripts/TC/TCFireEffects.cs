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
    playerInteract pi;
    GameObject fireRoot;
    ParticleSystem rimFireParticles;
    ParticleSystem sparkParticles;

    Transform shieldRoot;
    Transform outerDomeTransform;
    Transform innerDomeTransform;
    Transform shieldRimTransform;

    Material outerDomeMat;
    Material innerDomeMat;
    Material rimMat;

    bool wasActive;
    float spinAngle;

    static Mesh convexShieldMesh;

    void Awake()
    {
        pi = GetComponentInParent<playerInteract>();
        BuildFireEffects();
    }

    static void ConfigureParticleRenderer(ParticleSystemRenderer renderer, Material material, float minSize, float maxSize)
    {
        if (!renderer) return;
        if (material) renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.minParticleSize = minSize;
        renderer.maxParticleSize = maxSize;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.enableGPUInstancing = false;
    }

    void BuildFireEffects()
    {
        fireRoot = new GameObject("Chilli_Fire_Effects");
        fireRoot.transform.SetParent(transform, false);
        fireRoot.transform.localPosition = Vector3.zero;

        // 1. Shaders & Materials
        // Prefer Legacy Shaders/Particles/Additive (in AlwaysIncludedShaders and compiled on Android)
        // and never fall back to opaque Standard, which renders particles as solid lit quads on mobile.
        var partShader = Shader.Find("Legacy Shaders/Particles/Additive")
            ?? Shader.Find("Mobile/Particles/Additive")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Sprites/Default");
        var fireTex = Resources.Load<Texture2D>("TCUI/fire_particle");
        var energyTex = Resources.Load<Texture2D>("TCUI/fire_shield_energy");

        Material partMat = partShader ? new Material(partShader) : null;
        if (partMat)
        {
            if (fireTex) partMat.mainTexture = fireTex;
            partMat.color = new Color(1f, 0.7f, 0.25f, 1f);
            // Legacy Particles/Additive multiplies by 2x _TintColor; halve it so brightness and alpha match Editor.
            if (partMat.HasProperty("_TintColor")) partMat.SetColor("_TintColor", new Color(0.55f, 0.48f, 0.38f, 0.5f));
        }

        // 2. High-Quality Convex Fire Shield ahead of spider
        shieldRoot = new GameObject("ConvexFireShield").transform;
        shieldRoot.SetParent(fireRoot.transform, false);
        // Placed ahead of the spider's front ocular snout / legs
        shieldRoot.localPosition = new Vector3(0, 0.16f, 0.46f);
        shieldRoot.localRotation = Quaternion.identity;

        if (!convexShieldMesh) convexShieldMesh = CreateConvexShieldMesh(12, 28, 0.58f, 0.28f);

        var celShader = Shader.Find("TallCrawl/SpiderPreBaked") ?? Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Sprites/Default");

        // Outer blazing convex dome material
        outerDomeMat = new Material(celShader);
        outerDomeMat.color = new Color(1.0f, 0.48f, 0.12f, 1.0f);
        if (energyTex) outerDomeMat.mainTexture = energyTex;
        if (outerDomeMat.HasProperty("_EmissionColor"))
        {
            outerDomeMat.SetColor("_EmissionColor", new Color(1.0f, 0.42f, 0.08f) * 2.4f);
            outerDomeMat.EnableKeyword("_EMISSION");
        }
        if (outerDomeMat.HasProperty("_OutlineColor")) outerDomeMat.SetColor("_OutlineColor", new Color(1.0f, 0.95f, 0.4f, 1f));
        if (outerDomeMat.HasProperty("_OutlineWidth")) outerDomeMat.SetFloat("_OutlineWidth", 1.8f);

        // Inner hot core dome (brighter, smaller, creates rich layer depth)
        innerDomeMat = new Material(celShader);
        innerDomeMat.color = new Color(1.0f, 0.88f, 0.35f, 1.0f);
        if (innerDomeMat.HasProperty("_EmissionColor"))
        {
            innerDomeMat.SetColor("_EmissionColor", new Color(1.0f, 0.92f, 0.55f) * 3.0f);
            innerDomeMat.EnableKeyword("_EMISSION");
        }

        // Gold-amber protective outer rim
        rimMat = new Material(celShader);
        rimMat.color = new Color(1.0f, 0.75f, 0.2f, 1.0f);
        if (rimMat.HasProperty("_EmissionColor"))
        {
            rimMat.SetColor("_EmissionColor", new Color(1.0f, 0.65f, 0.15f) * 2.0f);
            rimMat.EnableKeyword("_EMISSION");
        }
        if (rimMat.HasProperty("_OutlineColor")) rimMat.SetColor("_OutlineColor", new Color(0.35f, 0.15f, 0.02f, 1f));
        if (rimMat.HasProperty("_OutlineWidth")) rimMat.SetFloat("_OutlineWidth", 1.5f);

        // Outer Dome
        var outerObj = new GameObject("OuterConvexDome");
        outerObj.transform.SetParent(shieldRoot, false);
        var mfOuter = outerObj.AddComponent<MeshFilter>();
        mfOuter.sharedMesh = convexShieldMesh;
        var mrOuter = outerObj.AddComponent<MeshRenderer>();
        mrOuter.sharedMaterial = outerDomeMat;
        mrOuter.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrOuter.receiveShadows = false;
        outerDomeTransform = outerObj.transform;

        // Inner Core Dome
        var innerObj = new GameObject("InnerCoreDome");
        innerObj.transform.SetParent(shieldRoot, false);
        innerObj.transform.localPosition = new Vector3(0, 0, 0.02f);
        innerObj.transform.localScale = Vector3.one * 0.78f;
        var mfInner = innerObj.AddComponent<MeshFilter>();
        mfInner.sharedMesh = convexShieldMesh;
        var mrInner = innerObj.AddComponent<MeshRenderer>();
        mrInner.sharedMaterial = innerDomeMat;
        mrInner.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrInner.receiveShadows = false;
        innerDomeTransform = innerObj.transform;

        // Solid Torus / Cylinder Reinforcement Rim at base of convex shield
        var rimObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var colRim = rimObj.GetComponent<Collider>(); if (colRim) DestroyImmediate(colRim);
        rimObj.transform.SetParent(shieldRoot, false);
        rimObj.transform.localPosition = new Vector3(0, 0, 0.02f);
        rimObj.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        rimObj.transform.localScale = new Vector3(1.18f, 0.015f, 1.18f);
        var mrRim = rimObj.GetComponent<MeshRenderer>();
        if (mrRim)
        {
            mrRim.sharedMaterial = rimMat;
            mrRim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrRim.receiveShadows = false;
        }
        shieldRimTransform = rimObj.transform;

        // 3. Dynamic Flame Rim Particles (Sweeps around the perimeter of the convex shield)
        var rimPsObj = new GameObject("RimFireParticles");
        rimPsObj.transform.SetParent(shieldRoot, false);
        rimPsObj.transform.localPosition = new Vector3(0, 0, 0.04f);

        rimFireParticles = rimPsObj.AddComponent<ParticleSystem>();
        var rmr = rimPsObj.GetComponent<ParticleSystemRenderer>();
        ConfigureParticleRenderer(rmr, partMat, 0.02f, 1.0f);

        var rmain = rimFireParticles.main;
        rmain.loop = true;
        rmain.playOnAwake = false;
        rmain.scalingMode = ParticleSystemScalingMode.Hierarchy;
        rmain.startLifetime = 0.35f;
        rmain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        rmain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.48f);
        rmain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.4f, 0.95f), new Color(1f, 0.35f, 0.05f, 0.95f));
        rmain.simulationSpace = ParticleSystemSimulationSpace.Local;
        rmain.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var remission = rimFireParticles.emission;
        remission.rateOverTime = 60f;

        var rshape = rimFireParticles.shape;
        rshape.shapeType = ParticleSystemShapeType.Circle;
        rshape.radius = 0.58f;
        rshape.arc = 360f;
        rshape.rotation = new Vector3(0, 0, 0);

        var rcol = rimFireParticles.colorOverLifetime;
        rcol.enabled = true;
        var rgrad = new Gradient();
        rgrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.5f), new GradientColorKey(new Color(0.6f, 0.08f, 0.0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        rcol.color = rgrad;

        var rsize = rimFireParticles.sizeOverLifetime;
        rsize.enabled = true;
        var sc = new AnimationCurve();
        sc.AddKey(0f, 0.4f);
        sc.AddKey(0.4f, 1.0f);
        sc.AddKey(1f, 0.1f);
        rsize.size = new ParticleSystem.MinMaxCurve(1f, sc);

        // 4. Directional Spark Trail Particles bursting forward from shield edge
        var sparkObj = new GameObject("Sparks");
        sparkObj.transform.SetParent(shieldRoot, false);
        sparkObj.transform.localPosition = new Vector3(0, 0, 0.15f);

        sparkParticles = sparkObj.AddComponent<ParticleSystem>();
        var smr = sparkObj.GetComponent<ParticleSystemRenderer>();
        ConfigureParticleRenderer(smr, partMat, 0.01f, 0.8f);

        var smain = sparkParticles.main;
        smain.loop = true;
        smain.playOnAwake = false;
        smain.scalingMode = ParticleSystemScalingMode.Hierarchy;
        smain.startLifetime = 0.28f;
        smain.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.8f);
        smain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        smain.startColor = new Color(1f, 0.98f, 0.8f, 1f);
        smain.simulationSpace = ParticleSystemSimulationSpace.Local;
        smain.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var semission = sparkParticles.emission;
        semission.rateOverTime = 36f;

        var sshape = sparkParticles.shape;
        sshape.shapeType = ParticleSystemShapeType.Cone;
        sshape.angle = 25f;
        sshape.radius = 0.35f;

        fireRoot.SetActive(false);
    }

    void Update()
    {
        if (!pi) pi = GetComponentInParent<playerInteract>();
        bool active = pi != null && pi.boostDuration > 0;

        if (active != wasActive)
        {
            wasActive = active;
            if (!fireRoot) return;

            if (active)
            {
                fireRoot.SetActive(true);
                if (rimFireParticles) { rimFireParticles.Clear(true); rimFireParticles.Play(true); }
                if (sparkParticles) { sparkParticles.Clear(true); sparkParticles.Play(true); }
                spinAngle = 0f;
            }
            else
            {
                if (rimFireParticles) rimFireParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (sparkParticles) sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                fireRoot.SetActive(false);
            }
        }

        if (active)
        {
            float dt = Time.deltaTime;
            spinAngle += dt * 260f; // clean rhythmic rotation of energy vortex

            // Smooth forward ramming thrust & expansion pulse
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 20f);
            float forwardThrust = 0.48f + 0.05f * Mathf.Sin(Time.time * 26f);

            if (shieldRoot)
            {
                shieldRoot.localPosition = new Vector3(0, 0.16f, forwardThrust);
                shieldRoot.localScale = new Vector3(pulse, pulse, 1f + 0.12f * Mathf.Sin(Time.time * 18f));
            }

            // Outer dome rotation with texture swirl
            if (outerDomeTransform)
            {
                outerDomeTransform.localRotation = Quaternion.Euler(0, 0, spinAngle);
            }

            // Inner core counter-rotation
            if (innerDomeTransform)
            {
                innerDomeTransform.localRotation = Quaternion.Euler(0, 0, -spinAngle * 1.4f);
            }
        }
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
