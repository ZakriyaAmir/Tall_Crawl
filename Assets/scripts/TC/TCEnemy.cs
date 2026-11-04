using System.Collections.Generic;
using UnityEngine;

public enum TCEnemyType
{
    Pinecone, // Spiky rolling pinecone tumbling down the trunk bark
    Wasp,     // Aerial darting hornet hovering off the bark with fluttering wings
    Squirrel  // Animated running squirrel scampering downwards along the trunk
}

/// <summary>
/// Base class for all interactive obstacles and enemies on the tree trunk.
/// Supports jumping over (pinecones, squirrels) and crushing via the Sonic-style Roll Attack!
/// </summary>
public class TCEnemy : MonoBehaviour
{
    public TCEnemyType enemyType;
    public virtual bool CanJumpOver => enemyType == TCEnemyType.Pinecone || enemyType == TCEnemyType.Squirrel;

    public float baseAngle;
    public float sweepAngle = 40f;
    public float speed = 2.2f;
    public float radius = 0.603f;
    public float y;
    public TCPooled pooled;

    protected Transform model;
    protected float phase;
    protected bool isSmashed;

    public virtual void Init(float startY, float startAngle, float sweep = 40f, float spd = 2.2f)
    {
        y = startY;
        baseAngle = startAngle;
        sweepAngle = sweep;
        speed = spd;
        phase = Random.Range(0f, Mathf.PI * 2f);
        isSmashed = false;
        gameObject.SetActive(true);
        if (model) model.gameObject.SetActive(true);
        UpdateTransform();
    }

    protected virtual void UpdateTransform()
    {
        transform.position = new Vector3(0, y, 0);
        transform.rotation = Quaternion.Euler(0, baseAngle, 0);
    }

    /// <summary>
    /// Smashed to pieces by the Sonic-style Roll Attack or Chilli Boost!
    /// </summary>
    public virtual void Smash()
    {
        if (isSmashed) return;
        isSmashed = true;

        if (pooled)
        {
            pooled.Release();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // ---------------- Procedural Materials & Textures ----------------

    static Material pineconeWoodMat, pineconeScaleMat, eyeGlowMat, waspBodyMat, waspBlackMat, waspWingMat;
    static Material squirrelFurMat, squirrelBellyMat, squirrelDarkMat, squirrelEyeMat;

    static Texture2D LoadTexture(string name)
    {
        var tex = Resources.Load<Texture2D>("Enemies/" + name);
#if UNITY_EDITOR
        if (!tex) tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TC/Resources/Enemies/" + name + ".png");
        if (!tex) tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TC/Textures/" + name + ".png");
#endif
        return tex;
    }

    static Material CreateCelMaterial(Color tint, Texture2D tex, float outlineWidth = 1.4f, Color? outlineColor = null, Color? emission = null)
    {
        var shader = Shader.Find("TallCrawl/SpiderPreBaked") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        var mat = new Material(shader);
        mat.shader = shader;
        mat.color = tint;
        if (tex != null) mat.mainTexture = tex;

        if (mat.HasProperty("_ShadowDull")) mat.SetFloat("_ShadowDull", 0.55f);
        if (mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", outlineColor ?? new Color(0.14f, 0.08f, 0.06f, 1f));
        if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", outlineWidth);
        if (mat.HasProperty("_EdgeThreshold")) mat.SetFloat("_EdgeThreshold", 0.22f);
        if (mat.HasProperty("_CelIntensity")) mat.SetFloat("_CelIntensity", 0.35f);

        if (emission.HasValue)
        {
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission.Value);
            mat.EnableKeyword("_EMISSION");
        }
        return mat;
    }

    static void EnsureMaterials()
    {
        if (!pineconeWoodMat)
        {
            var tex = LoadTexture("Pinecone_Texture");
            pineconeWoodMat = CreateCelMaterial(new Color(0.92f, 0.78f, 0.65f, 1f), tex, 1.4f, new Color(0.18f, 0.09f, 0.04f, 1f));
        }

        if (!pineconeScaleMat)
        {
            var tex = LoadTexture("Pinecone_Texture");
            pineconeScaleMat = CreateCelMaterial(new Color(0.78f, 0.55f, 0.35f, 1f), tex, 1.2f, new Color(0.15f, 0.07f, 0.03f, 1f));
        }

        if (!eyeGlowMat)
        {
            Color glow = new Color(1.0f, 0.35f, 0.08f, 1f);
            eyeGlowMat = CreateCelMaterial(glow, null, 0f, Color.clear, glow * 1.6f);
        }

        if (!waspBodyMat)
        {
            var tex = LoadTexture("Wasp_Stripes");
            waspBodyMat = CreateCelMaterial(Color.white, tex, 1.4f, new Color(0.10f, 0.08f, 0.02f, 1f));
        }

        if (!waspBlackMat)
        {
            waspBlackMat = CreateCelMaterial(new Color(0.12f, 0.10f, 0.14f, 1f), null, 1.2f);
        }

        if (!waspWingMat)
        {
            var tex = LoadTexture("Wasp_Wing");
            var shader = Shader.Find("Mobile/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Transparent/Diffuse");
            waspWingMat = new Material(shader);
            waspWingMat.shader = shader;
            waspWingMat.color = new Color(0.92f, 0.96f, 1f, 0.75f);
            if (tex != null) waspWingMat.mainTexture = tex;
        }

        if (!squirrelFurMat)
        {
            var tex = LoadTexture("Squirrel_Fur");
            squirrelFurMat = CreateCelMaterial(new Color(0.85f, 0.44f, 0.18f, 1f), tex, 1.3f, new Color(0.24f, 0.10f, 0.04f, 1f));
        }

        if (!squirrelBellyMat)
        {
            var tex = LoadTexture("Squirrel_Belly");
            squirrelBellyMat = CreateCelMaterial(new Color(0.98f, 0.92f, 0.82f, 1f), tex, 1.2f, new Color(0.24f, 0.10f, 0.04f, 1f));
        }

        if (!squirrelDarkMat)
        {
            squirrelDarkMat = CreateCelMaterial(new Color(0.35f, 0.16f, 0.08f, 1f), null, 1.2f, new Color(0.18f, 0.07f, 0.03f, 1f));
        }

        if (!squirrelEyeMat)
        {
            squirrelEyeMat = CreateCelMaterial(new Color(0.08f, 0.06f, 0.08f, 1f), null, 0.8f);
        }
    }

    protected static GameObject CreatePart(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(type);
        var col = g.GetComponent<Collider>();
        if (col) Object.DestroyImmediate(col);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        if (rot.HasValue) g.transform.localRotation = rot.Value;
        var r = g.GetComponent<MeshRenderer>();
        if (r && mat) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }

    /// <summary>
    /// Builds a stylized Spiky Pinecone hurdle that tumbles down the tree bark.
    /// Replaces the purple bug with a convenient, predictable, and genius natural hurdle!
    /// </summary>
    public static GameObject CreatePineconePrefab(Transform parent)
    {
        EnsureMaterials();
        var root = new GameObject("Pinecone_Hurdle");
        if (parent) root.transform.SetParent(parent, false);

        var pComp = root.AddComponent<TCPinecone>();
        pComp.enemyType = TCEnemyType.Pinecone;
        pComp.radius = 0.603f;

        // Container placed on trunk surface
        var mObj = new GameObject("Model");
        mObj.transform.SetParent(root.transform, false);
        mObj.transform.localPosition = new Vector3(0, 0, 0.603f);
        pComp.model = mObj.transform;

        // Pinecone Rolling Core
        var roller = new GameObject("Roller");
        roller.transform.SetParent(mObj.transform, false);
        pComp.roller = roller.transform;

        // Central egg-shaped pinecone body
        CreatePart(PrimitiveType.Sphere, roller.transform, Vector3.zero, new Vector3(0.36f, 0.44f, 0.36f), pineconeWoodMat);

        // Wooden Stem at top
        CreatePart(PrimitiveType.Cylinder, roller.transform, new Vector3(0, 0.25f, 0), new Vector3(0.06f, 0.12f, 0.06f), pineconeScaleMat, Quaternion.Euler(15f, 0, 0));

        // Radiating sharp wooden scales (3 rings of 6 scales each = 18 spiky scales around core)
        for (int ring = 0; ring < 3; ring++)
        {
            float ringY = (ring - 1) * 0.11f;
            float ringScale = ring == 1 ? 1.05f : 0.88f;
            float ringRadius = 0.18f * ringScale;
            int count = 6;
            for (int k = 0; k < count; k++)
            {
                float ang = (k / (float)count) * Mathf.PI * 2f + (ring * 0.45f);
                float sx = Mathf.Cos(ang) * ringRadius;
                float sz = Mathf.Sin(ang) * ringRadius;
                var scalePos = new Vector3(sx, ringY, sz);

                // Sharp pointed scale
                var scaleObj = CreatePart(PrimitiveType.Capsule, roller.transform, scalePos, new Vector3(0.08f, 0.16f, 0.08f) * ringScale, pineconeScaleMat);
                Vector3 outwardDir = new Vector3(sx, 0.04f, sz).normalized;
                scaleObj.transform.localRotation = Quaternion.LookRotation(outwardDir, Vector3.up);
            }
        }

        // Trigger Collider for collision
        var col = mObj.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.32f;
        col.center = Vector3.zero;

        root.tag = "branch"; // safe fallback for tag queries while TCEnemy component handles custom interaction
        root.SetActive(false);
        return root;
    }

    /// <summary>
    /// Builds a cute stylized hovering Wasp / Hornet enemy with animated flapping wings and pumping stinger.
    /// </summary>
    public static GameObject CreateWaspPrefab(Transform parent)
    {
        EnsureMaterials();
        var root = new GameObject("Wasp_Enemy");
        if (parent) root.transform.SetParent(parent, false);

        var wComp = root.AddComponent<TCWasp>();
        wComp.enemyType = TCEnemyType.Wasp;
        wComp.radius = 0.68f;

        var mObj = new GameObject("Model");
        mObj.transform.SetParent(root.transform, false);
        mObj.transform.localPosition = new Vector3(0, 0, 0.68f);
        wComp.model = mObj.transform;

        // Thorax
        CreatePart(PrimitiveType.Sphere, mObj.transform, new Vector3(0, 0.05f, 0), new Vector3(0.24f, 0.22f, 0.24f), waspBlackMat);

        // Head
        var head = CreatePart(PrimitiveType.Sphere, mObj.transform, new Vector3(0, 0.06f, 0.15f), new Vector3(0.20f, 0.18f, 0.20f), waspBlackMat);
        head.name = "Head";
        wComp.head = head.transform;

        CreatePart(PrimitiveType.Sphere, head.transform, new Vector3(-0.35f, 0.2f, 0.5f), new Vector3(0.36f, 0.36f, 0.36f), eyeGlowMat);
        CreatePart(PrimitiveType.Sphere, head.transform, new Vector3(0.35f, 0.2f, 0.5f), new Vector3(0.36f, 0.36f, 0.36f), eyeGlowMat);

        // Striped Abdomen (Yellow & Black with Texture) - animated pump
        var abdObj = new GameObject("Abdomen_Pivot");
        abdObj.transform.SetParent(mObj.transform, false);
        abdObj.transform.localPosition = new Vector3(0, 0.02f, -0.08f);
        wComp.abdomen = abdObj.transform;

        var abdMesh = CreatePart(PrimitiveType.Capsule, abdObj.transform, new Vector3(0, -0.06f, -0.12f), new Vector3(0.22f, 0.32f, 0.22f), waspBodyMat, Quaternion.Euler(-32f, 0, 0));
        // Sharp black stinger at the apex
        CreatePart(PrimitiveType.Cylinder, abdMesh.transform, new Vector3(0, -0.60f, 0), new Vector3(0.20f, 0.30f, 0.20f), waspBlackMat);

        // Wings (flapped via TCWasp script at 70Hz)
        var wingPivotL = new GameObject("WingPivot_L").transform;
        wingPivotL.SetParent(mObj.transform, false);
        wingPivotL.localPosition = new Vector3(-0.08f, 0.14f, 0.02f);
        var wingMeshL = CreatePart(PrimitiveType.Cube, wingPivotL, new Vector3(-0.16f, 0, 0), new Vector3(0.34f, 0.008f, 0.15f), waspWingMat);
        wComp.wingL = wingPivotL;

        var wingPivotR = new GameObject("WingPivot_R").transform;
        wingPivotR.SetParent(mObj.transform, false);
        wingPivotR.localPosition = new Vector3(0.08f, 0.14f, 0.02f);
        var wingMeshR = CreatePart(PrimitiveType.Cube, wingPivotR, new Vector3(0.16f, 0, 0), new Vector3(0.34f, 0.008f, 0.15f), waspWingMat);
        wComp.wingR = wingPivotR;

        // Trigger Collider
        var col = mObj.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.28f;
        col.center = new Vector3(0, 0.04f, 0);

        root.tag = "bees"; // safe fallback for tag queries while TCEnemy handles custom logic
        root.SetActive(false);
        return root;
    }

    /// <summary>
    /// Builds a cute stylized Squirrel obstacle that scampers head-first downwards along the tree trunk.
    /// Features procedural running animation: pumping legs, bobbing head, and bouncy bushy tail!
    /// </summary>
    public static GameObject CreateSquirrelPrefab(Transform parent)
    {
        EnsureMaterials();
        var root = new GameObject("Squirrel_Enemy");
        if (parent) root.transform.SetParent(parent, false);

        var sComp = root.AddComponent<TCSquirrel>();
        sComp.enemyType = TCEnemyType.Squirrel;
        sComp.radius = 0.603f;

        // Container placed on trunk surface
        var mObj = new GameObject("Model");
        mObj.transform.SetParent(root.transform, false);
        mObj.transform.localPosition = new Vector3(0, 0, 0.603f);
        sComp.model = mObj.transform;

        // Squirrel Body Pivot (facing downwards along tree)
        var bodyPivot = new GameObject("BodyPivot");
        bodyPivot.transform.SetParent(mObj.transform, false);
        bodyPivot.transform.localRotation = Quaternion.Euler(180f, 0, 0); // Face downward!
        sComp.bodyPivot = bodyPivot.transform;

        // Torso / Body (chestnut fur)
        var torso = CreatePart(PrimitiveType.Capsule, bodyPivot.transform, new Vector3(0, 0.08f, 0), new Vector3(0.24f, 0.38f, 0.22f), squirrelFurMat);
        // Cream belly patch
        CreatePart(PrimitiveType.Sphere, torso.transform, new Vector3(0, 0, 0.42f), new Vector3(0.72f, 0.85f, 0.35f), squirrelBellyMat);

        // Head Pivot
        var headPivot = new GameObject("HeadPivot");
        headPivot.transform.SetParent(bodyPivot.transform, false);
        headPivot.transform.localPosition = new Vector3(0, 0.28f, 0.05f);
        sComp.head = headPivot.transform;

        // Head sphere
        CreatePart(PrimitiveType.Sphere, headPivot.transform, Vector3.zero, new Vector3(0.24f, 0.22f, 0.24f), squirrelFurMat);
        // Snout
        CreatePart(PrimitiveType.Sphere, headPivot.transform, new Vector3(0, -0.04f, 0.12f), new Vector3(0.12f, 0.10f, 0.14f), squirrelBellyMat);
        // Nose (dark brown)
        CreatePart(PrimitiveType.Sphere, headPivot.transform, new Vector3(0, -0.02f, 0.19f), new Vector3(0.045f, 0.04f, 0.045f), squirrelDarkMat);
        // Cute dark eyes
        CreatePart(PrimitiveType.Sphere, headPivot.transform, new Vector3(-0.09f, 0.04f, 0.09f), new Vector3(0.045f, 0.045f, 0.045f), squirrelEyeMat);
        CreatePart(PrimitiveType.Sphere, headPivot.transform, new Vector3(0.09f, 0.04f, 0.09f), new Vector3(0.045f, 0.045f, 0.045f), squirrelEyeMat);
        // Cute ears
        CreatePart(PrimitiveType.Capsule, headPivot.transform, new Vector3(-0.08f, 0.13f, -0.02f), new Vector3(0.05f, 0.09f, 0.04f), squirrelDarkMat, Quaternion.Euler(15f, 0, -20f));
        CreatePart(PrimitiveType.Capsule, headPivot.transform, new Vector3(0.08f, 0.13f, -0.02f), new Vector3(0.05f, 0.09f, 0.04f), squirrelDarkMat, Quaternion.Euler(15f, 0, 20f));

        // Bushy Tail Pivot (attached at lower back)
        var tailPivot = new GameObject("TailPivot");
        tailPivot.transform.SetParent(bodyPivot.transform, false);
        tailPivot.transform.localPosition = new Vector3(0, -0.10f, -0.08f);
        sComp.tail = tailPivot.transform;

        // Big curved fluffy bushy tail made of overlapping rounded segments
        CreatePart(PrimitiveType.Sphere, tailPivot.transform, new Vector3(0, 0.12f, -0.10f), new Vector3(0.20f, 0.26f, 0.22f), squirrelFurMat);
        CreatePart(PrimitiveType.Sphere, tailPivot.transform, new Vector3(0, 0.28f, -0.14f), new Vector3(0.24f, 0.32f, 0.26f), squirrelFurMat);
        CreatePart(PrimitiveType.Sphere, tailPivot.transform, new Vector3(0, 0.44f, -0.08f), new Vector3(0.22f, 0.26f, 0.22f), squirrelDarkMat);

        // Legs (Front & Back, Left & Right)
        sComp.legFL = CreateLeg(bodyPivot.transform, new Vector3(-0.13f, 0.22f, 0.04f), true);
        sComp.legFR = CreateLeg(bodyPivot.transform, new Vector3(0.13f, 0.22f, 0.04f), true);
        sComp.legBL = CreateLeg(bodyPivot.transform, new Vector3(-0.14f, -0.04f, 0.02f), false);
        sComp.legBR = CreateLeg(bodyPivot.transform, new Vector3(0.14f, -0.04f, 0.02f), false);

        // Trigger Collider for collision & jump/smash detection
        var col = mObj.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.32f;
        col.center = Vector3.zero;

        root.tag = "branch"; // safe fallback for tag queries while TCEnemy handles custom interaction
        root.SetActive(false);
        return root;
    }

    static Transform CreateLeg(Transform parent, Vector3 localPos, bool isFront)
    {
        var pivot = new GameObject(isFront ? "LegFrontPivot" : "LegBackPivot").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = localPos;
        // Upper leg / paw capsule
        CreatePart(PrimitiveType.Capsule, pivot, new Vector3(0, -0.06f, 0.04f), new Vector3(0.065f, 0.14f, 0.065f), squirrelDarkMat, Quaternion.Euler(20f, 0, 0));
        return pivot;
    }
}

/// <summary>
/// Spiky Pinecone: tumbles down a specific lane on the trunk bark!
/// Highly convenient and predictable (stays in its lane).
/// Seamlessly engages all 3 controls:
/// 1) Steer around into another lane
/// 2) Jump over it as it rolls underneath
/// 3) Roll Attack into it to smash it to pieces for bonus coins!
/// </summary>
public class TCPinecone : TCEnemy
{
    public Transform roller;
    public float rollDownSpeed = 0.85f;
    float currentRollAngle;

    public override void Init(float startY, float startAngle, float sweep = 40f, float spd = 2.2f)
    {
        base.Init(startY, startAngle, sweep, spd);
        rollDownSpeed = Random.Range(0.70f, 1.05f);
        currentRollAngle = 0f;
    }

    void Update()
    {
        if (isSmashed) return;
        float dt = Time.deltaTime;

        // 1. Tumble smoothly down the trunk in its designated lane
        y -= rollDownSpeed * dt;

        // 2. End-over-end rolling rotation as it rolls along the rough bark
        currentRollAngle -= rollDownSpeed * 380f * dt;
        if (roller)
        {
            roller.localRotation = Quaternion.Euler(currentRollAngle, 0, 0);
        }

        // 3. Subtle rhythmic hop bounce on the textured tree bark
        if (model)
        {
            float bounce = Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.035f;
            model.localPosition = new Vector3(0, bounce, radius);
        }

        transform.position = new Vector3(0, y, 0);
        transform.rotation = Quaternion.Euler(0, baseAngle, 0);
    }
}

/// <summary>
/// Darting Wasp / Hornet: hovering aerial hazard.
/// Fully animated with 70Hz fluttering wings, rhythmic stinger abdomen pumping, and 3D figure-8 hover!
/// </summary>
public class TCWasp : TCEnemy
{
    public Transform wingL, wingR;
    public Transform abdomen, head;

    void Update()
    {
        if (isSmashed) return;
        float t = Time.time;

        // 1. High-frequency 70Hz wing flutter with pitch stroke angling
        float flap = Mathf.Sin(t * 70f) * 44f;
        float strokePitch = Mathf.Cos(t * 70f) * 16f;
        if (wingL) wingL.localRotation = Quaternion.Euler(strokePitch, 0, flap + 12f);
        if (wingR) wingR.localRotation = Quaternion.Euler(strokePitch, 0, -flap - 12f);

        // 2. Realistic wasp stinger abdomen pumping / curling
        if (abdomen)
        {
            float pump = Mathf.Sin(t * 7.5f + phase) * 20f;
            abdomen.localRotation = Quaternion.Euler(-26f + pump, 0, 0);
        }

        // 3. Head twitching scan
        if (head)
        {
            float twitch = Mathf.Sin(t * 3.2f) * 6f;
            head.localRotation = Quaternion.Euler(twitch, Mathf.Sin(t * 5.4f) * 8f, 0);
        }

        // 4. Natural 3D Figure-8 Hover & banking motion
        float hoverY = y + Mathf.Sin(t * 3.8f + phase) * 0.18f;
        float swayAngle = baseAngle + Mathf.Sin(t * 2.0f + phase) * (sweepAngle * 0.45f);
        float bankRoll = Mathf.Cos(t * 2.0f + phase) * -14f;
        float pitchBob = Mathf.Cos(t * 3.8f + phase) * 8f;

        transform.position = new Vector3(0, hoverY, 0);
        transform.rotation = Quaternion.Euler(pitchBob, swayAngle, bankRoll);
    }
}

/// <summary>
/// Animated Squirrel: scampers swiftly head-first down the trunk!
/// Features full quadruped running animation:
/// - Fast alternating front and hind leg scampering
/// - Rhythmic body pitch and spine flexion
/// - Dynamic springy bushy tail waving
/// - Alert head bobbing
/// </summary>
public class TCSquirrel : TCEnemy
{
    public Transform bodyPivot;
    public Transform head, tail;
    public Transform legFL, legFR, legBL, legBR;
    public float runSpeed = 1.6f;

    public override void Init(float startY, float startAngle, float sweep = 40f, float spd = 2.2f)
    {
        base.Init(startY, startAngle, sweep, spd);
        runSpeed = Random.Range(1.4f, 1.9f);
    }

    void Update()
    {
        if (isSmashed) return;
        float dt = Time.deltaTime;
        float t = Time.time;

        // 1. Scamper down the tree trunk
        y -= runSpeed * dt;

        // 2. High-speed running leg cycle (quadruped diagonal gait)
        float runFreq = 18f;
        float legCycle = t * runFreq;
        float angleFL = Mathf.Sin(legCycle) * 38f;
        float angleFR = -Mathf.Sin(legCycle) * 38f;
        float angleBL = -Mathf.Sin(legCycle) * 42f;
        float angleBR = Mathf.Sin(legCycle) * 42f;

        if (legFL) legFL.localRotation = Quaternion.Euler(angleFL, 0, 0);
        if (legFR) legFR.localRotation = Quaternion.Euler(angleFR, 0, 0);
        if (legBL) legBL.localRotation = Quaternion.Euler(angleBL, 0, 0);
        if (legBR) legBR.localRotation = Quaternion.Euler(angleBR, 0, 0);

        // 3. Body sprint bounce & gallop rhythm
        float gallop = Mathf.Abs(Mathf.Sin(legCycle * 0.5f));
        if (bodyPivot)
        {
            float pitch = 180f + Mathf.Sin(legCycle) * 7f;
            float roll = Mathf.Sin(legCycle * 0.5f) * 4f;
            bodyPivot.localRotation = Quaternion.Euler(pitch, 0, roll);
        }

        // 4. Bushy tail springy wave & counter-balance
        if (tail)
        {
            float tailWhip = Mathf.Sin(legCycle - 1.2f) * 22f;
            float tailSway = Mathf.Cos(legCycle * 0.5f) * 12f;
            tail.localRotation = Quaternion.Euler(tailWhip + 15f, tailSway, 0);
        }

        // 5. Alert head bobbing
        if (head)
        {
            float headBob = Mathf.Sin(legCycle * 0.5f) * 6f;
            head.localRotation = Quaternion.Euler(headBob - 10f, 0, 0);
        }

        // 6. Trunk surface positioning with scamper hop
        if (model)
        {
            float hop = gallop * 0.04f;
            model.localPosition = new Vector3(0, hop, radius);
        }

        transform.position = new Vector3(0, y, 0);
        transform.rotation = Quaternion.Euler(0, baseAngle, 0);
    }
}
