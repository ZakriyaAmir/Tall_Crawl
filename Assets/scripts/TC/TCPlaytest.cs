using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Automated play-tester. Only active when the player is launched with "-tcplaytest <dir>"
/// (optional "-tcruns N", "-tcscale S"). It plays real runs with a steering bot, exercises every
/// menu/economy flow, and writes a report (errors, FPS, GC, pool leaks, fairness, economy checks).
public class TCPlaytest : MonoBehaviour
{
    string dir; int runs = 6; float scale = 1f; int shot;
    readonly StringBuilder log = new StringBuilder();
    int errors, warnings, fails;
    readonly List<Vector2> rows = new List<Vector2>(); // (y, safeAngle), window 360 = bees encoded as negative y? kept separately
    readonly List<float> beeRows = new List<float>();
    float worstTurnRate; int unfairRows, rowCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var a = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(a, "-tcplaytest"); if (i < 0) return;
        PlayerPrefs.DeleteAll(); PlayerPrefs.SetInt("consent", 1); PlayerPrefs.Save();
        var g = new GameObject("TCPlaytest"); DontDestroyOnLoad(g);
        var t = g.AddComponent<TCPlaytest>(); t.dir = i + 1 < a.Length ? a[i + 1] : "/tmp";
        int r = System.Array.IndexOf(a, "-tcruns"); if (r >= 0) int.TryParse(a[r + 1], out t.runs);
        int s = System.Array.IndexOf(a, "-tcscale"); if (s >= 0) float.TryParse(a[s + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out t.scale);
    }

    void Awake()
    {
        Application.logMessageReceived += (msg, st, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; L("!! " + type + ": " + msg + " | " + st.Split('\n')[0]); }
            else if (type == LogType.Warning) { warnings++; if (warnings < 40) L("w: " + msg); }
        };
        TCTrack.OnRow += (y, ang, turn, win, secs, m) =>
        {
            rowCount++;
            if (win >= 360) { beeRows.Add(y); return; }
            rows.Add(new Vector2(y, ang));
            float need = Mathf.Max(0, turn - win * 0.5f) / Mathf.Max(0.05f, secs);
            worstTurnRate = Mathf.Max(worstTurnRate, need);
            if (need > 300) unfairRows++;
        };
    }

    void L(string s) { var line = $"[{Time.realtimeSinceStartup,7:0.0}] {s}"; log.AppendLine(line); try { File.AppendAllText(Path.Combine(dir, "playtest.txt"), line + "\n"); } catch { } }
    void Check(bool ok, string what) { if (!ok) { fails++; L("FAIL " + what); } else L("ok   " + what); }
    IEnumerator Shot(string n) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot($"{dir}/{++shot:00}_{n}.png"); yield return new WaitForSecondsRealtime(0.3f); }
    IEnumerator W(float s) { yield return new WaitForSecondsRealtime(s); }
    static Button Btn(string path) { var g = GameObject.Find(path); return g ? g.GetComponent<Button>() : null; }
    void Click(string path) { var b = Btn(path); if (b && b.interactable) b.onClick.Invoke(); else L("cannot click " + path + (b ? " (disabled)" : " (missing)")); }

    IEnumerator Start()
    {
        Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "playtest.txt"), "");
        L($"Tall Crawl playtest  screen {Screen.width}x{Screen.height}  safe {Screen.safeArea}  runs {runs} scale {scale}");
        yield return W(3f);
        var menu = FindObjectOfType<TCMenuUI>();
        // ---- Daily reward ----
        int c0 = TCSave.Coins;
        yield return Shot("daily");
        Click("TC_MenuUI/Safe/Daily/Card/Claim");
        yield return W(1f);
        Check(TCSave.Coins == c0 + TCConfig.I.dailyRewards[0], $"daily day1 paid {TCSave.Coins - c0}");
        int c1 = TCSave.Coins; Click("TC_MenuUI/Safe/Daily/Card/Claim");
        Check(TCSave.Coins == c1 && !TCSave.DailyAvailable(out _), "daily cannot be claimed twice");
        PlayerPrefs.SetString("tc_daily_last", System.DateTime.Now.Date.AddDays(-1).ToString("yyyy-MM-dd"));
        Check(TCSave.DailyAvailable(out int d2) && d2 == 1, "daily next day -> day 2");
        PlayerPrefs.SetString("tc_daily_last", System.DateTime.Now.Date.AddDays(-3).ToString("yyyy-MM-dd"));
        Check(TCSave.DailyAvailable(out int d3) && d3 == 0, "daily missed day -> streak reset");
        PlayerPrefs.SetString("tc_daily_last", System.DateTime.Now.Date.ToString("yyyy-MM-dd"));
        yield return Shot("main_menu");
        // ---- Panels ----
        foreach (var p in new[] { "Skins", "Shop", "Settings" })
        {
            var cg = menu.GetPanel(p); TCUI.Show(menu, cg); yield return W(0.8f);
            yield return Shot(p.ToLower());
            TCUI.Hide(menu, cg); TCUI.Hide(menu, cg); yield return W(0.6f); // double close must be harmless
            Check(!cg.gameObject.activeSelf, p + " closes");
        }
        // ---- Economy: upgrades ----
        TCSave.Coins = 1000;
        var shop = menu.GetPanel("Shop"); TCUI.Show(menu, shop); yield return W(0.5f);
        int before = TCSave.Coins; Click("TC_MenuUI/Safe/Shop/Card/Upg3/Buy");
        Check(TCSave.Upgrade(3) == 1 && TCSave.Coins == before - TCConfig.I.upgradeCosts[0], $"upgrade coin value: lvl {TCSave.Upgrade(3)} cost {before - TCSave.Coins}");
        TCSave.Coins = 50; Click("TC_MenuUI/Safe/Shop/Card/Upg1/Buy");
        Check(TCSave.Upgrade(1) == 0 && TCSave.Coins == 50, "cannot buy upgrade without coins");
        TCSave.Coins = -20; Check(TCSave.Coins == 0, "coins never negative");
        TCSave.Coins = 400; TCUI.Hide(menu, shop); yield return W(0.5f);
        // ---- Settings persistence ----
        TCSave.Music = false; TCSave.Sound = true;
        Check(PlayerPrefs.GetInt("tc_music_off") == 1, "music off persisted");
        var bgm = GameObject.Find("bgm"); if (bgm) Check(bgm.GetComponent<AudioSource>().mute, "menu music muted after toggle");
        TCSave.Music = true;
        // ---- Skins ----
        TCSave.UnlockSkin(2); TCSave.SelectedSkin = 2;
        Check(TCSave.SkinOwned(2) && !TCSave.SkinOwned(5), "skin unlock/ownership");

        // ---- Runs ----
        long mem0 = 0; int[] created0 = null;
        for (int run = 0; run < runs; run++)
        {
            if (run == 0) SceneManager.LoadScene("gameplay");
            yield return W(2.5f);
            if (run == 0) yield return Shot("tap_to_start");
            Time.timeScale = 1;
            var gm = gameManager.Instance; var pl = FindObjectOfType<player>(); var sp = FindObjectOfType<playerInteract>(); var tr = FindObjectOfType<TCTrack>();
            var ui = TCGameUI.Instance;
            rows.Clear(); beeRows.Clear();
            Click("TC_GameUI/Safe/Start");
            int coinsStart = TCSave.Coins, gemsStart = TCSave.Gems;
            bool skilled = run % 3 != 2; // every third run plays badly (random) to test deaths early
            float t = 0, fpsMin = 999, steerSign = 1; int frames = 0; float lastScore = 0, stuck = 0; int gc0 = System.GC.CollectionCount(0);
            float lastAngle = Ang(sp.transform.position), lastSteer = 0; bool paused = false, revived = false; int nan = 0;
            Time.timeScale = scale;
            while (true)
            {
                t += Time.unscaledDeltaTime; frames++;
                if (Time.unscaledDeltaTime > 0) fpsMin = Mathf.Min(fpsMin, 1f / Time.unscaledDeltaTime);
                if (pl.GameOver) break;
                var pos = sp.transform.position;
                if (float.IsNaN(pos.x + pos.y + pos.z)) nan++;
                float a = Ang(pos);
                // learn steering direction
                float moved = Mathf.DeltaAngle(lastAngle, a);
                if (Mathf.Abs(lastSteer) > 0.5f && Mathf.Abs(moved) > 0.5f) steerSign = Mathf.Sign(moved) * Mathf.Sign(lastSteer);
                lastAngle = a;
                float steer = 0;
                if (skilled)
                {
                    float target = a; foreach (var r in rows) if (r.x > pos.y - 0.1f) { target = r.y; break; }
                    float delta = Mathf.DeltaAngle(a, target);
                    steer = Mathf.Clamp(delta / 25f, -1, 1) * steerSign;
                    foreach (var b in beeRows) if (b > pos.y && b - pos.y < 0.35f + pl.calcSpeed * 0.12f) { pl.jump(); break; }
                }
                else steer = Mathf.Sin(t * 1.7f);
                player.DebugSteer = steer; lastSteer = steer;
                if (!pl.paused && Time.timeScale > 0)
                {
                    if (pl.currentScore <= lastScore + 0.0001f) stuck += Time.unscaledDeltaTime; else stuck = 0;
                    lastScore = pl.currentScore;
                    if (stuck > 1.5f) { L("FAIL player stuck at " + pl.currentScore); fails++; stuck = -999; }
                }
                if (!paused && t > 6f) // pause/resume mid run
                {
                    paused = true; ui.ShowPause(); yield return W(0.6f);
                    Check(Time.timeScale == 0, "pause freezes time");
                    if (run == 0) yield return Shot("pause");
                    Click("TC_GameUI/Pause/Card/Resume"); yield return W(0.5f);
                    Check(Time.timeScale == 1, "resume restores time"); Time.timeScale = scale;
                }
                if (run == 0 && (frames == 400 || frames == 900)) yield return Shot("gameplay");
                if (t > 75f) { L("run capped at 75s, forcing death"); sp.death(); }
                yield return null;
            }
            player.DebugSteer = 0; Time.timeScale = 1;
            float metres = pl.currentScore;
            L($"run {run} {(skilled ? "bot" : "random")}: {metres:0} m in {t:0.0}s, coins {gm.coinsCollected}, fpsMin {fpsMin:0}, gc0 {System.GC.CollectionCount(0) - gc0}, live {tr.LiveCount}, NaN {nan}, rows {rows.Count} bees {beeRows.Count}");
            var pools = tr.Pools; var sb = new StringBuilder("   pools created: ");
            foreach (var p in pools) sb.Append(p.Created + "/" + p.FreeCount + " ");
            L(sb.ToString());
            yield return W(1.8f);
            // revive flow
            var revive = GameObject.Find("TC_GameUI/Revive");
            Check(revive && revive.activeSelf, "revive offered on first death");
            if (run == 0) yield return Shot("revive");
            if (run % 2 == 0 && revive)
            {
                Click("TC_GameUI/Revive/Card/Ad"); revived = true; yield return W(2.2f);
                Check(!pl.GameOver && !pl.paused, "revived and moving");
                yield return W(6f); // timer must not fire results after revive
                Check(!gm.runEnded, "revive timer cancelled");
                Time.timeScale = scale; float tt = 0;
                while (!pl.GameOver && tt < 30) { tt += Time.unscaledDeltaTime; player.DebugSteer = Mathf.Sin(tt * 2.3f); yield return null; }
                player.DebugSteer = 0; Time.timeScale = 1;
                if (!pl.GameOver) sp.death();
                yield return W(1.8f);
                Check(!GameObject.Find("TC_GameUI/Revive"), "no 2nd revive in a run");
            }
            else yield return W(6.5f); // let the timer expire
            yield return W(1.5f);
            Check(gm.runEnded, "results shown / run banked");
            int expected = gm.coinsCollected + (int)pl.currentScore / 100 * TCConfig.I.coinsPerHundredMetres;
            Check(TCSave.Coins == coinsStart + expected, $"run reward banked once: +{TCSave.Coins - coinsStart} expected +{expected}");
            Check(TCSave.Gems == gemsStart + gm.RunGems, "run gems");
            if (run == 0) yield return Shot("game_over");
            int cb = TCSave.Coins;
            Click("TC_GameUI/Results/Card/Double"); Click("TC_GameUI/Results/Card/Double");
            gm.ClaimDouble();
            Check(TCSave.Coins == cb + gm.RunReward, $"x2 paid exactly once (+{TCSave.Coins - cb}, reward {gm.RunReward})");
            if (run == 0) yield return Shot("game_over_doubled");
            if (run == 1) { long m = System.GC.GetTotalMemory(true); mem0 = m; created0 = System.Array.ConvertAll(tr.Pools, p => p.Created); }
            if (run == runs - 1 && created0 != null)
            {
                L($"managed mem run1 {mem0 / 1024}KB -> now {System.GC.GetTotalMemory(true) / 1024}KB");
            }
            if (run % 3 == 1) { Click("TC_GameUI/Results/Card/Home"); yield return W(2.5f); Check(FindObjectOfType<TCMenuUI>(), "home -> menu"); yield return W(0.5f); var dm = FindObjectOfType<TCMenuUI>(); if (dm) TCUI.Hide(dm, dm.GetPanel("Daily")); Click("TC_MenuUI/Safe/Play"); if (!FindObjectOfType<player>()) SceneManager.LoadScene("gameplay"); }
            else Click("TC_GameUI/Results/Card/Again");
            Check(Time.timeScale == 1, "timescale reset after run");
        }
        yield return W(2f);
        L($"Track fairness: rows {rowCount}, worst required steer {worstTurnRate:0} deg/s, rows needing >300deg/s: {unfairRows}");
        L($"SUMMARY errors {errors} warnings {warnings} failed checks {fails}");
        
        Application.Quit();
    }

    static float Ang(Vector3 p) => Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
}
