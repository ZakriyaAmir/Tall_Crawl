using System.Collections;
using UnityEngine;

/// <summary>
/// Coin Magnet Controller & 3D Pickup Generator.
/// When active, draws nearby coins along the trunk directly into the spider,
/// while emitting a subtle, elegant, non-distracting magnetic ripple effect around the spider.
/// Duration scales with Store Upgrade ID 6: 8.0s base up to 20.0s at Max Level 5 (+2.4s per level).
/// </summary>
public class TCCoinMagnet : MonoBehaviour
{
    public static TCCoinMagnet Instance { get; private set; }

    public const float BaseDuration = 8.0f;
    public const float MaxDuration = 20.0f;
    public const float DurationPerUpgrade = 2.4f;

    /// <summary>
    /// Effective duration of Coin Magnet, scaling with Store Upgrade ID 6.
    /// Base: 8.0s, +2.4s per level (Level 5 = 20.0s).
    /// </summary>
    public static float Duration => BaseDuration + DurationPerUpgrade * Mathf.Clamp(TCSave.Upgrade(6), 0, 5);

    public bool IsActive => activeTimeRemaining > 0f;
    public float ActiveTimeRemaining => Mathf.Max(0f, activeTimeRemaining);

    /// <summary>
    /// Attraction height limit in game metres (attracts coins within 40m height distance).
    /// </summary>
    public float AttractionRadius => 40.0f;
    public float AttractionSpeed => 32.0f;

    // Subtle Spider Shockwave parameters:
    // Stays strictly around the spider (peak radius 0.48m, 60% transparent = 40% peak opacity, warm yellowish tint)
    const int SubtleWaveCount = 2;
    const float WaveLoopPeriod = 1.35f;

    struct ShockwaveWave
    {
        public Transform root;
        public MeshRenderer renderer;
        public Material material;
    }

    float activeTimeRemaining = 0f;
    float emissionTimer = 0f;
    Transform spiderTransform;
    GameObject magnetAuraObj;

    ShockwaveWave[] subtleShockwaves;
    Transform carapaceShimmer;
    Material carapaceShimmerMat;
    Transform[] microSparks;
    static Mesh cachedSubtleWaveMesh;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ResolveSpider();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (magnetAuraObj != null) Destroy(magnetAuraObj);
    }

    Transform ResolveSpider()
    {
        if (spiderTransform != null) return spiderTransform;
        var sp = FindObjectOfType<playerInteract>();
        if (sp != null) spiderTransform = sp.transform;
        else
        {
            var pl = FindObjectOfType<player>();
            if (pl != null) spiderTransform = pl.transform;
        }
        return spiderTransform;
    }

    public void Activate()
    {
        activeTimeRemaining = Duration;
        emissionTimer = 0f;
        TCSfx.Play("cooldown_activate", 0.9f);
        TCFx.Shake(0.08f);

        if (magnetAuraObj == null)
        {
            CreateMagnetAura();
        }
        if (magnetAuraObj != null)
        {
            magnetAuraObj.SetActive(true);
        }
    }

    void Update()
    {
        if (activeTimeRemaining > 0f)
        {
            activeTimeRemaining -= Time.deltaTime;
            emissionTimer += Time.deltaTime;

            var sp = ResolveSpider();
            if (sp != null)
            {
                if (magnetAuraObj == null) CreateMagnetAura();

                if (magnetAuraObj != null)
                {
                    // Situate the aura directly on the spider carapace plane (does not float high or block camera)
                    magnetAuraObj.transform.position = sp.position + sp.up * 0.04f;
                    magnetAuraObj.transform.rotation = sp.rotation;

                    // 1. Subtle, Non-Distracting Shockwave Ripples (stay strictly around spider, r = 0.16m to 0.48m, 60% transparent, yellowish tint)
                    if (subtleShockwaves != null)
                    {
                        for (int i = 0; i < subtleShockwaves.Length; i++)
                        {
                            var wave = subtleShockwaves[i];
                            if (wave.root == null || wave.material == null) continue;

                            float phaseOffset = (float)i / SubtleWaveCount;
                            float u = Mathf.Repeat((emissionTimer / WaveLoopPeriod) + phaseOffset, 1.0f);

                            // Smooth ease-out expansion from spider carapace out to just past the legs (0.16m to 0.48m)
                            float ease = 1f - Mathf.Pow(1f - u, 1.8f);
                            float radius = Mathf.Lerp(0.16f, 0.48f, ease);
                            wave.root.localScale = new Vector3(radius, 1f, radius);

                            // Further reduced transparency by 50% (peak opacity = 0.20, fading smoothly to 0 at edges)
                            float alpha = (u < 0.20f)
                                ? (u / 0.20f)
                                : Mathf.Pow(1f - (u - 0.20f) / 0.80f, 1.4f);
                            alpha *= 0.20f; // 50% less opacity than before (0.40 -> 0.20)

                            // Warm luminous yellowish magnetic ripple (golden yellow crest shifting into soft warm amber)
                            Color waveCol = Color.Lerp(
                                new Color(1.0f, 0.94f, 0.38f, alpha),
                                new Color(1.0f, 0.82f, 0.20f, alpha * 0.75f),
                                u
                            );

                            SetMatColor(wave.material, waveCol);
                        }
                    }

                    // 2. Soft Carapace Shimmer with yellowish tint (gentle ambient pulse, alpha ~0.04)
                    if (carapaceShimmer != null && carapaceShimmerMat != null)
                    {
                        float shimAlpha = 0.03f + 0.015f * Mathf.Sin(Time.time * 4f);
                        SetMatColor(carapaceShimmerMat, new Color(1.0f, 0.92f, 0.40f, shimAlpha));
                    }

                    // 3. Four Micro-Sparks (2 Red, 2 Blue) orbiting tightly around the spider body
                    if (microSparks != null)
                    {
                        for (int s = 0; s < microSparks.Length; s++)
                        {
                            if (microSparks[s] == null) continue;
                            float ang = (Time.time * 125f + s * 90f) * Mathf.Deg2Rad;
                            float r = 0.21f + 0.012f * Mathf.Sin(Time.time * 3f + s);
                            float y = 0.015f * Mathf.Cos(Time.time * 4f + s);
                            microSparks[s].localPosition = new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);
                        }
                    }
                }

                // Attract nearby coins along trunk towards spider (strictly within 20m distance)
                if (TCTrack.Instance != null)
                {
                    TCTrack.Instance.AttractCoins(sp.position, AttractionRadius, AttractionSpeed * Time.deltaTime);
                }
            }

            if (activeTimeRemaining <= 0f)
            {
                activeTimeRemaining = 0f;
                if (magnetAuraObj != null) magnetAuraObj.SetActive(false);
            }
        }
    }

    void CreateMagnetAura()
    {
        var sp = ResolveSpider();
        if (sp == null) return;

        var pl = sp.GetComponentInParent<player>();
        Transform auraParent = pl ? pl.transform : null;

        magnetAuraObj = new GameObject("Magnet_Active_Aura");
        if (auraParent) magnetAuraObj.transform.SetParent(auraParent, true);
        magnetAuraObj.transform.position = sp.position + sp.up * 0.04f;
        magnetAuraObj.transform.rotation = sp.rotation;

        var waveMesh = GetOrCreateSubtleShockwaveMesh();
        var addShader = FindAdditiveShader();

        // 1. Subtle Shockwave Ripple Rings (2 concentric waves)
        subtleShockwaves = new ShockwaveWave[SubtleWaveCount];
        for (int i = 0; i < SubtleWaveCount; i++)
        {
            var waveObj = new GameObject("Subtle_Shockwave_" + i);
            waveObj.transform.SetParent(magnetAuraObj.transform, false);
            waveObj.transform.localPosition = new Vector3(0, 0.002f * i, 0);

            var mf = waveObj.AddComponent<MeshFilter>();
            mf.sharedMesh = waveMesh;

            var mr = waveObj.AddComponent<MeshRenderer>();
            var mat = new Material(addShader) { name = "SubtleWave_Mat_" + i };
            if (mat.HasProperty("_MainTex")) mat.mainTexture = Texture2D.whiteTexture;
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            subtleShockwaves[i] = new ShockwaveWave
            {
                root = waveObj.transform,
                renderer = mr,
                material = mat
            };
        }

        // 2. Soft Carapace Ambient Glow Disc
        var shimObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var shimCol = shimObj.GetComponent<Collider>();
        if (shimCol) { if (Application.isPlaying) Destroy(shimCol); else DestroyImmediate(shimCol); }
        shimObj.name = "Carapace_Shimmer";
        shimObj.transform.SetParent(magnetAuraObj.transform, false);
        shimObj.transform.localPosition = Vector3.zero;
        shimObj.transform.localScale = new Vector3(0.24f, 0.001f, 0.24f);

        var shimMr = shimObj.GetComponent<MeshRenderer>();
        if (shimMr)
        {
            carapaceShimmerMat = new Material(addShader) { name = "Carapace_Shimmer_Mat" };
            SetMatColor(carapaceShimmerMat, new Color(1.0f, 0.92f, 0.40f, 0.08f));
            shimMr.sharedMaterial = carapaceShimmerMat;
            shimMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shimMr.receiveShadows = false;
        }
        carapaceShimmer = shimObj.transform;

        // 3. Four Micro-Sparks (2 Red North, 2 Blue South) orbiting the spider body
        microSparks = new Transform[4];
        var redSparkMat = CreateAdditiveGlowMaterial(new Color(1.0f, 0.20f, 0.28f, 0.45f));
        var blueSparkMat = CreateAdditiveGlowMaterial(new Color(0.20f, 0.70f, 1.0f, 0.45f));

        for (int s = 0; s < 4; s++)
        {
            var spk = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var spkCol = spk.GetComponent<Collider>();
            if (spkCol) { if (Application.isPlaying) Destroy(spkCol); else DestroyImmediate(spkCol); }
            spk.name = "MagnetSpark_" + s;
            spk.transform.SetParent(magnetAuraObj.transform, false);
            spk.transform.localScale = Vector3.one * 0.022f; // tiny, delicate
            var sr = spk.GetComponent<MeshRenderer>();
            if (sr)
            {
                sr.sharedMaterial = (s % 2 == 0) ? redSparkMat : blueSparkMat;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
            }
            microSparks[s] = spk.transform;
        }
    }

    /// <summary>
    /// Creates a smooth, planar annulus ripple mesh with soft radial alpha falloff and warm yellowish vertex tint.
    /// Inner rim (0.62) = alpha 0, Crest (0.80) = alpha 1, Outer rim (1.00) = alpha 0.
    /// </summary>
    static Mesh GetOrCreateSubtleShockwaveMesh()
    {
        if (cachedSubtleWaveMesh != null) return cachedSubtleWaveMesh;

        int segments = 36;
        var mesh = new Mesh { name = "Subtle_Shockwave_Mesh" };

        float[] radii = { 0.62f, 0.80f, 1.00f };
        Color[] baseColors = {
            new Color(1f, 0.95f, 0.40f, 0.0f),  // inner edge (transparent)
            new Color(1f, 0.95f, 0.40f, 1.0f),  // soft crest with yellowish tint
            new Color(1f, 0.85f, 0.25f, 0.0f)   // outer edge (transparent)
        };

        int ringCount = radii.Length;
        int vertCount = (segments + 1) * ringCount;
        var vertices = new Vector3[vertCount];
        var colors = new Color[vertCount];
        var uvs = new Vector2[vertCount];

        for (int r = 0; r < ringCount; r++)
        {
            float rad = radii[r];
            Color c = baseColors[r];
            for (int s = 0; s <= segments; s++)
            {
                float angle = (s / (float)segments) * Mathf.PI * 2f;
                int idx = r * (segments + 1) + s;
                vertices[idx] = new Vector3(Mathf.Cos(angle) * rad, 0f, Mathf.Sin(angle) * rad);
                colors[idx] = c;
                uvs[idx] = new Vector2(s / (float)segments, r / (float)(ringCount - 1));
            }
        }

        int quadCount = (ringCount - 1) * segments;
        var triangles = new int[quadCount * 12]; // double-sided
        int tIdx = 0;

        for (int r = 0; r < ringCount - 1; r++)
        {
            int rowA = r * (segments + 1);
            int rowB = (r + 1) * (segments + 1);
            for (int s = 0; s < segments; s++)
            {
                int a0 = rowA + s;
                int a1 = rowA + s + 1;
                int b0 = rowB + s;
                int b1 = rowB + s + 1;

                // Front
                triangles[tIdx++] = a0; triangles[tIdx++] = b0; triangles[tIdx++] = a1;
                triangles[tIdx++] = a1; triangles[tIdx++] = b0; triangles[tIdx++] = b1;

                // Back
                triangles[tIdx++] = a0; triangles[tIdx++] = a1; triangles[tIdx++] = b0;
                triangles[tIdx++] = a1; triangles[tIdx++] = b1; triangles[tIdx++] = b0;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        cachedSubtleWaveMesh = mesh;
        return cachedSubtleWaveMesh;
    }

    static void SetMatColor(Material mat, Color c)
    {
        if (!mat) return;
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        mat.color = c;
    }

    static Shader FindAdditiveShader()
    {
        return Shader.Find("Mobile/Particles/Additive")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default");
    }

    // =========================================================================
    // 3D MAGNET POWER-UP PICKUP GENERATION (HIGH-QUALITY 3D HORSESHOE MAGNET)
    // =========================================================================

    static Mesh cachedHorseshoeArchMesh;

    /// <summary>
    /// Procedurally creates a smooth swept tubular arch mesh sweeping 180 degrees from 0 to PI.
    /// Arch radius R = 0.16m, tube cross-section radius r = 0.050m.
    /// </summary>
    static Mesh GetOrCreateHorseshoeArchMesh()
    {
        if (cachedHorseshoeArchMesh != null) return cachedHorseshoeArchMesh;

        int archSegs = 24;
        int tubeSegs = 16;
        float R = 0.16f;
        float r = 0.050f;

        var mesh = new Mesh { name = "Horseshoe_Curved_Arch" };
        int vertCount = (archSegs + 1) * (tubeSegs + 1);
        var verts = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        for (int i = 0; i <= archSegs; i++)
        {
            float theta = (i / (float)archSegs) * Mathf.PI;
            float cx = R * Mathf.Cos(theta);
            float cy = R * Mathf.Sin(theta);

            float nx = Mathf.Cos(theta);
            float ny = Mathf.Sin(theta);

            for (int j = 0; j <= tubeSegs; j++)
            {
                float phi = (j / (float)tubeSegs) * Mathf.PI * 2f;
                float vx = Mathf.Cos(phi) * nx;
                float vy = Mathf.Cos(phi) * ny;
                float vz = Mathf.Sin(phi);

                int idx = i * (tubeSegs + 1) + j;
                verts[idx] = new Vector3(cx + r * vx, cy + r * vy, r * vz);
                normals[idx] = new Vector3(vx, vy, vz).normalized;
                uvs[idx] = new Vector2(i / (float)archSegs, j / (float)tubeSegs);
            }
        }

        int quadCount = archSegs * tubeSegs;
        var tris = new int[quadCount * 12]; // double-sided
        int tIdx = 0;

        for (int i = 0; i < archSegs; i++)
        {
            int rowA = i * (tubeSegs + 1);
            int rowB = (i + 1) * (tubeSegs + 1);
            for (int j = 0; j < tubeSegs; j++)
            {
                int v0 = rowA + j;
                int v1 = rowB + j;
                int v2 = rowB + j + 1;
                int v3 = rowA + j + 1;

                // Front
                tris[tIdx++] = v0; tris[tIdx++] = v1; tris[tIdx++] = v3;
                tris[tIdx++] = v1; tris[tIdx++] = v2; tris[tIdx++] = v3;

                // Back
                tris[tIdx++] = v0; tris[tIdx++] = v3; tris[tIdx++] = v1;
                tris[tIdx++] = v1; tris[tIdx++] = v3; tris[tIdx++] = v2;
            }
        }

        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        cachedHorseshoeArchMesh = mesh;
        return cachedHorseshoeArchMesh;
    }

    /// <summary>
    /// Builds the visual hierarchy for the overhauled 3D Horseshoe Magnet Pickup:
    /// - Smooth swept tubular Mirror-Chrome arch with Golden Insulator Clamp at apex.
    /// - Candy Gloss Ruby Red North leg with Chrome collar, shoe, embossed white "N", and glowing red node.
    /// - Candy Gloss Electric Blue South leg with Chrome collar, shoe, embossed white "S", and glowing blue node.
    /// - Crackling dynamic electric plasma arc between the two poles.
    /// - Twinkling star glints.
    /// (No weird background discs or cylinder rings).
    /// </summary>
    public static GameObject BuildMagnetMesh(Transform parent)
    {
        var model = new GameObject("Magnet_Visual_Mesh");
        model.transform.SetParent(parent, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        float archRadius = 0.16f;
        float tubeRadius = 0.050f;

        // 1. Shining PBR Materials with specular and HDR emission
        var redShineMat = CreateShiningMaterial(
            new Color(1.0f, 0.10f, 0.22f, 1f),
            new Color(1.0f, 0.12f, 0.25f) * 1.8f,
            smoothness: 0.96f, metallic: 0.85f
        );
        var blueShineMat = CreateShiningMaterial(
            new Color(0.04f, 0.46f, 1.0f, 1f),
            new Color(0.05f, 0.50f, 1.0f) * 1.8f,
            smoothness: 0.96f, metallic: 0.85f
        );
        var chromeMat = CreateShiningMaterial(
            new Color(0.92f, 0.95f, 1.0f, 1f),
            new Color(0.50f, 0.55f, 0.65f) * 0.9f,
            smoothness: 0.98f, metallic: 0.98f
        );
        var goldTrimMat = CreateShiningMaterial(
            new Color(1.0f, 0.82f, 0.20f, 1f),
            new Color(1.0f, 0.75f, 0.18f) * 1.4f,
            smoothness: 0.95f, metallic: 0.92f
        );
        var whiteEmblemMat = CreateShiningMaterial(
            new Color(0.98f, 0.98f, 1.0f, 1f),
            new Color(0.85f, 0.90f, 1.0f) * 1.2f,
            smoothness: 0.92f, metallic: 0.35f
        );

        // Additive Glow Materials
        var starMat = CreateAdditiveGlowMaterial(new Color(1.0f, 1.0f, 1.0f, 0.95f));
        var arcMat = CreateAdditiveGlowMaterial(new Color(0.35f, 0.85f, 1.0f, 0.90f));

        // 2. Top Semicircular Curved Tube Arch (Mirror Chrome)
        var archObj = new GameObject("Curved_Chrome_Arch");
        archObj.transform.SetParent(model.transform, false);
        var archMf = archObj.AddComponent<MeshFilter>();
        archMf.sharedMesh = GetOrCreateHorseshoeArchMesh();
        var archMr = archObj.AddComponent<MeshRenderer>();
        archMr.sharedMaterial = chromeMat;
        archMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Golden Insulator Apex Clamp Ring
        CreatePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(0, archRadius, 0),
            new Vector3(0.115f, 0.022f, 0.115f),
            goldTrimMat,
            Quaternion.Euler(0, 0, 90f)
        );

        // 3. Left Leg: Candy Ruby Red (North Pole)
        // Red Leg Tube
        CreatePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(-archRadius, -0.075f, 0),
            new Vector3(tubeRadius * 2f, 0.075f, tubeRadius * 2f),
            redShineMat
        );
        // Chrome Collar Ring
        CreatePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(-archRadius, -0.150f, 0),
            new Vector3(tubeRadius * 2.2f, 0.010f, tubeRadius * 2.2f),
            chromeMat
        );
        // Chrome Pole Shoe
        CreatePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(-archRadius, -0.185f, 0),
            new Vector3(tubeRadius * 2f, 0.028f, tubeRadius * 2f),
            chromeMat
        );
        // Rounded bottom cap
        CreatePart(PrimitiveType.Sphere, model.transform,
            new Vector3(-archRadius, -0.213f, 0),
            Vector3.one * (tubeRadius * 2f),
            chromeMat
        );
        // Red Node Sphere
        CreatePart(PrimitiveType.Sphere, model.transform,
            new Vector3(-archRadius, -0.225f, 0),
            Vector3.one * 0.075f,
            redShineMat
        );
        // Embossed "N" Monogram directly on the red leg (no background disc)
        BuildLetterBadge(model.transform, new Vector3(-archRadius, -0.075f, tubeRadius + 0.003f), 'N', whiteEmblemMat);

        // 4. Right Leg: Candy Electric Blue (South Pole)
        // Blue Leg Tube
        CreatePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(archRadius, -0.075f, 0),
            new Vector3(tubeRadius * 2f, 0.075f, tubeRadius * 2f),
            blueShineMat
        );
        // Chrome Collar Ring
        CreatePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(archRadius, -0.150f, 0),
            new Vector3(tubeRadius * 2.2f, 0.010f, tubeRadius * 2.2f),
            chromeMat
        );
        // Chrome Pole Shoe
        CreatePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(archRadius, -0.185f, 0),
            new Vector3(tubeRadius * 2f, 0.028f, tubeRadius * 2f),
            chromeMat
        );
        // Rounded bottom cap
        CreatePart(PrimitiveType.Sphere, model.transform,
            new Vector3(archRadius, -0.213f, 0),
            Vector3.one * (tubeRadius * 2f),
            chromeMat
        );
        // Blue Node Sphere
        CreatePart(PrimitiveType.Sphere, model.transform,
            new Vector3(archRadius, -0.225f, 0),
            Vector3.one * 0.075f,
            blueShineMat
        );
        // Embossed "S" Monogram directly on the blue leg (no background disc)
        BuildLetterBadge(model.transform, new Vector3(archRadius, -0.075f, tubeRadius + 0.003f), 'S', whiteEmblemMat);

        // 5. Dynamic Inter-Pole Plasma Arc Bridge
        var arcObj = CreatePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(0, -0.225f, 0),
            new Vector3(0.035f, archRadius, 0.035f),
            arcMat,
            Quaternion.Euler(0, 0, 90f)
        );

        // 6. Shining Star Glints
        var starMesh = GetOrCreateStarGlintMesh();
        var glints = new Transform[3];
        Vector3[] glintPositions = {
            new Vector3(-archRadius, -0.225f, 0.055f), // North Pole Node
            new Vector3(archRadius, -0.225f, 0.055f),  // South Pole Node
            new Vector3(0f, archRadius + 0.035f, 0.045f) // Apex Chrome Arch
        };

        for (int i = 0; i < 3; i++)
        {
            var glintObj = new GameObject("StarGlint_" + i);
            glintObj.transform.SetParent(model.transform, false);
            glintObj.transform.localPosition = glintPositions[i];
            glintObj.transform.localScale = Vector3.one * 0.18f;

            var mf = glintObj.AddComponent<MeshFilter>();
            mf.sharedMesh = starMesh;

            var mr = glintObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = starMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            glints[i] = glintObj.transform;
        }

        // 7. Attach dynamic shining animator
        var shine = model.AddComponent<TCMagnetShine>();
        shine.redMat = redShineMat;
        shine.blueMat = blueShineMat;
        shine.chromeMat = chromeMat;
        shine.plasmaArc = arcObj.transform;
        shine.starGlints = glints;

        return model;
    }

    /// <summary>
    /// Builds 3D embossed letter strokes ("N" or "S") sitting directly on the leg surface without any backplate circle.
    /// </summary>
    static void BuildLetterBadge(Transform parent, Vector3 localPos, char letter, Material letterMat)
    {
        var badge = new GameObject("Badge_" + letter);
        badge.transform.SetParent(parent, false);
        badge.transform.localPosition = localPos;
        badge.transform.localRotation = Quaternion.identity;

        if (letter == 'N')
        {
            // Left upright bar
            CreatePart(PrimitiveType.Cube, badge.transform,
                new Vector3(-0.012f, 0f, 0.003f),
                new Vector3(0.006f, 0.038f, 0.006f),
                letterMat
            );
            // Right upright bar
            CreatePart(PrimitiveType.Cube, badge.transform,
                new Vector3(0.012f, 0f, 0.003f),
                new Vector3(0.006f, 0.038f, 0.006f),
                letterMat
            );
            // Diagonal bar
            CreatePart(PrimitiveType.Cube, badge.transform,
                new Vector3(0f, 0f, 0.003f),
                new Vector3(0.006f, 0.045f, 0.006f),
                letterMat,
                Quaternion.Euler(0, 0, 32.2f)
            );
        }
        else if (letter == 'S')
        {
            // Top bar
            CreatePart(PrimitiveType.Cube, badge.transform,
                new Vector3(0f, 0.018f, 0.003f),
                new Vector3(0.026f, 0.006f, 0.006f),
                letterMat
            );
            // Upper-left bar
            CreatePart(PrimitiveType.Cube, badge.transform,
                new Vector3(-0.010f, 0.009f, 0.003f),
                new Vector3(0.006f, 0.014f, 0.006f),
                letterMat
            );
            // Mid bar
            CreatePart(PrimitiveType.Cube, badge.transform,
                new Vector3(0f, 0f, 0.003f),
                new Vector3(0.024f, 0.006f, 0.006f),
                letterMat
            );
            // Lower-right bar
            CreatePart(PrimitiveType.Cube, badge.transform,
                new Vector3(0.010f, -0.009f, 0.003f),
                new Vector3(0.006f, 0.014f, 0.006f),
                letterMat
            );
            // Bottom bar
            CreatePart(PrimitiveType.Cube, badge.transform,
                new Vector3(0f, -0.018f, 0.003f),
                new Vector3(0.026f, 0.006f, 0.006f),
                letterMat
            );
        }
    }

    /// <summary>
    /// Procedurally creates the complete 3D Horseshoe Magnet Pickup GameObject placed along the tree trunk.
    /// Features continuous rotation, trigger collider, and custom shining PBR materials.
    /// </summary>
    public static GameObject CreateMagnetPickupPrefab(Transform parent)
    {
        var root = new GameObject("Magnet_Pickup_Root");
        if (parent) root.transform.SetParent(parent, false);

        var item = new GameObject("MagnetItem");
        item.transform.SetParent(root.transform, false);
        item.transform.localPosition = new Vector3(0, 0, 0.603f);
        item.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        item.transform.localScale = Vector3.one * 0.50f;
        item.tag = "magnet";

        var rb = item.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.mass = 0.0000001f;

        var col = item.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.75f, 0.75f, 0.75f);

        var rot = item.AddComponent<continuousRotation>();
        rot.y = true;
        rot.speed = 105f;

        // Visual Model
        BuildMagnetMesh(item.transform);

        root.SetActive(false);
        return root;
    }

    static Material CreateShiningMaterial(Color mainCol, Color emissionCol, float smoothness = 0.96f, float metallic = 0.88f)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("TallCrawl/SpiderPreBaked")
            ?? Shader.Find("Mobile/Diffuse");

        var mat = new Material(shader) { name = "Magnet_Shine_Mat" };

        if (mat.HasProperty("_Color")) mat.SetColor("_Color", mainCol);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mainCol);
        mat.color = mainCol;

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", emissionCol);
            mat.EnableKeyword("_EMISSION");
        }

        return mat;
    }

    static Material CreateAdditiveGlowMaterial(Color glowCol)
    {
        var shader = FindAdditiveShader();
        var mat = new Material(shader) { name = "Magnet_Glow_Mat" };
        SetMatColor(mat, glowCol);
        return mat;
    }

    static Mesh cachedStarMesh;
    static Mesh GetOrCreateStarGlintMesh()
    {
        if (cachedStarMesh != null) return cachedStarMesh;

        var mesh = new Mesh { name = "Star_Glint_Mesh" };
        var verts = new Vector3[]
        {
            new Vector3(0.0f, 0.0f, 0.0f),       // 0: Center
            new Vector3(0.0f, 0.5f, 0.0f),       // 1: North
            new Vector3(0.08f, 0.08f, 0.0f),     // 2: NE
            new Vector3(0.5f, 0.0f, 0.0f),       // 3: East
            new Vector3(0.08f, -0.08f, 0.0f),    // 4: SE
            new Vector3(0.0f, -0.5f, 0.0f),      // 5: South
            new Vector3(-0.08f, -0.08f, 0.0f),   // 6: SW
            new Vector3(-0.5f, 0.0f, 0.0f),      // 7: West
            new Vector3(-0.08f, 0.08f, 0.0f)     // 8: NW
        };

        var colors = new Color[]
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 0.25f),
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 0.25f),
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 0.25f),
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 0.25f)
        };

        var tris = new int[48]; // double-sided (24 front + 24 back)
        int idx = 0;
        for (int i = 1; i <= 8; i++)
        {
            int nxt = (i == 8) ? 1 : i + 1;
            // Front
            tris[idx++] = 0; tris[idx++] = i; tris[idx++] = nxt;
            // Back
            tris[idx++] = 0; tris[idx++] = nxt; tris[idx++] = i;
        }

        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        cachedStarMesh = mesh;
        return cachedStarMesh;
    }

    static GameObject CreatePart(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(type);
        var col = g.GetComponent<Collider>();
        if (col) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        if (rot.HasValue) g.transform.localRotation = rot.Value;
        var r = g.GetComponent<MeshRenderer>();
        if (r && mat) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }
}

/// <summary>
/// Animates dynamic shining emission, crackling plasma arc, and star glints on the 3D magnet pickup.
/// </summary>
public class TCMagnetShine : MonoBehaviour
{
    public Material redMat;
    public Material blueMat;
    public Material chromeMat;
    public Transform plasmaArc;
    public Transform[] starGlints;

    Color redEmissionBase = new Color(1.0f, 0.12f, 0.25f);
    Color blueEmissionBase = new Color(0.05f, 0.50f, 1.0f);

    void Update()
    {
        float t = Time.time;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f);

        // Animate emissive shining shader glow
        if (redMat != null)
        {
            Color c = redEmissionBase * (1.4f + 0.9f * pulse);
            if (redMat.HasProperty("_EmissionColor")) redMat.SetColor("_EmissionColor", c);
        }
        if (blueMat != null)
        {
            Color c = blueEmissionBase * (1.4f + 0.9f * pulse);
            if (blueMat.HasProperty("_EmissionColor")) blueMat.SetColor("_EmissionColor", c);
        }

        // Animate dynamic crackling magnetic plasma arc between poles
        if (plasmaArc != null)
        {
            float jitter = Mathf.Sin(t * 28f) * 0.004f;
            float arcPulse = 1f + 0.30f * Mathf.Sin(t * 18f);
            plasmaArc.localPosition = new Vector3(0, -0.225f + jitter, jitter * 0.5f);
            plasmaArc.localScale = new Vector3(0.035f * arcPulse, 0.16f, 0.035f * arcPulse);
        }

        // Rotate and twinkle shining star glints
        if (starGlints != null)
        {
            for (int i = 0; i < starGlints.Length; i++)
            {
                if (starGlints[i] == null) continue;
                starGlints[i].Rotate(0, 0, (110f + i * 40f) * Time.deltaTime, Space.Self);
                float starPulse = 0.16f + 0.05f * Mathf.Sin(t * 8f + i * 1.6f);
                starGlints[i].localScale = Vector3.one * starPulse;
            }
        }
    }
}
