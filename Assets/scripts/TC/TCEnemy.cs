using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        TCHitFlash.FlashAndDestroy(gameObject, 0.12f);

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
    static Material squirrelMat;

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
        var shader = Shader.Find("TallCrawl/SpiderPreBaked")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Mobile/Diffuse")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Diffuse");
        if (!shader) return null;
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
            var shader = Shader.Find("Mobile/Particles/Alpha Blended")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Transparent/Diffuse")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");
            if (shader != null)
            {
                waspWingMat = new Material(shader);
                waspWingMat.shader = shader;
                waspWingMat.color = new Color(0.92f, 0.96f, 1f, 0.75f);
                if (tex != null) waspWingMat.mainTexture = tex;
            }
        }

        if (!squirrelMat)
        {
            var tex = LoadTexture("Squirrel_LowPoly");
            squirrelMat = CreateCelMaterial(Color.white, tex, 0.0f, Color.clear);
        }
        if (squirrelMat)
        {
            if (squirrelMat.HasProperty("_Brightness")) squirrelMat.SetFloat("_Brightness", 1.0f);
            if (squirrelMat.HasProperty("_EdgeThreshold")) squirrelMat.SetFloat("_EdgeThreshold", 0.0f); // NO edge stroke / black sides
            if (squirrelMat.HasProperty("_CelIntensity")) squirrelMat.SetFloat("_CelIntensity", 0.0f);   // NO directional shadow cut
            if (squirrelMat.HasProperty("_OutlineWidth")) squirrelMat.SetFloat("_OutlineWidth", 0.0f);   // NO inverted-hull outline
            if (squirrelMat.HasProperty("_ShadowDull")) squirrelMat.SetFloat("_ShadowDull", 1.0f);       // Receive NO shadows (100% lit)
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
        if (r)
        {
            if (mat) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
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

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = false;
        audio.spatialBlend = 0.6f;
        audio.minDistance = 1.0f;
        audio.maxDistance = 12.0f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        wComp.audioSrc = audio;

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

    // ---------------- Low-Poly Squirrel Mesh Loader & Direct Parser ----------------

    static readonly Dictionary<string, Mesh> squirrelMeshCache = new Dictionary<string, Mesh>();

    static Mesh GetSquirrelMesh(string partName)
    {
        if (squirrelMeshCache.TryGetValue(partName, out var cached) && cached)
            return cached;

        Mesh m = null;
#if UNITY_EDITOR
        m = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TC/Resources/Enemies/Squirrel_" + partName + ".obj");
        if (!m)
        {
            var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TC/Resources/Enemies/Squirrel_" + partName + ".obj");
            if (go)
            {
                var mf = go.GetComponentInChildren<MeshFilter>();
                if (mf) m = mf.sharedMesh;
            }
        }
#endif
        if (!m)
        {
            var resGo = Resources.Load<GameObject>("Enemies/Squirrel_" + partName);
            if (resGo)
            {
                var mf = resGo.GetComponentInChildren<MeshFilter>();
                if (mf) m = mf.sharedMesh;
            }
        }
        if (!m) m = Resources.Load<Mesh>("Enemies/Squirrel_" + partName);

        if (!m)
        {
            string path = System.IO.Path.Combine(Application.dataPath, "TC/Resources/Enemies/Squirrel_" + partName + ".obj");
            m = ParseObjDirect(path);
        }

        if (m) squirrelMeshCache[partName] = m;
        return m;
    }

    static Mesh ParseObjDirect(string path)
    {
        if (!System.IO.File.Exists(path)) return null;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        var lines = System.IO.File.ReadAllLines(path);
        foreach (var rawLine in lines)
        {
            var l = rawLine.Trim();
            if (l.StartsWith("v "))
            {
                var parts = l.Substring(2).Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                    verts.Add(new Vector3(float.Parse(parts[0], CultureInfo.InvariantCulture),
                                          float.Parse(parts[1], CultureInfo.InvariantCulture),
                                          float.Parse(parts[2], CultureInfo.InvariantCulture)));
            }
            else if (l.StartsWith("vt "))
            {
                var parts = l.Substring(3).Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    uvs.Add(new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture),
                                        float.Parse(parts[1], CultureInfo.InvariantCulture)));
            }
            else if (l.StartsWith("vn "))
            {
                var parts = l.Substring(3).Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                    normals.Add(new Vector3(float.Parse(parts[0], CultureInfo.InvariantCulture),
                                            float.Parse(parts[1], CultureInfo.InvariantCulture),
                                            float.Parse(parts[2], CultureInfo.InvariantCulture)));
            }
            else if (l.StartsWith("f "))
            {
                var parts = l.Substring(2).Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                var idxs = new List<int>();
                for (int i = 0; i < parts.Length; i++)
                {
                    var s = parts[i].Split('/')[0];
                    if (int.TryParse(s, out int idx))
                        idxs.Add(idx - 1);
                }
                if (idxs.Count == 3)
                {
                    tris.Add(idxs[0]); tris.Add(idxs[1]); tris.Add(idxs[2]);
                }
                else if (idxs.Count == 4)
                {
                    tris.Add(idxs[0]); tris.Add(idxs[1]); tris.Add(idxs[2]);
                    tris.Add(idxs[0]); tris.Add(idxs[2]); tris.Add(idxs[3]);
                }
            }
        }

        var m = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        m.SetVertices(verts);
        if (uvs.Count == verts.Count) m.SetUVs(0, uvs);
        if (normals.Count == verts.Count) m.SetNormals(normals);
        m.SetTriangles(tris, 0);
        if (normals.Count != verts.Count) m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static GameObject CreateMeshPart(string name, Mesh mesh, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        if (rot.HasValue) go.transform.localRotation = rot.Value;

        var mf = go.AddComponent<MeshFilter>();
        if (mesh) mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        if (mat) mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    static Transform CreateLegPart(string name, Mesh legMesh, Transform parent, Vector3 localPos)
    {
        var pivot = new GameObject(name + "_Pivot").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = localPos;
        CreateMeshPart(name + "_Model", legMesh, pivot, Vector3.zero, Vector3.one, squirrelMat);
        return pivot;
    }

    /// <summary>
    /// Builds an authentic stylized Low-Poly Squirrel obstacle that scampers down the trunk facing downward.
    /// Uses dedicated 3D low-poly meshes and a rich hand-painted fur & face texture atlas.
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

        // Squirrel Body Pivot:
        // Local +Z points DOWN the tree trunk (-Y in world).
        // Local +Y points RADIAL OUTWARD away from bark (+Z in world).
        // Local -Y points at the bark.
        // Local +/- X points around trunk circumference.
        var bodyPivot = new GameObject("BodyPivot");
        bodyPivot.transform.SetParent(mObj.transform, false);
        bodyPivot.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        sComp.bodyPivot = bodyPivot.transform;

        // 1. Torso / Body low-poly mesh (sleek arched back, cream belly, rich chestnut fur)
        var bodyMesh = GetSquirrelMesh("Body");
        CreateMeshPart("Torso", bodyMesh, bodyPivot.transform, Vector3.zero, Vector3.one, squirrelMat);

        // 2. Head Pivot & low-poly mesh (chubby cheeks, pointed snout, perked ears, shiny eyes)
        // Positioned at the leading front (+Z) of the body, facing downward where it heads
        var headPivot = new GameObject("HeadPivot");
        headPivot.transform.SetParent(bodyPivot.transform, false);
        headPivot.transform.localPosition = new Vector3(0, 0.05f, 0.20f);
        headPivot.transform.localRotation = Quaternion.Euler(10f, 0, 0);
        sComp.head = headPivot.transform;

        var headMesh = GetSquirrelMesh("Head");
        CreateMeshPart("HeadModel", headMesh, headPivot.transform, Vector3.zero, Vector3.one, squirrelMat);

        // 3. Iconic Bushy Tail Pivot & low-poly plume mesh (S-curved plume arching over back)
        var tailPivot = new GameObject("TailPivot");
        tailPivot.transform.SetParent(bodyPivot.transform, false);
        tailPivot.transform.localPosition = new Vector3(0, 0.03f, -0.22f);
        sComp.tail = tailPivot.transform;

        var tailMesh = GetSquirrelMesh("Tail");
        CreateMeshPart("TailModel", tailMesh, tailPivot.transform, Vector3.zero, Vector3.one, squirrelMat);

        // 4. Four Running Low-Poly Legs & Gripping Paws
        var legFLMesh = GetSquirrelMesh("LegFL");
        var legFRMesh = GetSquirrelMesh("LegFR");
        var legBLMesh = GetSquirrelMesh("LegBL");
        var legBRMesh = GetSquirrelMesh("LegBR");

        sComp.legFL = CreateLegPart("LegFL", legFLMesh, bodyPivot.transform, new Vector3(-0.11f, 0.01f, 0.12f));
        sComp.legFR = CreateLegPart("LegFR", legFRMesh, bodyPivot.transform, new Vector3( 0.11f, 0.01f, 0.12f));
        sComp.legBL = CreateLegPart("LegBL", legBLMesh, bodyPivot.transform, new Vector3(-0.14f, 0.04f, -0.11f));
        sComp.legBR = CreateLegPart("LegBR", legBRMesh, bodyPivot.transform, new Vector3( 0.14f, 0.04f, -0.11f));

        // Trigger Collider for collision & jump/smash detection
        var col = mObj.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.34f;
        col.center = new Vector3(0, 0, 0.08f);

        root.tag = "branch";
        root.SetActive(false);
        return root;
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
    public AudioSource audioSrc;

    bool hasPlayedBuzz;
    static player cachedPlayer;

    void Awake()
    {
        if (!audioSrc) audioSrc = GetComponent<AudioSource>();
        if (audioSrc) audioSrc.loop = false;
    }

    void OnEnable()
    {
        if (!audioSrc) audioSrc = GetComponent<AudioSource>();
        if (audioSrc) audioSrc.loop = false;
    }

    public override void Init(float startY, float startAngle, float sweep = 40f, float spd = 2.2f)
    {
        base.Init(startY, startAngle, sweep, spd);
        hasPlayedBuzz = false;
        if (!audioSrc) audioSrc = GetComponent<AudioSource>();
        if (audioSrc)
        {
            var clip = TCSfx.Get("wasp");
            if (clip && audioSrc.clip != clip) audioSrc.clip = clip;
            audioSrc.loop = false;
            audioSrc.volume = 0.85f;
        }
    }

    void OnDisable()
    {
        if (audioSrc && audioSrc.isPlaying) audioSrc.Stop();
    }

    public override void Smash()
    {
        if (audioSrc && audioSrc.isPlaying) audioSrc.Stop();
        base.Smash();
    }

    void PlayBuzz()
    {
        var clip = TCSfx.Get("wasp");
        if (clip)
        {
            if (audioSrc)
            {
                audioSrc.loop = false;
                audioSrc.volume = 0.85f;
                audioSrc.PlayOneShot(clip, 0.85f);
            }
            else
            {
                TCSfx.Play("wasp", 0.85f);
            }
        }
    }

    void Update()
    {
        if (isSmashed) return;
        float t = Time.time;

        // Proximity buzz warning sound when spider approaches within 4.5m - plays ONCE, never in a loop
        if (!hasPlayedBuzz)
        {
            if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
            if (cachedPlayer != null)
            {
                float dy = y - cachedPlayer.transform.position.y;
                if (dy > -0.5f && dy < 4.5f)
                {
                    hasPlayedBuzz = true;
                    PlayBuzz();
                }
            }
        }

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

    float delayTimer = 0f;
    bool hasPlayedSquirrelSound;
    ParticleSystem warningPS;
    GameObject warningGO;
    float warningAngleDeg;

    static player cachedPlayer;
    static playerInteract cachedInteract;

    static bool IsPlayerInChilliMode()
    {
        if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
        if (cachedPlayer != null)
        {
            if (cachedPlayer.boost) return true;
            if (!cachedInteract) cachedInteract = cachedPlayer.GetComponentInChildren<playerInteract>();
            if (cachedInteract != null && cachedInteract.boostDuration > 0f) return true;
        }
        return false;
    }

    static float GetPlayerY()
    {
        if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
        return cachedPlayer != null ? cachedPlayer.transform.position.y : 0f;
    }

    static float GetTopCameraViewY(float laneAngleDeg)
    {
        Camera cam = Camera.main;
        if (!cam)
        {
            if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
            if (cachedPlayer && cachedPlayer.maincam) cam = cachedPlayer.maincam;
        }

        if (cam != null)
        {
            // Viewport point (0.5, 1.02) corresponds to the top center of the camera screen
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 1.02f, 0f));
            float dx = ray.direction.x;
            float dz = ray.direction.z;
            float ox = ray.origin.x;
            float oz = ray.origin.z;
            float a = dx * dx + dz * dz;
            float b = 2f * (ox * dx + oz * dz);
            float c = ox * ox + oz * oz - (0.603f * 0.603f);
            float disc = b * b - 4f * a * c;
            if (a > 0.0001f && disc >= 0f)
            {
                float t1 = (-b - Mathf.Sqrt(disc)) / (2f * a);
                float t2 = (-b + Mathf.Sqrt(disc)) / (2f * a);
                float t = (t1 > 0f) ? t1 : t2;
                if (t > 0f)
                {
                    return ray.origin.y + ray.direction.y * t;
                }
            }
        }
        return GetPlayerY() + 5.8f;
    }

    public override void Init(float startY, float startAngle, float sweep = 40f, float spd = 2.2f)
    {
        base.Init(startY, startAngle, sweep, spd);
        runSpeed = Random.Range(1.4f, 1.9f);
        hasPlayedSquirrelSound = false;

        // Clean up any previous warning particles if recycled from pool
        if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); warningPS = null; }
        if (warningGO) { Object.Destroy(warningGO); warningGO = null; }

        // If player is already in chilli boost mode, start scampering immediately, play sound, and DO NOT spawn leaves
        if (IsPlayerInChilliMode())
        {
            delayTimer = 0f;
            if (model) model.gameObject.SetActive(true);
            SetCollidersActive(true);
            hasPlayedSquirrelSound = true;
            TCSfx.Play("squirrel", 0.8f);
        }
        else
        {
            delayTimer = 2.0f; // Leaves fall from top of camera view 2.0s before squirrel arrives
            if (model) model.gameObject.SetActive(false);
            // Disable hit colliders while hidden so no premature collision can happen during the advance warning
            SetCollidersActive(false);
            SpawnFallingLeavesWarning(startAngle);
        }
    }

    void SetCollidersActive(bool active)
    {
        var cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i]) cols[i].enabled = active;
        }
    }

    static Texture2D _leafTex;
    static Material _leafMat;

    public static Texture2D GenerateLeafTexture() => GetLeafTexture();

    static Texture2D GetLeafTexture()
    {
        if (_leafTex) return _leafTex;
        const int W = 64;
        const int H = 64;
        _leafTex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            name = "TC_ProceduralLeaf",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float ny = (y - 4f) / (H - 8f); // 0 at stem base, 1 at leaf tip
            for (int x = 0; x < W; x++)
            {
                float nx = (x - W * 0.5f) / (W * 0.5f); // -1 to 1 across leaf width
                if (ny <= 0f || ny >= 1f)
                {
                    px[y * W + x] = Color.clear;
                    continue;
                }

                // Natural pointed oval leaf profile: rounded belly at ny ~ 0.38, tapering to tip
                float maxWidth = Mathf.Sin(ny * Mathf.PI) * (1.1f - ny * 0.45f);
                float absX = Mathf.Abs(nx);
                if (absX > maxWidth)
                {
                    px[y * W + x] = Color.clear;
                    continue;
                }

                float edgeDist = 1f - (absX / Mathf.Max(0.01f, maxWidth));
                float alpha = Mathf.Clamp01(edgeDist * 4f);

                // Leaf center vein line
                float vein = Mathf.Clamp01(Mathf.Abs(nx) * 12f);
                float shade = Mathf.Lerp(0.82f, 1.0f, vein);

                px[y * W + x] = new Color(shade, shade, shade, alpha);
            }
        }
        _leafTex.SetPixels(px);
        _leafTex.Apply(false, true);
        return _leafTex;
    }

    static Material GetLeafMaterial()
    {
        if (_leafMat) return _leafMat;
        Shader sh = Shader.Find("TallCrawl/FX Particle")
            ?? Shader.Find("Mobile/Particles/Alpha Blended")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Unlit/Transparent");
        _leafMat = new Material(sh) { name = "TC_SquirrelLeavesMat" };
        var tex = GetLeafTexture();
        if (tex)
        {
            _leafMat.mainTexture = tex;
            if (_leafMat.HasProperty("_BaseMap")) _leafMat.SetTexture("_BaseMap", tex);
        }
        if (_leafMat.HasProperty("_Additive")) _leafMat.SetFloat("_Additive", 0f);
        if (_leafMat.HasProperty("_DepthPull")) _leafMat.SetFloat("_DepthPull", 0.32f);
        return _leafMat;
    }

    public override void Smash()
    {
        if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmitting); warningPS = null; }
        if (warningGO) { Object.Destroy(warningGO, 2.0f); warningGO = null; }
        base.Smash();
    }

    void OnDisable()
    {
        if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmitting); warningPS = null; }
        if (warningGO) { Object.Destroy(warningGO); warningGO = null; }
    }

    static GameObject warningPrefab;

    void SpawnFallingLeavesWarning(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float r = radius + 0.04f;
        float topY = GetTopCameraViewY(angleDeg);
        Vector3 spawnWorldPos = new Vector3(Mathf.Sin(rad) * r, topY, Mathf.Cos(rad) * r);
        Vector3 outward = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        Quaternion spawnWorldRot = Quaternion.LookRotation(outward, Vector3.up);

        // Load prefab from Resources
        if (!warningPrefab)
        {
            warningPrefab = Resources.Load<GameObject>("SquirrelLeavesWarning");
#if UNITY_EDITOR
            if (!warningPrefab)
                warningPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/SquirrelLeavesWarning.prefab");
#endif
        }

        if (!warningPrefab) return;

        // Instantiate pure prefab directly with zero runtime property alterations
        GameObject go = Object.Instantiate(warningPrefab, spawnWorldPos, spawnWorldRot);
        go.name = "SquirrelLeavesWarning";
        go.transform.SetParent(null);
        warningGO = go;
        warningAngleDeg = angleDeg;

        var ps = go.GetComponent<ParticleSystem>();
        warningPS = ps;
        if (ps)
        {
            var sh = ps.shape;
            sh.position = new Vector3(-0.10f, 0f, 0f);
            ps.Play(true);
        }
        Object.Destroy(go, 12.0f);
    }

    void Update()
    {
        if (isSmashed) return;
        float dt = Time.deltaTime;
        bool isChilli = IsPlayerInChilliMode();

        // Never show leaves during chilli mode: clear immediately if player enters chilli mode
        if (isChilli)
        {
            if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); warningPS = null; }
            if (warningGO) { Object.Destroy(warningGO); warningGO = null; }
            delayTimer = 0f;
            if (model && !model.gameObject.activeSelf) model.gameObject.SetActive(true);
            SetCollidersActive(true);
            if (!hasPlayedSquirrelSound)
            {
                hasPlayedSquirrelSound = true;
                TCSfx.Play("squirrel", 0.8f);
            }
        }

        if (delayTimer > 0f)
        {
            // If the player is in chilli mode or has climbed close to the squirrel, immediately wake up and scamper down
            float distToPlayer = y - GetPlayerY();
            if (isChilli || distToPlayer < 3.5f)
            {
                delayTimer = 0f;
            }
            else
            {
                delayTimer -= dt;
            }

            if (delayTimer <= 0f)
            {
                delayTimer = 0f;
                // Cease warning particle emission when the squirrel arrives from top of camera view
                if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmitting); warningPS = null; }
                if (warningGO) { Object.Destroy(warningGO, 2.5f); warningGO = null; }
                if (model) model.gameObject.SetActive(true);
                SetCollidersActive(true);
                if (!hasPlayedSquirrelSound)
                {
                    hasPlayedSquirrelSound = true;
                    TCSfx.Play("squirrel", 0.8f);
                }
            }
            else
            {
                // While waiting during advance warning, keep the falling leaves emitter positioned vertically at top of camera view
                if (warningGO)
                {
                    float rad = warningAngleDeg * Mathf.Deg2Rad;
                    float r = radius + 0.04f;
                    float topY = GetTopCameraViewY(warningAngleDeg);
                    warningGO.transform.position = new Vector3(Mathf.Sin(rad) * r, topY, Mathf.Cos(rad) * r);
                }

                // While waiting during advance warning, ensure model stays hidden so it never appears as a frozen statue
                if (model && model.gameObject.activeSelf) model.gameObject.SetActive(false);
                transform.position = new Vector3(0, y, 0);
                transform.rotation = Quaternion.Euler(0, baseAngle, 0);
                return;
            }
        }

        float t = Time.time;

        // 1. Scamper down the tree trunk (accelerated sprint in chilli mode)
        float moveSpeed = isChilli ? (runSpeed * 1.5f) : runSpeed;
        y -= moveSpeed * dt;

        // 2. High-speed running leg cycle (quadruped diagonal gait, snappy sprint in chilli mode)
        float runFreq = isChilli ? 26f : 18f;
        float legCycle = t * runFreq;
        float angleFL = Mathf.Sin(legCycle) * 38f;
        float angleFR = -Mathf.Sin(legCycle) * 38f;
        float angleBL = -Mathf.Sin(legCycle) * 42f;
        float angleBR = Mathf.Sin(legCycle) * 42f;

        if (legFL) legFL.localRotation = Quaternion.Euler(angleFL, 0, 0);
        if (legFR) legFR.localRotation = Quaternion.Euler(angleFR, 0, 0);
        if (legBL) legBL.localRotation = Quaternion.Euler(angleBL, 0, 0);
        if (legBR) legBR.localRotation = Quaternion.Euler(angleBR, 0, 0);

        // 3. Body sprint gallop rhythm
        float gallop = Mathf.Abs(Mathf.Sin(legCycle * 0.5f));
        if (bodyPivot)
        {
            float pitch = 90f + Mathf.Sin(legCycle) * 6f; // pitched down the trunk
            float roll = Mathf.Sin(legCycle * 0.5f) * 4f;
            bodyPivot.localRotation = Quaternion.Euler(pitch, 0, roll);
        }

        // 4. Bushy tail springy wave & counter-balance (arching over back)
        if (tail)
        {
            float tailWhip = Mathf.Sin(legCycle - 1.2f) * 16f;
            float tailSway = Mathf.Cos(legCycle * 0.5f) * 10f;
            tail.localRotation = Quaternion.Euler(tailWhip, tailSway, 0);
        }

        // 5. Alert head looking downward along heading direction
        if (head)
        {
            float headBob = Mathf.Sin(legCycle * 0.5f) * 5f;
            // Slight downward tilt toward the trunk ahead
            head.localRotation = Quaternion.Euler(headBob + 8f, 0, 0);
        }

        // 6. Trunk surface positioning with scamper hop
        if (model)
        {
            if (!model.gameObject.activeSelf) model.gameObject.SetActive(true);
            float hop = gallop * 0.04f;
            model.localPosition = new Vector3(0, hop, radius);
        }

        transform.position = new Vector3(0, y, 0);
        transform.rotation = Quaternion.Euler(0, baseAngle, 0);
    }
}
