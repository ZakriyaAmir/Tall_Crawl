using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

/// One-click project setup: Tools > Tall Crawl > Setup. Safe to run repeatedly.
public static class TCSetup
{
    const string Res = "Assets/TC/Resources";

    [MenuItem("Tools/Tall Crawl/Setup (assets, skins, scenes, settings)")]
    public static void All()
    {
        Directory.CreateDirectory(Res + "/Skins");
        Textures(); Materials(); Skins(); ConfigAssets(); Prefabs(); MenuScene(); GameScene(); Quality(); Player(); Icons();
        AssetDatabase.SaveAssets();
        Debug.Log("TCSetup done");
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!a) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
        return a;
    }

    static void ConfigAssets()
    {
        LoadOrCreate<TCConfig>(Res + "/TCConfig.asset");
        var a = LoadOrCreate<TCAssets>(Res + "/TCAssets.asset");
        a.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Montserrat-SemiBold SDF.asset");
        a.logo = MenuLogo();
        a.spiderModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/objects/models/spider/source/6daad4c45bdc412cba2ed00f2983c7bd.fbx.fbx");
        a.spiderAnimator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/animations/spider.controller");
        EditorUtility.SetDirty(a); AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/gameplay.unity");
        a = AssetDatabase.LoadAssetAtPath<TCAssets>(Res + "/TCAssets.asset");
        var pi = Object.FindObjectOfType<playerInteract>();
        if (pi) { a.coin = pi.coinSound; a.purchase = pi.healthSound; a.whoosh = null; } // no start whoosh
        var menu = File.ReadAllText("Assets/Scenes/MainMenu.unity");
        // click sound: the clip used on the menu "click" AudioSource
        var clickClip = AssetDatabase.FindAssets("t:AudioClip").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.ToLower().Contains("click"));
        if (clickClip != null) a.click = AssetDatabase.LoadAssetAtPath<AudioClip>(clickClip);
        EditorUtility.SetDirty(a);
    }

    const string IconDir = "Assets/TC/Icon";
    static Sprite MenuLogo()
    {
        var p = IconDir + "/MenuLogo.png";
        AssetDatabase.ImportAsset(p);
        var ti = (TextureImporter)AssetImporter.GetAtPath(p);
        if (ti.textureType != TextureImporterType.Sprite || ti.maxTextureSize != 1024) { ti.textureType = TextureImporterType.Sprite; ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.maxTextureSize = 1024; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    /// App icon (all Android sizes + legacy/round/adaptive) from Assets/TC/Icon/AppIcon.png.
    [MenuItem("Tools/Tall Crawl/Apply App Icon")]
    public static void Icons()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/AppIcon.png");
        var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/AppIconFg.png");
        foreach (var path in new[] { IconDir + "/AppIcon.png", IconDir + "/AppIconFg.png" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path); if (!ti) continue;
            ti.textureType = TextureImporterType.Default; ti.mipmapEnabled = false; ti.maxTextureSize = 1024; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.alphaIsTransparency = true; ti.SaveAndReimport();
        }
        icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/AppIcon.png"); fg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/AppIconFg.png");
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        var android = UnityEditor.Build.NamedBuildTarget.Android;
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(android))
        {
            var icons = PlayerSettings.GetPlatformIcons(android, kind);
            foreach (var pi in icons)
            {
                if (pi.maxLayerCount >= 2) pi.SetTextures(icon, fg); // adaptive: background = full art, empty foreground
                else pi.SetTexture(icon);
            }
            PlayerSettings.SetPlatformIcons(android, kind, icons);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("TCSetup icons applied");
    }

    /// v3 pass: regenerate skin materials, menu logo, app icon (no scene rebuild).
    public static void RefreshV3()
    {
        Directory.CreateDirectory(Res + "/Skins");
        Skins();
        var a = AssetDatabase.LoadAssetAtPath<TCAssets>(Res + "/TCAssets.asset");
        a.logo = MenuLogo(); a.whoosh = null; EditorUtility.SetDirty(a);
        Icons();
        AssetDatabase.SaveAssets();
        Debug.Log("TCSetup RefreshV3 done");
    }

    static void Textures()
    {
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/objects", "Assets/resources", "Assets/Textures", "Assets/UI", "Assets/FarlandSkies/Skyboxes" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (!ti) continue;
            bool sprite = ti.textureType == TextureImporterType.Sprite;
            bool normal = ti.textureType == TextureImporterType.NormalMap;
            bool sky = p.Contains("FarlandSkies");
            int max = normal ? 512 : sky ? 1024 : 1024;
            if (p.EndsWith("TCBG.png") || p.EndsWith("bgBlank.png")) max = 1024;
            ti.maxTextureSize = Mathf.Min(ti.maxTextureSize, max);
            if (ti.maxTextureSize > max) ti.maxTextureSize = max;
            ti.maxTextureSize = max;
            ti.mipmapEnabled = !sprite && !sky;
            if (sky) ti.wrapMode = TextureWrapMode.Clamp;
            var s = ti.GetPlatformTextureSettings("Android");
            s.overridden = true; s.maxTextureSize = max; s.format = sprite ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6; s.compressionQuality = 50;
            ti.SetPlatformTextureSettings(s);
            ti.SaveAndReimport();
        }
    }

    static void Materials()
    {
        foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/objects", "Assets/resources", "Assets/Textures" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            if (!m || !m.shader) continue;
            if (m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported) m.shader = Shader.Find("Standard");
            if (!m.shader.name.StartsWith("Skybox")) m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }
    }

    static void Skins()
    {
        var spiderShader = Shader.Find("TallCrawl/SpiderPreBaked") ?? Shader.Find("Mobile/Diffuse");
        for (int i = 0; i < TCSkins.All.Length; i++)
        {
            var d = TCSkins.All[i];
            string texPath = $"{Res}/Skins/Pattern_{i}.png";
            File.WriteAllBytes(texPath, Pattern(d).EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.maxTextureSize = 256; ti.mipmapEnabled = true;
            var s = ti.GetPlatformTextureSettings("Android"); s.overridden = true; s.maxTextureSize = 256; s.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(s);
            ti.SaveAndReimport();
            var mat = Mat($"{Res}/Skins/Skin_{i}.mat", spiderShader);
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            mat.color = Color.white;
            if (mat.HasProperty("_ShadowDull")) mat.SetFloat("_ShadowDull", 0.55f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (d.emission > 0) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", d.accent * d.emission); }
            else { mat.DisableKeyword("_EMISSION"); if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black); }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            AssetDatabase.DeleteAsset($"{Res}/Skins/Hat_{i}.mat"); // hats/props removed: skins are colour + spikes + shape only
        }
        var trail = Mat($"{Res}/Skins/Trail.mat", Shader.Find("Sprites/Default"));
        EditorUtility.SetDirty(trail);
    }
    static Material Mat(string path, Shader sh)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh; return m;
    }

    static Texture2D Pattern(SkinDef d)
    {
        const int N = 256; var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = x / (float)N, v = y / (float)N; bool acc = false; float k = 0;

            // Custom iconic patterns for Golden, Bee, Wasp and Squirrel skins
            if (d.name == "Golden")
            {
                float d_spine = Mathf.Abs(u - 0.25f);
                if (d_spine > 0.5f) d_spine = 1.0f - d_spine;
                float radiance = Mathf.Exp(-d_spine * 14.0f) * 0.35f;
                float sheen = Mathf.Sin((v * 1.5f + u * 2.0f) * Mathf.PI * 4.0f) * 0.04f;
                Color baseCol = Color.Lerp(new Color(1.0f, 0.88f, 0.10f), new Color(1.0f, 0.98f, 0.72f), radiance);
                float curv = 0.82f + 0.18f * Mathf.Sin(v * Mathf.PI * 0.5f);
                float s = 0.92f + 0.08f * Mathf.Cos(d_spine * Mathf.PI * 2.0f);
                baseCol *= curv * s * (1.0f + sheen);
                baseCol.a = 1f;
                t.SetPixel(x, y, baseCol);
                continue;
            }
            if (d.name == "Bee" || d.name == "Bees")
            {
                float d_spine = Mathf.Abs(u - 0.25f);
                if (d_spine > 0.5f) d_spine = 1.0f - d_spine;
                float wave = Mathf.Sin(d_spine * Mathf.PI * 2.0f) * 0.04f;
                float band_coord = v * 5.2f + wave;
                float band_frac = Mathf.Repeat(band_coord, 1.0f);
                int band_idx = (int)band_coord;
                bool is_dark = (band_idx % 2 == 1);
                float transition = Mathf.Min(band_frac, 1.0f - band_frac) * 6.0f;
                float blend = Mathf.Clamp01(transition);

                Color gold = Color.Lerp(new Color(1f, 0.76f, 0.09f), new Color(0.93f, 0.66f, 0.05f), d_spine * 1.5f);
                Color charcoal = new Color(0.09f, 0.08f, 0.10f);
                Color baseBee;
                if (v < 0.06f || v > 0.91f) baseBee = charcoal;
                else if (is_dark) baseBee = Color.Lerp(gold, charcoal, blend);
                else baseBee = Color.Lerp(charcoal, gold, blend);

                float curv = 0.74f + 0.26f * Mathf.Sin(v * Mathf.PI * 0.5f);
                float s = 0.93f + 0.07f * Mathf.Cos(d_spine * Mathf.PI * 2f);
                baseBee *= curv * s; baseBee.a = 1f;
                t.SetPixel(x, y, baseBee);
                continue;
            }
            if (d.name == "Wasp")
            {
                float d_spine = Mathf.Abs(u - 0.25f);
                if (d_spine > 0.5f) d_spine = 1.0f - d_spine;
                float chevron = d_spine * 0.42f;
                float seg_phase = Mathf.Repeat(v * 6.5f + chevron, 1.0f);
                int seg_idx = (int)(v * 6.5f + chevron);
                bool is_black = (seg_idx % 2 == 1);
                if (d_spine < 0.06f && seg_phase > 0.35f) is_black = true;
                if (!is_black && (d_spine > 0.08f && d_spine < 0.18f) && (seg_phase > 0.35f && seg_phase < 0.65f) && (v > 0.15f && v < 0.85f)) is_black = true;
                if (v < 0.08f || v > 0.88f) is_black = true;

                Color cWasp = is_black ? new Color(0.09f, 0.07f, 0.10f) : (d_spine < 0.08f ? new Color(1f, 0.83f, 0.12f) : new Color(0.97f, 0.73f, 0.04f));
                float curv = 0.72f + 0.28f * Mathf.Sin(v * Mathf.PI * 0.5f);
                float s = 0.92f + 0.08f * Mathf.Cos(d_spine * Mathf.PI * 2f);
                cWasp *= curv * s; cWasp.a = 1f;
                t.SetPixel(x, y, cWasp);
                continue;
            }
            if (d.name == "Squirrel")
            {
                float d_spine = Mathf.Abs(u - 0.25f);
                if (d_spine > 0.5f) d_spine = 1.0f - d_spine;
                float d_belly = Mathf.Abs(u - 0.75f);
                if (d_belly > 0.5f) d_belly = 1.0f - d_belly;

                Color baseCol;
                if (d_belly < 0.15f) baseCol = new Color(1f, 0.96f, 0.90f);
                else if (d_belly < 0.23f)
                {
                    float f = (d_belly - 0.15f) / 0.08f;
                    baseCol = Color.Lerp(new Color(1f, 0.96f, 0.90f), new Color(0.84f, 0.35f, 0.12f), f);
                }
                else if (d_spine < 0.040f && v > 0.10f && v < 0.88f) baseCol = new Color(0.28f, 0.11f, 0.05f);
                else if (d_spine < 0.080f && v > 0.12f && v < 0.86f)
                {
                    float f = Mathf.Abs(d_spine - 0.060f) / 0.020f;
                    baseCol = Color.Lerp(new Color(0.99f, 0.89f, 0.76f), new Color(0.84f, 0.35f, 0.12f), f * 0.4f);
                }
                else if (d_spine < 0.115f && v > 0.15f && v < 0.82f) baseCol = new Color(0.41f, 0.16f, 0.08f);
                else baseCol = v < 0.4f ? new Color(0.88f, 0.38f, 0.13f) : new Color(0.82f, 0.33f, 0.11f);

                if (v > 0.80f)
                {
                    float tailF = (v - 0.80f) / 0.20f;
                    baseCol = Color.Lerp(baseCol, new Color(0.45f, 0.18f, 0.08f), tailF * 0.65f);
                }

                float curv = 0.76f + 0.24f * Mathf.Sin(v * Mathf.PI * 0.5f);
                float s = 0.94f + 0.06f * Mathf.Cos(d_spine * Mathf.PI * 2f);
                baseCol *= curv * s; baseCol.a = 1f;
                t.SetPixel(x, y, baseCol);
                continue;
            }
            if (d.name == "Ladybug")
            {
                float d_spine = Mathf.Abs(u - 0.25f);
                if (d_spine > 0.5f) d_spine = 1.0f - d_spine;

                // 7 distinct black spots on red elytra
                Vector3[] spots = new Vector3[] {
                    new Vector3(0.25f, 0.19f, 0.045f),
                    new Vector3(0.25f - 0.11f, 0.35f, 0.048f),
                    new Vector3(0.25f + 0.11f, 0.35f, 0.048f),
                    new Vector3(0.25f - 0.075f, 0.58f, 0.050f),
                    new Vector3(0.25f + 0.075f, 0.58f, 0.050f),
                    new Vector3(0.25f - 0.115f, 0.77f, 0.044f),
                    new Vector3(0.25f + 0.115f, 0.77f, 0.044f),
                };

                float min_d = 999.0f;
                for (int sp = 0; sp < spots.Length; sp++)
                {
                    float du = u - spots[sp].x;
                    if (du > 0.5f) du -= 1.0f;
                    else if (du < -0.5f) du += 1.0f;
                    float dist = Mathf.Sqrt((du * 1.5f) * (du * 1.5f) + (v - spots[sp].y) * (v - spots[sp].y));
                    float delta = dist - spots[sp].z;
                    if (delta < min_d) min_d = delta;
                }

                bool is_seam = (d_spine < 0.008f) && (v > 0.10f && v < 0.88f);
                bool is_collar = (v < 0.07f) || (v > 0.91f);
                bool is_belly = d_spine > 0.28f;

                Color redCol = new Color(0.93f, 0.15f, 0.15f);
                Color blackCol = new Color(0.08f, 0.07f, 0.09f);
                Color cLady;

                if (is_collar || is_belly || is_seam) cLady = blackCol;
                else if (min_d < 0) cLady = blackCol;
                else if (min_d < 0.012f)
                {
                    float t_edge = min_d / 0.012f;
                    cLady = Color.Lerp(blackCol, redCol, t_edge);
                }
                else cLady = redCol;

                float curv = 0.75f + 0.25f * Mathf.Sin(v * Mathf.PI * 0.5f);
                float s = 0.94f + 0.06f * Mathf.Cos(d_spine * Mathf.PI * 2.0f);
                cLady *= curv * s; cLady.a = 1f;
                t.SetPixel(x, y, cLady);
                continue;
            }

            switch (d.pattern)
            {
                case SkinPattern.Stripes: acc = Mathf.Repeat(v * 8, 1) < 0.45f; break;
                case SkinPattern.Dots: acc = Vector2.Distance(new Vector2(Mathf.Repeat(u * 8, 1), Mathf.Repeat(v * 8, 1)), Vector2.one * 0.5f) < 0.22f; break;
                case SkinPattern.Checker: acc = ((int)(u * 8) + (int)(v * 8)) % 2 == 0; break;
                case SkinPattern.Zigzag: acc = Mathf.Repeat(v * 6 + Mathf.Abs(Mathf.Repeat(u * 8, 1) - 0.5f), 1) < 0.4f; break;
                case SkinPattern.Gradient: k = v; break;
            }
            Color c = d.pattern == SkinPattern.Gradient ? Color.Lerp(d.body, d.accent, k) : acc ? d.accent : d.body;
            if (d.pattern == SkinPattern.Solid) c = d.body;

            // Pre-baked lighting & curvature:
            // Medium, balanced tone: top has warm ambient light, underside has soft baked roll-off,
            // so spiders look richly toned without looking overblown or too bright.
            float curvature = 0.68f + 0.22f * Mathf.Sin(v * Mathf.PI * 0.5f);
            float side = 0.94f + 0.06f * Mathf.Sin(u * Mathf.PI);
            float layer = 1f - 0.04f * Mathf.Pow(Mathf.Abs(Mathf.Sin(v * Mathf.PI * 34f)), 6f); // faint 3D-print layer lines
            c *= curvature * side * layer;

            c.a = 1; t.SetPixel(x, y, c);
        }
        t.Apply(); return t;
    }

    static void Prefabs()
    {
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefabs" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var root = PrefabUtility.LoadPrefabContents(p);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = p.Contains("coin") || p.Contains("bees") || p.Contains("Dust") ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = false;
                if (r is ParticleSystemRenderer pr && pr.trailMaterial == null) { var mats = pr.sharedMaterials; if (mats.Length > 1) pr.sharedMaterials = new[] { mats[0] }; }
            }
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.None; }
            PrefabUtility.SaveAsPrefabAsset(root, p);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void MenuScene()
    {
        var sc = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var old = GameObject.Find("Canvas"); if (old) Object.DestroyImmediate(old);
        var mgr = GameObject.Find("Manager");
        foreach (var t in Object.FindObjectsOfType<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        if (!mgr.GetComponent<TCMenuUI>()) mgr.AddComponent<TCMenuUI>();
        var ads = Object.FindObjectOfType<AdsManager>(true);
        if (ads) { var so = new SerializedObject(ads); var tm = so.FindProperty("testMode"); if (tm != null) tm.boolValue = true; so.ApplyModifiedProperties(); }
        var iap = Object.FindObjectOfType<UnityIAP>(true);
        if (iap) { iap.kProductIDs = new[] { "com.zakriya.tallcrawl.coins_1000", "com.zakriya.tallcrawl.noads", "com.zakriya.tallcrawl.premium" }; EditorUtility.SetDirty(iap); }
        var cam = Camera.main; if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = TCUI.Hex("#FFE3D3"); }
        EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
    }

    static void GameScene()
    {
        var sc = EditorSceneManager.OpenScene("Assets/Scenes/gameplay.unity");
        foreach (var t in Object.FindObjectsOfType<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        var gm = Object.FindObjectOfType<gameManager>(true); var sys = gm.gameObject;
        for (int i = sys.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(sys.transform.GetChild(i).gameObject);
        sys.name = "GameSystems";
        var stem = GameObject.Find("level/stem").transform;
        for (int i = stem.childCount - 1; i >= 0; i--) Object.DestroyImmediate(stem.GetChild(i).gameObject);
        var track = sys.GetComponent<TCTrack>() ?? sys.AddComponent<TCTrack>();
        P(ref track.trunkPrefab, "treeChunk"); P(ref track.branchPrefab, "branchParent"); P(ref track.beesPrefab, "beesParent");
        P(ref track.coinPrefab, "coin"); P(ref track.heartPrefab, "healthParent"); P(ref track.boostPrefab, "boostParent");
        track.stemParent = stem; track.hazardParent = GameObject.Find("level/branches").transform; track.bonusParent = GameObject.Find("level/bonus").transform;
        track.player = Object.FindObjectOfType<player>().transform;
        if (!sys.GetComponent<TCGameUI>()) sys.AddComponent<TCGameUI>();
        var refl = GameObject.Find("player/PlayerReflection"); if (refl) refl.SetActive(false);
        var pl = Object.FindObjectOfType<player>(); pl.p1 = pl.transform; EditorUtility.SetDirty(pl);
        foreach (var l in Object.FindObjectsOfType<Light>(true))
        {
            if (l.type == LightType.Directional)
            {
                l.shadows = LightShadows.Soft; l.shadowStrength = 0.5f; l.intensity = 1.25f;
                l.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                var anim = l.GetComponent<Animator>(); if (anim) anim.enabled = false;
            }
            else { l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForceVertex; }
        }
        foreach (var smr in Object.FindObjectsOfType<SkinnedMeshRenderer>(true)) { smr.quality = SkinQuality.Bone2; smr.updateWhenOffscreen = false; }
        var spider = Object.FindObjectOfType<playerInteract>().GetComponent<Rigidbody>(); if (spider) { spider.isKinematic = true; spider.useGravity = false; }
        EditorUtility.SetDirty(track);
        EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
    }
    static void P(ref GameObject f, string n) { f = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/" + n + ".prefab"); }

    static void Quality()
    {
        int cur = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.vSyncCount = 0; QualitySettings.pixelLightCount = 1; QualitySettings.shadows = ShadowQuality.HardOnly;
            QualitySettings.shadowDistance = 12; QualitySettings.shadowResolution = ShadowResolution.Medium; QualitySettings.shadowCascades = 1;
            QualitySettings.antiAliasing = 2; QualitySettings.softParticles = false; QualitySettings.skinWeights = SkinWeights.TwoBones;
            QualitySettings.globalTextureMipmapLimit = 0; QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        }
        QualitySettings.SetQualityLevel(cur, false);
        Time.fixedDeltaTime = 0.02f;
    }

    static void Player()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)35;
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.gpuSkinning = true;
        PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.Android.bundleVersionCode = Mathf.Max(PlayerSettings.Android.bundleVersionCode + 1, 10);
        var m = typeof(PlayerSettings).GetMethod("SetBatchingForPlatform", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        m?.Invoke(null, new object[] { BuildTarget.Android, 1, 1 });
    }

    public static void BuildAndroid()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = false;
        var r = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/gameplay.unity" }, "/workspace/TallCrawl-debug.apk", BuildTarget.Android, BuildOptions.Development);
        Debug.Log("BUILD RESULT " + r.summary.result + " size " + r.summary.totalSize);
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    public static void BuildCapture()
    {
        var r = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/gameplay.unity" }, "/workspace/tc_cap/tc.x86_64", BuildTarget.StandaloneLinux64, BuildOptions.None);
        Debug.Log("CAPTURE BUILD " + r.summary.result);
    }
}
