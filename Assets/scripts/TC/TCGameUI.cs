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
        boostText.outlineWidth = 0.25f; boostText.outlineColor = TCUI.Cocoa; boostText.gameObject.SetActive(false);
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
            case "coin": Pop("+" + (TCConfig.I.coinValue * (1 + TCSave.Upgrade(3))), pos, TCUI.Butter); break;
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
        if (!t) { t = TCUI.Label("Pop", safe.parent, "", 64, c); t.rectTransform.sizeDelta = new Vector2(400, 100); t.outlineWidth = 0.22f; t.outlineColor = TCUI.Cocoa; }
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
        hintText.outlineWidth = 0.25f; hintText.outlineColor = TCUI.Cocoa;
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
        hintText.text = "SWIPE UP to jump\nover bee swarms!";
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
        pausePanel = TCUI.Panel("Pause", safe.parent, out var card, new Vector2(820, 1000), "PAUSED");
        float y = 230;
        TCUI.Place((RectTransform)TCUI.Btn("Resume", card, "RESUME", TCUI.Mint, HidePause, 60, TCUI.Icon("play")).transform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(600, 140));
        TCUI.Place((RectTransform)TCUI.Btn("Restart", card, "RESTART", TCUI.Butter, () => { gm.EndRun(false); gm.Restart(); }).transform, new Vector2(0.5f, 0.5f), new Vector2(0, y - 180), new Vector2(600, 130));
        TCUI.Place((RectTransform)TCUI.Btn("Home", card, "HOME", TCUI.Sky, () => { gm.EndRun(false); gm.Home(); }).transform, new Vector2(0.5f, 0.5f), new Vector2(0, y - 340), new Vector2(600, 130));
        Toggle(card, "Sound", -300, () => TCSave.Sound, v => TCSave.Sound = v);
        Toggle(card, "Vibration", -400, () => TCSave.Vibration, v => TCSave.Vibration = v);
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
    void HidePause() { TCUI.Hide(this, pausePanel, () => gm.Pause(false)); }

    // ---------------- Revive ----------------
    void BuildRevive()
    {
        revivePanel = TCUI.Panel("Revive", safe.parent, out var card, new Vector2(820, 900), "SECOND CHANCE?");
        reviveTimer = TCUI.Label("Timer", card, "5", 160, TCUI.Peach); TCUI.Place(reviveTimer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(300, 200));
        reviveTimer.outlineWidth = 0.2f; reviveTimer.outlineColor = TCUI.Cocoa;
        TCUI.Place((RectTransform)TCUI.Btn("Ad", card, "REVIVE (AD)", TCUI.Mint, () => TCServices.Rewarded(DoRevive, "revive"), 54, TCUI.Icon("play")).transform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(620, 140));
        reviveGemBtn = TCUI.Btn("Gems", card, "REVIVE  " + TCConfig.I.reviveCostGems, TCUI.Sky, () =>
        {
            if (TCSave.Gems >= TCConfig.I.reviveCostGems) { TCSave.Gems -= TCConfig.I.reviveCostGems; DoRevive(); }
        }, 54, TCUI.Icon("gem"));
        TCUI.Place((RectTransform)reviveGemBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(620, 130));
        TCUI.Place((RectTransform)TCUI.Btn("Skip", card, "NO THANKS", TCUI.Grey, SkipRevive, 44).transform, new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(420, 100));
    }
    Coroutine reviveCo;
    public void ShowGameOver(bool canRevive)
    {
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
        if (reviveCo != null) StopCoroutine(reviveCo);
        TCUI.Hide(this, revivePanel); gm.Revive();
    }
    void SkipRevive() { if (reviveCo != null) StopCoroutine(reviveCo); TCUI.Hide(this, revivePanel, ShowResults); }

    // ---------------- Results ----------------
    void BuildResults()
    {
        resultPanel = TCUI.Panel("Results", safe.parent, out var card, new Vector2(880, 1250), "GAME OVER");
        resMetres = TCUI.Label("Metres", card, "0M", 140, TCUI.Cocoa); TCUI.Place(resMetres.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 400), new Vector2(800, 170));
        resBest = TCUI.Label("Best", card, "BEST 0M", 48, TCUI.Peach); TCUI.Place(resBest.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 290), new Vector2(800, 70));
        resCoins = Row(card, "Coins collected", 180); resBonus = Row(card, "Distance bonus", 100); resTotal = Row(card, "Total", 10);
        resTotal.fontSize = 60;
        doubleBtn = TCUI.Btn("Double", card, "x2 COINS (AD)", TCUI.Butter, () => TCServices.Rewarded(() =>
        {
            int before = gm.RunReward; TCSave.Coins += before; TCSave.LifetimeCoins += before; resTotal.text = (before * 2).ToString();
            doubleBtn.interactable = false; StartCoroutine(TCUI.Punch(resTotal.transform, 0.4f, 0.4f)); TCAudio.Play(TCAssets.I.purchase);
        }, "double_coins"), 54, TCUI.Icon("play"));
        TCUI.Place((RectTransform)doubleBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(660, 140));
        TCUI.Place((RectTransform)TCUI.Btn("Again", card, "PLAY AGAIN", TCUI.Mint, () => gm.Restart(), 58).transform, new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(660, 150));
        TCUI.Place((RectTransform)TCUI.Btn("Home", card, "HOME", TCUI.Sky, () => gm.Home(), 48).transform, new Vector2(0.5f, 0.5f), new Vector2(0, -500), new Vector2(420, 110));
    }
    TextMeshProUGUI Row(RectTransform card, string label, float y)
    {
        var row = TCUI.Img(label, card, new Color(1, 1, 1, 0.9f)); TCUI.Place(row.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(720, 76));
        var l = TCUI.Label("L", row.transform, label, 42, TCUI.Cocoa, TextAlignmentOptions.Left); TCUI.Stretch(l.rectTransform, 30, 200, 0, 0);
        var ic = TCUI.Img("I", row.transform, Color.white, TCUI.Icon("coin")); TCUI.Place(ic.rectTransform, new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(54, 54));
        var v = TCUI.Label("V", row.transform, "0", 48, TCUI.Cocoa, TextAlignmentOptions.Right); TCUI.Stretch(v.rectTransform, 300, 80, 0, 0);
        return v;
    }
    void ShowResults()
    {
        int prevBest = TCSave.Best;
        int total = gm.EndRun(false);
        resMetres.text = gm.Metres + "M";
        bool newBest = gm.Metres > prevBest;
        resBest.text = newBest ? "NEW BEST!" : "BEST " + TCSave.Best + "M";
        resCoins.text = gm.coinsCollected.ToString(); resBonus.text = gm.DistanceBonus.ToString(); resTotal.text = total.ToString();
        doubleBtn.interactable = total > 0;
        TCUI.Show(this, resultPanel);
        if (newBest) StartCoroutine(TCUI.Punch(resBest.transform, 0.35f, 0.5f));
    }
}
