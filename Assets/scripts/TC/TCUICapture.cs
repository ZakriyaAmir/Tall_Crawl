using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// polish3 UI screenshot tour, only active when the player is started with "-tcui <dir>":
/// menu, skins, shop, settings, daily, consent, toasts, panel entrance + press frames, HUD (chips, lives,
/// power-up timers, WEB letters), pause, 3-2-1 countdown, revive, game over before/after x2.
public class TCUICapture : MonoBehaviour
{
    string dir; int n;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-tcui"); if (i < 0) return;
        PlayerPrefs.DeleteAll(); PlayerPrefs.SetInt("consent", 1); TCSave.TutorialDone = true;
        var g = new GameObject("TCUICapture"); DontDestroyOnLoad(g);
        g.AddComponent<TCUICapture>().dir = i + 1 < args.Length ? args[i + 1] : "/tmp";
    }
    IEnumerator Shot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot($"{dir}/{++n:00}_{name}.png"); yield return null; }
    IEnumerator W(float s) { yield return new WaitForSecondsRealtime(s); }
    static CanvasGroup Find(string name)
    {
        foreach (var cg in Resources.FindObjectsOfTypeAll<CanvasGroup>()) if (cg.name == name && cg.gameObject.scene.IsValid()) return cg;
        return null;
    }
    static void Press(GameObject g, bool down)
    {
        if (!g) return;
        var ev = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        if (down) ExecuteEvents.Execute(g, ev, ExecuteEvents.pointerDownHandler); else ExecuteEvents.Execute(g, ev, ExecuteEvents.pointerUpHandler);
        if (down) ExecuteEvents.Execute(g, ev, ExecuteEvents.pointerEnterHandler);
    }
    static void Toast(TCMenuUI menu, string msg, bool flashy)
    {
        var m = typeof(TCMenuUI).GetMethod("Toast", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(bool) }, null);
        m?.Invoke(menu, new object[] { msg, flashy });
    }

    IEnumerator Start()
    {
        yield return W(3.5f);
        var menu = FindObjectOfType<TCMenuUI>();
        TCSave.Coins += 2500; TCSave.Gems += 12;
        // Daily first (it auto-opens on a fresh install)
        var daily = Find("Daily");
        if (daily && !daily.gameObject.activeSelf) TCUI.Show(menu, daily);
        yield return W(1.0f); yield return Shot("daily");
        if (daily) TCUI.Hide(menu, daily); yield return W(0.7f);
        yield return Shot("menu");
        yield return W(1.2f); yield return Shot("menu_b");
        // Play button press squash + shine
        var play = GameObject.Find("TC_MenuUI/Safe/Play");
        Press(play, true); yield return W(0.12f); yield return Shot("menu_press"); Press(play, false); yield return W(0.6f);
        // Panel entrance (staggered) frames on Settings
        var settings = Find("Settings");
        if (settings)
        {
            TCUI.Show(menu, settings);
            for (int f = 0; f < 5; f++) { yield return W(0.06f); yield return Shot("settings_open_" + f); }
            yield return W(0.8f); yield return Shot("settings");
            TCUI.Hide(menu, settings); yield return W(0.6f);
        }
        foreach (var p in new[] { "Skins", "Shop", "Consent" })
        {
            var cg = Find(p); if (!cg) continue;
            TCUI.Show(menu, cg); yield return W(1.1f); yield return Shot(p.ToLower());
            TCUI.Hide(menu, cg); yield return W(0.6f);
        }
        Toast(menu, "Boost Time upgraded!", false); yield return W(0.6f); yield return Shot("toast");
        yield return W(1.6f);
        Toast(menu, "CHEAT ON: 9,000,000 coins & gems!", true); yield return W(0.7f); yield return Shot("toast_flashy");
        yield return W(2.8f);

        SceneManager.LoadScene("gameplay");
        yield return W(3f);
        yield return Shot("tap_to_start");
        GameObject.Find("TC_GameUI/Safe/Start")?.GetComponent<Button>()?.onClick.Invoke();
        var ui = FindObjectOfType<TCGameUI>(); if (ui) ui.StartRun();
        var pl = FindObjectOfType<player>();
        var sp = FindObjectOfType<playerInteract>();
        var track = FindObjectOfType<TCTrack>();
        if (sp) sp.GrantGrace(120f);
        if (pl) pl.paused = false;
        float t = 0; while (t < 4f) { t += Time.deltaTime; player.DebugSteer = Mathf.Sin(t * 0.9f); yield return null; }
        player.DebugSteer = 0;
        // WEB letters HUD (two collected) + power-up timers
        var web = TCWebBonus.Instance;
        if (web && sp) { web.CollectLetter('W', sp.transform.position); yield return W(0.3f); web.CollectLetter('E', sp.transform.position); }
        if (track && sp)
        {
            foreach (var proto in new[] { track.magnetPrefab, track.boostPrefab })
            {
                if (!proto) continue;
                var g = Instantiate(proto, new Vector3(0, sp.transform.position.y + 0.9f, 0), Quaternion.identity); g.SetActive(true);
                foreach (var c in g.GetComponentsInChildren<Collider>()) if (!c.CompareTag("Untagged")) { sp.HandlePickup(c); break; }
                Destroy(g);
            }
        }
        yield return W(0.9f); yield return Shot("hud");
        if (pl) pl.paused = true; yield return W(0.5f); yield return Shot("hud_paused_player");
        if (sp) sp.boostDuration = 0.01f;
        yield return W(1.2f);
        // Pause + countdown
        if (pl) pl.paused = false; yield return W(0.4f);
        if (ui) ui.ShowPause();
        yield return W(1.0f); yield return Shot("pause");
        GameObject.Find("TC_GameUI/Pause/Card/Resume")?.GetComponent<Button>()?.onClick.Invoke();
        yield return W(0.35f); yield return Shot("countdown_3");
        yield return W(0.85f); yield return Shot("countdown_2");
        yield return W(2.2f);
        // Revive, then game over before/after x2
        if (gameManager.Instance && gameManager.Instance.coinsCollected < 24) gameManager.Instance.coinsCollected = 24;
        if (ui) ui.ShowGameOver(true);
        yield return W(1.2f); yield return Shot("revive");
        GameObject.Find("TC_GameUI/Revive/Card/Skip")?.GetComponent<Button>()?.onClick.Invoke();
        yield return W(1.8f); yield return Shot("gameover_before_x2");
        var dbl = GameObject.Find("TC_GameUI/Results/Card/Double");
        if (dbl) { dbl.GetComponent<Button>()?.onClick.Invoke(); yield return W(1.4f); yield return Shot("gameover_after_x2"); }
        Application.Quit();
    }
}
