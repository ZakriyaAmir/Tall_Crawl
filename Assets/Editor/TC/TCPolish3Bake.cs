using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// polish3: applies the palette theme (TCTheme) IN PLACE to the baked canvases TC_MenuUI (MainMenu.unity) and
/// TC_GameUI (gameplay.unity). Only colours, effects and the theme's own decoration children (Bevel, DecoGrad,
/// DecoRing, ShineClip, Sparkle, Wash) are touched - positions/sizes you edited in the scene are kept (the ribbon
/// header is widened once to make room for its tails). Already-themed objects (TCThemed v3) are skipped, so it is
/// safe to re-run and it never overwrites colours you change afterwards in the editor.
/// Batchmode: -executeMethod TCPolish3Bake.All
public static class TCPolish3Bake
{
    const string UIDir = "Assets/TC/Resources/TCUI", MatDir = "Assets/TC/UI";
    static readonly string[] Sprites = { "ui_bevel", "ui_ring", "ui_cardgrad", "ui_ribbon", "ui_leaf", "ui_wash", "ui_sparkle" };

    [MenuItem("Tools/Tall Crawl/Polish3/Apply Palette Theme To Scenes")]
    public static void All()
    {
        ImportSprites();
        TCTheme.ClearCache(); TCUI.ResetSpriteCache();
        TCTheme.MatFor = TextMat;
        bool wasBaking = TCUI.Baking; TCUI.Baking = false;
        try
        {
            Theme("Assets/Scenes/MainMenu.unity", "TC_MenuUI", true);
            Theme("Assets/Scenes/gameplay.unity", "TC_GameUI", false);
        }
        finally { TCTheme.MatFor = null; TCUI.Baking = wasBaking; }
        AssetDatabase.SaveAssets();
        Debug.Log("TCPolish3Bake: done");
    }

    [MenuItem("Tools/Tall Crawl/Polish3/Import Theme Sprites")]
    public static void ImportSprites()
    {
        AssetDatabase.Refresh();
        foreach (var n in Sprites)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(UIDir + "/" + n + ".png"); if (!ti) { Debug.LogWarning("TCPolish3Bake: missing " + n); continue; }
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed; ti.spritePixelsPerUnit = 100;
            ti.spriteBorder = n == "ui_ribbon" ? new Vector4(90, 0, 90, 0) : n == "ui_bevel" || n == "ui_ring" || n == "ui_cardgrad" ? new Vector4(26, 26, 26, 26) : Vector4.zero;
            var ps = ti.GetPlatformTextureSettings("Android"); ps.overridden = false; ti.SetPlatformTextureSettings(ps);
            ti.SaveAndReimport();
        }
        foreach (var n in new[] { "icon_gift", "icon_shirt", "icon_bag" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(UIDir + "/" + n + ".png"); if (ti) ti.SaveAndReimport();
        }
    }

    static void Theme(string scenePath, string canvasName, bool menu)
    {
        var sc = EditorSceneManager.OpenScene(scenePath);
        Canvas canvas = null;
        foreach (var c in Object.FindObjectsOfType<Canvas>(true)) if (c && c.name == canvasName && c.transform.parent == null) { canvas = c; break; }
        if (!canvas) { Debug.LogWarning("TCPolish3Bake: no " + canvasName + " in " + scenePath); return; }
        TCTheme.ThemeTree(canvas.transform);
        if (menu) TCTheme.MenuBackground(canvas.transform.Find("BG"));
        int n = 0; foreach (var m in canvas.GetComponentsInChildren<TCThemed>(true)) n++;
        EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
        Debug.Log("TCPolish3Bake: themed " + canvasName + " (" + n + " objects) in " + scenePath);
    }

    /// Shared TMP material per outline width (deep-brown outline), so baked labels never hold instance materials.
    static Material TextMat(TMP_FontAsset font, float w)
    {
        if (!font || !font.material) return null;
        Directory.CreateDirectory(MatDir);
        string safe = string.Join("_", font.name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "");
        string p = MatDir + "/P3Text_" + safe + "_" + Mathf.RoundToInt(w * 100) + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m)
        {
            m = new Material(font.material) { name = Path.GetFileNameWithoutExtension(p) };
            AssetDatabase.CreateAsset(m, p);
        }
        m.EnableKeyword("OUTLINE_ON"); m.SetFloat(ShaderUtilities.ID_OutlineWidth, w); m.SetColor(ShaderUtilities.ID_OutlineColor, TCUI.Ink);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Full polish3 pipeline for the screenshot build: theme the scenes, then build the capture player.
    public static void ThemeAndBuild() { All(); TCSetup.BuildCapture(); }
}
