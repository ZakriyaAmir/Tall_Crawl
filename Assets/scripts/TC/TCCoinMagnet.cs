using System.Collections;
using UnityEngine;

/// <summary>
/// Coin Magnet Controller & 3D Pickup Generator.
/// When active, draws nearby coins along the trunk directly into the spider,
/// while emitting a subtle, elegant, 70% transparent yellow circular ripple around the spider.
/// Duration scales with Store Upgrade ID 6: 8.0s base up to 20.0s at Max Level 5 (+2.4s per level).
/// </summary>
[DefaultExecutionOrder(100)]
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

    // Shield Bubble parameters (electric cyan forcefield bubble around spider, scaled down 40% then adjusted +15%):
    const float BaseShieldDiameter = 0.580f; // 0.504f + 15%
    GameObject shieldObj;
    MeshRenderer shieldRenderer;
    Material shieldMat;
    GameObject shieldInnerObj;
    MeshRenderer shieldInnerRenderer;
    Material shieldInnerMat;
    float shieldSpinAngle = 0f;
    float shieldInnerAngle = 0f;
    float shieldPulseTimer = 0f;
    static Mesh cachedShieldQuadMesh;

    struct LightningArc
    {
        public LineRenderer line;
        public float phase;
        public float baseAngle;
    }

    float activeTimeRemaining = 0f;
    float totalActiveDuration = 8f;
    float emissionTimer = 0f;
    Transform spiderTransform;
    TCCuteSpider cuteSpider;
    GameObject magnetAuraObj;

    LightningArc[] lightningArcs;

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

    public Vector3 GetSpiderVisualCenter()
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

        return sp.position + sp.up * 0.07f;
    }

    public void Activate()
    {
        bool wasActive = activeTimeRemaining > 0f && magnetAuraObj != null && magnetAuraObj.activeSelf;
        totalActiveDuration = Duration;
        activeTimeRemaining = Mathf.Max(activeTimeRemaining, totalActiveDuration);

        if (!wasActive)
        {
            emissionTimer = 0f;
            TCSfx.Play("cooldown_activate", 0.9f);
            TCFx.Shake(0.08f);
        }
        else
        {
            // When already active, keep VFX going seamlessly without restarting ease-in, shrinking, or flickering
            if (emissionTimer < 0.6f) emissionTimer = 0.6f; // ensure fully opened
            TCSfx.Play("cooldown_activate", 0.8f);
            TCFx.Shake(0.05f);
        }

        if (magnetAuraObj == null)
        {
            CreateMagnetAura();
        }
        if (magnetAuraObj != null)
        {
            magnetAuraObj.SetActive(true);
            if (shieldObj != null) shieldObj.SetActive(true);
            if (shieldInnerObj != null) shieldInnerObj.SetActive(true);
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

                Vector3 spiderCenter = GetSpiderVisualCenter();

                if (magnetAuraObj != null)
                {
                    // Compute smooth ease-in on activation and ease-out on expiration (0.6s transitions)
                    float easeIn = Mathf.Clamp01(emissionTimer / 0.6f);
                    easeIn = Mathf.SmoothStep(0f, 1f, easeIn);

                    float easeOut = Mathf.Clamp01(activeTimeRemaining / 0.6f);
                    easeOut = Mathf.SmoothStep(0f, 1f, easeOut);

                    float globalEnvelope = easeIn * easeOut;

                    // Situate the aura directly on the spider center (follows jumping spider into the air)
                    magnetAuraObj.transform.position = spiderCenter;
                    magnetAuraObj.transform.rotation = sp.rotation;

                    // Electric Cyan Shield Bubble around the spider (clean transparent forcefield)
                    UpdateShieldVisuals(spiderCenter, sp, dt, globalEnvelope);

                    // 3D crackling electric discharge arcs
                    TickLightningArcs(dt, globalEnvelope);
                }

                // Attract nearby coins along trunk towards spider (strictly within 40m distance)
                if (TCTrack.Instance != null)
                {
                    TCTrack.Instance.AttractCoins(spiderCenter, AttractionRadius, AttractionSpeed * Time.deltaTime);
                }
            }

            if (activeTimeRemaining <= 0f)
            {
                activeTimeRemaining = 0f;
                if (magnetAuraObj != null) magnetAuraObj.SetActive(false);
                TCSfx.Play("menu_drop", 0.75f, 0.95f);
            }
        }
    }

    void LateUpdate()
    {
        // Keep Coin Magnet VFX (shield bubble and crackling lightning arcs) perfectly locked to
        // the spider's 3D visual position during jump, double jump backflip, and landing.
        if (activeTimeRemaining > 0f && magnetAuraObj != null && magnetAuraObj.activeSelf)
        {
            var sp = ResolveSpider();
            if (sp != null)
            {
                Vector3 spiderCenter = GetSpiderVisualCenter();
                magnetAuraObj.transform.position = spiderCenter;
                magnetAuraObj.transform.rotation = sp.rotation;

                var cam = Camera.main;
                Quaternion camRot = (cam != null) ? cam.transform.rotation : sp.rotation;

                if (shieldObj != null)
                {
                    shieldObj.transform.position = spiderCenter;
                    shieldObj.transform.rotation = camRot * Quaternion.Euler(0f, 0f, shieldSpinAngle);
                }
                if (shieldInnerObj != null)
                {
                    shieldInnerObj.transform.position = spiderCenter;
                    shieldInnerObj.transform.rotation = camRot * Quaternion.Euler(0f, 0f, shieldInnerAngle);
                }
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
        Vector3 spiderCenter = GetSpiderVisualCenter();
        magnetAuraObj.transform.position = spiderCenter;
        magnetAuraObj.transform.rotation = sp.rotation;

        // 0. Electric Cyan Shield Bubble (camera-facing forcefield bubble with transparent center & perimeter sparks)
        var shieldMesh = GetOrCreateShieldQuadMesh();
        var shieldTex = Resources.Load<Texture2D>("TCUI/shield_bubble");
        var shieldShader = FindShieldShader();

        shieldObj = new GameObject("Magnet_Shield_Bubble");
        shieldObj.transform.SetParent(magnetAuraObj.transform, false);
        shieldObj.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        var mfShield = shieldObj.AddComponent<MeshFilter>();
        mfShield.sharedMesh = shieldMesh;
        shieldRenderer = shieldObj.AddComponent<MeshRenderer>();
        shieldMat = new Material(shieldShader) { name = "Magnet_Shield_Mat" };
        if (shieldTex) shieldMat.mainTexture = shieldTex;
        ConfigureShieldMaterial(shieldMat);
        shieldRenderer.sharedMaterial = shieldMat;
        shieldRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shieldRenderer.receiveShadows = false;
        shieldRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        shieldRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        // Inner counter-rotating shield layer for rich 3D plasma depth
        shieldInnerObj = new GameObject("Magnet_Shield_Inner");
        shieldInnerObj.transform.SetParent(magnetAuraObj.transform, false);
        shieldInnerObj.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        var mfInner = shieldInnerObj.AddComponent<MeshFilter>();
        mfInner.sharedMesh = shieldMesh;
        shieldInnerRenderer = shieldInnerObj.AddComponent<MeshRenderer>();
        shieldInnerMat = new Material(shieldShader) { name = "Magnet_Shield_Inner_Mat" };
        if (shieldTex) shieldInnerMat.mainTexture = shieldTex;
        ConfigureShieldMaterial(shieldInnerMat);
        shieldInnerRenderer.sharedMaterial = shieldInnerMat;
        shieldInnerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shieldInnerRenderer.receiveShadows = false;
        shieldInnerRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        shieldInnerRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

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

        float dur = 0.30f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur;
            float fade = 1f - k;

            Vector3 spiderCenter = GetSpiderVisualCenter();
            var sp = ResolveSpider();
            if (magnetAuraObj != null)
            {
                magnetAuraObj.transform.position = spiderCenter;
                if (sp != null) magnetAuraObj.transform.rotation = sp.rotation;
            }

            // Shield bubble expansion & shatter fade
            if (shieldObj != null)
            {
                shieldObj.transform.position = spiderCenter;
                float popScale = Mathf.Lerp(BaseShieldDiameter, BaseShieldDiameter * 1.55f, Mathf.Sqrt(k));
                shieldObj.transform.localScale = Vector3.one * popScale;
                SetShieldMatColor(shieldMat, new Color(1f, 1f, 1f, fade * 0.95f));
            }
            if (shieldInnerObj != null)
            {
                shieldInnerObj.transform.position = spiderCenter;
                float popInner = Mathf.Lerp(BaseShieldDiameter * 0.88f, BaseShieldDiameter * 1.70f, Mathf.Sqrt(k));
                shieldInnerObj.transform.localScale = Vector3.one * popInner;
                SetShieldMatColor(shieldInnerMat, new Color(0.85f, 0.98f, 1f, fade * 0.60f));
            }

            TickLightningArcs(0.03f, fade);
            yield return null;
        }
        if (shieldObj != null) shieldObj.SetActive(false);
        if (shieldInnerObj != null) shieldInnerObj.SetActive(false);
        if (magnetAuraObj) magnetAuraObj.SetActive(false);
    }

    void CreateLightningArcs()
    {
        const int n = 8;
        lightningArcs = new LightningArc[n];
        var shader = FindAdditiveShader();
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Magnet_Arc_" + i);
            go.transform.SetParent(magnetAuraObj.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
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
            SetMatColor(mat, new Color(0.4f, 0.92f, 1f, 0.95f));
            lr.sharedMaterial = mat;
            lr.startColor = new Color(1f, 1f, 1f, 0.98f);
            lr.endColor = new Color(0.2f, 0.85f, 1f, 0.5f);
            lightningArcs[i] = new LightningArc
            {
                line = lr,
                phase = Random.value * 10f,
                baseAngle = i * (360f / n) + Random.Range(-10f, 10f)
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
            arc.phase += dt * 24f;
            // Flicker: some frames the bolt is hidden so it crackles realistically.
            bool on = Mathf.Sin(arc.phase * 8.5f + i * 1.3f) > -0.2f;
            arc.line.enabled = on && alphaMul > 0.05f;
            if (!on) continue;
            float ang = (arc.baseAngle + emissionTimer * 45f + i * 15f) * Mathf.Deg2Rad;
            float inner = 0.028f;
            float outer = 0.248f + 0.028f * Mathf.Sin(arc.phase);
            var pts = new Vector3[6];
            for (int s = 0; s < 6; s++)
            {
                float u = s / 5f;
                float r = Mathf.Lerp(inner, outer, u);
                float jag = (s > 0 && s < 5) ? (Mathf.PerlinNoise(arc.phase * 0.9f, s * 1.8f + i) - 0.5f) * 0.062f : 0f;
                float jagY = (s > 0 && s < 5) ? (Mathf.PerlinNoise(s * 2.1f, arc.phase * 0.9f + i) - 0.5f) * 0.055f : 0f;
                float a = ang + jag * 3f;
                pts[s] = new Vector3(Mathf.Cos(a) * r, 0.02f + jagY, Mathf.Sin(a) * r);
            }
            arc.line.SetPositions(pts);
            float aCol = Mathf.Clamp01(alphaMul) * (0.65f + 0.35f * Random.value);
            var c0 = new Color(1.0f, 1.0f, 1.0f, aCol);
            var c1 = new Color(0.2f, 0.85f, 1.0f, aCol * 0.7f);
            arc.line.startColor = c0;
            arc.line.endColor = c1;
            lightningArcs[i] = arc;
        }
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

    void UpdateShieldVisuals(Vector3 spiderCenter, Transform sp, float dt, float globalEnvelope)
    {
        if (shieldObj == null || sp == null) return;

        // Position shield centered on spider body (follows jumping spider into the air)
        shieldObj.transform.position = spiderCenter;

        // Animate smooth rotation of outer electric lightning rim and inner energy
        shieldSpinAngle += dt * 22f;
        shieldInnerAngle -= dt * 15f;
        shieldPulseTimer += dt * 4.5f;

        // Breathing electric pulse + micro-vibration
        float pulse = 1f + 0.035f * Mathf.Sin(shieldPulseTimer);
        float jitter = (Mathf.PerlinNoise(shieldPulseTimer * 3.2f, 0f) - 0.5f) * 0.015f;
        float currentDiameter = (BaseShieldDiameter * (pulse + jitter)) * globalEnvelope;

        var cam = Camera.main;
        Quaternion camRot = (cam != null) ? cam.transform.rotation : sp.rotation;

        // Outer shield: camera-facing billboard quad
        shieldObj.transform.rotation = camRot * Quaternion.Euler(0f, 0f, shieldSpinAngle);
        shieldObj.transform.localScale = Vector3.one * currentDiameter;

        // Vibrant cyan-electric glow tint with smooth envelope fade
        Color outerColor = new Color(1f, 1f, 1f, globalEnvelope);
        SetShieldMatColor(shieldMat, outerColor);

        // Inner shield: slightly smaller counter-rotating layer for 3D plasma depth
        if (shieldInnerObj != null)
        {
            shieldInnerObj.transform.position = spiderCenter;
            shieldInnerObj.transform.rotation = camRot * Quaternion.Euler(0f, 0f, shieldInnerAngle);
            float innerDiameter = currentDiameter * 0.88f * (1f + 0.02f * Mathf.Cos(shieldPulseTimer * 1.4f));
            shieldInnerObj.transform.localScale = Vector3.one * innerDiameter;

            Color innerColor = new Color(0.85f, 0.98f, 1f, globalEnvelope * 0.50f);
            SetShieldMatColor(shieldInnerMat, innerColor);
        }
    }

    static void ConfigureShieldMaterial(Material mat)
    {
        if (!mat) return;
        if (mat.HasProperty("_Additive")) mat.SetFloat("_Additive", 0.40f);
        if (mat.HasProperty("_Boost")) mat.SetFloat("_Boost", 1.20f);
        if (mat.HasProperty("_DepthPull")) mat.SetFloat("_DepthPull", 0.10f);
    }

    static Shader FindShieldShader()
    {
        return Shader.Find("TallCrawl/FX Particle")
            ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Mobile/Particles/Alpha Blended")
            ?? FindAdditiveShader();
    }

    static void SetShieldMatColor(Material mat, Color c)
    {
        if (!mat) return;
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        mat.color = c;
    }

    static Mesh GetOrCreateShieldQuadMesh()
    {
        if (cachedShieldQuadMesh != null) return cachedShieldQuadMesh;
        var mesh = new Mesh { name = "Magnet_Shield_Quad_Mesh" };
        float h = 0.5f;
        mesh.vertices = new Vector3[]
        {
            new Vector3(-h, -h, 0f),
            new Vector3( h, -h, 0f),
            new Vector3(-h,  h, 0f),
            new Vector3( h,  h, 0f)
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
            -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward
        };
        mesh.triangles = new int[]
        {
            0, 2, 1, 2, 3, 1,
            0, 1, 2, 2, 1, 3
        };
        mesh.RecalculateBounds();
        cachedShieldQuadMesh = mesh;
        return cachedShieldQuadMesh;
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
