using System.IO; using System.Linq; using System.Text;
using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine;
public static class TCAudit {
  public static void Run() {
    var sb = new StringBuilder();
    foreach (var sp in new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/gameplay.unity"}) {
      var sc = EditorSceneManager.OpenScene(sp);
      sb.AppendLine("## SCENE "+sp);
      foreach (var r in sc.GetRootGameObjects()) Dump(r.transform, 0, sb);
    }
    foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[]{"Assets/prefabs"})) {
      var p = AssetDatabase.GUIDToAssetPath(g); sb.AppendLine("## PREFAB "+p);
      Dump(AssetDatabase.LoadAssetAtPath<GameObject>(p).transform, 0, sb);
    }
    sb.AppendLine("## TEXTURES");
    foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[]{"Assets"})) {
      var p = AssetDatabase.GUIDToAssetPath(g); if (p.Contains("Firebase")||p.Contains("GoogleMobileAds")||p.Contains("TextMesh")) continue;
      var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti==null) continue;
      var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
      sb.AppendLine($"{p} {t?.width}x{t?.height} type={ti.textureType} max={ti.maxTextureSize} mip={ti.mipmapEnabled} file={new FileInfo(p).Length/1024}KB");
    }
    File.WriteAllText("/workspace/tc_out/audit_raw.txt", sb.ToString());
  }
  static void Dump(Transform t, int d, StringBuilder sb) {
    var comps = t.GetComponents<Component>();
    var s = string.Join(",", comps.Select(c => c==null ? "MISSING_SCRIPT" : c.GetType().Name));
    string mats="";
    var r = t.GetComponent<Renderer>();
    if (r) mats = " mats=["+string.Join(";", r.sharedMaterials.Select(m => m==null?"NULL":m.name+"/"+(m.shader?m.shader.name:"noshader")+(m.mainTexture?"":"/notex")))+"]";
    sb.AppendLine(new string(' ', d*2)+t.name+(t.gameObject.activeSelf?"":" (off)")+" : "+s+mats);
    foreach (Transform c in t) Dump(c, d+1, sb);
  }
}
