using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// VFX/UI screenshot tour, only active when the player is started with "-tcfx <dir>".
/// Stages each power-up pickup in front of the spider, collects them (burst + active effects),
/// shows the boost (fire shield, trail, speed lines), the HUD lives panel and menu button juice.
public class TCFxCapture : MonoBehaviour
{
    string dir; int n;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-tcfx"); if (i < 0) return;
        PlayerPrefs.DeleteAll(); PlayerPrefs.SetInt("consent", 1); TCSave.TutorialDone = true;
        PlayerPrefs.SetString("tc_daily_last", System.DateTime.Now.Date.ToString("yyyy-MM-dd"));
        var g = new GameObject("TCFxCapture"); DontDestroyOnLoad(g);
        g.AddComponent<TCFxCapture>().dir = i + 1 < args.Length ? args[i + 1] : "/tmp";
    }
    IEnumerator Shot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot($"{dir}/{++n:00}_{name}.png"); yield return null; }
    IEnumerator W(float s) { yield return new WaitForSecondsRealtime(s); }

    static void Press(GameObject g, bool down)
    {
        if (!g) return;
        var ev = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        if (down) ExecuteEvents.Execute(g, ev, ExecuteEvents.pointerDownHandler);
        else { ExecuteEvents.Execute(g, ev, ExecuteEvents.pointerUpHandler); }
    }

    IEnumerator Start()
    {
        yield return W(4f);
        var menu = FindObjectOfType<TCMenuUI>();
        foreach (var cg in Resources.FindObjectsOfTypeAll<CanvasGroup>()) if (cg.name == "Daily" && cg.gameObject.scene.IsValid() && cg.gameObject.activeSelf) TCUI.Hide(menu, cg);
        yield return W(1f);
        yield return Shot("menu");
        var play = GameObject.Find("TC_MenuUI/Safe/Play");
        Press(play, true);
        for (int f = 0; f < 4; f++) { yield return W(0.03f); yield return Shot("menu_press_" + f); }
        Press(play, false);
        for (int f = 0; f < 6; f++) { yield return W(0.04f); yield return Shot("menu_release_" + f); }
        yield return W(0.8f);
        CanvasGroup shop = null;
        foreach (var cg in Resources.FindObjectsOfTypeAll<CanvasGroup>()) if (cg.name == "Shop" && cg.gameObject.scene.IsValid()) shop = cg;
        if (shop)
        {
            TCUI.Show(menu, shop);
            for (int f = 0; f < 6; f++) { yield return W(0.05f); yield return Shot("panel_open_" + f); }
            yield return W(0.6f); yield return Shot("panel_open_done");
            TCUI.Hide(menu, shop); yield return W(0.6f);
        }

        SceneManager.LoadScene("gameplay");
        yield return W(3f);
        GameObject.Find("TC_GameUI/Safe/Start")?.GetComponent<Button>()?.onClick.Invoke();
        var ui = FindObjectOfType<TCGameUI>(); if (ui) ui.StartRun();
        var pl = FindObjectOfType<player>();
        var sp = FindObjectOfType<playerInteract>();
        var track = FindObjectOfType<TCTrack>();
        yield return W(0.6f);
        pl.paused = true;
        ClearHazards();
        yield return W(0.4f);
        yield return Shot("hud");

        var protos = new[] { track.boostPrefab, track.heartPrefab, track.cooldownPrefab, track.magnetPrefab };
        var names = new[] { "chilli", "health", "slowmo", "magnet" };
        float ang = Mathf.Atan2(sp.transform.position.x, sp.transform.position.z) * Mathf.Rad2Deg;
        for (int i = 0; i < protos.Length; i++)
        {
            if (!protos[i]) continue;
            var g = Instantiate(protos[i], new Vector3(0, sp.transform.position.y + 1.25f, 0), Quaternion.Euler(0, ang, 0));
            g.SetActive(true);
            yield return W(0.9f); yield return Shot("pickup_" + names[i] + "_a");
            yield return W(0.45f); yield return Shot("pickup_" + names[i] + "_b");
            // collect: burst on pickup
            Collider col = null;
            foreach (var c in g.GetComponentsInChildren<Collider>()) if (!c.CompareTag("Untagged")) { col = c; break; }
            if (col) g.transform.position = new Vector3(0, sp.transform.position.y + 0.9f, 0);
            if (col && sp.HandlePickup(col))
            {
                for (int f = 0; f < 4; f++) { yield return W(0.06f); yield return Shot("burst_" + names[i] + "_" + f); }
            }
            if (names[i] == "chilli")
            {
                pl.paused = false;
                yield return W(0.35f); yield return Shot("boost_0");
                yield return W(0.5f); yield return Shot("boost_1");
                yield return W(0.5f); yield return Shot("boost_2");
                pl.paused = true; sp.boostDuration = 0.01f;
                yield return W(1.4f);
            }
            if (names[i] == "magnet") { yield return W(0.5f); yield return Shot("magnet_active"); }
            Destroy(g);
            ClearHazards();
            yield return W(0.4f);
        }
        // speed lines at normal speed
        pl.paused = false; yield return W(1.2f); yield return Shot("climb_speed"); pl.paused = true;
        // Lives panel states
        for (int l = sp.MaxLife; l >= 1; l--)
        {
            sp.life = l; yield return W(0.5f); yield return Shot("lives_" + l);
        }
        sp.life = 3; yield return W(0.3f);
        sp.life = 2; playerInteract.OnEvent?.Invoke("hit", sp.transform.position);
        for (int f = 0; f < 5; f++) { yield return W(0.07f); yield return Shot("lives_hit_" + f); }
        yield return W(0.6f);
        sp.life = 3; playerInteract.OnEvent?.Invoke("heart", sp.transform.position);
        for (int f = 0; f < 4; f++) { yield return W(0.07f); yield return Shot("lives_gain_" + f); }
        yield return W(0.6f);
        sp.life = 1; yield return W(0.9f); yield return Shot("lives_danger");
        Application.Quit();
    }

    static void ClearHazards()
    {
        foreach (var p in FindObjectsOfType<TCPooled>()) if (p.kind != "trunk") p.gameObject.SetActive(false);
    }
}
