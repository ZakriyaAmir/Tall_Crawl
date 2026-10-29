using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Automated screenshot tour, only active when the player is started with "-tccapture <dir>".
public class TCCapture : MonoBehaviour
{
    string dir; int n;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-tccapture"); if (i < 0) return;
        PlayerPrefs.DeleteAll(); PlayerPrefs.SetInt("consent", 1);
        var g = new GameObject("TCCapture"); DontDestroyOnLoad(g);
        g.AddComponent<TCCapture>().dir = i + 1 < args.Length ? args[i + 1] : "/tmp";
    }
    IEnumerator Shot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot($"{dir}/{++n:00}_{name}.png"); yield return new WaitForSecondsRealtime(0.3f); }
    CanvasGroup Panel(string n) { var mu = FindObjectOfType<TCMenuUI>(); if (mu && mu.GetPanel(n)) return mu.GetPanel(n); var g = GameObject.Find("TC_MenuUI/Safe/" + n) ?? GameObject.Find(n); if (!g) foreach (var cg in Resources.FindObjectsOfTypeAll<CanvasGroup>()) if (cg.name == n && cg.gameObject.scene.IsValid()) return cg; return g ? g.GetComponent<CanvasGroup>() : null; }
    IEnumerator Start()
    {
        // Menu entrance frames (drop on silk, somersault, landing, wave, UI pop-in)
        yield return new WaitForSecondsRealtime(0.15f);
        for (int f = 0; f < 16; f++) { yield return Shot("menu_anim_" + f.ToString("00")); }
        yield return new WaitForSecondsRealtime(1.5f);
        var menu = FindObjectOfType<TCMenuUI>();
        yield return Shot("daily_reward");
        TCUI.Hide(menu, Panel("Daily")); yield return new WaitForSecondsRealtime(0.8f);
        yield return Shot("main_menu");
        TCSave.Coins += 2500; TCSave.UnlockSkin(1); TCSave.UnlockSkin(3); TCSave.SelectedSkin = 3;
        foreach (var p in new[] { "Skins", "Shop", "Settings" })
        {
            var b = GameObject.Find("TC_MenuUI/Safe/" + p) ?? null;
            var cg = Panel(p); TCUI.Show(menu, cg); yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot(p.ToLower()); TCUI.Hide(menu, cg); yield return new WaitForSecondsRealtime(0.6f);
        }
        menu.PreviewSkin(3); yield return new WaitForSecondsRealtime(0.5f);
        yield return Shot("main_menu_skin");
        // Skin tour: every skin in the menu preview (colour + spikes + body shape only).
        for (int i = 0; i < TCSkins.All.Length; i++) { menu.PreviewSkin(i); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("skin_" + i + "_" + TCSkins.All[i].name.Replace(" ", "")); }
        TCSave.SelectedSkin = 2; // gameplay part with the Widow skin (black, red eyes)
        SceneManager.LoadScene("gameplay");
        yield return new WaitForSecondsRealtime(2.5f);
        yield return Shot("tap_to_start_tutorial");
        GameObject.Find("TC_GameUI/Safe/Start").GetComponent<Button>().onClick.Invoke();
        float t = 0; var pl = FindObjectOfType<player>();
        while (t < 9f) { t += Time.deltaTime; player.DebugSteer = Mathf.Sin(t * 0.9f); if (Mathf.Abs(t - 3f) < 0.02f || Mathf.Abs(t - 7.5f) < 0.02f) yield return Shot("gameplay"); yield return null; }
        player.DebugSteer = 0;
        yield return Shot("gameplay");
        FindObjectOfType<playerInteract>().death();
        yield return new WaitForSecondsRealtime(2f);
        yield return Shot("revive");
        yield return new WaitForSecondsRealtime(6.5f);
        yield return Shot("game_over");
        Application.Quit();
    }
}
