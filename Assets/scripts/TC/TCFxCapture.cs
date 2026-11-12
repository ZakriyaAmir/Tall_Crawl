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
    bool letterOnly, swingOnly;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-tcfx");
        int j = System.Array.IndexOf(args, "-tcletter");
        int k = System.Array.IndexOf(args, "-tcswing");
        if (i < 0 && j < 0 && k < 0) return;
        PlayerPrefs.DeleteAll(); PlayerPrefs.SetInt("consent", 1); TCSave.TutorialDone = true;
        PlayerPrefs.SetString("tc_daily_last", System.DateTime.Now.Date.ToString("yyyy-MM-dd"));
        var g = new GameObject("TCFxCapture"); DontDestroyOnLoad(g);
        var cap = g.AddComponent<TCFxCapture>();
        int idx = k >= 0 ? k : (j >= 0 ? j : i);
        cap.dir = idx + 1 < args.Length ? args[idx + 1] : "/tmp";
        cap.letterOnly = j >= 0 && k < 0;
        cap.swingOnly = k >= 0;
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
        if (letterOnly)
        {
            yield return LetterOnlyShot();
            Application.Quit();
            yield break;
        }
        if (swingOnly)
        {
            yield return SwingOnlyShot();
            Application.Quit();
            yield break;
        }
        yield return W(4f);
        var menu = FindObjectOfType<TCMenuUI>();
        foreach (var cg in Resources.FindObjectsOfTypeAll<CanvasGroup>()) if (cg.name == "Daily" && cg.gameObject.scene.IsValid() && cg.gameObject.activeSelf) TCUI.Hide(menu, cg);
        yield return W(1f);
        yield return Shot("menu");
        yield return Shot("logo");
        // Spider tap actions on the menu preview
        if (menu)
        {
            var preview = GameObject.Find("PreviewSpider") ?? Object.FindObjectOfType<TCCuteSpider>()?.gameObject;
            var cute = preview ? preview.GetComponent<TCCuteSpider>() : Object.FindObjectOfType<TCCuteSpider>();
            if (cute)
            {
                var acts = (TCCuteSpider.TouchAct[])System.Enum.GetValues(typeof(TCCuteSpider.TouchAct));
                for (int a = 0; a < acts.Length; a++)
                {
                    cute.BeginTouch(acts[a]);
                    yield return W(0.35f);
                    yield return Shot("spider_" + acts[a].ToString().ToLower());
                    yield return W(0.55f);
                }
            }
        }
        // Shop + skins contrast, insufficient-coins toast, cheat toast
        CanvasGroup skinsCg = null, settingsCg = null, shopEarly = null;
        foreach (var cg in Resources.FindObjectsOfTypeAll<CanvasGroup>())
        {
            if (!cg.gameObject.scene.IsValid()) continue;
            if (cg.name == "Shop") shopEarly = cg;
            if (cg.name == "Skins") skinsCg = cg;
            if (cg.name == "Settings") settingsCg = cg;
        }
        if (shopEarly && menu)
        {
            TCUI.Show(menu, shopEarly);
            yield return W(0.5f); yield return Shot("shop_readable");
            TCUI.Hide(menu, shopEarly); yield return W(0.35f);
        }
        if (skinsCg && menu)
        {
            TCUI.Show(menu, skinsCg);
            yield return W(0.5f); yield return Shot("skins_sort");
            yield return Shot("skins_back");
            int keepCoins = TCSave.Coins;
            TCSave.Coins = 0;
            var buy = GameObject.Find("TC_MenuUI/Skins/Card/Action") ?? GameObject.Find("Action");
            if (buy)
            {
                var b = buy.GetComponent<Button>();
                if (b) b.onClick.Invoke();
                yield return W(0.35f); yield return Shot("insufficient_coins");
            }
            TCSave.Coins = keepCoins;
            TCUI.Hide(menu, skinsCg); yield return W(0.35f);
        }
        if (settingsCg && menu)
        {
            TCUI.Show(menu, settingsCg);
            yield return W(0.3f);
            bool cheatOk = false;
            yield return TCCheatPlaythrough.Run(menu, ok => cheatOk = ok);
            Debug.Log("[TCFxCapture] real-event cheat playthrough ok=" + cheatOk);
            yield return W(0.35f); yield return Shot("cheat_toast");
            TCUI.Hide(menu, settingsCg); yield return W(0.3f);
        }
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

        // Normal run (not the staged tour): confirm a WEB letter actually spawns
        // from the track pool / prefab. Time is sped up only for the wait.
        if (pl) pl.paused = false;
        if (sp) sp.GrantGrace(90f);
        float prevScale = Time.timeScale;
        Time.timeScale = 8f;
        float t0 = Time.realtimeSinceStartup;
        Transform letterT = null;
        while (Time.realtimeSinceStartup - t0 < 12f)
        {
            foreach (var L in Object.FindObjectsOfType<TCWebLetter>())
                if (L && L.gameObject.activeInHierarchy) { letterT = L.transform; break; }
            if (letterT) break;
            yield return null;
        }
        Time.timeScale = prevScale <= 0f ? 1f : prevScale;
        if (!letterT && track != null)
        {
            Debug.Log("[TCFxCapture] Natural letter roll did not land in the wait — placing one via the real letter pool");
            track.SpawnLetterNow();
            foreach (var L in Object.FindObjectsOfType<TCWebLetter>())
                if (L && L.gameObject.activeInHierarchy) { letterT = L.transform; break; }
        }
        if (pl) pl.paused = true;
        yield return W(0.55f);
        yield return Shot("web_letter_run");
        Debug.Log("[TCFxCapture] WEB letter shot: " + (letterT ? letterT.root.name : "NONE") +
                  " at " + (pl ? pl.currentScore.ToString("0") : "?") + " m; pool=" +
                  (track != null ? "track present" : "no track"));

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
            if (names[i] == "magnet")
        {
            yield return W(0.5f); yield return Shot("magnet_active");
            yield return W(0.25f); yield return Shot("magnet_lightning");
            TCCoinMagnet.Instance?.Break();
            yield return W(0.12f); yield return Shot("magnet_break");
        }
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

        // Pause placement + 3-2-1 resume countdown
        sp.life = 3;
        if (ui) ui.ShowPause();
        yield return W(0.45f); yield return Shot("pause");
        yield return Shot("pause_button");
        var resumeBtn = GameObject.Find("TC_GameUI/Pause/Card/Resume");
        if (resumeBtn) resumeBtn.GetComponent<Button>()?.onClick.Invoke();
        else { var pbtn = GameObject.Find("Resume"); pbtn?.GetComponent<Button>()?.onClick.Invoke(); }
        yield return W(0.25f); yield return Shot("countdown_3");
        yield return W(0.85f); yield return Shot("countdown_2");
        yield return W(0.85f); yield return Shot("countdown_1");
        yield return W(1.0f);

        // WEB letters HUD + swing
        var web = TCWebBonus.Instance;
        if (web == null)
        {
            var wb = new GameObject("TCWebBonus");
            web = wb.AddComponent<TCWebBonus>();
        }
        var wpos = sp.transform.position;
        web.CollectLetter('W', wpos); yield return W(0.2f); yield return Shot("web_hud_w");
        web.CollectLetter('E', wpos); yield return W(0.2f); yield return Shot("web_hud_we");
        if (pl) pl.paused = false;
        if (sp) sp.GrantGrace(30f);
        web.CollectLetter('B', wpos);
        yield return W(0.25f); yield return Shot("web_hud_web");
        // Dense showcase: 20 frames across the 10 thwip-and-orbit cycles, plus landing.
        int swingShots = 20;
        float step = TCWebBonus.SwingSeconds / swingShots;
        for (int f = 0; f < swingShots; f++)
        {
            yield return Shot("web_swing_" + f);
            yield return W(step);
        }
        yield return W(0.45f);
        yield return Shot("web_swing_land");

        // Game over + x2 button hide / Home-Restart slide
        if (pl) pl.currentScore = 850;
        if (gameManager.Instance) { /* coins already accrued */ }
        var gm = gameManager.Instance;
        if (gm != null)
        {
            // Ensure there is something to double
            var field = typeof(gameManager).GetField("coinsCollected");
            if (gm.coinsCollected < 12) gm.coinsCollected = 12;
        }
        pl.paused = true;
        if (ui) ui.ShowGameOver(false);
        yield return W(0.7f); yield return Shot("gameover_before_x2");
        var dbl = GameObject.Find("TC_GameUI/Results/Card/Double");
        if (dbl)
        {
            var db = dbl.GetComponent<Button>();
            if (db) db.onClick.Invoke();
            // Standalone/desktop cannot play AdMob; no custom panel. Device grants
            // via OnUserEarnedReward. Editor grants immediately (logged).
            yield return W(0.8f); yield return Shot("gameover_after_x2");
        }

        Application.Quit();
    }

    static void ClearHazards()
    {
        foreach (var p in FindObjectsOfType<TCPooled>()) if (p.kind != "trunk") p.gameObject.SetActive(false);
    }

    IEnumerator LetterOnlyShot()
    {
        yield return W(0.6f);
        SceneManager.LoadScene("gameplay");
        yield return W(2.5f);
        GameObject.Find("TC_GameUI/Safe/Start")?.GetComponent<Button>()?.onClick.Invoke();
        var ui = FindObjectOfType<TCGameUI>(); if (ui) ui.StartRun();
        var pl = FindObjectOfType<player>();
        var sp = FindObjectOfType<playerInteract>();
        var track = FindObjectOfType<TCTrack>();
        yield return W(0.8f);
        if (pl) pl.paused = false;
        if (sp) sp.GrantGrace(90f);
        // Climb far enough for the usual mid-run framing, then freeze and place a letter.
        float prevScale = Time.timeScale;
        Time.timeScale = 4f;
        yield return W(2.2f);
        Time.timeScale = prevScale <= 0f ? 1f : prevScale;
        if (pl) pl.paused = true;
        // A natural spawn can land a long way up the trunk (and in a lane
        // the camera isn't facing). For a readable still, place one via the
        // real pool, 1.7 wu ahead in the player's current lane.
        foreach (var L in Object.FindObjectsOfType<TCWebLetter>())
        {
            var pooled = L.GetComponentInParent<TCPooled>();
            if (pooled) pooled.Release();
            else if (L) L.gameObject.SetActive(false);
        }
        Transform letterT = null;
        if (track != null)
        {
            track.SpawnLetterNow();
            foreach (var L in Object.FindObjectsOfType<TCWebLetter>())
                if (L && L.gameObject.activeInHierarchy) { letterT = L.transform; break; }
        }
        yield return W(0.55f);
        yield return Shot("web_letter_run");
        Debug.Log("[TCFxCapture] letter-only shot: " + (letterT ? letterT.root.name : "NONE") +
                  " at " + (pl ? pl.currentScore.ToString("0") : "?") + " m pos=" +
                  (letterT ? letterT.position.ToString("F2") : "-"));
    }


    IEnumerator SwingOnlyShot()
    {
        yield return W(0.6f);
        SceneManager.LoadScene("gameplay");
        yield return W(2.5f);
        GameObject.Find("TC_GameUI/Safe/Start")?.GetComponent<Button>()?.onClick.Invoke();
        var ui = FindObjectOfType<TCGameUI>(); if (ui) ui.StartRun();
        var pl = FindObjectOfType<player>();
        var sp = FindObjectOfType<playerInteract>();
        yield return W(0.8f);
        if (pl) pl.paused = false;
        if (sp) sp.GrantGrace(90f);
        float prev = Time.timeScale;
        Time.timeScale = 4f;
        yield return W(1.6f);
        Time.timeScale = prev <= 0f ? 1f : prev;
        var web = TCWebBonus.Instance;
        if (web == null)
        {
            var wb = new GameObject("TCWebBonus");
            web = wb.AddComponent<TCWebBonus>();
        }
        web.BeginSwing();
        // Wait for the camera pull-in so the first frame is already close.
        yield return W(0.32f);
        // 18 frames fit inside the 9s swing even with Shot()'s extra end-of-frame waits.
        const int frames = 18;
        float step = 0.38f;
        for (int f = 0; f < frames; f++)
        {
            yield return Shot("web_swing_" + f);
            yield return W(step);
        }
        // Land / +5000m while the close camera is held.
        float t0 = Time.realtimeSinceStartup;
        while (!(web && web.ShowcaseHold) && Time.realtimeSinceStartup - t0 < 4f)
            yield return null;
        yield return W(0.12f);
        yield return Shot("web_swing_land");
        yield return W(0.22f);
        yield return Shot("web_swing_pop");
        Debug.Log("[TCFxCapture] swing-only done, hold=" + (web && web.ShowcaseHold) + " swinging=" + (web && web.IsSwinging));
    }

}
