using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Forces External Dependency Manager to resolve the Android libraries (Google Mobile Ads,
/// UMP, Firebase) into Assets/Plugins/Android/mainTemplate.gradle. Run after updating the
/// Google Mobile Ads package, or from batchmode: -executeMethod TCAdsSetup.ResolveAndroid
public static class TCAdsSetup
{
    [MenuItem("Tools/Tall Crawl/Resolve Android Ad Dependencies")]
    public static void ResolveAndroid()
    {
        var t = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("GooglePlayServices.PlayServicesResolver", false))
            .FirstOrDefault(x => x != null);
        if (t == null) { Debug.LogError("[TCAdsSetup] External Dependency Manager not found"); return; }
        var m = t.GetMethod("ResolveSync", new[] { typeof(bool) });
        bool ok = m != null && (bool)m.Invoke(null, new object[] { true });
        Debug.Log("[TCAdsSetup] Android dependency resolution " + (ok ? "succeeded" : "FAILED"));
        AssetDatabase.Refresh();
    }

    public static void ResolveAndBuild()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        ResolveAndroid();
        TCSetup.BuildAndroid();
    }
}
