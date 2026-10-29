using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class TCVerification
{
    [MenuItem("Tall Crawl/Verify All")]
    public static void RunVerification()
    {
        var report = new StringBuilder();
        int pass = 0, fail = 0;

        void Log(string msg, bool ok = true)
        {
            if (ok) { pass++; report.AppendLine("[PASS] " + msg); Debug.Log("[VERIFY PASS] " + msg); }
            else { fail++; report.AppendLine("[FAIL] " + msg); Debug.LogError("[VERIFY FAIL] " + msg); }
        }

        report.AppendLine("=== TALL CRAWL FULL VERIFICATION REPORT ===");
        report.AppendLine("Timestamp: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        // 1. Verify Skins
        Log("TCSkins.All count is 20", TCSkins.All.Length == 20);
        if (TCSkins.All.Length >= 20)
        {
            Log("Skin 15 is Bumblebee", TCSkins.All[15].name == "Bumblebee");
            Log("Skin 16 is Ladybug", TCSkins.All[16].name == "Ladybug");
            Log("Skin 17 is Tiger", TCSkins.All[17].name == "Tiger");
            Log("Skin 18 is Tarantula", TCSkins.All[18].name == "Tarantula");
            Log("Skin 19 is Golden with 100,000 coins", TCSkins.All[19].name == "Golden" && TCSkins.All[19].amount == 100000 && TCSkins.All[19].unlock == SkinUnlock.Coins);
        }

        // 2. Verify Branch Flower Variants
        var flowerNone = Resources.Load<Material>("Buisson_Atlas_None") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/objects/models/bush/source/Buisson_Atlas_None.mat");
        var flowerRed = Resources.Load<Material>("Buisson_Atlas_Red") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/objects/models/bush/source/Buisson_Atlas_Red.mat");
        var flowerYellow = Resources.Load<Material>("Buisson_Atlas_Yellow") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/objects/models/bush/source/Buisson_Atlas_Yellow.mat");
        var flowerPink = Resources.Load<Material>("Buisson_Atlas_Pink") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/objects/models/bush/source/Buisson_Atlas_Pink.mat");

        Log("Buisson_Atlas_None material exists", flowerNone != null);
        Log("Buisson_Atlas_Red material exists", flowerRed != null);
        Log("Buisson_Atlas_Yellow material exists", flowerYellow != null);
        Log("Buisson_Atlas_Pink material exists", flowerPink != null);

        // 3. Verify Wood / Log materials
        var logMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/objects/materials/log.mat");
        var woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/objects/materials/wood.mat");
        Log("Log material exists", logMat != null);
        Log("Wood material exists", woodMat != null);

        // 4. Verify MainMenu Scene
        var menuScene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        int missingScriptsMenu = 0;
        foreach (var go in menuScene.GetRootGameObjects())
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                missingScriptsMenu += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            }
        }
        Log("MainMenu scene has 0 missing scripts", missingScriptsMenu == 0);

        var mgr = GameObject.Find("Manager");
        Log("Manager object exists in MainMenu", mgr != null);
        if (mgr)
        {
            var menuUI = mgr.GetComponent<TCMenuUI>();
            Log("TCMenuUI component attached to Manager", menuUI != null);
        }

        // 5. Verify Gameplay Scene
        var gameScene = EditorSceneManager.OpenScene("Assets/Scenes/gameplay.unity");
        int missingScriptsGame = 0;
        foreach (var go in gameScene.GetRootGameObjects())
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                missingScriptsGame += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            }
        }
        Log("gameplay scene has 0 missing scripts", missingScriptsGame == 0);

        // Return to MainMenu scene
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");

        report.AppendLine($"=== SUMMARY: {pass} PASSED, {fail} FAILED ===");
        string reportPath = Path.Combine(Application.dataPath, "../verify_report.txt");
        File.WriteAllText(reportPath, report.ToString());
        Debug.Log(report.ToString());
    }

    [MenuItem("Tall Crawl/Play Mode Test")]
    public static void RunPlayModeTest()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        PlayerPrefs.SetInt("tc_playmode_test_active", 1);
        PlayerPrefs.Save();
        EditorApplication.isPlaying = true;
    }
}

public class TCPlayModeVerifier : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        if (PlayerPrefs.GetInt("tc_playmode_test_active", 0) != 1) return;
        var go = new GameObject("TCPlayModeVerifier");
        DontDestroyOnLoad(go);
        go.AddComponent<TCPlayModeVerifier>();
    }

    IEnumerator Start()
    {
        PlayerPrefs.DeleteKey("tc_playmode_test_active");
        PlayerPrefs.Save();

        yield return new WaitForSeconds(1.5f);

        var report = new StringBuilder();
        int pass = 0, fail = 0;

        void Log(string msg, bool ok = true)
        {
            if (ok) { pass++; report.AppendLine("[PASS] " + msg); Debug.Log("[PLAYTEST PASS] " + msg); }
            else { fail++; report.AppendLine("[FAIL] " + msg); Debug.LogError("[PLAYTEST FAIL] " + msg); }
        }

        report.AppendLine("=== PLAY MODE RUNTIME VERIFICATION ===");

        var canvas = GameObject.Find("TC_MenuUI");
        Log("TC_MenuUI canvas exists", canvas != null);

        if (canvas)
        {
            var playBtn = canvas.transform.Find("Safe/Play");
            Log("Play button exists", playBtn != null);

            var logo = canvas.transform.Find("Safe/Logo");
            Log("Logo exists", logo != null);

            var coins = canvas.transform.Find("Safe/Coins");
            var gems = canvas.transform.Find("Safe/Gems");
            var best = canvas.transform.Find("Safe/Best");
            Log("Currency pills and Best score pill exist", coins != null && gems != null && best != null);

            var previewStage = GameObject.Find("SkinPreviewStage");
            Log("SkinPreviewStage exists", previewStage != null);

            var menuUI = UnityEngine.Object.FindFirstObjectByType<TCMenuUI>();
            Log("TCMenuUI instance found", menuUI != null);
            if (menuUI)
            {
                var skinsPanel = menuUI.GetPanel("Skins");
                var shopPanel = menuUI.GetPanel("Shop");
                var settingsPanel = menuUI.GetPanel("Settings");
                var dailyPanel = menuUI.GetPanel("Daily");
                var consentPanel = menuUI.GetPanel("Consent");

                Log("Skins panel is inactive by default", skinsPanel && !skinsPanel.gameObject.activeSelf);
                Log("Shop panel is inactive by default", shopPanel && !shopPanel.gameObject.activeSelf);
                Log("Settings panel is inactive by default", settingsPanel && !settingsPanel.gameObject.activeSelf);
                Log("Daily panel is inactive by default", dailyPanel && !dailyPanel.gameObject.activeSelf);
                Log("Consent panel is inactive by default", consentPanel && !consentPanel.gameObject.activeSelf);

                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath, "../playmode_screenshot.png"));
                yield return new WaitForSeconds(0.4f);

                // Now test opening Skins panel
                var skinsBtn = canvas.transform.Find("Safe/Skins")?.GetComponent<Button>();
                if (skinsBtn)
                {
                    skinsBtn.onClick.Invoke();
                    yield return new WaitForSeconds(0.6f);

                    Log("Skins panel opened successfully", skinsPanel && skinsPanel.gameObject.activeSelf);
                    int skinButtonsCount = 0;
                    for (int i = 0; i < 20; i++)
                    {
                        var b = skinsPanel.transform.Find("Card/Skin" + i);
                        if (b != null) skinButtonsCount++;
                    }
                    Log("All 20 skin buttons exist in Skins panel", skinButtonsCount == 20);

                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath, "../playmode_skins_screenshot.png"));
                    yield return new WaitForSeconds(0.4f);

                    // Click Close button
                    var closeBtn = skinsPanel.transform.Find("Card/Close")?.GetComponent<Button>();
                    if (closeBtn) closeBtn.onClick.Invoke();
                    yield return new WaitForSeconds(0.5f);
                    Log("Skins panel closed successfully", skinsPanel && !skinsPanel.gameObject.activeSelf);
                }
            }
        }

        report.AppendLine($"=== RUNTIME SUMMARY: {pass} PASSED, {fail} FAILED ===");
        string reportPath = Path.Combine(Application.dataPath, "../playmode_report.txt");
        File.WriteAllText(reportPath, report.ToString());

        yield return new WaitForSeconds(0.5f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
