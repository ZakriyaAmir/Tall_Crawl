using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// In-game HUD, tap-to-start + first-run tutorial, pause, revive and results screens, popups.
public class TCGameUI : MonoBehaviour
{
    public static TCGameUI Instance;
    player pl; playerInteract spider; gameManager gm;
    RectTransform safe, heartsRow; TextMeshProUGUI scoreText, coinText, hintText, boostText;
    readonly List<Image> hearts = new List<Image>();
    CanvasGroup startOverlay, pausePanel, revivePanel, resultPanel, flash;
    RectTransform hand; Coroutine tutorialCo;
    int shownCoins = -1, shownMetres = -1, shownLife = -1;
    bool started, turnedOnce, jumpedOnce;
    readonly Queue<TextMeshProUGUI> popPool = new Queue<TextMeshProUGUI>();
    TextMeshProUGUI resMetres, resBest, resCoins, resBonus, resTotal, reviveTimer; Button doubleBtn, reviveGemBtn;
    Camera cam;

    void Awake() { Instance = this; }

    void Start()
    {
        pl = FindObjectOfType<player>(); spider = FindObjectOfType<playerInteract>(); gm = gameManager.Instance; cam = Camera.main;
        var canvas = TCUI.MakeCanvas("TC_GameUI", 10);
        safe = TCUI.SafeRoot(canvas.transform);
        BuildHud(); BuildStart(); BuildPause(); BuildRevive(); BuildResults();
        flash = TCUI.Rect("Flash", canvas.transform).gameObject.AddComponent<CanvasGroup>();
        TCUI.Stretch((RectTransform)flash.transform); var fi = flash.gameObject.AddComponent<Image>(); fi.color = new Color(1, 0.35f, 0.4f, 0.35f); fi.raycastTarget = false; flash.alpha = 0;
        playerInteract.OnEvent += OnGameEvent;
        player.OnTurned += () => turnedOnce = true;
        player.OnJumped += () => jumpedOnce = true;
        TCUI.Show(this, startOverlay, false);
    }
    void OnDestroy() { playerInteract.OnEvent -= OnGameEvent; player.OnTurned = null; player.OnJumped = null; }

    // ---------------- HUD ----------------
    void BuildHud()
    {
        var top = TCUI.Rect("Top", safe); top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1); top.sizeDelta = new Vector2(0, 200); top.anchoredPosition = Vector2.zero;
        var pill = TCUI.Img("ScorePill", top, new Color(1, 1, 1, 0.85f)); TCUI.Place(pill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(360, 120));
        scoreText = TCUI.Label("Score", pill.transform, "0M", 72, TCUI.Cocoa); TCUI.Stretch(scoreText.rectTransform);
        var cp = TCUI.Img("Coins", top, new Color(1, 1, 1, 0.85f)); TCUI.Place(cp.rectTransform, new Vector2(1, 0.5f), new Vector2(-190, 10), new Vector2(260, 96));
        var ci = TCUI.Img("Icon", cp.transform, Color.white, TCUI.Icon("coin")); TCUI.Place(ci.rectTransform, new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(72, 72));
        coinText = TCUI.Label("Value", cp.transform, "0", 52, TCUI.Cocoa, TextAlignmentOptions.Right); TCUI.Stretch(coinText.rectTransform, 90, 26, 0, 0);
        heartsRow = TCUI.Rect("Hearts", top); TCUI.Place(heartsRow, new Vector2(0, 0.5f), new Vector2(170, 10), new Vector2(300, 80));
        var pause = TCUI.Btn("Pause", top, "", TCUI.Butter, ShowPause, 40, TCUI.Icon("pause"));
        TCUI.Place((RectTransform)pause.transform, new Vector2(0, 0.5f), new Vector2(170, -110), new Vector2(110, 110));
        boostText = TCUI.Label("Boost", safe, "BOOST!", 110, TCUI.Peach); TCUI.Place(boostText.rectTransform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(900, 160));
        TCUI.Outline(boostText, 0.25f); boostText.gameObject.SetActive(false);
    }

    void RefreshHearts()
    {
        int max = spider.MaxLife;
        while (hearts.Count < max)
        {
            var h = TCUI.Img("Heart", heartsRow, Color.white, TCUI.Icon("heart"));
            TCUI.Place(h.rectTransform, new Vector2(0, 0.5f), new Vector2(40 + hearts.Count * 66, 0), new Vector2(64, 64));
            hearts.Add(h);
        }
        for (int i = 0; i < hearts.Count; i++)
        {
            bool full = i < spider.life;
            hearts[i].sprite = TCUI.Icon(full ? "heart" : "heartEmpty");
            hearts[i].gameObject.SetActive(i < max);
        }
    }

    void Update()
    {
        if (!pl) return;
        if (Input.GetKeyDown(KeyCode.Escape)) // Android back: pause / resume
        {
            if (pausePanel.gameObject.activeSelf) HidePause(); else ShowPause();
        }
        int m = (int)pl.currentScore;
        if (m != shownMetres) { shownMetres = m; scoreText.text = m + "M"; }
        if (gm.coinsCollected != shownCoins) { if (shownCoins >= 0) StartCoroutine(TCUI.Punch(coinText.transform.parent, 0.15f, 0.18f)); shownCoins = gm.coinsCollected; coinText.text = shownCoins.ToString(); }
        if (spider.life != shownLife) { shownLife = spider.life; RefreshHearts(); }
        if (boostText.gameObject.activeSelf != pl.boost) boostText.gameObject.SetActive(pl.boost);
        if (pl.boost) boostText.transform.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(Time.time * 14));
        if (flash.alpha > 0) flash.alpha = Mathf.MoveTowards(flash.alpha, 0, Time.unscaledDeltaTime * 2.5f);
    }

    void OnGameEvent(string e, Vector3 pos)
    {
        switch (e)
        {
            case "coin": Pop("+" + gameManager.CoinValue, pos, TCUI.Butter); break;
            case "heart": Pop("+1 HEART", pos, TCUI.Blush); StartCoroutine(TCUI.Punch(heartsRow, 0.25f)); break;
            case "boost": Pop("BOOST", pos, TCUI.Peach); break;
            case "smash": Pop("SMASH!", pos, TCUI.Mint); break;
            case "hit": flash.alpha = 1; StartCoroutine(TCUI.Punch(heartsRow, 0.3f)); break;
        }
    }

    void Pop(string text, Vector3 world, Color c)
    {
        if (!cam) cam = Camera.main; if (!cam) return;
        var t = popPool.Count > 0 ? popPool.Dequeue() : null;
        if (!t) { t = TCUI.Label("Pop", safe.parent, "", 64, c); t.rectTransform.sizeDelta = new Vector2(400, 100); TCUI.Outline(t, 0.22f); }
        t.text = text; t.color = c; t.gameObject.SetActive(true);
        StartCoroutine(PopRoutine(t, cam.WorldToScreenPoint(world)));
    }
    IEnumerator PopRoutine(TextMeshProUGUI t, Vector3 screen)
    {
        var canvas = t.canvas; float s = canvas ? canvas.scaleFactor : 1;
        var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = Vector2.zero;
        Vector2 start = new Vector2(screen.x, screen.y) / s;
        for (float x = 0; x < 0.7f; x += Time.deltaTime)
        {
            float k = x / 0.7f;
            rt.anchoredPosition = start + new Vector2(0, 160 * TCUI.EaseOut(k));
            rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.2f, k / 0.15f) : 1);
            var c = t.color; c.a = 1 - k * k; t.color = c;
            yield return null;
        }
        t.gameObject.SetActive(false); popPool.Enqueue(t);
    }

    // ---------------- Start / Tutorial ----------------
    void BuildStart()
    {
        var root = TCUI.Rect("Start", safe); TCUI.Stretch(root);
        startOverlay = root.gameObject.AddComponent<CanvasGroup>();
        var hit = root.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0.001f);
        var b = root.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.onClick.AddListener(StartRun);
        hintText = TCUI.Label("Hint", root, TCSave.TutorialDone ? "TAP TO CLIMB" : "DRAG LEFT / RIGHT\nto circle the trunk", 72, Color.white);
        TCUI.Outline(hintText, 0.25f);
        TCUI.Place(hintText.rectTransform, new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(1000, 260));
        hand = TCUI.Img("Hand", root, Color.white, TCUI.Icon("hand")).rectTransform; TCUI.Place(hand, new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(150, 150));
        StartCoroutine(HandWiggle());
    }
    IEnumerator HandWiggle()
    {
        while (true)
        {
            if (hand && hand.gameObject.activeInHierarchy) hand.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 3) * 180, 0);
            hintText.transform.localScale = Vector3.one * (1 + 0.04f * Mathf.Sin(Time.unscaledTime * 4));
            yield return null;
        }
    }

    void StartRun()
    {
        if (started) return; started = true;
        startOverlay.blocksRaycasts = false;
        gm.BeginRun();
        if (TCSave.TutorialDone) TCUI.Hide(this, startOverlay);
        else tutorialCo = StartCoroutine(Tutorial());
    }

    IEnumerator Tutorial()
    {
        turnedOnce = jumpedOnce = false;
        hintText.text = "DRAG LEFT / RIGHT\nto dodge branches";
        float t = 0; while (!turnedOnce && t < 6) { t += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(1.5f);
        hintText.text = "SWIPE UP or DOUBLE-TAP\nto jump over bee swarms!";
        hand.gameObject.SetActive(false);
        t = 0; while (!jumpedOnce && t < 6) { t += Time.deltaTime; yield return null; }
        hintText.text = "Grab coins, hearts\nand chilli boosts!";
        yield return new WaitForSeconds(2.2f);
        TCSave.TutorialDone = true;
        TCUI.Hide(this, startOverlay);
    }

    // ---------------- Pause ----------------
    void BuildPause()
    {
        pausePanel = TCUI.Panel("Pause", safe.parent, out var card, new Vector2(820, 1100), "PAUSED");
        float y = 260;
        TCUI.Place((RectTransform)TCUI.Btn("Resume", card, "RESUME", TCUI.Mint, HidePause, 60, TCUI.Icon("play")).transform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(600, 140));
        TCUI.Place((RectTransform)TCUI.Btn("Restart", card, "RESTART", TCUI.Butter, () => { gm.EndRun(false); gm.Restart(); }).transform, new Vector2(0.5f, 0.5f), new Vector2(0, y - 180), new Vector2(600, 130));
        TCUI.Place((RectTransform)TCUI.Btn("Home", card, "HOME", TCUI.Sky, () => { gm.EndRun(false); gm.Home(); }).transform, new Vector2(0.5f, 0.5f), new Vector2(0, y - 340), new Vector2(600, 130));
        Toggle(card, "Sound", -270, () => TCSave.Sound, v => TCSave.Sound = v);
        Toggle(card, "Music", -360, () => TCSave.Music, v => { TCSave.Music = v; gm.SetMusic(v); });
        Toggle(card, "Vibration", -450, () => TCSave.Vibration, v => TCSave.Vibration = v);
    }
    public static void Toggle(RectTransform card, string label, float y, System.Func<bool> get, System.Action<bool> set, float width = 600)
    {
        var row = TCUI.Rect(label, card); TCUI.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(width, 90));
        var l = TCUI.Label("L", row, label, 48, TCUI.Cocoa, TextAlignmentOptions.Left); TCUI.Stretch(l.rectTransform, 0, 200, 0, 0);
        Button b = null; TextMeshProUGUI bt = null;
        b = TCUI.Btn("T", row, "", get() ? TCUI.Mint : TCUI.Grey, () =>
        {
            set(!get());
            b.GetComponent<Image>().color = get() ? TCUI.Mint : TCUI.Grey; bt.text = get() ? "ON" : "OFF";
        }, 44);
        TCUI.Place((RectTransform)b.transform, new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(180, 84));
        bt = TCUI.Label("V", b.transform, get() ? "ON" : "OFF", 44, TCUI.Cocoa); TCUI.Stretch(bt.rectTransform);
    }

    public void ShowPause()
    {
        if (!started || pl.GameOver || pausePanel.gameObject.activeSelf) return;
        gm.Pause(true); TCUI.Show(this, pausePanel);
    }
    void HidePause() { if (!pausePanel.gameObject.activeSelf) return; TCUI.Hide(this, pausePanel, () => gm.Pause(false)); }

    // ---------------- Revive ----------------
    void BuildRevive()
    {
        revivePanel = TCUI.Panel("Revive", safe.parent, out var card, new Vector2(840, 940), "KEEP CLIMBING?");
        var timerBg = TCUI.Img("TimerBg", card, new Color(1f, 0.92f, 0.85f, 0.95f), TCUI.Circle);
        TCUI.Place(timerBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(160, 160));
        reviveTimer = TCUI.Label("Timer", timerBg.transform, "5", 100, TCUI.Peach);
        TCUI.Stretch(reviveTimer.rectTransform);
        TCUI.Outline(reviveTimer, 0.2f);

        var adBtn = TCUI.Btn("Ad", card, "REVIVE (WATCH AD)", TCUI.Mint, () => { StopReviveTimer(); TCServices.Rewarded(DoRevive, "revive"); }, 50, TCUI.Icon("play"));
        TCUI.Place((RectTransform)adBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(680, 130));

        reviveGemBtn = TCUI.Btn("Gems", card, "REVIVE (" + TCConfig.I.reviveCostGems + " GEMS)", TCUI.Sky, () =>
        {
            if (TCSave.Gems >= TCConfig.I.reviveCostGems) { TCSave.Gems -= TCConfig.I.reviveCostGems; DoRevive(); }
        }, 48, TCUI.Icon("gem"));
        TCUI.Place((RectTransform)reviveGemBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(680, 120));

        var skipBtn = TCUI.Btn("Skip", card, "NO THANKS", TCUI.Grey, SkipRevive, 42);
        TCUI.Place((RectTransform)skipBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -240), new Vector2(440, 95));
    }
    Coroutine reviveCo;
    void StopReviveTimer() { if (reviveCo != null) { StopCoroutine(reviveCo); reviveCo = null; } }
    public void ShowGameOver(bool canRevive)
    {
        // A death during the first-run tutorial left its overlay up over the results
        if (tutorialCo != null) { StopCoroutine(tutorialCo); tutorialCo = null; TCSave.TutorialDone = true; }
        if (startOverlay.alpha > 0) TCUI.Hide(this, startOverlay);
        if (canRevive) { reviveGemBtn.interactable = TCSave.Gems >= TCConfig.I.reviveCostGems; TCUI.Show(this, revivePanel); reviveCo = StartCoroutine(ReviveCountdown()); }
        else ShowResults();
    }
    IEnumerator ReviveCountdown()
    {
        for (int i = 5; i > 0; i--) { reviveTimer.text = i.ToString(); StartCoroutine(TCUI.Punch(reviveTimer.transform, 0.25f)); yield return new WaitForSecondsRealtime(1.1f); }
        SkipRevive();
    }
    void DoRevive()
    {
        StopReviveTimer();
        if (!revivePanel.gameObject.activeSelf || gm.runEnded) return; // timer already sent us to results
        TCUI.Hide(this, revivePanel); gm.Revive();
    }
    void SkipRevive() { StopReviveTimer(); if (!revivePanel.gameObject.activeSelf) return; TCUI.Hide(this, revivePanel, ShowResults); }

    // ---------------- Results ----------------
    void BuildResults()
    {
        resultPanel = TCUI.Panel("Results", safe.parent, out var card, new Vector2(860, 1200), "GAME OVER");

        var caption = TCUI.Label("Caption", card, "DISTANCE CLIMBED", 32, new Color(0.24f, 0.19f, 0.27f, 0.65f));
        TCUI.Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 465), new Vector2(600, 38));

        resMetres = TCUI.Label("Metres", card, "0 M", 92, TCUI.Cocoa);
        resMetres.enableAutoSizing = true; resMetres.fontSizeMin = 56; resMetres.fontSizeMax = 92;
        TCUI.Place(resMetres.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 390), new Vector2(760, 95));

        var bestBadge = TCUI.Img("BestBadge", card, new Color(1f, 0.93f, 0.86f, 0.95f), TCUI.Round);
        TCUI.Place(bestBadge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 295), new Vector2(380, 58));
        resBest = TCUI.Label("Best", bestBadge.transform, "BEST 0 M", 34, TCUI.Peach);
        resBest.enableWordWrapping = false; resBest.enableAutoSizing = true; resBest.fontSizeMin = 20; resBest.fontSizeMax = 34;
        TCUI.Stretch(resBest.rectTransform, 14, 14, 4, 4);

        resCoins = Row(card, "Coins Gathered", 195, false);
        resBonus = Row(card, "Distance Bonus", 95, false);
        resTotal = Row(card, "Total Coins Earned", -10, true);

        doubleBtn = TCUI.Btn("Double", card, "x2 COINS (WATCH AD)", TCUI.Butter, () => TCServices.Rewarded(() =>
        {
            if (!gm.ClaimDouble()) return; // reward callbacks can fire twice; never double-pay
            resTotal.text = (gm.RunReward * 2).ToString("N0");
            doubleBtn.interactable = false; StartCoroutine(TCUI.Punch(resTotal.transform, 0.4f, 0.4f)); TCAudio.Play(TCAssets.I.purchase);
        }, "double_coins"), 48, TCUI.Icon("play"));
        TCUI.Place((RectTransform)doubleBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -155), new Vector2(700, 120));

        var againBtn = TCUI.Btn("Again", card, "PLAY AGAIN", TCUI.Mint, () => gm.Restart(), 54);
        TCUI.Place((RectTransform)againBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -295), new Vector2(700, 125));

        var homeBtn = TCUI.Btn("Home", card, "MAIN MENU", TCUI.Sky, () => gm.Home(), 44);
        TCUI.Place((RectTransform)homeBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -430), new Vector2(700, 100));
    }

    TextMeshProUGUI Row(RectTransform card, string label, float y, bool isTotal)
    {
        Color bgCol = isTotal ? new Color(1f, 0.95f, 0.88f, 0.98f) : new Color(1f, 1f, 1f, 0.92f);
        var row = TCUI.Img(label, card, bgCol);
        TCUI.Place(row.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(740, isTotal ? 94 : 84));

        var l = TCUI.Label("L", row.transform, label, isTotal ? 36 : 32, TCUI.Cocoa, TextAlignmentOptions.Left);
        l.enableWordWrapping = false;
        TCUI.Stretch(l.rectTransform, 32, 260, 0, 0);

        var ic = TCUI.Img("I", row.transform, Color.white, TCUI.Icon("coin"));
        TCUI.Place(ic.rectTransform, new Vector2(1, 0.5f), new Vector2(-42, 0), new Vector2(isTotal ? 54 : 46, isTotal ? 54 : 46));

        var v = TCUI.Label("V", row.transform, "0", isTotal ? 46 : 40, isTotal ? TCUI.Hex("#E07A5F") : TCUI.Cocoa, TextAlignmentOptions.Right);
        v.enableWordWrapping = false; v.enableAutoSizing = true; v.fontSizeMin = 22; v.fontSizeMax = isTotal ? 46 : 40;
        TCUI.Stretch(v.rectTransform, 300, 84, 0, 0);
        return v;
    }

    void ShowResults()
    {
        int prevBest = TCSave.Best;
        int total = gm.EndRun(false);
        resMetres.text = gm.Metres.ToString("N0") + " M";
        bool newBest = gm.Metres > prevBest;
        resBest.text = newBest ? "NEW BEST!" : "BEST  " + TCSave.Best.ToString("N0") + " M";
        resCoins.text = gm.coinsCollected.ToString("N0");
        resBonus.text = gm.DistanceBonus.ToString("N0") + (gm.RunGems > 0 ? "  <color=#5FA8D3>+" + gm.RunGems + " GEM" + (gm.RunGems > 1 ? "S" : "") + "</color>" : "");
        resTotal.text = total.ToString("N0");
        doubleBtn.interactable = total > 0;
        TCUI.Show(this, resultPanel);
        if (newBest) StartCoroutine(TCUI.Punch(resBest.transform, 0.35f, 0.5f));
    }
}
