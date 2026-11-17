using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Bakes every procedural enemy into an editable prefab under Assets/TC/Resources/Enemies/Prefabs
/// (meshes -> Meshes/*.asset, materials -> Materials/*.mat, generated textures -> Textures/*.png,
/// wasp + bat visuals -> Parts/*.prefab nested inside Wasp_Enemy). TCTrack loads them from Resources.
/// Re-baking overwrites the assets in place (GUIDs kept), so hand edits to the prefabs are lost on re-bake.
public static class TCEnemyPrefabBaker
{
    public const string Dir = "Assets/TC/Resources/Enemies/Prefabs";
    static readonly Dictionary<string, Object> savedThisRun = new Dictionary<string, Object>();
    static readonly Dictionary<Object, Object> objMap = new Dictionary<Object, Object>();

    [MenuItem("Tall Crawl/Bake Enemy Prefabs")]
    public static void BakeAll()
    {
        savedThisRun.Clear(); objMap.Clear();
        foreach (var sub in new[] { "", "/Meshes", "/Materials", "/Textures", "/Parts" }) Directory.CreateDirectory(Dir + sub);
        AssetDatabase.Refresh();

        Bake("Pinecone_Enemy", TCEnemy.CreatePineconePrefab(null), null);
        Bake("Squirrel_Enemy", TCEnemy.CreateSquirrelPrefab(null), null);
        Bake("Wasp_Enemy", TCEnemy.CreateWaspPrefab(null), new[] { "Model/Wasp_Visual", "Model/Bat_Visual" });
        foreach (TCSnake.Variant v in System.Enum.GetValues(typeof(TCSnake.Variant)))
            Bake("Snake_" + v, TCSnake.CreateSnakePrefab(null, v), null);

        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[TCEnemyPrefabBaker] baked enemy prefabs into " + Dir + " (" + savedThisRun.Count + " assets)");
    }

    static void Bake(string name, GameObject go, string[] nested)
    {
        go.name = name;
        SaveDeps(go);
        if (nested != null)
            foreach (var path in nested)
            {
                var child = go.transform.Find(path); if (!child) { Debug.LogWarning("[TCEnemyPrefabBaker] missing " + path); continue; }
                PrefabUtility.SaveAsPrefabAssetAndConnect(child.gameObject, $"{Dir}/Parts/{child.name}.prefab", InteractionMode.AutomatedAction);
            }
        PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/{name}.prefab", out bool ok);
        Debug.Log($"[TCEnemyPrefabBaker] {name}: {(ok ? "saved" : "FAILED")}");
        Object.DestroyImmediate(go);
    }

    static string Clean(string s) { foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return string.IsNullOrEmpty(s) ? "unnamed" : s.Replace(" ", "_"); }

    static T Persist<T>(T obj, string folder, string ext) where T : Object
    {
        if (!obj || AssetDatabase.Contains(obj)) return obj;
        if (objMap.TryGetValue(obj, out var mapped)) return (T)mapped;
        string baseName = Clean(obj.name), path = $"{Dir}/{folder}/{baseName}.{ext}";
        for (int i = 2; savedThisRun.TryGetValue(path, out var other) && other != obj; i++) path = $"{Dir}/{folder}/{baseName}_{i}.{ext}";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing) { EditorUtility.CopySerialized(obj, existing); existing.name = Path.GetFileNameWithoutExtension(path); savedThisRun[path] = existing; objMap[obj] = existing; return existing; }
        AssetDatabase.CreateAsset(obj, path); savedThisRun[path] = obj; objMap[obj] = obj; return obj;
    }

    static Texture2D PersistTexture(Texture2D tex)
    {
        if (!tex || AssetDatabase.Contains(tex)) return tex;
        if (tex == Texture2D.whiteTexture) return null;
        string path = $"{Dir}/Textures/{Clean(tex.name)}.png";
        if (savedThisRun.TryGetValue(path, out var done)) return (Texture2D)done;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp) { imp.textureType = TextureImporterType.Default; imp.wrapMode = tex.wrapMode; imp.mipmapEnabled = true; imp.sRGBTexture = true; imp.SaveAndReimport(); }
        var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path); savedThisRun[path] = asset; return asset;
    }

    static void SaveDeps(GameObject go)
    {
        var matMap = new Dictionary<Material, Material>();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh && !AssetDatabase.Contains(mf.sharedMesh)) mf.sharedMesh = Persist(mf.sharedMesh, "Meshes", "asset");
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i]; if (!m || AssetDatabase.Contains(m)) continue;
                if (!matMap.TryGetValue(m, out var saved))
                {
                    if (m.HasProperty("_MainTex")) { var t = m.mainTexture as Texture2D; if (t && !AssetDatabase.Contains(t)) m.mainTexture = PersistTexture(t); }
                    if (string.IsNullOrEmpty(m.name)) m.name = go.name + "_mat";
                    saved = Persist(m, "Materials", "mat"); matMap[m] = saved;
                }
                ms[i] = saved;
            }
            r.sharedMaterials = ms;
        }
    }
}

public static class TCEnemyPrefabBuild
{
    /// batchmode: bake the enemy prefabs, then build the capture player (one Unity launch).
    public static void BakeAndBuildCapture() { TCEnemyPrefabBaker.BakeAll(); TCSetup.BuildCapture(); }
}
