using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TCPowerUpPrefabBaker
{
    const string PrefabsDir = "Assets/prefabs";
    const string ResourcesDir = "Assets/Resources/PowerUps";
    const string MatsDir = "Assets/Resources/PowerUps/Materials";
    const string MeshesDir = "Assets/Resources/PowerUps/Meshes";
    const string TexturesDir = "Assets/Resources/PowerUps/Textures";

    const int CurrentBakeVersion = 4; // Incremented for circular glow additions

    static TCPowerUpPrefabBaker()
    {
        EditorApplication.delayCall += () =>
        {
            int bakedVersion = EditorPrefs.GetInt("TCPowerUpBaker_Version", 0);
            string greenMatPath = $"{MatsDir}/Glow_Health_Green.mat";
            if (bakedVersion < CurrentBakeVersion || !File.Exists(greenMatPath) ||
                !File.Exists($"{PrefabsDir}/Magnet_Pickup_Root.prefab") ||
                !File.Exists($"{PrefabsDir}/Cooldown_Pickup_Root.prefab"))
            {
                Bake();
                EditorPrefs.SetInt("TCPowerUpBaker_Version", CurrentBakeVersion);
            }
        };
    }

    [MenuItem("Tools/Tall Crawl/Bake All PowerUp Prefabs & Glows")]
    public static void Bake()
    {
        Directory.CreateDirectory(PrefabsDir);
        Directory.CreateDirectory(ResourcesDir);
        Directory.CreateDirectory(MatsDir);
        Directory.CreateDirectory(MeshesDir);
        Directory.CreateDirectory(TexturesDir);

        BakeGlowAssets();
        BakeMagnetMaterialsAndMesh();
        BakeCooldownMaterialsAndMesh();

        BakeMagnet();
        BakeCooldown();
        BakeHealthGlow();
        BakeBoostGlow();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TCPowerUpPrefabBaker] All PowerUp prefabs, materials, meshes, and circular glows successfully baked!");
    }

    #region Glow Assets (Texture, Mesh, Materials)

    static void BakeGlowAssets()
    {
        // 1. Ensure Glow Circle Texture import settings
        string texPath = $"{TexturesDir}/GlowCircle_256.png";
        if (File.Exists(texPath))
        {
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                bool modified = false;
                if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; modified = true; }
                if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; modified = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; modified = true; }
                if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; modified = true; }
                if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; modified = true; }
                if (modified) importer.SaveAndReimport();
            }
        }
        var glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // 2. Build and save Circular Glow Disc Mesh
        string meshPath = $"{MeshesDir}/Glow_Disc_Mesh.asset";
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        var newMesh = BuildGlowDiscMesh(40, 0.5f);
        if (!existingMesh)
        {
            newMesh.name = "Glow_Disc_Mesh";
            AssetDatabase.CreateAsset(newMesh, meshPath);
        }
        else
        {
            existingMesh.Clear();
            existingMesh.vertices = newMesh.vertices;
            existingMesh.normals = newMesh.normals;
            existingMesh.uv = newMesh.uv;
            existingMesh.colors = newMesh.colors;
            existingMesh.triangles = newMesh.triangles;
            existingMesh.RecalculateBounds();
            EditorUtility.SetDirty(existingMesh);
        }

        // 3. Save the 4 vibrant glow materials:
        // Health: Green
        GetOrCreateGlowMat("Glow_Health_Green", new Color(0.12f, 1.0f, 0.35f, 0.75f), glowTex);
        // Boost: Red
        GetOrCreateGlowMat("Glow_Boost_Red", new Color(1.0f, 0.16f, 0.12f, 0.80f), glowTex);
        // Cooldown: Blue
        GetOrCreateGlowMat("Glow_Cooldown_Blue", new Color(0.20f, 0.78f, 1.0f, 0.75f), glowTex);
        // Magnet: Yellow
        GetOrCreateGlowMat("Glow_Magnet_Yellow", new Color(1.0f, 0.88f, 0.12f, 0.80f), glowTex);
    }

    /// <summary>
    /// Builds a smooth planar circular disc mesh in XY with double-sided faces,
    /// radial vertex colors (alpha = 1.0 at center, alpha = 0.0 at rim), and standard UVs.
    /// </summary>
    public static Mesh BuildGlowDiscMesh(int segments = 40, float radius = 0.5f)
    {
        var mesh = new Mesh { name = "Glow_Disc_Mesh" };
        int totalVertsPerSide = segments + 1;
        var verts = new Vector3[totalVertsPerSide * 2];
        var norms = new Vector3[totalVertsPerSide * 2];
        var uvs = new Vector2[totalVertsPerSide * 2];
        var colors = new Color[totalVertsPerSide * 2];
        var tris = new List<int>();

        // Front Face (+Z normal)
        verts[0] = Vector3.zero;
        norms[0] = Vector3.forward;
        uvs[0] = new Vector2(0.5f, 0.5f);
        colors[0] = new Color(1f, 1f, 1f, 1f);

        float angleStep = 360f / segments * Mathf.Deg2Rad;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            int idx = i + 1;
            verts[idx] = new Vector3(cos * radius, sin * radius, 0f);
            norms[idx] = Vector3.forward;
            uvs[idx] = new Vector2(0.5f + 0.5f * cos, 0.5f + 0.5f * sin);
            colors[idx] = new Color(1f, 1f, 1f, 0f);

            int next = (i + 1) % segments + 1;
            tris.Add(0);
            tris.Add(next);
            tris.Add(idx);
        }

        // Back Face (-Z normal, reverse winding)
        int backOffset = totalVertsPerSide;
        verts[backOffset] = Vector3.zero;
        norms[backOffset] = -Vector3.forward;
        uvs[backOffset] = new Vector2(0.5f, 0.5f);
        colors[backOffset] = new Color(1f, 1f, 1f, 1f);

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            int idx = backOffset + i + 1;
            verts[idx] = new Vector3(cos * radius, sin * radius, 0f);
            norms[idx] = -Vector3.forward;
            uvs[idx] = new Vector2(0.5f + 0.5f * cos, 0.5f + 0.5f * sin);
            colors[idx] = new Color(1f, 1f, 1f, 0f);

            int next = backOffset + (i + 1) % segments + 1;
            tris.Add(backOffset);
            tris.Add(idx);
            tris.Add(next);
        }

        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Material GetOrCreateGlowMat(string name, Color tintColor, Texture2D glowTex)
    {
        string path = $"{MatsDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        // Find best built-in additive/unlit shader with tint color support
        string[] candidates = new string[]
        {
            "Legacy Shaders/Particles/Additive",
            "Particles/Standard Unlit",
            "Sprites/Default",
            "Mobile/Particles/Additive"
        };
        Shader shader = null;
        foreach (var c in candidates)
        {
            shader = Shader.Find(c);
            if (shader != null) break;
        }
        if (shader == null) shader = Shader.Find("Standard");

        if (!mat)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        if (glowTex)
        {
            mat.mainTexture = glowTex;
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", glowTex);
        }
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tintColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", tintColor);

        if (shader.name.Contains("Standard Unlit"))
        {
            mat.SetFloat("_Mode", 2); // Additive
            mat.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    #endregion

    #region Standard Materials

    static Material GetOrCreateStandardMat(string name, Color col, Color emissionCol, float smoothness = 0.85f, float metallic = 0.1f)
    {
        string path = $"{MatsDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");
        if (!mat)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.color = col;
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

        if (emissionCol != Color.black)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionCol);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    #endregion

    #region Magnet

    static void BakeMagnetMaterialsAndMesh()
    {
        GetOrCreateStandardMat("Magnet_CandyRed", new Color(1.0f, 0.10f, 0.22f, 1f), new Color(1.0f, 0.12f, 0.25f) * 1.5f, 0.92f, 0.2f);
        GetOrCreateStandardMat("Magnet_CandyBlue", new Color(0.04f, 0.46f, 1.0f, 1f), new Color(0.05f, 0.50f, 1.0f) * 1.5f, 0.92f, 0.2f);
        GetOrCreateStandardMat("Magnet_Chrome", new Color(0.92f, 0.95f, 1.0f, 1f), new Color(0.40f, 0.45f, 0.55f) * 0.8f, 0.96f, 0.95f);
        GetOrCreateStandardMat("Magnet_WhiteEmblem", new Color(0.98f, 0.98f, 1.0f, 1f), Color.white * 0.8f, 0.85f, 0.1f);

        string meshPath = $"{MeshesDir}/Single_U_Horseshoe_Magnet.asset";
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        var genMesh = TCCoinMagnet.GetOrCreateSingleUMagnetMesh();
        if (!existingMesh)
        {
            var savedMesh = Object.Instantiate(genMesh);
            savedMesh.name = "Single_U_Horseshoe_Magnet";
            AssetDatabase.CreateAsset(savedMesh, meshPath);
        }
    }

    static void BakeMagnet()
    {
        var redMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/Magnet_CandyRed.mat");
        var blueMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/Magnet_CandyBlue.mat");
        var chromeMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/Magnet_Chrome.mat");
        var whiteMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/Magnet_WhiteEmblem.mat");
        var uMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesDir}/Single_U_Horseshoe_Magnet.asset");
        var glowMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesDir}/Glow_Disc_Mesh.asset");
        var yellowGlowMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/Glow_Magnet_Yellow.mat");

        var root = new GameObject("Magnet_Pickup_Root");
        var item = new GameObject("MagnetItem");
        item.transform.SetParent(root.transform, false);
        item.transform.localPosition = new Vector3(0, 0, 0.603f);
        item.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        item.transform.localScale = Vector3.one * 0.45f;
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

        var model = new GameObject("Magnet_Visual_Mesh");
        model.transform.SetParent(item.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        float archRadius = 0.16f;
        float tubeRadius = 0.050f;
        float legBottomY = -0.16f;

        // 1. Single Continuous U-Shaped Magnet Body
        var uBody = new GameObject("U_Magnet_Body");
        uBody.transform.SetParent(model.transform, false);
        var mf = uBody.AddComponent<MeshFilter>();
        mf.sharedMesh = uMesh;
        var mr = uBody.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new Material[] { blueMat, redMat };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // 2. Left Pole Tip (North - Silver / Chrome Cap & Pole Shoe)
        CreatePrimitivePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(-archRadius, legBottomY - 0.025f, 0f),
            new Vector3(tubeRadius * 2f, 0.025f, tubeRadius * 2f),
            chromeMat
        );
        CreatePrimitivePart(PrimitiveType.Sphere, model.transform,
            new Vector3(-archRadius, legBottomY - 0.050f, 0f),
            Vector3.one * (tubeRadius * 2f),
            chromeMat
        );
        BuildLetterBadge(model.transform, new Vector3(-archRadius, -0.075f, tubeRadius + 0.003f), 'N', whiteMat);

        // 3. Right Pole Tip (South - Silver / Chrome Cap & Pole Shoe)
        CreatePrimitivePart(PrimitiveType.Cylinder, model.transform,
            new Vector3(archRadius, legBottomY - 0.025f, 0f),
            new Vector3(tubeRadius * 2f, 0.025f, tubeRadius * 2f),
            chromeMat
        );
        CreatePrimitivePart(PrimitiveType.Sphere, model.transform,
            new Vector3(archRadius, legBottomY - 0.050f, 0f),
            Vector3.one * (tubeRadius * 2f),
            chromeMat
        );
        BuildLetterBadge(model.transform, new Vector3(archRadius, -0.075f, tubeRadius + 0.003f), 'S', whiteMat);

        // 4. Subtle shining pulse animator
        var shine = model.AddComponent<TCMagnetShine>();
        shine.redMat = redMat;
        shine.blueMat = blueMat;
        shine.chromeMat = chromeMat;

        // 5. Circular Yellow Glow behind Magnet
        var glowObj = new GameObject("CircularGlow_Yellow");
        glowObj.transform.SetParent(root.transform, false);
        glowObj.transform.localPosition = new Vector3(0f, 0f, 0.585f);
        glowObj.transform.localRotation = Quaternion.identity;
        glowObj.transform.localScale = Vector3.one * 0.70f;
        var glowMf = glowObj.AddComponent<MeshFilter>();
        glowMf.sharedMesh = glowMesh;
        var glowMr = glowObj.AddComponent<MeshRenderer>();
        glowMr.sharedMaterial = yellowGlowMat;
        glowMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowMr.receiveShadows = false;
        var pg = glowObj.AddComponent<TCPickupGlow>();
        pg.targetPickup = item.transform;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        SavePrefab(root, "Magnet_Pickup_Root");
        Object.DestroyImmediate(root);
    }

    #endregion

    #region Cooldown Snowflake

    static void BakeCooldownMaterialsAndMesh()
    {
        GetOrCreateStandardMat("Cooldown_IceCrystal", new Color(0.45f, 0.85f, 1.0f, 1f), new Color(0.20f, 0.70f, 1.0f) * 1.4f, 0.95f, 0.25f);
        GetOrCreateStandardMat("Cooldown_FrostCore", new Color(0.95f, 0.98f, 1.0f, 1f), new Color(0.85f, 0.95f, 1.0f) * 1.6f, 0.98f, 0.15f);

        string meshPath = $"{MeshesDir}/Snowflake_3D_Mesh.asset";
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (!existingMesh)
        {
            var snowMesh = Build3DSnowflakeMesh();
            snowMesh.name = "Snowflake_3D_Mesh";
            AssetDatabase.CreateAsset(snowMesh, meshPath);
        }
        else
        {
            var newMesh = Build3DSnowflakeMesh();
            existingMesh.Clear();
            existingMesh.vertices = newMesh.vertices;
            existingMesh.normals = newMesh.normals;
            existingMesh.uv = newMesh.uv;
            existingMesh.triangles = newMesh.triangles;
            existingMesh.RecalculateBounds();
            EditorUtility.SetDirty(existingMesh);
        }
    }

    public static Mesh Build3DSnowflakeMesh()
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        void AddBox(Vector3 center, Vector3 size, float rotationZDeg)
        {
            float rad = rotationZDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            Vector3 h = size * 0.5f;
            Vector3[] localCorners = new Vector3[]
            {
                new Vector3(-h.x, -h.y, -h.z),
                new Vector3( h.x, -h.y, -h.z),
                new Vector3( h.x,  h.y, -h.z),
                new Vector3(-h.x,  h.y, -h.z),
                new Vector3(-h.x, -h.y,  h.z),
                new Vector3( h.x, -h.y,  h.z),
                new Vector3( h.x,  h.y,  h.z),
                new Vector3(-h.x,  h.y,  h.z)
            };

            int baseIdx = verts.Count;
            for (int i = 0; i < 8; i++)
            {
                Vector3 lc = localCorners[i];
                float rx = lc.x * cos - lc.y * sin;
                float ry = lc.x * sin + lc.y * cos;
                float rz = lc.z;
                verts.Add(center + new Vector3(rx, ry, rz));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(rx + 0.5f, ry + 0.5f));
            }

            int[] faceTris = new int[]
            {
                0, 2, 1, 0, 3, 2, // back (-Z)
                4, 5, 6, 4, 6, 7, // front (+Z)
                0, 1, 5, 0, 5, 4, // bottom (-Y)
                2, 3, 7, 2, 7, 6, // top (+Y)
                0, 4, 7, 0, 7, 3, // left (-X)
                1, 2, 6, 1, 6, 5  // right (+X)
            };

            for (int i = 0; i < faceTris.Length; i++)
            {
                tris.Add(baseIdx + faceTris[i]);
            }
        }

        // 1. Central faceted hexagonal crystal hub
        for (int i = 0; i < 3; i++)
        {
            AddBox(Vector3.zero, new Vector3(0.18f, 0.18f, 0.08f), i * 60f);
        }

        // 2. 6 Radial Snowflake Arms
        for (int arm = 0; arm < 6; arm++)
        {
            float armAngle = arm * 60f;
            float rad = armAngle * Mathf.Deg2Rad;
            float dx = Mathf.Cos(rad);
            float dy = Mathf.Sin(rad);

            Vector3 spineCenter = new Vector3(dx * 0.27f, dy * 0.27f, 0f);
            AddBox(spineCenter, new Vector3(0.046f, 0.42f, 0.055f), armAngle - 90f);

            Vector3 tipCenter = new Vector3(dx * 0.49f, dy * 0.49f, 0f);
            AddBox(tipCenter, new Vector3(0.060f, 0.060f, 0.070f), armAngle - 45f);

            Vector3 innerStem = new Vector3(dx * 0.21f, dy * 0.21f, 0f);
            float barbInnerDist = 0.085f;
            float leftInnerAngle = armAngle + 30f;
            float rightInnerAngle = armAngle - 30f;
            Vector3 leftInnerCenter = innerStem + new Vector3(Mathf.Cos(leftInnerAngle * Mathf.Deg2Rad), Mathf.Sin(leftInnerAngle * Mathf.Deg2Rad), 0f) * barbInnerDist;
            Vector3 rightInnerCenter = innerStem + new Vector3(Mathf.Cos(rightInnerAngle * Mathf.Deg2Rad), Mathf.Sin(rightInnerAngle * Mathf.Deg2Rad), 0f) * barbInnerDist;
            AddBox(leftInnerCenter, new Vector3(0.034f, 0.17f, 0.045f), leftInnerAngle - 90f);
            AddBox(rightInnerCenter, new Vector3(0.034f, 0.17f, 0.045f), rightInnerAngle - 90f);

            Vector3 outerStem = new Vector3(dx * 0.35f, dy * 0.35f, 0f);
            float barbOuterDist = 0.065f;
            float leftOuterAngle = armAngle + 30f;
            float rightOuterAngle = armAngle - 30f;
            Vector3 leftOuterCenter = outerStem + new Vector3(Mathf.Cos(leftOuterAngle * Mathf.Deg2Rad), Mathf.Sin(leftOuterAngle * Mathf.Deg2Rad), 0f) * barbOuterDist;
            Vector3 rightOuterCenter = outerStem + new Vector3(Mathf.Cos(rightOuterAngle * Mathf.Deg2Rad), Mathf.Sin(rightOuterAngle * Mathf.Deg2Rad), 0f) * barbOuterDist;
            AddBox(leftOuterCenter, new Vector3(0.030f, 0.13f, 0.045f), leftOuterAngle - 90f);
            AddBox(rightOuterCenter, new Vector3(0.030f, 0.13f, 0.045f), rightOuterAngle - 90f);
        }

        var mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void BakeCooldown()
    {
        var crystalMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/Cooldown_IceCrystal.mat");
        var coreMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/Cooldown_FrostCore.mat");
        var snowMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesDir}/Snowflake_3D_Mesh.asset");
        var glowMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesDir}/Glow_Disc_Mesh.asset");
        var blueGlowMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/Glow_Cooldown_Blue.mat");

        var root = new GameObject("Cooldown_Pickup_Root");
        var item = new GameObject("CooldownItem");
        item.transform.SetParent(root.transform, false);
        item.transform.localPosition = new Vector3(0, 0, 0.603f);
        item.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        item.transform.localScale = Vector3.one * 0.45f;
        item.tag = "cooldown";

        var rb = item.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.mass = 0.0000001f;

        var col = item.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.85f, 0.85f, 0.4f);

        var rot = item.AddComponent<continuousRotation>();
        rot.y = true;
        rot.speed = 90f;

        var model = new GameObject("VisualModel");
        model.transform.SetParent(item.transform, false);

        // 1. 3D Snowflake Model with Ice Crystal Material
        var snowObj = new GameObject("Snowflake_3D");
        snowObj.transform.SetParent(model.transform, false);
        snowObj.transform.localPosition = Vector3.zero;
        snowObj.transform.localRotation = Quaternion.identity;
        var mf = snowObj.AddComponent<MeshFilter>();
        mf.sharedMesh = snowMesh;
        var mr = snowObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = crystalMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // 2. Glowing Frost Core Diamond in the Center
        var coreObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        coreObj.name = "CenterFrostDiamond";
        var colCore = coreObj.GetComponent<Collider>();
        if (colCore) Object.DestroyImmediate(colCore);
        coreObj.transform.SetParent(model.transform, false);
        coreObj.transform.localPosition = Vector3.zero;
        coreObj.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        coreObj.transform.localScale = Vector3.one * 0.16f;
        var mrCore = coreObj.GetComponent<MeshRenderer>();
        mrCore.sharedMaterial = coreMat;
        mrCore.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrCore.receiveShadows = false;

        // 3. Circular Blue Glow behind Snowflake
        var glowObj = new GameObject("CircularGlow_Blue");
        glowObj.transform.SetParent(root.transform, false);
        glowObj.transform.localPosition = new Vector3(0f, 0f, 0.585f);
        glowObj.transform.localRotation = Quaternion.identity;
        glowObj.transform.localScale = Vector3.one * 0.70f;
        var glowMf = glowObj.AddComponent<MeshFilter>();
        glowMf.sharedMesh = glowMesh;
        var glowMr = glowObj.AddComponent<MeshRenderer>();
        glowMr.sharedMaterial = blueGlowMat;
        glowMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowMr.receiveShadows = false;
        var pg = glowObj.AddComponent<TCPickupGlow>();
        pg.targetPickup = item.transform;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        SavePrefab(root, "Cooldown_Pickup_Root");
        Object.DestroyImmediate(root);
    }

    #endregion

    #region Health & Boost Glow Baking

    static void BakeHealthGlow()
    {
        string path = $"{PrefabsDir}/Health_Pickup_Root.prefab";
        if (!File.Exists(path)) return;

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var oldGlow = root.transform.Find("CircularGlow_Green");
            if (oldGlow) Object.DestroyImmediate(oldGlow.gameObject);

            var heart = root.transform.Find("Heart");
            var glowMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesDir}/Glow_Disc_Mesh.asset");
            var greenGlowMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/Glow_Health_Green.mat");

            var glowObj = new GameObject("CircularGlow_Green");
            glowObj.transform.SetParent(root.transform, false);
            glowObj.transform.localPosition = new Vector3(0f, -0.105f, 0.5464f);
            glowObj.transform.localRotation = Quaternion.identity;
            glowObj.transform.localScale = Vector3.one * 0.8f;

            var mf = glowObj.AddComponent<MeshFilter>();
            mf.sharedMesh = glowMesh;
            var mr = glowObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = greenGlowMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var pg = glowObj.AddComponent<TCPickupGlow>();
            pg.pulseSpeed = 0f;
            pg.pulseMagnitude = 0f;
            if (heart)
            {
                pg.targetPickup = heart;
                heart.localPosition = new Vector3(0f, -0.4105f, 0.6253f);
                heart.localRotation = Quaternion.Euler(0f, 119.2f, 0f);
                heart.localScale = new Vector3(3f, 3f, 3f);

                var anim = heart.GetComponent<Animator>();
                if (anim) Object.DestroyImmediate(anim);
            }

            SavePrefab(root, "Health_Pickup_Root");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void BakeBoostGlow()
    {
        string path = $"{PrefabsDir}/Chilli_Pickup_Root.prefab";
        if (!File.Exists(path)) return;

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var oldGlow = root.transform.Find("CircularGlow_Red");
            if (oldGlow) Object.DestroyImmediate(oldGlow.gameObject);

            var picante = root.transform.Find("picante");
            var glowMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshesDir}/Glow_Disc_Mesh.asset");
            var redGlowMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/Glow_Boost_Red.mat");

            var glowObj = new GameObject("CircularGlow_Red");
            glowObj.transform.SetParent(root.transform, false);
            glowObj.transform.localPosition = new Vector3(0f, -0.046f, 0.590f);
            glowObj.transform.localRotation = Quaternion.identity;
            glowObj.transform.localScale = Vector3.one * 0.68f;

            var mf = glowObj.AddComponent<MeshFilter>();
            mf.sharedMesh = glowMesh;
            var mr = glowObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = redGlowMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var pg = glowObj.AddComponent<TCPickupGlow>();
            if (picante) pg.targetPickup = picante;

            SavePrefab(root, "Chilli_Pickup_Root");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    #endregion

    #region Helpers

    static void CreatePrimitivePart(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        var g = GameObject.CreatePrimitive(type);
        var col = g.GetComponent<Collider>();
        if (col) Object.DestroyImmediate(col);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        if (r)
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    static void BuildLetterBadge(Transform parent, Vector3 localPos, char letter, Material letterMat)
    {
        var badge = new GameObject("Letter_" + letter);
        badge.transform.SetParent(parent, false);
        badge.transform.localPosition = localPos;

        if (letter == 'N')
        {
            CreatePrimitivePart(PrimitiveType.Cube, badge.transform, new Vector3(-0.010f, 0f, 0f), new Vector3(0.006f, 0.038f, 0.006f), letterMat);
            CreatePrimitivePart(PrimitiveType.Cube, badge.transform, new Vector3(0.010f, 0f, 0f), new Vector3(0.006f, 0.038f, 0.006f), letterMat);
            var diag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = diag.GetComponent<Collider>(); if (col) Object.DestroyImmediate(col);
            diag.transform.SetParent(badge.transform, false);
            diag.transform.localPosition = Vector3.zero;
            diag.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            diag.transform.localScale = new Vector3(0.006f, 0.042f, 0.006f);
            var r = diag.GetComponent<Renderer>();
            if (r) { r.sharedMaterial = letterMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        }
        else if (letter == 'S')
        {
            CreatePrimitivePart(PrimitiveType.Cube, badge.transform, new Vector3(0f, 0.018f, 0f), new Vector3(0.026f, 0.006f, 0.006f), letterMat);
            CreatePrimitivePart(PrimitiveType.Cube, badge.transform, new Vector3(-0.010f, 0.009f, 0f), new Vector3(0.006f, 0.014f, 0.006f), letterMat);
            CreatePrimitivePart(PrimitiveType.Cube, badge.transform, Vector3.zero, new Vector3(0.026f, 0.006f, 0.006f), letterMat);
            CreatePrimitivePart(PrimitiveType.Cube, badge.transform, new Vector3(0.010f, -0.009f, 0f), new Vector3(0.006f, 0.014f, 0.006f), letterMat);
            CreatePrimitivePart(PrimitiveType.Cube, badge.transform, new Vector3(0f, -0.18f, 0f), new Vector3(0.026f, 0.006f, 0.006f), letterMat);
        }
    }

    static void SavePrefab(GameObject root, string name)
    {
        string p1 = $"{PrefabsDir}/{name}.prefab";
        string p2 = $"{ResourcesDir}/{name}.prefab";

        PrefabUtility.SaveAsPrefabAsset(root, p1);
        PrefabUtility.SaveAsPrefabAsset(root, p2);
    }

    #endregion
}
