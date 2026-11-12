using System.Collections;
using UnityEngine;

/// <summary>
/// Coin Magnet Controller & 3D Pickup Generator.
/// When active, draws nearby coins along the trunk directly into the spider,
/// while emitting a subtle, elegant, 70% transparent yellow circular ripple around the spider.
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

    // Spider Shockwave parameters:
    // Concentric expanding silver magnetic shockwave rings and 8 electric bolts around the spider
    const int SubtleWaveCount = 3;
    const float WaveLoopPeriod = 1.40f;
    const int BoltCount = 8;

    struct ShockwaveWave
    {
        public Transform root;
        public MeshRenderer renderer;
        public Material material;
    }

    struct MagnetBolt
    {
        public Transform root;
        public MeshRenderer renderer;
        public Material material;
        public float baseAngle;
    }

    struct LightningArc
    {
        public LineRenderer line;
        public float phase;
        public float baseAngle;
    }

    float activeTimeRemaining = 0f;
    float emissionTimer = 0f;
    float boltOrbitAngle = 0f;
    Transform spiderTransform;
    GameObject magnetAuraObj;

    ShockwaveWave[] subtleShockwaves;
    MagnetBolt[] magnetBolts;
    LightningArc[] lightningArcs;
    static Mesh cachedSubtleWaveMesh;
    static Mesh cachedBoltQuadMesh;

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
        boltOrbitAngle = 0f;
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
            float dt = Time.deltaTime;
            activeTimeRemaining -= dt;
            emissionTimer += dt;

            var sp = ResolveSpider();
            if (sp != null)
            {
                if (magnetAuraObj == null) CreateMagnetAura();

                if (magnetAuraObj != null)
                {
                    // Situate the aura directly on the spider carapace plane (does not float high or block camera)
                    magnetAuraObj.transform.position = sp.position + sp.up * 0.04f;
                    magnetAuraObj.transform.rotation = sp.rotation;

                    // 1. Shimmering silver circular shockwaves (gleaming platinum / silver-white ripples)
                    if (subtleShockwaves != null)
                    {
                        for (int i = 0; i < subtleShockwaves.Length; i++)
                        {
                            var wave = subtleShockwaves[i];
                            if (wave.root == null || wave.material == null) continue;

                            float phaseOffset = (float)i / SubtleWaveCount;
                            float u = Mathf.Repeat((emissionTimer / WaveLoopPeriod) + phaseOffset, 1.0f);

                            // Smooth ease-out expansion from spider body out past the legs (0.10m to 0.32m)
                            float ease = 1f - Mathf.Pow(1f - u, 2.0f);
                            float radius = Mathf.Lerp(0.10f, 0.32f, ease);
                            wave.root.localScale = new Vector3(radius, 1f, radius);

                            // Crisp bell curve with punchy opacity clearly visible on phone screens
                            float alpha = Mathf.Sin(u * Mathf.PI);
                            alpha = Mathf.Pow(alpha, 1.2f) * 0.85f;

                            // Metallic platinum / cool steel with a bright chrome crest
                            Color waveCol = Color.Lerp(
                                new Color(0.92f, 0.95f, 1.0f, alpha),
                                new Color(0.55f, 0.62f, 0.72f, alpha * 0.78f),
                                u
                            );

                            SetMatColor(wave.material, waveCol);
                        }
                    }

                    // 2. 8 Electric Bolt Icons pulsing around the spider in gleaming silver-white
                    if (magnetBolts != null)
                    {
                        boltOrbitAngle += dt * 32f;
                        float basePulseTime = Time.unscaledTime * 7f;

                        for (int i = 0; i < magnetBolts.Length; i++)
                        {
                            var bolt = magnetBolts[i];
                            if (bolt.root == null || bolt.material == null) continue;

                            float angleDeg = bolt.baseAngle + boltOrbitAngle;
                            float angleRad = angleDeg * Mathf.Deg2Rad;

                            // Dynamic rhythmic electric pulse for each bolt
                            float pulse = 0.5f + 0.5f * Mathf.Sin(basePulseTime + i * 0.785f);
                            float radius = 0.28f + 0.03f * pulse;
                            float scale = Mathf.Lerp(0.70f, 0.98f, pulse);

                            bolt.root.localPosition = new Vector3(Mathf.Cos(angleRad) * radius, 0.005f, Mathf.Sin(angleRad) * radius);
                            // Point the bolt outward radially away from the spider
                            bolt.root.localRotation = Quaternion.Euler(0f, -angleDeg + 90f, 0f);
                            bolt.root.localScale = Vector3.one * scale;

                            // Shimmering silver electric bolt color
                            float alpha = Mathf.Lerp(0.65f, 1.0f, pulse);
                            Color boltCol = Color.Lerp(
                                new Color(0.85f, 0.92f, 1.0f, alpha),
                                new Color(1.0f, 1.0f, 1.0f, alpha),
                                pulse
                            );
                            SetMatColor(bolt.material, boltCol);
                        }
                    }

                    TickLightningArcs(dt);
                }

                // Attract nearby coins along trunk towards spider (strictly within 40m distance)
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

        // 1. Silver Shockwave Ripple Rings (concentric hollow circular rings)
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
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            subtleShockwaves[i] = new ShockwaveWave
            {
                root = waveObj.transform,
                renderer = mr,
                material = mat
            };
        }

        // 2. 8 Electric Bolt Icons pulsing around the spider
        var boltMesh = GetOrCreateBoltQuadMesh();
        var boltTex = Resources.Load<Texture2D>("TCUI/icon_bolt");
        magnetBolts = new MagnetBolt[BoltCount];

        for (int i = 0; i < BoltCount; i++)
        {
            var boltObj = new GameObject("Magnet_Bolt_" + i);
            boltObj.transform.SetParent(magnetAuraObj.transform, false);

            var mf = boltObj.AddComponent<MeshFilter>();
            mf.sharedMesh = boltMesh;

            var mr = boltObj.AddComponent<MeshRenderer>();
            var mat = new Material(addShader) { name = "MagnetBolt_Mat_" + i };
            if (boltTex) mat.mainTexture = boltTex;
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            float baseAng = i * (360f / BoltCount);
            magnetBolts[i] = new MagnetBolt
            {
                root = boltObj.transform,
                renderer = mr,
                material = mat,
                baseAngle = baseAng
            };
        }

        CreateLightningArcs();
    }

    public void Break()
    {
        if (!IsActive) return;
        activeTimeRemaining = 0f;
        TCSfx.Play("hit", 0.8f);
        TCSfx.Play("whoosh", 0.55f);
        TCFx.Shake(0.22f);
        if (isActiveAndEnabled) StartCoroutine(BreakRoutine());
        else if (magnetAuraObj) magnetAuraObj.SetActive(false);
    }

    IEnumerator BreakRoutine()
    {
        if (magnetAuraObj == null) yield break;
        magnetAuraObj.SetActive(true);
        if (magnetBolts != null)
            foreach (var b in magnetBolts)
                if (b.root) b.root.gameObject.SetActive(false);

        float dur = 0.28f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur;
            if (subtleShockwaves != null)
            {
                for (int i = 0; i < subtleShockwaves.Length; i++)
                {
                    var wave = subtleShockwaves[i];
                    if (wave.root == null || wave.material == null) continue;
                    float radius = Mathf.Lerp(0.22f, 0.55f, k);
                    wave.root.localScale = new Vector3(radius, 1f, radius);
                    SetMatColor(wave.material, new Color(1f, 1f, 1f, 1f - k));
                }
            }
            TickLightningArcs(0.03f, 1f - k);
            yield return null;
        }
        if (magnetAuraObj) magnetAuraObj.SetActive(false);
        if (magnetBolts != null)
            foreach (var b in magnetBolts)
                if (b.root) b.root.gameObject.SetActive(true);
    }

    void CreateLightningArcs()
    {
        const int n = 5;
        lightningArcs = new LightningArc[n];
        var shader = FindAdditiveShader();
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Magnet_Arc_" + i);
            go.transform.SetParent(magnetAuraObj.transform, false);
            go.transform.localPosition = new Vector3(0f, -0.018f, 0f);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 6;
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.widthMultiplier = 0.012f;
            var mat = new Material(shader) { name = "MagnetArc_Mat_" + i };
            if (mat.HasProperty("_MainTex")) mat.mainTexture = Texture2D.whiteTexture;
            SetMatColor(mat, new Color(0.85f, 0.95f, 1f, 0.9f));
            lr.sharedMaterial = mat;
            lr.startColor = new Color(0.95f, 0.98f, 1f, 0.95f);
            lr.endColor = new Color(0.55f, 0.75f, 1f, 0.15f);
            lightningArcs[i] = new LightningArc
            {
                line = lr,
                phase = Random.value * 10f,
                baseAngle = i * (360f / n) + Random.Range(-12f, 12f)
            };
        }
    }

    void TickLightningArcs(float dt, float alphaMul = 1f)
    {
        if (lightningArcs == null) return;
        for (int i = 0; i < lightningArcs.Length; i++)
        {
            var arc = lightningArcs[i];
            if (arc.line == null) continue;
            arc.phase += dt * 18f;
            // Flicker: some frames the bolt is hidden so it crackles.
            bool on = Mathf.Sin(arc.phase * 7.3f + i) > -0.15f;
            arc.line.enabled = on && alphaMul > 0.05f;
            if (!on) continue;
            float ang = (arc.baseAngle + emissionTimer * 40f + i * 8f) * Mathf.Deg2Rad;
            float inner = 0.08f;
            float outer = 0.26f + 0.04f * Mathf.Sin(arc.phase);
            var pts = new Vector3[6];
            for (int s = 0; s < 6; s++)
            {
                float u = s / 5f;
                float r = Mathf.Lerp(inner, outer, u);
                float jag = (s > 0 && s < 5) ? (Mathf.PerlinNoise(arc.phase * 0.7f, s * 1.7f + i) - 0.5f) * 0.07f : 0f;
                float a = ang + jag * 3f;
                // Sit under the ring, slightly dropped on Y.
                pts[s] = new Vector3(Mathf.Cos(a) * r, -0.01f - 0.012f * u, Mathf.Sin(a) * r);
            }
            arc.line.SetPositions(pts);
            float aCol = Mathf.Clamp01(alphaMul) * (0.55f + 0.45f * Random.value);
            var c0 = new Color(0.95f, 0.98f, 1f, aCol);
            var c1 = new Color(0.55f, 0.78f, 1f, aCol * 0.2f);
            arc.line.startColor = c0;
            arc.line.endColor = c1;
            lightningArcs[i] = arc;
        }
    }

    /// <summary>
    /// Creates a smooth, planar annulus ripple mesh with soft radial alpha falloff and silver-white vertex tint.
    /// Double-ring crest: Inner rim (0.80) = alpha 0, Crest (0.90 to 0.95) = alpha 0.9, Outer rim (1.00) = alpha 0.
    /// Completely hollow middle so there is zero obstruction in the center.
    /// </summary>
    static Mesh GetOrCreateSubtleShockwaveMesh()
    {
        if (cachedSubtleWaveMesh != null) return cachedSubtleWaveMesh;

        int segments = 48;
        var mesh = new Mesh { name = "Subtle_Shockwave_Mesh" };

        // Defined ring thickness (20% of radius) for sharp, clear visibility on mobile screens
        float[] radii = { 0.80f, 0.90f, 0.95f, 1.00f };
        Color[] baseColors = {
            new Color(0.70f, 0.76f, 0.85f, 0.0f),  // inner edge (transparent steel)
            new Color(0.98f, 0.99f, 1.0f, 1.00f), // sharp inner crest (chrome highlight)
            new Color(0.62f, 0.70f, 0.82f, 0.95f), // outer crest (cool metal)
            new Color(0.40f, 0.48f, 0.58f, 0.0f)  // outer edge
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

    static Mesh GetOrCreateBoltQuadMesh()
    {
        if (cachedBoltQuadMesh != null) return cachedBoltQuadMesh;

        var mesh = new Mesh { name = "Magnet_Bolt_Quad" };
        float halfW = 0.050f;
        float halfH = 0.075f;
        mesh.vertices = new Vector3[]
        {
            new Vector3(-halfW, 0f, -halfH),
            new Vector3( halfW, 0f, -halfH),
            new Vector3(-halfW, 0f,  halfH),
            new Vector3( halfW, 0f,  halfH)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.normals = new Vector3[]
        {
            Vector3.up, Vector3.up, Vector3.up, Vector3.up
        };
        mesh.triangles = new int[]
        {
            // Front (visible from +Y)
            0, 2, 1, 2, 3, 1,
            // Back (visible from -Y)
            0, 1, 2, 2, 1, 3
        };
        mesh.RecalculateBounds();
        cachedBoltQuadMesh = mesh;
        return cachedBoltQuadMesh;
    }

    static void SetMatColor(Material mat, Color c)
    {
        if (!mat) return;
        // Legacy Shaders/Particles/Additive applies a 2x factor on _TintColor; halve it for 1:1 parity
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", c * 0.5f);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        mat.color = c;
    }

    static Shader FindAdditiveShader()
    {
        return Shader.Find("Legacy Shaders/Particles/Additive")
            ?? Shader.Find("Mobile/Particles/Additive")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default");
    }

    // =========================================================================
    // 3D MAGNET POWER-UP PICKUP GENERATION (SINGLE U-SHAPED HORSESHOE MAGNET)
    // =========================================================================

    static Mesh cachedSingleUMagnetMesh;

    /// <summary>
    /// Procedurally creates a single continuous U-shaped swept tubular horseshoe magnet mesh.
    /// Contains two submeshes:
    /// Submesh 0: Right half of U (Candy Electric Blue)
    /// Submesh 1: Left half of U (Candy Ruby Red)
    /// Seamlessly joined at the apex top (X=0, Y=R).
    /// </summary>
    public static Mesh GetOrCreateSingleUMagnetMesh()
    {
        if (cachedSingleUMagnetMesh != null) return cachedSingleUMagnetMesh;

        float archRadius = 0.16f;
        float tubeRadius = 0.050f;
        float legLength = 0.16f;

        int legSegs = 10;
        int archSegs = 20;
        int tubeSegs = 16;

        var path = new System.Collections.Generic.List<Vector3>(48);
        var normals = new System.Collections.Generic.List<Vector3>(48);
        var binormals = new System.Collections.Generic.List<Vector3>(48);

        // 1. Right leg (Blue) moving UP from Y = -legLength to 0 at X = +archRadius
        for (int i = 0; i < legSegs; i++)
        {
            float t = i / (float)legSegs;
            float y = -legLength + t * legLength;
            path.Add(new Vector3(archRadius, y, 0f));
            normals.Add(new Vector3(1f, 0f, 0f));
            binormals.Add(new Vector3(0f, 0f, 1f));
        }

        // 2. Arch moving counter-clockwise from theta = 0 to pi in Y > 0
        for (int i = 0; i <= archSegs; i++)
        {
            float theta = (i / (float)archSegs) * Mathf.PI;
            float cx = archRadius * Mathf.Cos(theta);
            float cy = archRadius * Mathf.Sin(theta);
            path.Add(new Vector3(cx, cy, 0f));
            normals.Add(new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0f));
            binormals.Add(new Vector3(0f, 0f, 1f));
        }

        // 3. Left leg (Red) moving DOWN from Y = 0 to -legLength at X = -archRadius
        for (int i = 1; i <= legSegs; i++)
        {
            float t = i / (float)legSegs;
            float y = -t * legLength;
            path.Add(new Vector3(-archRadius, y, 0f));
            normals.Add(new Vector3(-1f, 0f, 0f));
            binormals.Add(new Vector3(0f, 0f, 1f));
        }

        int totalSlices = path.Count;
        int vertCount = totalSlices * (tubeSegs + 1);
        var verts = new Vector3[vertCount];
        var meshNormals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        for (int i = 0; i < totalSlices; i++)
        {
            Vector3 P = path[i];
            Vector3 N = normals[i];
            Vector3 B = binormals[i];

            for (int j = 0; j <= tubeSegs; j++)
            {
                float phi = (j / (float)tubeSegs) * Mathf.PI * 2f;
                Vector3 vNorm = (Mathf.Cos(phi) * N + Mathf.Sin(phi) * B).normalized;

                int idx = i * (tubeSegs + 1) + j;
                verts[idx] = P + tubeRadius * vNorm;
                meshNormals[idx] = vNorm;
                uvs[idx] = new Vector2(i / (float)(totalSlices - 1), j / (float)tubeSegs);
            }
        }

        // Submesh 0: Right half of U (slices 0 to midSlice) -> Blue
        // Submesh 1: Left half of U (slices midSlice to totalSlices-1) -> Red
        int midSlice = legSegs + archSegs / 2;

        var trisBlue = new System.Collections.Generic.List<int>(midSlice * tubeSegs * 12);
        for (int i = 0; i < midSlice; i++)
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
                trisBlue.Add(v0); trisBlue.Add(v1); trisBlue.Add(v3);
                trisBlue.Add(v1); trisBlue.Add(v2); trisBlue.Add(v3);

                // Back
                trisBlue.Add(v0); trisBlue.Add(v3); trisBlue.Add(v1);
                trisBlue.Add(v1); trisBlue.Add(v3); trisBlue.Add(v2);
            }
        }

        var trisRed = new System.Collections.Generic.List<int>((totalSlices - 1 - midSlice) * tubeSegs * 12);
        for (int i = midSlice; i < totalSlices - 1; i++)
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
                trisRed.Add(v0); trisRed.Add(v1); trisRed.Add(v3);
                trisRed.Add(v1); trisRed.Add(v2); trisRed.Add(v3);

                // Back
                trisRed.Add(v0); trisRed.Add(v3); trisRed.Add(v1);
                trisRed.Add(v1); trisRed.Add(v3); trisRed.Add(v2);
            }
        }

        var mesh = new Mesh { name = "Single_U_Horseshoe_Magnet" };
        mesh.subMeshCount = 2;
        mesh.vertices = verts;
        mesh.normals = meshNormals;
        mesh.uv = uvs;
        mesh.SetTriangles(trisBlue.ToArray(), 0);
        mesh.SetTriangles(trisRed.ToArray(), 1);
        mesh.RecalculateBounds();

        cachedSingleUMagnetMesh = mesh;
        return cachedSingleUMagnetMesh;
    }

    /// <summary>
    /// Builds a single clean U-shaped cartoon Horseshoe Magnet (Red and Blue halves with silver ends).
    /// </summary>
    public static GameObject BuildMagnetMesh(Transform parent)
    {
        var model = new GameObject("Magnet_Visual_Mesh");
        model.transform.SetParent(parent, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        float archRadius = 0.16f;
        float tubeRadius = 0.050f;
        float legBottomY = -0.16f;

        // 1. Shining PBR Materials: Candy Red, Candy Blue, and Polished Chrome
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
        var whiteEmblemMat = CreateShiningMaterial(
            new Color(0.98f, 0.98f, 1.0f, 1f),
            new Color(0.85f, 0.90f, 1.0f) * 1.2f,
            smoothness: 0.92f, metallic: 0.35f
        );

        // 2. Single Continuous U-Shaped Magnet Mesh (Right half = Blue, Left half = Red)
        var uBody = new GameObject("U_Magnet_Body");
        uBody.transform.SetParent(model.transform, false);
        var mf = uBody.AddComponent<MeshFilter>();
        mf.sharedMesh = GetOrCreateSingleUMagnetMesh();
        var mr = uBody.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new Material[] { blueShineMat, redShineMat };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 3. Left Pole Tip (North - Silver / Chrome Cap & Pole Shoe)
        CreatePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(-archRadius, legBottomY - 0.025f, 0f),
            new Vector3(tubeRadius * 2f, 0.025f, tubeRadius * 2f),
            chromeMat
        );
        CreatePart(PrimitiveType.Sphere, model.transform,
            new Vector3(-archRadius, legBottomY - 0.050f, 0f),
            Vector3.one * (tubeRadius * 2f),
            chromeMat
        );
        // Embossed 3D "N" directly on the red leg
        BuildLetterBadge(model.transform, new Vector3(-archRadius, -0.075f, tubeRadius + 0.003f), 'N', whiteEmblemMat);

        // 4. Right Pole Tip (South - Silver / Chrome Cap & Pole Shoe)
        CreatePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(archRadius, legBottomY - 0.025f, 0f),
            new Vector3(tubeRadius * 2f, 0.025f, tubeRadius * 2f),
            chromeMat
        );
        CreatePart(PrimitiveType.Sphere, model.transform,
            new Vector3(archRadius, legBottomY - 0.050f, 0f),
            Vector3.one * (tubeRadius * 2f),
            chromeMat
        );
        // Embossed 3D "S" directly on the blue leg
        BuildLetterBadge(model.transform, new Vector3(archRadius, -0.075f, tubeRadius + 0.003f), 'S', whiteEmblemMat);

        // 5. Attach dynamic shining animator for metallic reflections
        var shine = model.AddComponent<TCMagnetShine>();
        shine.redMat = redShineMat;
        shine.blueMat = blueShineMat;
        shine.chromeMat = chromeMat;

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

        // Visual Model (Single U-shaped Horseshoe Magnet)
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
/// Animates dynamic shining emission on the single 3D U-shaped magnet pickup.
/// </summary>
public class TCMagnetShine : MonoBehaviour
{
    public Material redMat;
    public Material blueMat;
    public Material chromeMat;

    Color redEmissionBase = new Color(1.35f, 0.18f, 0.28f);
    Color blueEmissionBase = new Color(0.12f, 0.65f, 1.35f);

    void Update()
    {
        float t = Time.time;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f);

        // Animate emissive shining shader glow on red and blue nodes
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
    }
}
