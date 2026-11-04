using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Sets up finely baked lighting and light probes for squirrel, branches, and tree in gameplay.unity.
/// Menu: Tools > Tall Crawl > Bake Lighting & Probes
/// </summary>
public static class TCBakeLighting
{
    const string GameplayScenePath = "Assets/Scenes/gameplay.unity";

    [MenuItem("Tools/Tall Crawl/Bake Lighting & Probes")]
    public static void BakeAll()
    {
        try
        {
            System.IO.File.WriteAllText("/tmp/tcbake.log", "Starting BakeAll...\n");

            // 1. Re-apply finely baked textures
            TCUIBake.ApplyBark();
            System.IO.File.AppendAllText("/tmp/tcbake.log", "ApplyBark done\n");

            // 2. Open gameplay scene
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            System.IO.File.AppendAllText("/tmp/tcbake.log", "OpenScene done\n");

            // 3. Setup Directional Light for fine indirect baking
            var dirLightGo = GameObject.Find("Directional Light");
            if (dirLightGo)
            {
                var l = dirLightGo.GetComponent<Light>();
                if (l)
                {
                    l.lightmapBakeType = LightmapBakeType.Mixed;
                    l.intensity = 1.15f;
                    l.shadowStrength = 0.45f;
                    l.color = new Color(1.0f, 0.96f, 0.88f, 1f);
                    EditorUtility.SetDirty(l);
                }
            }

            // 4. Setup or update 3D Light Probe Group around tree cylinder
            var lpgGo = GameObject.Find("LightProbeGroup");
            if (!lpgGo)
            {
                lpgGo = new GameObject("LightProbeGroup");
                Undo.RegisterCreatedObjectUndo(lpgGo, "Create LightProbeGroup");
            }
            var lpg = lpgGo.GetComponent<LightProbeGroup>();
            if (!lpg) lpg = lpgGo.AddComponent<LightProbeGroup>();

            var positions = new List<Vector3>();
            // Probe volume around trunk: 9 height tiers, 2 radial layers (trunk surface & branch tips), 8 angles
            float[] heights = { -4f, -1f, 2f, 5f, 8f, 11f, 14f, 17f, 20f };
            float[] radii = { 0.65f, 1.30f };
            const int angleSteps = 8;

            foreach (float y in heights)
            {
                foreach (float r in radii)
                {
                    for (int i = 0; i < angleSteps; i++)
                    {
                        float rad = (i / (float)angleSteps) * Mathf.PI * 2f;
                        positions.Add(new Vector3(Mathf.Cos(rad) * r, y, Mathf.Sin(rad) * r));
                    }
                }
            }
            lpg.probePositions = positions.ToArray();
            EditorUtility.SetDirty(lpg);
            System.IO.File.AppendAllText("/tmp/tcbake.log", "Light probes set: " + positions.Count + "\n");

            // 5. Ensure scene lightmap settings are fine
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;

            // 6. Save scene
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            System.IO.File.AppendAllText("/tmp/tcbake.log", "Saved scene and assets\n");

            // 7. Trigger bake
            Debug.Log($"[TCBakeLighting] Configured {positions.Count} light probes. Starting bake...");
            if (!Lightmapping.isRunning)
            {
                Lightmapping.BakeAsync();
                System.IO.File.AppendAllText("/tmp/tcbake.log", "BakeAsync called\n");
            }
            else
            {
                System.IO.File.AppendAllText("/tmp/tcbake.log", "Lightmapping is already running\n");
            }
        }
        catch (System.Exception ex)
        {
            System.IO.File.AppendAllText("/tmp/tcbake.log", "ERROR: " + ex.ToString() + "\n");
        }
    }
}
