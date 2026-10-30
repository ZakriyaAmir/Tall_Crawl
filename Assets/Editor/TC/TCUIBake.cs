using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Bakes the hypercasual UI (built by TCMenuUI / TCGameUI) into MainMenu.unity and gameplay.unity as real GameObjects.
/// Batchmode: -executeMethod TCUIBake.BakeAll. Re-running replaces the baked canvases (it discards manual edits to them).
public static class TCUIBake
{
    const string SpriteDir = "Assets/TC/Resources/TCUI", MatDir = "Assets/TC/UI";
    static readonly string[] IconNames = { "coin", "gem", "hand", "heart", "heartEmpty", "home", "pause", "play", "restart", "check", "gear", "lock", "shirt", "bag", "gift" };

    [MenuItem("Tools/Tall Crawl/Bake UI Into Scenes")]
    public static void BakeAll()
    {
        ApplyBark();
        ExportSprites();
        TCUI.ResetSpriteCache();
        TCUI.BakeOutlineMat = OutlineMat;
        TCUI.Baking = true;
        try
        {
            BakeScene("Assets/Scenes/MainMenu.unity", "TC_MenuUI", () => Object.FindObjectOfType<TCMenuUI>(true).BuildUI());
            BakeScene("Assets/Scenes/gameplay.unity", "TC_GameUI", () => Object.FindObjectOfType<TCGameUI>(true).BuildUI());
        }
        finally { TCUI.Baking = false; TCUI.BakeOutlineMat = null; }
        AssetDatabase.SaveAssets();
        Debug.Log("TCUIBake: done");
    }

    /// Hypercasual forest bark (Tools/gen_bark.py) on the trunk (wood.mat, treeChunk) and branch (log.mat, branchParent + TCBranchVariant) materials.
    [MenuItem("Tools/Tall Crawl/Apply Bark Textures")]
    public static void ApplyBark()
    {
        foreach (var n in new[] { "Bark_Trunk", "Bark_Branch" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath("Assets/TC/Textures/" + n + "_n.png");
            ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport();
            var ci = (TextureImporter)AssetImporter.GetAtPath("Assets/TC/Textures/" + n + ".png");
            ci.wrapMode = TextureWrapMode.Repeat; ci.anisoLevel = 4; ci.SaveAndReimport();
        }
        Set("Assets/objects/materials/wood.mat", "Bark_Trunk");
        Set("Assets/objects/materials/log.mat", "Bark_Branch");
        AssetDatabase.SaveAssets();
    }
    static void Set(string matPath, string tex)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TC/Textures/" + tex + ".png"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TC/Textures/" + tex + "_n.png"));
        m.SetFloat("_BumpScale", 0.8f); m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Glossiness", 0.12f); m.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(m);
    }

    static void BakeScene(string path, string canvasName, System.Action build)
    {
        var sc = EditorSceneManager.OpenScene(path);
        foreach (var c in Object.FindObjectsOfType<Canvas>(true)) if (c && c.name == canvasName) Object.DestroyImmediate(c.gameObject);
        build();
        EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
        Debug.Log("TCUIBake: baked " + path);
    }

    static void ExportSprites()
    {
        Directory.CreateDirectory(SpriteDir); Directory.CreateDirectory(MatDir);
        TCUI.Readable = true; TCUI.ResetSpriteCache();
        Save(TCUI.Round, "round", 26); Save(TCUI.Circle, "circle", 0);
        foreach (var n in IconNames) Save(TCUI.Icon(n), "icon_" + n, 0);
        // menu background gradient
        var tex = new Texture2D(2, 64);
        for (int y = 0; y < 64; y++) { var c = Color.Lerp(TCUI.Hex("#FFD3B8"), TCUI.Hex("#7FD0FF"), y / 63f); tex.SetPixel(0, y, c); tex.SetPixel(1, y, c); }
        File.WriteAllBytes(SpriteDir + "/menu_gradient.png", tex.EncodeToPNG());
        TCUI.Readable = false; TCUI.ResetSpriteCache();
        AssetDatabase.Refresh();
        foreach (var f in Directory.GetFiles(SpriteDir, "*.png"))
        {
            var p = f.Replace('\\', '/'); var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            string n = Path.GetFileNameWithoutExtension(p);
            ti.textureType = n == "menu_gradient" ? TextureImporterType.Default : TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.spritePixelsPerUnit = 100;
            ti.spriteBorder = n == "round" ? new Vector4(26, 26, 26, 26) : Vector4.zero;
            ti.SaveAndReimport();
        }
    }
    static void Save(Sprite s, string name, int border) => File.WriteAllBytes(SpriteDir + "/" + name + ".png", s.texture.EncodeToPNG());

    static Material OutlineMat(float w)
    {
        string p = MatDir + "/TextOutline_" + Mathf.RoundToInt(w * 100) + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m) return m;
        var font = TCAssets.I.font; if (!font) return null;
        m = new Material(font.material) { name = Path.GetFileNameWithoutExtension(p) };
        m.EnableKeyword("OUTLINE_ON"); m.SetFloat("_OutlineWidth", w); m.SetColor("_OutlineColor", TCUI.Cocoa);
        AssetDatabase.CreateAsset(m, p); return m;
    }
}
