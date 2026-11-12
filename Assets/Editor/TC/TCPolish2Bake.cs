using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// polish2 bakes. Each step only ADDS/updates its own objects (PickupFX, Lives slots, Shine) and keeps
/// everything else in the prefabs/scenes untouched, so manual editor edits survive (unlike TCUIBake.BakeAll).
/// Batchmode: -executeMethod TCPolish2Bake.All
public static class TCPolish2Bake
{
    const string FxDir = "Assets/TC/Resources/FX", MatDir = "Assets/TC/Resources/FX/Materials", UIDir = "Assets/TC/Resources/TCUI";

    [MenuItem("Tools/Tall Crawl/Polish2/Bake All (FX materials, pickups, lives panel, shine)")]
    public static void All()
    {
        BakeMaterials();
        BakePickups();
        BakeLivesPanel();
        BakeShine();
        BakeHudLayout();
        BakeReadable();
        BakeIcons();
        AssetDatabase.SaveAssets();
        Debug.Log("TCPolish2Bake: done");
    }

    [MenuItem("Tools/Tall Crawl/Polish2/Bake FX Materials")]
    public static void BakeMaterials()
    {
        AssetDatabase.Refresh();
        foreach (var f in Directory.GetFiles(FxDir, "fx_*.png"))
        {
            var p = f.Replace('\\', '/'); var ti = (TextureImporter)AssetImporter.GetAtPath(p); if (!ti) continue;
            string n = Path.GetFileNameWithoutExtension(p);
            ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; ti.mipmapEnabled = true;
            ti.wrapMode = n == "fx_hex" || n == "fx_noise" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Compressed; ti.SaveAndReimport();
        }
        foreach (var n in new[] { "life_heart", "life_heart_empty", "life_glow", "ui_shine" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(UIDir + "/" + n + ".png"); if (!ti) continue;
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
        }
        Directory.CreateDirectory(MatDir);
        foreach (var n in TCFxLib.Names.Concat(new[] { "Shield" }))
        {
            string path = MatDir + "/FX_" + n + ".mat";
            var fresh = TCFxLib.Create(n);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) AssetDatabase.CreateAsset(fresh, path);
            else { m.shader = fresh.shader; m.CopyPropertiesFromMaterial(fresh); EditorUtility.SetDirty(m); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("TCPolish2Bake: materials");
    }

    static readonly (string file, TCPickupKind kind, string tag)[] Pickups =
    {
        ("Chilli_Pickup_Root", TCPickupKind.Chilli, "boost"), ("Health_Pickup_Root", TCPickupKind.Health, "health"),
        ("Cooldown_Pickup_Root", TCPickupKind.SlowMo, "cooldown"), ("Magnet_Pickup_Root", TCPickupKind.Magnet, "magnet"),
    };

    [MenuItem("Tools/Tall Crawl/Polish2/Bake Pickup FX")]
    public static void BakePickups()
    {
        foreach (var dir in new[] { "Assets/prefabs", "Assets/resources/PowerUps" })
            foreach (var p in Pickups)
            {
                string path = dir + "/" + p.file + ".prefab";
                if (!File.Exists(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var item = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(tr => tr.CompareTag(p.tag));
                    if (!item) { Debug.LogWarning("TCPolish2Bake: no tagged item in " + path); continue; }
                    Transform visual = item;
                    foreach (Transform c in item) if (c.GetComponentInChildren<Renderer>(true)) { visual = c; break; }
                    if (p.kind == TCPickupKind.Health) FaceOutward(root.transform, visual);
                    var oldFx = root.transform.Find("PickupFX");
                    if (oldFx) oldFx.gameObject.SetActive(false);
                    NormalizePickupVisual(item, visual, 0.28f);
                    if (p.kind == TCPickupKind.Magnet) PolishMagnetVisual(visual);
                    var rs = item.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && (oldFx == null || !r.transform.IsChildOf(oldFx))).ToArray();
                    var b = rs.Length > 0 ? rs[0].bounds : new Bounds(item.position, Vector3.one * 0.28f);
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    float size = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.y, b.extents.z), 0.10f, 0.20f);
                    var centre = root.transform.InverseTransformPoint(b.center);
                    // retire the old flat glow disc (kept in the prefab, renderer off) - the new halo replaces it
                    foreach (var g in root.GetComponentsInChildren<TCPickupGlow>(true)) { var mr = g.GetComponent<Renderer>(); if (mr) mr.enabled = false; }
                    TCPickupFX.Build(root, p.kind, item, visual, centre, size);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"TCPolish2Bake: pickup {path} size {size:0.00} centre {centre}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
    }

    /// Turns a flat model (heart) so its broad face looks out from the trunk toward the player camera.
    static void FaceOutward(Transform root, Transform visual)
    {
        var mf = visual.GetComponentInChildren<MeshFilter>(true); if (!mf || !mf.sharedMesh) return;
        var sz = mf.sharedMesh.bounds.size; sz = Vector3.Scale(sz, mf.transform.lossyScale);
        Vector3 axis = sz.x <= sz.y && sz.x <= sz.z ? Vector3.right : sz.y <= sz.z ? Vector3.up : Vector3.forward;
        var w = mf.transform.TransformDirection(axis);
        if (Vector3.Dot(w, root.forward) < 0) w = -w;
        visual.rotation = Quaternion.FromToRotation(w, root.forward) * visual.rotation;
        Debug.Log("TCPolish2Bake: heart thin axis " + axis + " size " + sz);
    }

    static Transform FindPath(string path)
    {
        var parts = path.Split('/');
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            var t = root.transform;
            for (int i = 1; i < parts.Length && t; i++) t = t.Find(parts[i]);
            if (t) return t;
        }
        return null;
    }

    static T Ensure<T>(GameObject g) where T : Component { var c = g.GetComponent<T>(); return c ? c : g.AddComponent<T>(); }
    static RectTransform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (!t) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); t = go.transform; }
        return (RectTransform)t;
    }
    static Image Img(Transform parent, string name, Sprite s, Color c, Vector2 pos, Vector2 size, Vector2 anchor)
    {
        var rt = Child(parent, name);
        var i = Ensure<Image>(rt.gameObject); i.sprite = s; i.color = c; i.raycastTarget = false; i.preserveAspect = true;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        return i;
    }

    [MenuItem("Tools/Tall Crawl/Polish2/Bake Lives Panel")]
    public static void BakeLivesPanel()
    {
        var sc = EditorSceneManager.OpenScene("Assets/Scenes/gameplay.unity");
        var hearts = FindPath("TC_GameUI/Safe/Top/Hearts") as RectTransform;
        if (!hearts) { Debug.LogWarning("TCPolish2Bake: Hearts not found"); return; }
        var full = AssetDatabase.LoadAssetAtPath<Sprite>(UIDir + "/life_heart.png");
        var empty = AssetDatabase.LoadAssetAtPath<Sprite>(UIDir + "/life_heart_empty.png");
        var glow = AssetDatabase.LoadAssetAtPath<Sprite>(UIDir + "/life_glow.png");
        var round = AssetDatabase.LoadAssetAtPath<Sprite>(UIDir + "/round.png");
        bool fresh = !hearts.GetComponent<TCLivesPanel>();
        var lp = Ensure<TCLivesPanel>(hearts.gameObject);
        const int N = 5; const float spacing = 62f, pad = 22f, h = 84f;

        var bg = Child(hearts, "LivesBg"); bg.SetAsFirstSibling();
        var bi = Ensure<Image>(bg.gameObject);
        if (fresh)
        {
            bi.sprite = round; bi.type = Image.Type.Sliced; bi.color = new Color(1, 1, 1, 0.85f); bi.raycastTarget = false;
            bg.anchorMin = bg.anchorMax = new Vector2(0, 0.5f); bg.pivot = new Vector2(0, 0.5f);
            bg.anchoredPosition = new Vector2(0, 0); bg.sizeDelta = new Vector2(pad * 2 + spacing * 3, h);
            var ol = Ensure<Outline>(bg.gameObject); ol.effectColor = new Color(0.29f, 0.23f, 0.25f, 0.35f); ol.effectDistance = new Vector2(4, -4);
        }
        lp.background = bi;
        lp.slots = new RectTransform[N]; lp.hearts = new Image[N]; lp.glows = new Image[N]; lp.ghosts = new Image[N];
        for (int i = 0; i < N; i++)
        {
            var slot = Child(hearts, "Slot" + i);
            if (fresh) { slot.anchorMin = slot.anchorMax = new Vector2(0, 0.5f); slot.pivot = new Vector2(0.5f, 0.5f); slot.anchoredPosition = new Vector2(pad + spacing * (i + 0.5f), 0); slot.sizeDelta = new Vector2(64, 64); }
            var c = new Vector2(0.5f, 0.5f);
            lp.glows[i] = Img(slot, "Glow", glow, new Color(1, 0.3f, 0.35f, 0), Vector2.zero, new Vector2(110, 110), c);
            lp.hearts[i] = Img(slot, "Heart", full, Color.white, Vector2.zero, new Vector2(60, 60), c);
            lp.ghosts[i] = Img(slot, "Ghost", full, new Color(1, 1, 1, 0), Vector2.zero, new Vector2(60, 60), c);
            lp.ghosts[i].enabled = false; lp.glows[i].enabled = false;
            lp.slots[i] = slot;
        }
        lp.full = full; lp.empty = empty; lp.slotSpacing = spacing; lp.padding = pad;
        EditorUtility.SetDirty(lp);
        EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
        Debug.Log("TCPolish2Bake: lives panel baked at " + hearts.anchoredPosition + " size " + hearts.sizeDelta);
    }

    [MenuItem("Tools/Tall Crawl/Polish2/Bake Button Shine")]
    public static void BakeShine()
    {
        var shine = AssetDatabase.LoadAssetAtPath<Sprite>(UIDir + "/ui_shine.png");
        void Do(string scene, params string[] paths)
        {
            var sc = EditorSceneManager.OpenScene(scene);
            foreach (var p in paths)
            {
                var b = FindPath(p) as RectTransform; if (!b) { Debug.LogWarning("TCPolish2Bake: no " + p); continue; }
                var mask = Ensure<Mask>(b.gameObject); mask.showMaskGraphic = true;
                var band = Child(b, "Shine"); band.SetAsLastSibling();
                var i = Ensure<Image>(band.gameObject); i.sprite = shine; i.color = new Color(1, 1, 1, 0.55f); i.raycastTarget = false;
                band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f); band.pivot = new Vector2(0.5f, 0.5f);
                band.sizeDelta = new Vector2(70, b.rect.height * 1.8f); band.localRotation = Quaternion.Euler(0, 0, -18);
                var s = Ensure<TCShine>(b.gameObject); s.band = band;
                EditorUtility.SetDirty(b.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
        }
        Do("Assets/Scenes/MainMenu.unity", "TC_MenuUI/Safe/Play");
        Do("Assets/Scenes/gameplay.unity", "TC_GameUI/Results/Card/Again", "TC_GameUI/Revive/Card/Ad");
        Debug.Log("TCPolish2Bake: shine");
    }

    const float TargetPickupVisual = 0.28f;

    static void NormalizePickupVisual(Transform item, Transform visual, float target)
    {
        if (!visual) return;
        var rs = visual.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
        if (rs.Length == 0) rs = item.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.name != "Halo" && r.name != "Rays").ToArray();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maxDim < 0.001f) return;
        float mul = target / maxDim;
        mul = Mathf.Clamp(mul, 0.2f, 5f);
        visual.localScale *= mul;
        Debug.Log($"TCPolish2Bake: scale {visual.name} x{mul:0.00} (was {maxDim:0.000} -> ~{target})");
    }

    static void PolishMagnetVisual(Transform visual)
    {
        if (!visual) return;
        foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (!r || !r.sharedMaterial) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;
                // Unique instance so we do not dirty other prefabs sharing the source mat.
                m = Object.Instantiate(m);
                m.name = m.name.Replace("(Clone)", "") + "_Pop";
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    var e = m.GetColor("_EmissionColor");
                    if (e.maxColorComponent < 0.2f) e = new Color(1.0f, 0.75f, 0.2f);
                    m.SetColor("_EmissionColor", e * 1.8f);
                }
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", Mathf.Max(m.GetFloat("_Metallic"), 0.55f));
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", Mathf.Max(m.GetFloat("_Glossiness"), 0.7f));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Max(m.GetFloat("_Smoothness"), 0.7f));
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
        // Thin unlit outline shell so the magnet reads against the halo.
        if (visual.Find("MagnetRim")) return;
        var src = visual.GetComponentInChildren<MeshFilter>(true);
        if (!src || !src.sharedMesh) return;
        var rim = new GameObject("MagnetRim");
        rim.transform.SetParent(src.transform, false);
        rim.transform.localPosition = Vector3.zero;
        rim.transform.localRotation = Quaternion.identity;
        rim.transform.localScale = Vector3.one * 1.07f;
        var mf = rim.AddComponent<MeshFilter>(); mf.sharedMesh = src.sharedMesh;
        var mr = rim.AddComponent<MeshRenderer>();
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit");
        var rimMat = new Material(sh) { name = "MagnetRim" };
        if (rimMat.HasProperty("_Color")) rimMat.SetColor("_Color", new Color(1f, 0.92f, 0.45f, 1f));
        mr.sharedMaterial = rimMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    [MenuItem("Tools/Tall Crawl/Polish2/Bake HUD Layout")]
    public static void BakeHudLayout()
    {
        var sc = EditorSceneManager.OpenScene("Assets/Scenes/gameplay.unity");
        var pause = FindPath("TC_GameUI/Safe/Top/Pause") as RectTransform;
        if (pause)
        {
            pause.anchorMin = pause.anchorMax = new Vector2(1f, 0.5f);
            pause.pivot = new Vector2(0.5f, 0.5f);
            pause.anchoredPosition = new Vector2(-80f, -118f);
            pause.sizeDelta = new Vector2(120f, 120f);
            EditorUtility.SetDirty(pause.gameObject);
            Debug.Log("TCPolish2Bake: pause button " + pause.anchoredPosition + " size " + pause.sizeDelta);
        }
        else Debug.LogWarning("TCPolish2Bake: Pause button not found");
        EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
    }

    [MenuItem("Tools/Tall Crawl/Polish2/Bake Readable Buttons")]
    public static void BakeReadable()
    {
        bool wasBaking = TCUI.Baking;
        TCUI.Baking = false; // ApplyReadable should rewrite already-baked labels
        try
        {
            foreach (var scene in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/gameplay.unity" })
            {
                var sc = EditorSceneManager.OpenScene(scene);
                foreach (var canvas in Object.FindObjectsOfType<Canvas>())
                    TCUI.ApplyReadable(canvas.transform);
                EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
                Debug.Log("TCPolish2Bake: readable " + scene);
            }
        }
        finally { TCUI.Baking = wasBaking; }
    }

    [MenuItem("Tools/Tall Crawl/Polish2/Bake Icons")]
    public static void BakeIcons()
    {
        TCSetup.RefreshV3();
        Debug.Log("TCPolish2Bake: icons");
    }
}
