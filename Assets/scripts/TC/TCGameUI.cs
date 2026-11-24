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
    RectTransform safe, heartsRow, coinIconTarget; TextMeshProUGUI scoreText, coinText, hintText, boostText, comboText;
    CanvasGroup comboCanvasGroup; Image comboBg;
    Coroutine comboPunchCo, comboPayoutCo; int lastChilliCombo = -1;
    RectTransform speedPanel; TextMeshProUGUI speedText, chilliTimerText, slowmoTimerText, magnetTimerText, slowmoStockText; GameObject chilliRow, slowmoRow, magnetRow, slowmoStockRow; RectTransform chilliRect, slowmoRect, magnetRect, slowmoStockRect;
    int lastCooldownCount = -1;
    Vector2 panelNormalSize;
    float currentPanelHeight;
    readonly List<Image> hearts = new List<Image>();
    CanvasGroup startOverlay, pausePanel, revivePanel, resultPanel, flash, frozenOverlay;
    Image frozenOverlayImg;
    RectTransform hand; Coroutine tutorialCo;
    int shownCoins = -1, shownMetres = -1, shownLife = -1;
    bool started, turnedOnce, jumpedOnce, rolledOnce;
    readonly Queue<TextMeshProUGUI> popPool = new Queue<TextMeshProUGUI>();
    readonly Queue<Image> flyingCoinPool = new Queue<Image>();
    readonly Queue<RectTransform> flyingLetterPool = new Queue<RectTransform>();
    TextMeshProUGUI resMetres, resBest, resCoins, resBonus, resTotal, reviveTimer, countDownText; Button doubleBtn, reviveGemBtn, reviveAdBtn;
    RectTransform resultHomeRt, resultAgainRt, countDownRoot;
    CanvasGroup countDownGroup;
    Vector2 resultHomeRest, resultAgainRest;
    bool resuming;
    TextMeshProUGUI[] webLetterText;
    Image[] webLetterBg;
    RectTransform webLettersRow;
    string lastWebHud = "";
    Camera cam;
    RectTransform fxRoot;
    Canvas fxCanvas;
    int lastSpeedTenths = -1;
    int lastChilliTenths = -1;
    int lastSlowmoTenths = -1;
    int lastMagnetTenths = -1;
    Coroutine coinPunchCo;
    enum HandAnimMode { Horizontal, SwipeUp, SwipeDown, PointCooldown, Hidden }
    HandAnimMode handMode = HandAnimMode.Horizontal;
    float currentTargetTimeScale = 1f;
    bool isTutorialRun;
    public bool IsTutorialRun => isTutorialRun;

    void Awake()
    {
        Instance = this;
        // If tutorial was explicitly requested (e.g. from How To Play), clear the one-time request flag
        // and ensure the run is flagged as completed for future runs so subsequent plays don't replay it.
        if (TCSave.TutorialRequested)
        {
            TCSave.TutorialRequested = false;
            TCSave.TutorialDone = true;
            isTutorialRun = true;
        }
        else
        {
            isTutorialRun = !TCSave.TutorialDone;
        }
    }

    void Start()
    {
        pl = FindObjectOfType<player>(); spider = FindObjectOfType<playerInteract>(); gm = gameManager.Instance; cam = Camera.main;
        if (!FindObjectOfType<TCTimeDilation>())
        {
            var tdObj = new GameObject("TCTimeDilation");
            tdObj.AddComponent<TCTimeDilation>();
        }
        if (!FindObjectOfType<TCCoinMagnet>())
        {
            var cmObj = new GameObject("TCCoinMagnet");
            cmObj.AddComponent<TCCoinMagnet>();
        }
        if (!FindObjectOfType<TCWebBonus>())
        {
            var wb = new GameObject("TCWebBonus");
            wb.AddComponent<TCWebBonus>();
        }
        BuildUI();
        if (isTutorialRun && gm != null)
        {
            gm.SetTutorialMidday();
        }
        hintText.text = "TOUCH TO CLIMB";
        playerInteract.OnEvent += OnGameEvent;
        player.OnTurned += () => turnedOnce = true;
        player.OnJumped += () => jumpedOnce = true;
        player.OnRolled += () => rolledOnce = true;
        TCUI.Show(this, startOverlay, false);
    }
    /// Builds (or, when baked into the scene, binds to) the HUD and every in-game screen. Also run in edit mode by TCUIBake.
    public void BuildUI()
    {
        var canvas = TCUI.MakeCanvas("TC_GameUI", 10);
        safe = TCUI.SafeRoot(canvas.transform);
        BuildHud(); BuildStart(); BuildPause(); BuildRevive(); BuildResults();
        var fr = TCUI.Rect("Flash", canvas.transform); bool fresh = TCUI.Fresh(fr);
        flash = TCUI.Add<CanvasGroup>(fr.gameObject); TCUI.Stretch(fr);
        if (fresh) { var fi = fr.gameObject.AddComponent<Image>(); fi.color = new Color(TCUI.Crimson.r, TCUI.Crimson.g, TCUI.Crimson.b, 0.35f); fi.raycastTarget = false; }
        flash.alpha = 0;

        // Frozen screen frame overlay for slow-motion cooldown power up
        var fz = TCUI.Rect("FrozenOverlay", canvas.transform); bool freshFz = TCUI.Fresh(fz);
        frozenOverlay = TCUI.Add<CanvasGroup>(fz.gameObject); TCUI.Stretch(fz);
        if (freshFz)
        {
            frozenOverlayImg = fz.gameObject.AddComponent<Image>();
            frozenOverlayImg.sprite = Resources.Load<Sprite>("TCUI/overlay_frozen");
            frozenOverlayImg.color = Color.white;
            frozenOverlayImg.raycastTarget = false;
        }
        else
        {
            frozenOverlayImg = fz.GetComponent<Image>();
            if (frozenOverlayImg && !frozenOverlayImg.sprite)
                frozenOverlayImg.sprite = Resources.Load<Sprite>("TCUI/overlay_frozen");
        }
        if (frozenOverlayImg) frozenOverlayImg.raycastTarget = false;
        frozenOverlay.alpha = 0f;
        frozenOverlay.interactable = false;
        frozenOverlay.blocksRaycasts = false;
        // polish3 palette theme: styles anything not already themed in the baked scene (never overrides editor edits)
        TCTheme.ThemeTree(canvas.transform);
    }
    void OnDestroy()
    {
        IsTutorialFrozen = false;
        Time.timeScale = 1f;
        playerInteract.OnEvent -= OnGameEvent;
        player.OnTurned = null;
        player.OnJumped = null;
        player.OnRolled = null;
    }

    // ---------------- HUD ----------------
    void BuildHud()
    {
        var top = TCUI.Rect("Top", safe); if (TCUI.Fresh(top)) { top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1); top.sizeDelta = new Vector2(0, 200); top.anchoredPosition = Vector2.zero; }
        var pill = TCUI.Img("ScorePill", top, new Color(1, 1, 1, 0.85f)); TCUI.Place(pill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(360, 120));
        scoreText = TCUI.Label("Score", pill.transform, "0M", 72, TCUI.Cocoa); TCUI.Stretch(scoreText.rectTransform);
        var cp = TCUI.Img("Coins", top, new Color(1, 1, 1, 0.85f)); TCUI.Place(cp.rectTransform, new Vector2(1, 0.5f), new Vector2(-188, 10), new Vector2(240, 96));
        var ci = TCUI.Img("Icon", cp.transform, Color.white, TCUI.Icon("coin")); TCUI.Place(ci.rectTransform, new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(68, 68));
        coinIconTarget = ci.rectTransform;
        coinText = TCUI.Label("Value", cp.transform, "0", 50, TCUI.Cocoa, TextAlignmentOptions.Right); TCUI.Stretch(coinText.rectTransform, 82, 22, 0, 0);

        // Combo & bonus calculation pill panel directly centered below Coins panel with breathing room
        var comboPill = top.Find("CoinComboPill");
        if (comboPill != null) Destroy(comboPill.gameObject);

        comboBg = TCUI.Img("CoinComboPill", top, new Color(0.12f, 0.08f, 0.05f, 0.90f));
        var pillRt = comboBg.rectTransform;
        // Coins panel is baked in scene at anchoredPosition (-144.2f, 0), size (240, 96), pivot (0.5, 0.5)
        // Center bonus panel to Coins panel x position:
        float coinCenterX = cp ? cp.rectTransform.anchoredPosition.x : -144.2f;
        float coinBottomY = cp ? (cp.rectTransform.anchoredPosition.y - cp.rectTransform.rect.height * cp.rectTransform.pivot.y) : -48f;

        pillRt.anchorMin = new Vector2(1f, 0.5f);
        pillRt.anchorMax = new Vector2(1f, 0.5f);
        pillRt.pivot = new Vector2(0.5f, 1f);
        // Clean 12px space under the Coins panel bottom, perfectly centered with Coins panel horizontally
        pillRt.anchoredPosition = new Vector2(coinCenterX, coinBottomY - 12f);
        pillRt.sizeDelta = new Vector2(240f, 62f);

        comboCanvasGroup = comboBg.gameObject.GetComponent<CanvasGroup>();
        if (!comboCanvasGroup) comboCanvasGroup = comboBg.gameObject.AddComponent<CanvasGroup>();

        comboText = TCUI.Label("Text", comboBg.transform, "", 22, Color.white, TextAlignmentOptions.Center);
        var comboRt = comboText.rectTransform;
        TCUI.Stretch(comboRt, 10, 10, 4, 4);

        comboText.alignment = TextAlignmentOptions.Center;
        comboText.enableWordWrapping = true;
        comboText.enableAutoSizing = true;
        comboText.fontSizeMin = 14f;
        comboText.fontSizeMax = 22f;

        var outline = comboText.GetComponent<Outline>();
        if (!outline) outline = comboText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        comboBg.gameObject.SetActive(false);
        heartsRow = TCUI.Rect("Hearts", top); TCUI.Place(heartsRow, new Vector2(0, 0.5f), new Vector2(170, 10), new Vector2(300, 80));
        var pause = TCUI.Btn("Pause", top, "", TCUI.Butter, ShowPause, 40, TCUI.Icon("pause"));
        // Perfectly balanced on the top-right header row alongside Coins, ScorePill, and Hearts
        TCUI.Place((RectTransform)pause.transform, new Vector2(1, 0.5f), new Vector2(-54, 10), new Vector2(96, 96));
        BuildWebHud(top);
        BuildCountdown();

        // Speed Multiplier & Chilli Mode Panel (Left Side of HUD - baked in scene so user can edit transform)
        speedPanel = TCUI.Rect("SpeedChilliPanel", safe);
        if (speedPanel != null)
        {
            // Fix pivot to top-center so expanding/collapsing grows downwards smoothly
            float topY = speedPanel.anchoredPosition.y + (1f - speedPanel.pivot.y) * speedPanel.sizeDelta.y;
            speedPanel.pivot = new Vector2(0.5f, 1f);
            speedPanel.anchoredPosition = new Vector2(speedPanel.anchoredPosition.x, topY);

            panelNormalSize = new Vector2(speedPanel.sizeDelta.x, 88f);
            currentPanelHeight = 88f;
            speedPanel.sizeDelta = new Vector2(panelNormalSize.x, currentPanelHeight);

            var sRow = speedPanel.Find("SpeedRow");
            if (sRow != null)
            {
                speedText = sRow.Find("SpeedVal")?.GetComponent<TextMeshProUGUI>();
                var boltImg = sRow.Find("BoltIcon")?.GetComponent<Image>();
                if (boltImg != null && boltImg.sprite == null)
                    boltImg.sprite = TCUI.Icon("bolt");
            }
            var cRow = speedPanel.Find("ChilliRow");
            if (cRow != null)
            {
                chilliRow = cRow.gameObject;
                chilliRect = cRow as RectTransform;
                if (chilliRect != null)
                {
                    chilliRect.anchorMin = new Vector2(0, 1);
                    chilliRect.anchorMax = new Vector2(1, 1);
                    chilliRect.pivot = new Vector2(0.5f, 1);
                    chilliRect.sizeDelta = new Vector2(0, 78);
                }
                chilliTimerText = cRow.Find("ChilliTime")?.GetComponent<TextMeshProUGUI>();
                var chilliImg = cRow.Find("ChilliIcon")?.GetComponent<Image>();
                if (chilliImg != null && chilliImg.sprite == null)
                    chilliImg.sprite = TCUI.Icon("chilli");
                chilliRow.SetActive(false);
            }

            // Create or bind SlowmoRow segment
            var smRow = speedPanel.Find("SlowmoRow");
            if (smRow == null && cRow != null)
            {
                var clone = Instantiate(cRow.gameObject, speedPanel);
                clone.name = "SlowmoRow";
                smRow = clone.transform;
            }
            if (smRow != null)
            {
                slowmoRow = smRow.gameObject;
                slowmoRect = smRow as RectTransform;
                if (slowmoRect != null)
                {
                    slowmoRect.anchorMin = new Vector2(0, 1);
                    slowmoRect.anchorMax = new Vector2(1, 1);
                    slowmoRect.pivot = new Vector2(0.5f, 1);
                    slowmoRect.sizeDelta = new Vector2(0, 78);
                }
                slowmoTimerText = smRow.Find("ChilliTime")?.GetComponent<TextMeshProUGUI>() ?? smRow.Find("SlowmoTime")?.GetComponent<TextMeshProUGUI>();
                if (slowmoTimerText != null)
                {
                    slowmoTimerText.name = "SlowmoTime";
                    slowmoTimerText.color = TCUI.Brown;
                }
                var smIcon = smRow.Find("ChilliIcon")?.GetComponent<Image>() ?? smRow.Find("SlowmoIcon")?.GetComponent<Image>();
                if (smIcon != null)
                {
                    smIcon.name = "SlowmoIcon";
                    smIcon.sprite = TCUI.Icon("cooldown");
                }
                slowmoRow.SetActive(false);
            }

            // Create or bind MagnetRow segment
            var magRow = speedPanel.Find("MagnetRow");
            if (magRow == null && cRow != null)
            {
                var clone = Instantiate(cRow.gameObject, speedPanel);
                clone.name = "MagnetRow";
                magRow = clone.transform;
            }
            if (magRow != null)
            {
                magnetRow = magRow.gameObject;
                magnetRect = magRow as RectTransform;
                if (magnetRect != null)
                {
                    magnetRect.anchorMin = new Vector2(0, 1);
                    magnetRect.anchorMax = new Vector2(1, 1);
                    magnetRect.pivot = new Vector2(0.5f, 1);
                    magnetRect.sizeDelta = new Vector2(0, 78);
                }
                magnetTimerText = magRow.Find("ChilliTime")?.GetComponent<TextMeshProUGUI>() ?? magRow.Find("MagnetTime")?.GetComponent<TextMeshProUGUI>();
                if (magnetTimerText != null)
                {
                    magnetTimerText.name = "MagnetTime";
                    magnetTimerText.color = TCUI.Crimson;
                }
                var magIcon = magRow.Find("ChilliIcon")?.GetComponent<Image>() ?? magRow.Find("MagnetIcon")?.GetComponent<Image>();
                if (magIcon != null)
                {
                    magIcon.name = "MagnetIcon";
                    magIcon.sprite = TCUI.Icon("magnet");
                }
                magnetRow.SetActive(false);
            }

            // Create or bind SlowmoStockRow segment (shows available slow-motion charges on left HUD)
            var stRow = speedPanel.Find("SlowmoStockRow");
            if (stRow == null && cRow != null)
            {
                var clone = Instantiate(cRow.gameObject, speedPanel);
                clone.name = "SlowmoStockRow";
                stRow = clone.transform;
            }
            if (stRow != null)
            {
                slowmoStockRow = stRow.gameObject;
                slowmoStockRect = stRow as RectTransform;
                if (slowmoStockRect != null)
                {
                    slowmoStockRect.anchorMin = new Vector2(0, 1);
                    slowmoStockRect.anchorMax = new Vector2(1, 1);
                    slowmoStockRect.pivot = new Vector2(0.5f, 1);
                    slowmoStockRect.sizeDelta = new Vector2(0, 78);
                }
                slowmoStockText = stRow.Find("ChilliTime")?.GetComponent<TextMeshProUGUI>() ?? stRow.Find("SlowmoTime")?.GetComponent<TextMeshProUGUI>() ?? stRow.Find("StockText")?.GetComponent<TextMeshProUGUI>();
                if (slowmoStockText != null)
                {
                    slowmoStockText.name = "StockText";
                    slowmoStockText.color = TCUI.Brown;
                }
                var stIcon = stRow.Find("ChilliIcon")?.GetComponent<Image>() ?? stRow.Find("SlowmoIcon")?.GetComponent<Image>() ?? stRow.Find("StockIcon")?.GetComponent<Image>();
                if (stIcon != null)
                {
                    stIcon.name = "StockIcon";
                    stIcon.sprite = TCUI.Icon("cooldown");
                }
                slowmoStockRow.SetActive(false);
            }
        }

        boostText = TCUI.Label("Boost", safe, "BOOST!", 110, TCUI.Cream); TCUI.Place(boostText.rectTransform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(900, 160));
        TCUI.Outline(boostText, 0.25f); boostText.gameObject.SetActive(false);
    }

    TCLivesPanel livesPanel; bool livesChecked;
    void RefreshHearts()
    {
        if (!livesChecked) { livesChecked = true; livesPanel = heartsRow ? heartsRow.GetComponent<TCLivesPanel>() : null; }
        if (livesPanel) { livesPanel.SetLives(spider.life, spider.MaxLife); return; }
        int max = spider.MaxLife;
        float spacing = max >= 5 ? 50f : 66f;
        float startX = max >= 5 ? 32f : 40f;
        int heartSafety = 0;
        while (hearts.Count < max && ++heartSafety < 30)
        {
            var h = TCUI.Img("Heart", heartsRow, Color.white, TCUI.Icon("heart"));
            TCUI.Place(h.rectTransform, new Vector2(0, 0.5f), new Vector2(startX + hearts.Count * spacing, 0), new Vector2(56, 56));
            hearts.Add(h);
        }
        for (int i = 0; i < hearts.Count; i++)
        {
            bool full = i < spider.life;
            hearts[i].sprite = TCUI.Icon(full ? "heart" : "heartEmpty");
            hearts[i].gameObject.SetActive(i < max);
        }
    }

    public bool Started => started;
    public bool IsPauseOpen => pausePanel != null && pausePanel.gameObject.activeSelf;
    public bool IsTutorialFrozen { get; private set; }
    bool IsPausedOrDead => IsPauseOpen || (gm != null && (gm.IsPaused || gm.runEnded)) || (pl != null && pl.GameOver);
    float SpiderY => (spider != null) ? spider.transform.position.y : (pl != null ? pl.p1.position.y : 0f);

    void Update()
    {
        if (!pl) return;
        if (!started)
        {
            if (!IsPausedOrDead)
            {
                bool fingerDown = (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) || Input.GetMouseButtonDown(0);
                if (fingerDown) StartRun();
            }
        }
        else if (startOverlay != null && (startOverlay.blocksRaycasts || startOverlay.interactable))
        {
            startOverlay.blocksRaycasts = false;
            startOverlay.interactable = false;
        }
        if (Input.GetKeyDown(KeyCode.Escape) && !resuming) // Android back: pause / resume
        {
            if (pausePanel.gameObject.activeSelf) HidePause(); else ShowPause();
        }
        RefreshWebHud();
        int m = (int)pl.currentScore;
        if (m != shownMetres) { if (shownMetres > 0 && m / 100 > shownMetres / 100 && scoreText) StartCoroutine(TCUI.Punch(scoreText.transform.parent, 0.18f, 0.3f)); shownMetres = m; scoreText.text = m + "M"; } // polish2: milestone pop every 100 m
        if (gm.coinsCollected != shownCoins)
        {
            if (shownCoins >= 0 && coinPunchCo == null && coinText != null)
                coinPunchCo = StartCoroutine(CoinPunch(coinText.transform.parent));
            shownCoins = gm.coinsCollected;
            coinText.text = shownCoins.ToString();
        }
        if (spider.life != shownLife) { shownLife = spider.life; RefreshHearts(); }
        if (boostText.gameObject.activeSelf != pl.boost)
        {
            boostText.gameObject.SetActive(pl.boost);
            if (!pl.boost) boostText.transform.localScale = Vector3.one;
        }
        if (pl.boost)
        {
            boostText.transform.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(Time.time * 14));
            if (spider != null && comboBg != null && comboPayoutCo == null)
            {
                if (!comboBg.gameObject.activeSelf)
                {
                    comboBg.gameObject.SetActive(true);
                    if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f;
                }
                int combo = spider.ChilliComboCount;
                int coins = spider.ChilliCoinsEarned;
                float mult = spider.ChilliComboMultiplier;
                if (combo > 0)
                {
                    comboText.text = $"{coins} x {mult:0.0}x <color=#FFE082><size=80%>(x{combo})</size></color>";
                }
                else
                {
                    comboText.text = $"{coins} x 1.0x";
                }
            }
        }
        else
        {
            if (comboPayoutCo == null && comboBg != null && comboBg.gameObject.activeSelf)
                comboBg.gameObject.SetActive(false);
        }

        // Speed Multiplier, Chilli Mode & Slow-Mo Panel (dirtied only when values change)
        if (speedPanel != null)
        {
            bool slowActive = TCTimeDilation.Instance != null && TCTimeDilation.Instance.IsActive;
            bool chilliActive = spider != null && spider.boostDuration > 0;

            if (speedText != null)
            {
                float baseMult = TCConfig.I.SpeedMultiplierAtDistance(pl.currentScore);
                float speedMult = slowActive ? (baseMult * Time.timeScale) : (pl.boost ? baseMult * TCConfig.I.boostMultiplier : baseMult);
                int speedTenths = Mathf.RoundToInt(speedMult * 10f);
                if (speedTenths != lastSpeedTenths)
                {
                    lastSpeedTenths = speedTenths;
                    speedText.text = (speedTenths / 10f).ToString("0.0") + "x";
                }
                Color targetCol = slowActive ? TCUI.Brown : (pl.boost ? TCUI.Crimson : TCUI.Ink);
                if (speedText.color != targetCol) speedText.color = targetCol;
            }

            if (chilliRow != null && chilliRow.activeSelf != chilliActive)
                chilliRow.SetActive(chilliActive);

            if (chilliActive && chilliTimerText != null)
            {
                int chilliTenths = Mathf.CeilToInt(spider.boostDuration * 10f);
                int curCombo = spider.ChilliComboCount;
                if (chilliTenths != lastChilliTenths || curCombo != lastChilliCombo)
                {
                    lastChilliTenths = chilliTenths;
                    lastChilliCombo = curCombo;
                    string comboSuffix = curCombo > 0 ? $"  <color=#FFD200>x{curCombo}</color>" : "";
                    chilliTimerText.text = (chilliTenths / 10f).ToString("0.0") + "s" + comboSuffix;
                }
            }

            if (slowmoRow != null && slowmoRow.activeSelf != slowActive)
                slowmoRow.SetActive(slowActive);

            if (slowActive && slowmoTimerText != null && TCTimeDilation.Instance != null)
            {
                int slowmoTenths = Mathf.CeilToInt(TCTimeDilation.Instance.ActiveTimeRemaining * 10f);
                if (slowmoTenths != lastSlowmoTenths)
                {
                    lastSlowmoTenths = slowmoTenths;
                    slowmoTimerText.text = (slowmoTenths / 10f).ToString("0.0") + "s";
                }
            }

            bool magnetActive = TCCoinMagnet.Instance != null && TCCoinMagnet.Instance.IsActive;
            if (magnetRow != null && magnetRow.activeSelf != magnetActive)
                magnetRow.SetActive(magnetActive);

            if (magnetActive && magnetTimerText != null && TCCoinMagnet.Instance != null)
            {
                int magTenths = Mathf.CeilToInt(TCCoinMagnet.Instance.ActiveTimeRemaining * 10f);
                if (magTenths != lastMagnetTenths)
                {
                    lastMagnetTenths = magTenths;
                    magnetTimerText.text = (magTenths / 10f).ToString("0.0") + "s";
                }
            }

            int cdCount = playerInteract.CooldownInventory;
            bool slowStockActive = cdCount > 0;
            if (slowmoStockRow != null && slowmoStockRow.activeSelf != slowStockActive)
                slowmoStockRow.SetActive(slowStockActive);

            if (slowStockActive && slowmoStockText != null)
            {
                if (cdCount != lastCooldownCount)
                {
                    lastCooldownCount = cdCount;
                    slowmoStockText.text = "x" + cdCount;
                }
            }

            // Sequential layout of active segments below SpeedRow
            float nextY = -88f;
            if (chilliActive && chilliRect != null)
            {
                chilliRect.anchoredPosition = new Vector2(0, nextY);
                nextY -= 78f;
            }
            if (slowActive && slowmoRect != null)
            {
                slowmoRect.anchoredPosition = new Vector2(0, nextY);
                nextY -= 78f;
            }
            if (slowStockActive && slowmoStockRect != null)
            {
                slowmoStockRect.anchoredPosition = new Vector2(0, nextY);
                nextY -= 78f;
            }
            if (magnetActive && magnetRect != null)
            {
                magnetRect.anchoredPosition = new Vector2(0, nextY);
                nextY -= 78f;
            }

            // Dynamic panel expansion: 88f base + 78f for each active segment
            float targetHeight = 88f + (chilliActive ? 78f : 0f) + (slowActive ? 78f : 0f) + (slowStockActive ? 78f : 0f) + (magnetActive ? 78f : 0f);
            if (Mathf.Abs(currentPanelHeight - targetHeight) > 0.05f)
            {
                currentPanelHeight = Mathf.MoveTowards(currentPanelHeight, targetHeight, Time.unscaledDeltaTime * 420f);
                speedPanel.sizeDelta = new Vector2(panelNormalSize.x, currentPanelHeight);
            }
            else if (speedPanel.sizeDelta.y != targetHeight)
            {
                currentPanelHeight = targetHeight;
                speedPanel.sizeDelta = new Vector2(panelNormalSize.x, targetHeight);
            }
        }

        if (flash.alpha > 0) flash.alpha = Mathf.MoveTowards(flash.alpha, 0, Time.unscaledDeltaTime * 2.5f);

        // Smooth fade-in and fade-out of the frozen screen frame overlay during cooldown slow-mo
        if (frozenOverlay != null)
        {
            bool slowActive = TCTimeDilation.Instance != null && TCTimeDilation.Instance.IsActive;
            float targetAlpha = slowActive ? (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f)) : 0f;
            float fadeSpeed = slowActive ? 3.5f : 2.2f;
            frozenOverlay.alpha = Mathf.MoveTowards(frozenOverlay.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
            if (frozenOverlay.gameObject.activeSelf != (frozenOverlay.alpha > 0.001f || slowActive))
                frozenOverlay.gameObject.SetActive(frozenOverlay.alpha > 0.001f || slowActive);
        }
    }

    void OnGameEvent(string e, Vector3 pos)
    {
        switch (e)
        {
            case "coin":
                Pop("+" + gameManager.CoinValue, pos, TCUI.Butter);
                FlyCoin(pos);
                break;
            case "chilli_kill":
                // 5 coins spawn and fly to coin counter with bonus coin sound
                int curCombo = spider != null ? spider.ChilliComboCount : 1;
                Pop($"x{curCombo} COMBO!\n+5 COINS", pos, TCUI.Hex("#FFD200"));
                if (comboPunchCo != null) StopCoroutine(comboPunchCo);
                if (comboBg != null)
                    comboPunchCo = StartCoroutine(ComboPunch(comboBg.transform));
                StartCoroutine(SpawnChilliBonusCoins(pos));
                break;
            case "heart": Pop("+1 HEART", pos, TCUI.Blush); StartCoroutine(TCUI.Punch(heartsRow, 0.25f)); break;
            case "boost": Pop("BOOST", pos, TCUI.Peach); break;
            case "cooldown": Pop("FREEZE +1", pos, TCUI.Tan); if (slowmoStockRect) StartCoroutine(TCUI.Punch(slowmoStockRect, 0.25f)); break;
            case "smash": Pop("SMASH!", pos, TCUI.Cream); break;
            case "hit": flash.alpha = 1; StartCoroutine(TCUI.Punch(heartsRow, 0.3f)); break;
        }
    }

    IEnumerator SpawnChilliBonusCoins(Vector3 centerWorldPos)
    {
        for (int i = 0; i < 5; i++)
        {
            // Spawn consecutive coins in an upward trail/string rather than a closed radial clump
            Vector3 coinWorld = centerWorldPos + new Vector3(0f, 0.25f + i * 0.28f, 0f);

            FlyCoin(coinWorld);
            if (gm) gm.addCoin();
            TCSfx.Play("coin", 0.38f, 1.0f + i * 0.05f);
            yield return new WaitForSeconds(0.06f);
        }
    }

    public void OnChilliBoostStarted()
    {
        if (comboPayoutCo != null)
        {
            StopCoroutine(comboPayoutCo);
            comboPayoutCo = null;
        }
        if (comboBg != null && comboText != null)
        {
            if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f;
            comboText.alpha = 1f;
            comboText.text = "0 x 1.0x";
            comboBg.gameObject.SetActive(true);
        }
    }

    public void ProcessChilliComboEnd(int combo, int coinsEarned)
    {
        if (combo > 0 && coinsEarned > 0)
        {
            float multiplier = 1.0f + combo * 0.1f;
            int totalCoins = Mathf.RoundToInt(coinsEarned * multiplier);
            int bonusCoins = totalCoins - coinsEarned;
            if (bonusCoins > 0 && gm != null)
            {
                gm.addCoin(bonusCoins);
            }
            if (comboPayoutCo != null) StopCoroutine(comboPayoutCo);
            comboPayoutCo = StartCoroutine(ShowChilliComboPayoutRoutine(combo, coinsEarned, bonusCoins, multiplier));
        }
        else
        {
            if (comboBg != null) comboBg.gameObject.SetActive(false);
        }
    }

    public void ShowStringBonusCalculation(int totalStringCoins, int bonusCoins, Vector3 worldPos)
    {
        if (comboPayoutCo != null) StopCoroutine(comboPayoutCo);
        comboPayoutCo = StartCoroutine(ShowStringBonusPayoutRoutine(totalStringCoins, bonusCoins, worldPos));
    }

    IEnumerator ShowStringBonusPayoutRoutine(int totalStringCoins, int bonusCoins, Vector3 worldPos)
    {
        if (comboBg == null || comboText == null) yield break;
        comboBg.gameObject.SetActive(true);
        if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f;
        comboText.alpha = 1f;
        // Calculation & bonus text placed cleanly below the coins panel
        comboText.text = $"{totalStringCoins} COINS x 1.5\n<color=#FFB300>+{bonusCoins} BONUS!</color>";

        if (comboPunchCo != null) StopCoroutine(comboPunchCo);
        comboPunchCo = StartCoroutine(ComboPunch(comboBg.transform));

        // Hold calculation below coins panel for 2.2 seconds
        yield return new WaitForSecondsRealtime(2.2f);

        // Smooth fade out
        float fadeDuration = 0.35f;
        for (float elapsed = 0; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / fadeDuration;
            if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f - t;
            else comboText.alpha = 1f - t;
            yield return null;
        }
        comboBg.gameObject.SetActive(false);
        if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f;
        comboText.alpha = 1f;
        comboPayoutCo = null;
    }

    IEnumerator ShowChilliComboPayoutRoutine(int combo, int coinsEarned, int bonusCoins, float multiplier)
    {
        if (comboBg == null || comboText == null) yield break;
        comboBg.gameObject.SetActive(true);
        if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f;
        comboText.alpha = 1f;
        comboText.text = $"{coinsEarned} x {multiplier:0.0} = <color=#FFF8D0>+{bonusCoins} BONUS</color>";

        TCSfx.Play("menu_flip", 0.85f);
        TCSfx.Play("coin", 0.9f, 1.25f);

        // Punch little calculation pill panel under coin counter
        if (comboPunchCo != null) StopCoroutine(comboPunchCo);
        comboPunchCo = StartCoroutine(ComboPunch(comboBg.transform));

        // Burst flying coins from player directly to wallet coin icon
        int flyCount = Mathf.Clamp(bonusCoins, 4, 10);
        for (int i = 0; i < flyCount; i++)
        {
            Vector3 startPos = spider != null ? spider.transform.position + Vector3.up * 0.8f : (cam != null ? cam.transform.position + cam.transform.forward * 4f : Vector3.zero);
            FlyCoin(startPos);
            TCSfx.Play("coin", 0.35f, 1.0f + i * 0.05f);
            yield return new WaitForSecondsRealtime(0.045f);
        }

        // Hold calculation under coin counter for 2.2 seconds
        yield return new WaitForSecondsRealtime(2.2f);

        // Smooth fade out
        float fadeDuration = 0.35f;
        for (float elapsed = 0; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / fadeDuration;
            if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f - t;
            else comboText.alpha = 1f - t;
            yield return null;
        }
        comboBg.gameObject.SetActive(false);
        if (comboCanvasGroup != null) comboCanvasGroup.alpha = 1f;
        comboText.alpha = 1f;
        comboPayoutCo = null;
    }

    IEnumerator ComboPunch(Transform target)
    {
        if (!target) yield break;
        Vector3 orig = Vector3.one;
        float duration = 0.22f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / duration;
            float scale = 1.0f + 0.2f * Mathf.Sin(t * Mathf.PI);
            target.localScale = orig * scale;
            yield return null;
        }
        target.localScale = orig;
    }

    void EnsureFxCanvas()
    {
        if (fxRoot) return;
        var fxObj = new GameObject("TC_GameUI_FX");
        fxObj.transform.SetParent(safe ? safe.parent : transform, false);
        var c = fxObj.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 15; // Above normal HUD, isolated from HUD redraws
        fxCanvas = c;
        fxRoot = fxObj.GetComponent<RectTransform>();
        TCUI.Stretch(fxRoot);
    }

    IEnumerator CoinPunch(Transform target)
    {
        if (!target) yield break;
        Vector3 orig = Vector3.one;
        float duration = 0.14f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = elapsed / duration;
            float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.16f;
            target.localScale = orig * s;
            yield return null;
        }
        target.localScale = orig;
        coinPunchCo = null;
    }

    void FlyCoin(Vector3 worldPos)
    {
        if (!cam) cam = Camera.main;
        if (!cam || !safe || !coinIconTarget) return;
        EnsureFxCanvas();

        var img = flyingCoinPool.Count > 0 ? flyingCoinPool.Dequeue() : null;
        if (!img)
        {
            img = TCUI.Img("FlyCoin", fxRoot, Color.white, TCUI.Icon("coin"));
            img.rectTransform.sizeDelta = new Vector2(56, 56);
            img.raycastTarget = false;
        }

        img.gameObject.SetActive(true);
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        StartCoroutine(FlyCoinRoutine(img, screenPos));
    }

    IEnumerator FlyCoinRoutine(Image coinImg, Vector3 screenStart)
    {
        var canvas = coinImg.canvas;
        float s = canvas ? canvas.scaleFactor : 1f;
        var rt = coinImg.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;

        Vector2 start = new Vector2(screenStart.x, screenStart.y) / s;
        // Subtle random arc offset so multiple coins in a string fan out beautifully
        Vector2 midOffset = new Vector2(Random.Range(-90f, 90f), Random.Range(60f, 150f));

        float duration = 0.52f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = elapsed / duration;
            // Target UI position
            Vector2 target = coinIconTarget ? (Vector2)coinIconTarget.position / s : start + new Vector2(0, 300);

            // Quadratic bezier curve from spider contact to Coins UI pill
            Vector2 control = (start + target) * 0.5f + midOffset;
            float u = 1f - t;
            Vector2 current = u * u * start + 2f * u * t * control + t * t * target;

            rt.anchoredPosition = current;

            // Scale up slightly at launch, then shrink as it arrives into the UI icon
            float scale = t < 0.25f ? Mathf.Lerp(0.5f, 1.25f, t / 0.25f) : Mathf.Lerp(1.25f, 0.6f, (t - 0.25f) / 0.75f);
            rt.localScale = Vector3.one * scale;

            // Spin gently as it flies
            rt.localEulerAngles = new Vector3(0, 0, t * 540f);

            yield return null;
        }

        // Punch the Coins UI pill on arrival (coalesced so rapid streaks don't fight)
        if (coinIconTarget && coinIconTarget.parent && coinPunchCo == null)
        {
            coinPunchCo = StartCoroutine(CoinPunch(coinIconTarget.parent));
        }

        coinImg.gameObject.SetActive(false);
        flyingCoinPool.Enqueue(coinImg);
    }

    public void FlyWebLetter(char c, Vector3 worldPos)
    {
        c = char.ToUpperInvariant(c);
        int idx = (c == 'W') ? 0 : (c == 'E') ? 1 : (c == 'B') ? 2 : -1;
        if (idx < 0) return;
        if (!cam) cam = Camera.main;
        if (!cam || !safe) return;
        EnsureFxCanvas();

        RectTransform root = flyingLetterPool.Count > 0 ? flyingLetterPool.Dequeue() : null;
        Image bg = null;
        TextMeshProUGUI label = null;
        if (!root)
        {
            var go = new GameObject("FlyLetterRoot");
            root = go.AddComponent<RectTransform>();
            root.SetParent(fxRoot, false);
            bg = TCUI.Img("Bg", root, TCUI.Crimson);
            TCUI.Stretch(bg.rectTransform);
            bg.raycastTarget = false;
            label = TCUI.Label("T", root, "", 36, TCUI.Cream);
            TCUI.Stretch(label.rectTransform);
            label.raycastTarget = false;
            TCTheme.StyleText(label, TCUI.Cream, 0.26f, true);
        }
        else
        {
            bg = root.GetComponentInChildren<Image>(true);
            label = root.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        root.sizeDelta = new Vector2(66, 56);
        if (label) label.text = c.ToString();
        if (bg) bg.color = TCUI.Mint;
        root.gameObject.SetActive(true);

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        StartCoroutine(FlyWebLetterRoutine(root, idx, screenPos));
    }

    IEnumerator FlyWebLetterRoutine(RectTransform letterRt, int slotIdx, Vector3 screenStart)
    {
        var canvas = letterRt.GetComponentInParent<Canvas>();
        float s = canvas ? canvas.scaleFactor : 1f;
        letterRt.anchorMin = letterRt.anchorMax = Vector2.zero;

        Vector2 start = new Vector2(screenStart.x, screenStart.y) / s;
        Vector2 midOffset = new Vector2(Random.Range(-50f, 50f), Random.Range(90f, 180f));

        float duration = 0.55f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = elapsed / duration;
            Vector2 target = (webLetterBg != null && slotIdx < webLetterBg.Length && webLetterBg[slotIdx])
                ? (Vector2)webLetterBg[slotIdx].rectTransform.position / s
                : start + new Vector2(0, 400);

            // Quadratic bezier curve from pickup contact directly to WEB HUD slot
            Vector2 control = (start + target) * 0.5f + midOffset;
            float u = 1f - t;
            Vector2 current = u * u * start + 2f * u * t * control + t * t * target;
            letterRt.anchoredPosition = current;

            // Start medium, punch up slightly mid-flight, settle cleanly to slot size
            float scale = t < 0.25f ? Mathf.Lerp(0.6f, 1.35f, t / 0.25f) : Mathf.Lerp(1.35f, 1.0f, (t - 0.25f) / 0.75f);
            letterRt.localScale = Vector3.one * scale;

            // Slight playful wobble rotation
            float rot = Mathf.Sin(t * Mathf.PI * 3f) * 14f * (1f - t);
            letterRt.localEulerAngles = new Vector3(0, 0, rot);

            yield return null;
        }

        // Arrival punch on the target slot
        if (webLetterBg != null && slotIdx < webLetterBg.Length && webLetterBg[slotIdx])
        {
            StartCoroutine(TCUI.Punch(webLetterBg[slotIdx].transform, 0.32f, 0.28f));
        }

        letterRt.gameObject.SetActive(false);
        flyingLetterPool.Enqueue(letterRt);
    }

    public void TriggerHighScoreCelebration()
    {
        if (safe != null) StartCoroutine(ConfettiCelebration(safe));
        if (scoreText != null) StartCoroutine(TCUI.Punch(scoreText.transform, 0.45f, 0.5f));
    }

    public void Pop(string text, Vector3 world, Color c)
    {
        if (!cam) cam = Camera.main; if (!cam) return;
        EnsureFxCanvas();
        var t = popPool.Count > 0 ? popPool.Dequeue() : null;
        if (!t) { t = TCUI.Label("Pop", fxRoot, "", 64, c); t.rectTransform.sizeDelta = new Vector2(400, 100); TCUI.Outline(t, 0.22f); t.raycastTarget = false; }
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
        var root = TCUI.Rect("Start", safe); bool fresh = TCUI.Fresh(root); TCUI.Stretch(root);
        startOverlay = TCUI.Add<CanvasGroup>(root.gameObject);
        startOverlay.blocksRaycasts = false;
        var hit = root.GetComponent<Image>(); if (hit) hit.raycastTarget = false;
        var b = root.GetComponent<Button>(); if (b) b.enabled = false;
        hintText = TCUI.Label("Hint", root, "TOUCH TO CLIMB", 72, TCUI.Cream);
        TCUI.Outline(hintText, 0.25f);
        hintText.raycastTarget = false;
        TCUI.Place(hintText.rectTransform, new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(1000, 260));
        hand = TCUI.Img("Hand", root, Color.white, TCUI.Icon("hand")).rectTransform;
        hand.GetComponent<Image>().raycastTarget = false;
        TCUI.Place(hand, new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(150, 150));
        if (Application.isPlaying) StartCoroutine(HandWiggle());
    }
    bool IsTutorialAborted => pl == null || pl.GameOver || (spider != null && spider.IsDead) || (gm != null && gm.runEnded);
    bool IsPausedOverlay => IsPauseOpen || (gm != null && gm.IsPaused);
    IEnumerator WaitWhilePausedOverlay() { while (IsPausedOverlay && !IsTutorialAborted) yield return null; }

    IEnumerator SmoothTimeScale(float targetScale, float duration)
    {
        currentTargetTimeScale = targetScale;
        yield return WaitWhilePausedOverlay();
        if (IsTutorialAborted) yield break;

        float start = Time.timeScale;
        if (duration <= 0.001f)
        {
            Time.timeScale = targetScale;
            yield break;
        }
        for (float elapsed = 0; elapsed < duration; )
        {
            if (IsTutorialAborted) yield break;
            if (IsPausedOverlay) { yield return null; continue; }
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(start, targetScale, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        yield return WaitWhilePausedOverlay();
        if (!IsTutorialAborted) Time.timeScale = targetScale;
    }

    IEnumerator HandWiggle()
    {
        while (true)
        {
            if (hand)
            {
                if (handMode == HandAnimMode.Horizontal)
                {
                    if (!hand.gameObject.activeSelf) hand.gameObject.SetActive(true);
                    hand.localScale = Vector3.one;
                    hand.localEulerAngles = Vector3.zero;
                    hand.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 3.5f) * 180f, 0);
                    var img = hand.GetComponent<Image>();
                    if (img && img.color != Color.white) img.color = Color.white;
                }
                else if (handMode == HandAnimMode.SwipeUp)
                {
                    if (!hand.gameObject.activeSelf) hand.gameObject.SetActive(true);
                    hand.localScale = Vector3.one;
                    hand.localEulerAngles = new Vector3(0, 0, 90f); // point finger upwards
                    float t = Mathf.Repeat(Time.unscaledTime * 1.6f, 1f);
                    hand.anchoredPosition = new Vector2(0, Mathf.Lerp(-70f, 110f, t));
                    var img = hand.GetComponent<Image>();
                    if (img)
                    {
                        Color c = Color.white;
                        c.a = (t < 0.2f) ? t / 0.2f : (t > 0.8f ? (1f - t) / 0.2f : 1f);
                        img.color = c;
                    }
                }
                else if (handMode == HandAnimMode.SwipeDown)
                {
                    if (!hand.gameObject.activeSelf) hand.gameObject.SetActive(true);
                    hand.localScale = Vector3.one;
                    hand.localEulerAngles = new Vector3(0, 0, -90f); // point finger downwards
                    float t = Mathf.Repeat(Time.unscaledTime * 1.6f, 1f);
                    hand.anchoredPosition = new Vector2(0, Mathf.Lerp(110f, -70f, t));
                    var img = hand.GetComponent<Image>();
                    if (img)
                    {
                        Color c = Color.white;
                        c.a = (t < 0.2f) ? t / 0.2f : (t > 0.8f ? (1f - t) / 0.2f : 1f);
                        img.color = c;
                    }
                }
                else if (handMode == HandAnimMode.PointCooldown)
                {
                    if (!hand.gameObject.activeSelf) hand.gameObject.SetActive(true);
                    if (slowmoStockRect != null)
                    {
                        var cdPos = slowmoStockRect.position;
                        hand.position = cdPos + new Vector3(65f, 0f, 0f);
                        hand.localEulerAngles = new Vector3(0, 0, 180f); // point left towards the HUD row
                        float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 8f);
                        hand.localScale = Vector3.one * pulse;
                    }
                    var img = hand.GetComponent<Image>();
                    if (img && img.color != Color.white) img.color = Color.white;
                }
                else
                {
                    if (hand.gameObject.activeSelf) hand.gameObject.SetActive(false);
                    hand.localScale = Vector3.one;
                }
            }
            if (hintText && hintText.gameObject.activeInHierarchy)
            {
                hintText.transform.localScale = Vector3.one * (1 + 0.04f * Mathf.Sin(Time.unscaledTime * 4));
            }
            yield return null;
        }
    }

    public void StartRun()
    {
        if (started) return; started = true;
        startOverlay.blocksRaycasts = false;
        var b = startOverlay.GetComponentInChildren<Button>(); if (b) b.enabled = false;
        var hit = startOverlay.GetComponent<Image>(); if (hit) hit.raycastTarget = false;
        if (hintText)
        {
            hintText.raycastTarget = false;
            hintText.text = "";
        }
        handMode = HandAnimMode.Hidden;
        if (hand)
        {
            var hi = hand.GetComponent<Image>(); if (hi) hi.raycastTarget = false;
            hand.gameObject.SetActive(false);
        }
        gm.BeginRun();
        if (isTutorialRun) tutorialCo = StartCoroutine(Tutorial());
        else
        {
            if (pl) { pl.SetSteerAllowed(true); pl.SetJumpAllowed(true); pl.SetSlowmoAllowed(true); }
            TCUI.Hide(this, startOverlay);
        }
    }

    IEnumerator Tutorial()
    {
        // -------------------------------------------------------------
        // STEP 1: DODGE THE BRANCH
        // (Skipped initial free-roam left/right rotation to prevent spider climbing infinitely)
        // -------------------------------------------------------------
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);
        pl.SetSlowmoAllowed(false);
        startOverlay.gameObject.SetActive(true);
        startOverlay.alpha = 1f;
        startOverlay.interactable = false;
        startOverlay.blocksRaycasts = false;
        startOverlay.transform.SetAsLastSibling();

        // Spawn a spiky branch directly in the spider's path ahead
        float branchAngle = pl.CurrentYaw;
        float branchY = SpiderY + 4.5f;
        var branch = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialBranch(branchY, branchAngle) : null;

        // Pre-spawn straight strings of 4 coins on BOTH sides after dodging the branch (left and right)
        // so whichever side the player dodges toward, coins are already in the scene seamlessly
        float coinStartY = branchY + 1.0f;
        float leftCoinAngle = branchAngle - 55f;
        float rightCoinAngle = branchAngle + 55f;
        var leftCoins = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialCoins(coinStartY, leftCoinAngle, 4) : null;
        var rightCoins = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialCoins(coinStartY, rightCoinAngle, 4) : null;

        // Approach branch at normal speed until 2.0m away
        while (branch != null && branch.gameObject.activeInHierarchy && (branchY - SpiderY) > 2.0f)
        {
            yield return null;
        }

        // Approach in slow motion until frozen right in front of the branch (0.90m)
        yield return StartCoroutine(SmoothTimeScale(0.18f, 0.2f));
        while (branch != null && branch.gameObject.activeInHierarchy && (branchY - SpiderY) > 0.90f)
        {
            yield return null;
        }

        if (branch != null && branch.gameObject.activeInHierarchy)
        {
            // Freeze right in front of the branch!
            yield return WaitWhilePausedOverlay();
            if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
            IsTutorialFrozen = true;
            Time.timeScale = 0f;
            currentTargetTimeScale = 0f;

            pl.SetSteerAllowed(true);
            pl.SetJumpAllowed(false);
            handMode = HandAnimMode.Horizontal;
            hintText.text = "DRAG TO DODGE\nthe branch!";

            // Let the player move enough (at least 50 degrees away from the branch) to dodge it properly!
            while (branch != null && branch.gameObject.activeInHierarchy && Mathf.Abs(Mathf.DeltaAngle(branchAngle, pl.CurrentYaw)) < 50f)
            {
                if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
                yield return null;
            }

            // Align the dodged side's coins directly down the player's clear line of sight
            float chosenYaw = pl.CurrentYaw;
            bool dodgedLeft = Mathf.DeltaAngle(branchAngle, chosenYaw) < 0f;
            var activeCoins = dodgedLeft ? leftCoins : rightCoins;
            if (activeCoins != null)
            {
                foreach (var c in activeCoins)
                {
                    if (c && c.gameObject.activeInHierarchy)
                    {
                        c.transform.rotation = Quaternion.Euler(0, chosenYaw, 0);
                    }
                }
            }

            // Player moved enough to fully clear the branch! Lock controls and climb straight past safely
            IsTutorialFrozen = false;
            hintText.text = "";
            handMode = HandAnimMode.Hidden;
            pl.SetSteerAllowed(false);
            pl.SetJumpAllowed(false);
            foreach (var col in branch.GetComponentsInChildren<Collider>()) col.enabled = false;

            yield return WaitWhilePausedOverlay();
            if (IsTutorialAborted) yield break;
            Time.timeScale = 1.0f;
            currentTargetTimeScale = 1.0f;

            // Wait until spider climbs past the branch
            while (branch != null && branch.gameObject.activeInHierarchy && SpiderY < branchY + 0.8f)
            {
                if (IsTutorialAborted) yield break;
                yield return null;
            }

            // Wait until spider collects the pre-existing coins
            while (SpiderY < coinStartY + 1.6f)
            {
                if (IsTutorialAborted) yield break;
                yield return null;
            }
        }
        else
        {
            IsTutorialFrozen = false;
            yield return WaitWhilePausedOverlay();
            if (!IsTutorialAborted) { Time.timeScale = 1.0f; currentTargetTimeScale = 1.0f; }
        }

        // Lock controls again until next step
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);
        yield return new WaitForSeconds(0.4f);

        // -------------------------------------------------------------
        // STEP 3: DODGE THE WASP
        // -------------------------------------------------------------
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);

        // Spawn a wasp directly in the spider's path ahead
        float waspAngle = pl.CurrentYaw;
        float waspY = SpiderY + 4.5f;
        var wasp = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialWasp(waspY, waspAngle) : null;

        // Climb forward until approaching the wasp (2.0m away)
        while (wasp != null && wasp.gameObject.activeInHierarchy && (waspY - SpiderY) > 2.0f)
        {
            yield return null;
        }

        // Approach in slow motion until frozen clearly in front of the wasp (1.15m clears hovering sting collider)
        yield return StartCoroutine(SmoothTimeScale(0.18f, 0.2f));
        while (wasp != null && wasp.gameObject.activeInHierarchy && (waspY - SpiderY) > 1.15f)
        {
            yield return null;
        }

        if (wasp != null && wasp.gameObject.activeInHierarchy)
        {
            // Freeze the spider completely right in front of the wasp!
            yield return WaitWhilePausedOverlay();
            if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
            IsTutorialFrozen = true;
            Time.timeScale = 0f;
            currentTargetTimeScale = 0f;

            pl.SetSteerAllowed(true);
            pl.SetJumpAllowed(false);
            handMode = HandAnimMode.Horizontal;
            hintText.text = "DRAG TO DODGE\nthe wasp!";

            // Let the player move enough (at least 50 degrees away from the wasp) to dodge it properly!
            while (wasp != null && wasp.gameObject.activeInHierarchy && Mathf.Abs(Mathf.DeltaAngle(waspAngle, pl.CurrentYaw)) < 50f)
            {
                if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
                yield return null;
            }

            // Player moved enough to fully clear the wasp! Lock controls and climb straight past safely
            IsTutorialFrozen = false;
            hintText.text = "";
            handMode = HandAnimMode.Hidden;
            pl.SetSteerAllowed(false);
            pl.SetJumpAllowed(false);
            foreach (var col in wasp.GetComponentsInChildren<Collider>()) col.enabled = false;

            yield return WaitWhilePausedOverlay();
            if (IsTutorialAborted) yield break;
            Time.timeScale = 1.0f;
            currentTargetTimeScale = 1.0f;

            // Wait until spider climbs past the wasp
            while (wasp != null && wasp.gameObject.activeInHierarchy && SpiderY < waspY + 0.8f)
            {
                if (IsTutorialAborted) yield break;
                yield return null;
            }
        }
        else
        {
            IsTutorialFrozen = false;
            yield return WaitWhilePausedOverlay();
            if (!IsTutorialAborted) { Time.timeScale = 1.0f; currentTargetTimeScale = 1.0f; }
        }

        // Lock controls again until next step
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);
        yield return new WaitForSeconds(0.6f);

        // -------------------------------------------------------------
        // STEP 4: JUMP OVER BEES
        // -------------------------------------------------------------
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);

        // Spawn bees well outside the camera view ahead (8.0m) so they scroll naturally into view rather than popping in
        float beeY = SpiderY + 8.0f;
        var bees = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialBees(beeY) : null;

        // Climb forward until approaching bees (2.0m away)
        while (bees != null && bees.gameObject.activeInHierarchy && (beeY - SpiderY) > 2.0f)
        {
            yield return null;
        }

        // Approach in slow motion until frozen in front of the bees (0.90m)
        yield return StartCoroutine(SmoothTimeScale(0.18f, 0.2f));
        while (bees != null && bees.gameObject.activeInHierarchy && (beeY - SpiderY) > 0.90f)
        {
            yield return null;
        }

        if (bees != null && bees.gameObject.activeInHierarchy)
        {
            // Freeze completely right in front of the bees!
            yield return WaitWhilePausedOverlay();
            if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
            IsTutorialFrozen = true;
            Time.timeScale = 0f;
            currentTargetTimeScale = 0f;

            pl.SetSteerAllowed(false);
            pl.SetJumpAllowed(true);
            handMode = HandAnimMode.SwipeUp;
            hintText.text = "SWIPE UP TO JUMP\nover hazards!";

            jumpedOnce = false;
            Vector2 touchDownPos = Vector2.zero;
            bool isDown = false;
            float tutLastTapTime = -1f;
            Vector2 tutLastTapPos = Vector2.zero;

            // Wait until player jumps (listen for swipe up, double tap, or spacebar)
            while (!jumpedOnce && (spider == null || !spider.IsJumping))
            {
                if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
                if (IsPausedOverlay || pl.IsInputBlocked)
                {
                    isDown = false;
                    tutLastTapTime = -1f;
                    yield return null;
                    continue;
                }
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                {
                    pl.jump();
                    jumpedOnce = true;
                    break;
                }
                if (Input.touchCount > 0)
                {
                    var t = Input.GetTouch(0);
                    if (t.phase == TouchPhase.Began) { touchDownPos = t.position; isDown = true; }
                    else if (isDown)
                    {
                        float upDist = t.position.y - touchDownPos.y;
                        float sideDist = Mathf.Abs(t.position.x - touchDownPos.x);
                        if (upDist > Screen.height * 0.055f && upDist > sideDist * 1.35f)
                        {
                            tutLastTapTime = -1f;
                            pl.jump();
                            jumpedOnce = true;
                            break;
                        }
                        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                        {
                            if (Vector2.Distance(t.position, touchDownPos) < Screen.height * 0.04f)
                            {
                                // Double tap required: single tap does NOT jump
                                if (tutLastTapTime > 0 && (Time.unscaledTime - tutLastTapTime) < 0.35f
                                    && Vector2.Distance(t.position, tutLastTapPos) < Screen.height * 0.12f)
                                {
                                    tutLastTapTime = -1f;
                                    pl.jump();
                                    jumpedOnce = true;
                                    break;
                                }
                                else
                                {
                                    tutLastTapTime = Time.unscaledTime;
                                    tutLastTapPos = t.position;
                                }
                            }
                            else
                            {
                                tutLastTapTime = -1f;
                            }
                            isDown = false;
                        }
                    }
                }
                else
                {
                    if (Input.GetMouseButtonDown(0)) { touchDownPos = Input.mousePosition; isDown = true; }
                    else if (isDown)
                    {
                        float upDist = ((Vector2)Input.mousePosition).y - touchDownPos.y;
                        float sideDist = Mathf.Abs(((Vector2)Input.mousePosition).x - touchDownPos.x);
                        if (upDist > Screen.height * 0.055f && upDist > sideDist * 1.35f)
                        {
                            tutLastTapTime = -1f;
                            pl.jump();
                            jumpedOnce = true;
                            break;
                        }
                        if (Input.GetMouseButtonUp(0))
                        {
                            if (Vector2.Distance(Input.mousePosition, touchDownPos) < Screen.height * 0.04f)
                            {
                                // Double click required: single click does NOT jump
                                if (tutLastTapTime > 0 && (Time.unscaledTime - tutLastTapTime) < 0.35f
                                    && Vector2.Distance(Input.mousePosition, tutLastTapPos) < Screen.height * 0.12f)
                                {
                                    tutLastTapTime = -1f;
                                    pl.jump();
                                    jumpedOnce = true;
                                    break;
                                }
                                else
                                {
                                    tutLastTapTime = Time.unscaledTime;
                                    tutLastTapPos = Input.mousePosition;
                                }
                            }
                            else
                            {
                                tutLastTapTime = -1f;
                            }
                            isDown = false;
                        }
                    }
                }
                yield return null;
            }

            // Jump performed! Instantly restore normal time so player sees the full jump!
            IsTutorialFrozen = false;
            hintText.text = "";
            handMode = HandAnimMode.Hidden;
            pl.SetSteerAllowed(false);
            pl.SetJumpAllowed(false);
            foreach (var col in bees.GetComponentsInChildren<Collider>()) col.enabled = false;

            yield return WaitWhilePausedOverlay();
            if (IsTutorialAborted) yield break;
            Time.timeScale = 1.0f;
            currentTargetTimeScale = 1.0f;

            // Let the player sail safely over the bees
            while (bees != null && bees.gameObject.activeInHierarchy && SpiderY < beeY + 0.8f)
            {
                if (IsTutorialAborted) yield break;
                yield return null;
            }
        }
        else
        {
            IsTutorialFrozen = false;
            yield return WaitWhilePausedOverlay();
            if (!IsTutorialAborted) { Time.timeScale = 1.0f; currentTargetTimeScale = 1.0f; }
        }

        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);
        yield return new WaitForSeconds(0.6f);

        // -------------------------------------------------------------
        // STEP 5: DODGE THE SNAKE
        // -------------------------------------------------------------
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);

        // Spawn a snake ahead in the spider's path spiraling down the trunk
        float snakeAngle = pl.CurrentYaw;
        float snakeY = SpiderY + 4.8f;
        var snake = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialSnake(snakeY, snakeAngle) : null;
        hintText.text = "SNAKE INCOMING!";
        handMode = HandAnimMode.Hidden;

        // Climb forward until approaching the snake (2.2m away)
        while (snake != null && snake.gameObject.activeInHierarchy && (snake.y - SpiderY) > 2.2f)
        {
            if (IsTutorialAborted) yield break;
            yield return null;
        }

        // Approach in slow motion until frozen right in front of the snake (1.2m away)
        yield return StartCoroutine(SmoothTimeScale(0.18f, 0.2f));
        while (snake != null && snake.gameObject.activeInHierarchy && (snake.y - SpiderY) > 1.2f)
        {
            if (IsTutorialAborted) yield break;
            yield return null;
        }

        if (snake != null && snake.gameObject.activeInHierarchy)
        {
            // Freeze right before collision and prompt player to drag and dodge the snake!
            yield return WaitWhilePausedOverlay();
            if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
            IsTutorialFrozen = true;
            Time.timeScale = 0f;
            currentTargetTimeScale = 0f;

            pl.SetSteerAllowed(true);
            pl.SetJumpAllowed(false);
            handMode = HandAnimMode.Horizontal;
            hintText.text = "DRAG TO DODGE\nthe snake!";

            // Wait until the player circles at least 50 degrees away from the snake's lane
            while (snake != null && snake.gameObject.activeInHierarchy && Mathf.Abs(Mathf.DeltaAngle(snakeAngle, pl.CurrentYaw)) < 50f)
            {
                if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
                yield return null;
            }

            // Player moved enough to fully clear the snake! Lock controls and climb straight past safely
            IsTutorialFrozen = false;
            hintText.text = "";
            handMode = HandAnimMode.Hidden;
            pl.SetSteerAllowed(false);
            pl.SetJumpAllowed(false);
            foreach (var col in snake.GetComponentsInChildren<Collider>()) col.enabled = false;

            yield return WaitWhilePausedOverlay();
            if (IsTutorialAborted) yield break;
            Time.timeScale = 1.0f;
            currentTargetTimeScale = 1.0f;

            // Wait until spider climbs safely past the snake
            while (snake != null && snake.gameObject.activeInHierarchy && SpiderY < snake.y + 0.8f)
            {
                if (IsTutorialAborted) yield break;
                yield return null;
            }
        }
        else
        {
            IsTutorialFrozen = false;
            yield return WaitWhilePausedOverlay();
            if (!IsTutorialAborted) { Time.timeScale = 1.0f; currentTargetTimeScale = 1.0f; }
        }

        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);
        yield return new WaitForSeconds(0.6f);

        // -------------------------------------------------------------
        // STEP 6: DODGE THE SQUIRREL
        // -------------------------------------------------------------
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);

        // Spawn a squirrel directly in the spider's path ahead (falling leaves warning triggers immediately!)
        float squirrelAngle = pl.CurrentYaw;
        float squirrelY = SpiderY + 6.0f;
        var squirrel = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialSquirrel(squirrelY, squirrelAngle) : null;
        hintText.text = "WATCH FOR FALLING LEAVES!\nSquirrel incoming!";
        handMode = HandAnimMode.Hidden;

        // Climb forward while leaves fall and squirrel begins scampering down
        while (squirrel != null && squirrel.gameObject.activeInHierarchy && (squirrel.transform.position.y - SpiderY) > 3.0f)
        {
            if (IsTutorialAborted) yield break;
            yield return null;
        }

        // Slow down smoothly as squirrel approaches down the trunk
        yield return StartCoroutine(SmoothTimeScale(0.18f, 0.2f));
        while (squirrel != null && squirrel.gameObject.activeInHierarchy && (squirrel.transform.position.y - SpiderY) > 1.8f)
        {
            if (IsTutorialAborted) yield break;
            yield return null;
        }

        if (squirrel != null && squirrel.gameObject.activeInHierarchy)
        {
            // Freeze right before collision and prompt player to steer away to the other side
            yield return WaitWhilePausedOverlay();
            if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
            IsTutorialFrozen = true;
            Time.timeScale = 0f;
            currentTargetTimeScale = 0f;

            pl.SetSteerAllowed(true);
            pl.SetJumpAllowed(false);
            handMode = HandAnimMode.Horizontal;
            hintText.text = "DRAG TO AVOID\nthe squirrel!";

            // Wait until the player circles at least 50 degrees away from the squirrel's lane
            while (squirrel != null && squirrel.gameObject.activeInHierarchy && Mathf.Abs(Mathf.DeltaAngle(squirrelAngle, pl.CurrentYaw)) < 50f)
            {
                if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
                yield return null;
            }

            // Player cleared the lane! Unfreeze and pass safely
            IsTutorialFrozen = false;
            hintText.text = "";
            handMode = HandAnimMode.Hidden;
            pl.SetSteerAllowed(false);
            pl.SetJumpAllowed(false);
            foreach (var col in squirrel.GetComponentsInChildren<Collider>()) col.enabled = false;

            yield return WaitWhilePausedOverlay();
            if (IsTutorialAborted) yield break;
            Time.timeScale = 1.0f;
            currentTargetTimeScale = 1.0f;

            while (squirrel != null && squirrel.gameObject.activeInHierarchy && SpiderY < squirrel.transform.position.y + 0.8f)
            {
                if (IsTutorialAborted) yield break;
                yield return null;
            }
        }
        else
        {
            IsTutorialFrozen = false;
            yield return WaitWhilePausedOverlay();
            if (!IsTutorialAborted) { Time.timeScale = 1.0f; currentTargetTimeScale = 1.0f; }
        }

        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);
        yield return new WaitForSeconds(0.6f);

        // -------------------------------------------------------------
        // STEP 6: REACH THE CHILLI
        // -------------------------------------------------------------
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);

        // Spawn a chilli ahead in the spider's path
        float chilliAngle = pl.CurrentYaw;
        float chilliY = SpiderY + 4.2f;
        var chilli = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialChilli(chilliY, chilliAngle) : null;
        hintText.text = "GRAB THE CHILLI\nfor Sonic Boost!";
        handMode = HandAnimMode.Hidden;

        // Climb forward until approaching the chilli (1.8m away)
        while (chilli != null && chilli.gameObject.activeInHierarchy && (chilliY - SpiderY) > 1.8f)
        {
            yield return null;
        }

        // Slow motion approach towards the chilli without freezing
        yield return StartCoroutine(SmoothTimeScale(0.2f, 0.2f));

        // Keep moving in slow motion until very close to the chilli (0.25m away)
        while (chilli != null && chilli.gameObject.activeInHierarchy && (chilliY - SpiderY) > 0.25f)
        {
            yield return null;
        }

        // Spider is very close to the chilli: restore normal time scale right before contact!
        yield return WaitWhilePausedOverlay();
        if (IsTutorialAborted) yield break;
        Time.timeScale = 1.0f;
        currentTargetTimeScale = 1.0f;
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);

        // Wait until spider grabs the chilli
        while (chilli != null && chilli.gameObject.activeInHierarchy && (chilliY - SpiderY) > -0.2f)
        {
            yield return null;
        }

        // Lock steering completely so player cannot move left or right after collecting chilli
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);

        // Spawn a wasp and branch directly in the spider's path to demonstrate chilli invincibility & smash ability
        float smashLane = pl.CurrentYaw;
        float waspSmashY = SpiderY + 2.2f;
        float branchSmashY = SpiderY + 4.4f;
        var tutWasp = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialWasp(waspSmashY, smashLane) : null;
        var tutBranch = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialBranch(branchSmashY, smashLane) : null;

        hintText.text = "SMASH THROUGH ENEMIES\nwhile boosting!";
        // Keep steering aligned down the smash lane so the player collides with both obstacles
        float smashTimer = 0f;
        while (smashTimer < 2.0f && !IsTutorialAborted)
        {
            pl.SetSteerAllowed(false);
            smashTimer += Time.deltaTime;

            // When spider reaches the wasp during chilli boost, ensure wasp is smashed and killed
            if (tutWasp != null && tutWasp.gameObject.activeInHierarchy && SpiderY >= waspSmashY - 0.25f)
            {
                TCSfx.Play("wasp_kill", 0.95f);
                tutWasp.Smash();
            }

            // When spider reaches the branch during chilli boost, smash through it
            if (tutBranch != null && tutBranch.gameObject.activeInHierarchy && SpiderY >= branchSmashY - 0.25f)
            {
                TCFx.Spawn(spider != null ? spider.impactParticle : null, tutBranch.transform.position);
                tutBranch.Release();
                TCAudio.Play(TCAssets.I != null ? TCAssets.I.purchase : null);
                TCFx.Shake(0.18f);
                TCServices.LightHaptic();
            }

            yield return null;
        }

        hintText.text = "";
        yield return new WaitForSeconds(0.6f);

        // -------------------------------------------------------------
        // STEP 6: SLOW MOTION POWER UP
        // -------------------------------------------------------------
        // Restrict steering left/right & jumping until slow-mo power up is collected
        pl.SetSteerAllowed(false);
        pl.SetJumpAllowed(false);

        // Spawn a cooldown slow-mo pickup directly in the spider's forward path
        float cdAngle = pl.CurrentYaw;
        float cdY = SpiderY + 4.2f;
        var cdPickup = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialCooldown(cdY, cdAngle) : null;
        hintText.text = "COLLECT FREEZE POWERUP!";
        handMode = HandAnimMode.Hidden;

        // Approach cooldown pickup until collected or passed
        while (cdPickup != null && cdPickup.gameObject.activeInHierarchy && (cdY - SpiderY) > -0.6f && playerInteract.CooldownInventory <= 0)
        {
            yield return null;
        }

        float cdWaitElapsed = 0f;
        while (playerInteract.CooldownInventory <= 0 && cdWaitElapsed < 0.8f)
        {
            cdWaitElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Safety fallback so high boost speed or physics hitch on mobile can never softlock collection
        if (playerInteract.CooldownInventory <= 0)
        {
            if (cdPickup != null)
            {
                for (int i = 0; i < cdPickup.transform.childCount; i++)
                    cdPickup.transform.GetChild(i).gameObject.SetActive(false);
            }
            playerInteract.CooldownInventory = 1;
            playerInteract.OnEvent?.Invoke("cooldown", spider != null ? spider.transform.position : pl.transform.position);
            TCSfx.Play("cooldown_collect", 0.85f);
        }

        // Wait for 2 seconds after collecting (and finish any remaining upgraded chilli boost) before hurdle prompt
        hintText.text = "";
        yield return new WaitForSeconds(2.0f);
        while (spider != null && (spider.boostDuration > 0f || pl.boost))
        {
            yield return null;
        }

        // Spawn a hurdle ahead to dodge using slow motion
        float hurdleAngle = pl.CurrentYaw;
        float hurdleY = SpiderY + 5.0f;
        var hurdle = TCTrack.Instance != null ? TCTrack.Instance.SpawnTutorialBranch(hurdleY, hurdleAngle) : null;

        // Approach hurdle until 2.2m away
        while (hurdle != null && hurdle.gameObject.activeInHierarchy && (hurdleY - SpiderY) > 2.2f)
        {
            yield return null;
        }

        if (hurdle != null && hurdle.gameObject.activeInHierarchy)
        {
            // Ensure player has a Slow-Mo charge available if they tapped it early while waiting
            if (playerInteract.CooldownInventory <= 0 && (TCTimeDilation.Instance == null || !TCTimeDilation.Instance.IsActive))
            {
                playerInteract.CooldownInventory = 1;
            }
            if (startOverlay != null)
            {
                startOverlay.blocksRaycasts = false;
                startOverlay.interactable = false;
            }

            // Ease into slow motion and freeze before collision if player takes time to swipe down for slow-mo
            hintText.text = "SWIPE DOWN\nto trigger slow motion!";
            handMode = HandAnimMode.SwipeDown;

            // Allow slow-motion swipe down immediately as time starts slowing down before reaching the obstacle!
            if (pl) pl.SetSlowmoAllowed(true);

            float slowEaseStart = Time.timeScale;
            float slowEaseElapsed = 0f;
            while (hurdle != null && hurdle.gameObject.activeInHierarchy && (TCTimeDilation.Instance == null || !TCTimeDilation.Instance.IsActive))
            {
                if (IsTutorialAborted) { IsTutorialFrozen = false; yield break; }
                if (IsPausedOverlay) { yield return null; continue; }
                if ((hurdleY - SpiderY) <= 1.0f)
                {
                    IsTutorialFrozen = true;
                    Time.timeScale = 0f;
                    currentTargetTimeScale = 0f;
                }
                else
                {
                    slowEaseElapsed += Time.unscaledDeltaTime;
                    float frac = Mathf.Clamp01(slowEaseElapsed / 0.2f);
                    float ts = Mathf.Lerp(slowEaseStart, 0.15f, frac);
                    Time.timeScale = ts;
                    currentTargetTimeScale = ts;
                }
                yield return null;
            }
            IsTutorialFrozen = false;

            currentTargetTimeScale = 1.0f;

            // Hide hand pointer and re-enable full steering & jump control to dodge the hurdle!
            handMode = HandAnimMode.Hidden;
            hintText.text = "DODGE THE HURDLE!";
            pl.SetSteerAllowed(true);
            pl.SetJumpAllowed(true);

            // Wait until the hurdle is passed or dodged
            bool hurdleDodged = false;
            while (hurdle != null && hurdle.gameObject.activeInHierarchy && (hurdleY - SpiderY) > -0.6f)
            {
                if (IsTutorialAborted) yield break;
                if (!hurdleDodged && Mathf.Abs(Mathf.DeltaAngle(hurdleAngle, pl.CurrentYaw)) >= 48f)
                {
                    hurdleDodged = true;
                    // Player dodged enough! Disable hurdle colliders so no grazing collision can occur
                    foreach (var col in hurdle.GetComponentsInChildren<Collider>()) col.enabled = false;
                    pl.SetSteerAllowed(false);
                    pl.SetJumpAllowed(false);
                }
                yield return null;
            }
            hintText.text = "";
            pl.SetSteerAllowed(false);
            pl.SetJumpAllowed(false);
        }
        else
        {
            IsTutorialFrozen = false;
            yield return WaitWhilePausedOverlay();
            if (!IsTutorialAborted) { Time.timeScale = 1.0f; currentTargetTimeScale = 1.0f; }
            pl.SetSteerAllowed(true);
            pl.SetJumpAllowed(true);
        }

        // Complete the tutorial!
        isTutorialRun = false;
        TCSave.TutorialDone = true;
        if (gm != null)
        {
            gm.EndTutorialTimeFreeze();
        }
        if (TCTrack.Instance != null)
        {
            TCTrack.Instance.OnTutorialFinished();
        }

        // Re-enable controls immediately so normal gameplay can be controlled!
        if (pl)
        {
            pl.SetSteerAllowed(true);
            pl.SetJumpAllowed(true);
            pl.SetSlowmoAllowed(true);
        }

        hintText.text = "ALL SET!\nYOU'RE GOOD TO GO!";
        TCSfx.Play("celebrate", 0.95f);
        StartCoroutine(TCUI.Punch(hintText.transform, 0.35f, 0.45f));

        yield return new WaitForSeconds(2.0f);
        TCUI.Hide(this, startOverlay);
        if (pl)
        {
            pl.SetSteerAllowed(true);
            pl.SetJumpAllowed(true);
        }
    }

    // ---------------- Pause ----------------
    void BuildPause()
    {
        pausePanel = TCUI.Panel("Pause", safe.parent, out var card, new Vector2(820, 1100), "PAUSED");
        float y = 326;
        TCUI.Place((RectTransform)TCUI.Btn("Resume", card, "RESUME", TCUI.Mint, HidePause, 60, TCUI.Icon("play")).transform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(600, 140));
        TCUI.Place((RectTransform)TCUI.Btn("Restart", card, "RESTART", TCUI.Butter, () => { gm.EndRun(false); gm.Restart(); }).transform, new Vector2(0.5f, 0.5f), new Vector2(0, y - 180), new Vector2(600, 130));
        TCUI.Place((RectTransform)TCUI.Btn("Home", card, "HOME", TCUI.Sky, () => { gm.EndRun(false); gm.Home(); }).transform, new Vector2(0.5f, 0.5f), new Vector2(0, y - 340), new Vector2(600, 130));
        Toggle(card, "Sound", -174, () => TCSave.Sound, v => TCSave.Sound = v);
        Toggle(card, "Music", -294, () => TCSave.Music, v => { TCSave.Music = v; gm.SetMusic(v); });
        Toggle(card, "Vibration", -414, () => TCSave.Vibration, v => TCSave.Vibration = v);
    }
    public static void Toggle(RectTransform card, string label, float y, System.Func<bool> get, System.Action<bool> set, float width = 600)
    {
        var row = TCUI.Rect(label, card); TCUI.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(width, 90));
        var l = TCUI.Label("L", row, label, 48, TCUI.Cocoa, TextAlignmentOptions.Left); TCUI.Stretch(l.rectTransform, 0, 200, 0, 0);
        Button b = null; TextMeshProUGUI bt = null;
        b = TCUI.Btn("T", row, "", get() ? TCUI.Crimson : TCUI.Tan, () =>
        {
            set(!get());
            TCTheme.SetFace(b, get() ? TCUI.Crimson : TCUI.Tan); bt.text = get() ? "ON" : "OFF";
        }, 44);
        TCUI.Place((RectTransform)b.transform, new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(180, 84));
        bt = TCUI.Label("V", b.transform, get() ? "ON" : "OFF", 44, TCUI.Cocoa); TCUI.Stretch(bt.rectTransform);
        TCTheme.SetFace(b, get() ? TCUI.Crimson : TCUI.Tan); bt.text = get() ? "ON" : "OFF"; // live state (also for baked UI)
    }

    public void ShowPause()
    {
        if (resuming) return;
        if (!started || pl == null || pl.GameOver || (gm != null && gm.runEnded) || pausePanel.gameObject.activeSelf) return;
        TCUI.LiftOverlay(pausePanel.transform, 80);
        gm.Pause(true); TCUI.Show(this, pausePanel);
    }
    void HidePause()
    {
        if (!pausePanel.gameObject.activeSelf || resuming) return;
        // Keep the game paused until the 3-2-1 countdown finishes.
        TCUI.Hide(this, pausePanel, () => StartCoroutine(ResumeCountdown()));
    }

    IEnumerator ResumeCountdown()
    {
        resuming = true;
        if (gm != null) gm.Pause(true);
        Time.timeScale = 0f;
        if (countDownGroup)
        {
            countDownGroup.gameObject.SetActive(true);
            countDownGroup.alpha = 1f;
            countDownGroup.transform.SetAsLastSibling();
        }
        for (int i = 3; i >= 1; i--)
        {
            if (countDownText) countDownText.text = i.ToString();
            if (countDownRoot) StartCoroutine(TCUI.Punch(countDownRoot, 0.35f, 0.35f));
            TCSfx.Play("chime", 0.7f);
            yield return new WaitForSecondsRealtime(0.85f);
        }
        if (countDownGroup)
        {
            countDownGroup.alpha = 0f;
            countDownGroup.gameObject.SetActive(false);
        }
        resuming = false;
        if (gm != null) gm.Pause(false);
        if (pl != null && pl.GameOver) { Time.timeScale = 1f; yield break; }
        bool slowActive = TCTimeDilation.Instance != null && TCTimeDilation.Instance.IsActive;
        Time.timeScale = slowActive ? TCTimeDilation.SlowScale : currentTargetTimeScale;
    }

    // ---------------- Revive ----------------
    void BuildRevive()
    {
        revivePanel = TCUI.Panel("Revive", safe.parent, out var card, new Vector2(840, 940), "KEEP CLIMBING?");
        var timerBg = TCUI.Img("TimerBg", card, TCUI.Cream, TCUI.Circle);
        TCUI.Place(timerBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(160, 160));
        reviveTimer = TCUI.Label("Timer", timerBg.transform, "5", 100, TCUI.Crimson);
        TCUI.Stretch(reviveTimer.rectTransform);
        TCUI.Outline(reviveTimer, 0.2f);

        reviveAdBtn = TCUI.Btn("Ad", card, "REVIVE (WATCH AD)", TCUI.Mint, () =>
        {
            StopReviveTimer();
            TCServices.Rewarded(DoRevive, "revive", () =>
            {
                // If ad not available / failed, restart countdown so user can pick gems or skip
                if (revivePanel != null && revivePanel.gameObject.activeSelf)
                {
                    StopReviveTimer();
                    reviveCo = StartCoroutine(ReviveCountdown());
                }
            });
        }, 50, TCUI.Icon("play"));
        TCUI.Place((RectTransform)reviveAdBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -45), new Vector2(680, 130));

        reviveGemBtn = TCUI.Btn("Gems", card, "REVIVE (" + TCConfig.I.reviveCostGems + " GEMS)", TCUI.Sky, () =>
        {
            if (TCSave.Gems >= TCConfig.I.reviveCostGems) { TCSave.Gems -= TCConfig.I.reviveCostGems; DoRevive(); }
        }, 48, TCUI.Icon("gem"));
        TCUI.Place((RectTransform)reviveGemBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -225), new Vector2(680, 120));

        var skipBtn = TCUI.Btn("Skip", card, "NO THANKS", Color.clear, SkipRevive, 42);
        TCUI.Place((RectTransform)skipBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -385), new Vector2(440, 95));
        skipBtn.transition = Selectable.Transition.None;
        var skipImg = skipBtn.GetComponent<Image>();
        if (skipImg) skipImg.color = new Color(skipImg.color.r, skipImg.color.g, skipImg.color.b, 0f);
        var skipOutline = skipBtn.GetComponent<UnityEngine.UI.Outline>();
        if (skipOutline) skipOutline.enabled = false;
        foreach (var f in card.GetComponentsInChildren<TCFollowRect>(true))
        {
            if (f && f.target == skipBtn.transform)
            {
                f.gameObject.SetActive(false);
            }
        }
    }
    Coroutine reviveCo;
    void StopReviveTimer() { if (reviveCo != null) { StopCoroutine(reviveCo); reviveCo = null; } }
    public void ShowGameOver(bool canRevive)
    {
        IsTutorialFrozen = false;
        if (pausePanel != null && pausePanel.gameObject.activeSelf) pausePanel.gameObject.SetActive(false);
        if (gm != null && gm.IsPaused) gm.Pause(false);
        Time.timeScale = 1f;
        if (pl) { pl.ResetInputState(); pl.SetSteerAllowed(true); pl.SetJumpAllowed(true); pl.SetSlowmoAllowed(true); }
        // A death during the first-run tutorial left its overlay up over the results
        if (tutorialCo != null) { StopCoroutine(tutorialCo); tutorialCo = null; isTutorialRun = false; TCSave.TutorialDone = true; if (gm != null) gm.EndTutorialTimeFreeze(); }
        if (startOverlay.alpha > 0) TCUI.Hide(this, startOverlay);
        if (canRevive)
        {
            bool adReady = TCServices.IsRewardedReady();
            if (reviveAdBtn)
            {
                reviveAdBtn.gameObject.SetActive(adReady);
            }
            if (reviveGemBtn)
            {
                reviveGemBtn.interactable = TCSave.Gems >= TCConfig.I.reviveCostGems;
                var gemRt = (RectTransform)reviveGemBtn.transform;
                // Center the gem button nicely if the watch-ad button is hidden
                gemRt.anchoredPosition = new Vector2(0, adReady ? -225 : -135);
            }
            TCUI.Show(this, revivePanel);
            reviveCo = StartCoroutine(ReviveCountdown());
        }
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

        var caption = TCUI.Label("Caption", card, "DISTANCE CLIMBED", 32, TCUI.Brown);
        TCUI.Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 465), new Vector2(600, 38));

        resMetres = TCUI.Label("Metres", card, "0 M", 92, TCUI.Cocoa);
        if (TCUI.Fresh(resMetres)) { resMetres.enableAutoSizing = true; resMetres.fontSizeMin = 56; resMetres.fontSizeMax = 92; }
        TCUI.Place(resMetres.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 390), new Vector2(760, 95));

        var bestBadge = TCUI.Img("BestBadge", card, TCUI.Tan, TCUI.Round);
        TCUI.Place(bestBadge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 295), new Vector2(380, 58));
        resBest = TCUI.Label("Best", bestBadge.transform, "BEST 0 M", 34, TCUI.Ink);
        if (TCUI.Fresh(resBest)) { resBest.enableWordWrapping = false; resBest.enableAutoSizing = true; resBest.fontSizeMin = 20; resBest.fontSizeMax = 34; }
        TCUI.Stretch(resBest.rectTransform, 14, 14, 4, 4);

        resCoins = Row(card, "Coins Gathered", 195, false);
        resBonus = Row(card, "Distance Bonus", 95, false);
        resTotal = Row(card, "Total Coins Earned", -10, true);

        doubleBtn = TCUI.Btn("Double", card, "x2 BONUS (WATCH AD)", TCUI.Crimson, () => TCServices.Rewarded(() =>
        {
            if (!gm.ClaimDouble()) return; // reward callbacks can fire twice; never double-pay
            resCoins.text = (gm.coinsCollected * 2).ToString("N0");
            resBonus.text = (gm.DistanceBonus * 2).ToString("N0") + GemSuffix();
            resTotal.text = gm.RunReward.ToString("N0");
            StartCoroutine(TCUI.Punch(resTotal.transform, 0.4f, 0.4f));
            TCAudio.Play(TCAssets.I.purchase);
            StartCoroutine(HideDoubleAndSlideActions());
        }, "double_coins"), 46, TCUI.Icon("play"));
        TCUI.Place((RectTransform)doubleBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -165), new Vector2(680, 115));

        // Bottom action row: Home (left) and Restart (right), compact with icons only and clean spacing
        var homeBtn = TCUI.Btn("Home", card, "", TCUI.Sky, () => gm.Home(), 40, TCUI.Icon("home"));
        TCUI.Place((RectTransform)homeBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(-155, -340), new Vector2(170, 140));
        resultHomeRt = (RectTransform)homeBtn.transform;
        resultHomeRest = resultHomeRt.anchoredPosition;

        var againBtn = TCUI.Btn("Again", card, "", TCUI.Mint, () => gm.Restart(), 40, TCUI.Icon("restart"));
        TCUI.Place((RectTransform)againBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(155, -340), new Vector2(170, 140));
        resultAgainRt = (RectTransform)againBtn.transform;
        resultAgainRest = resultAgainRt.anchoredPosition;
    }

    TextMeshProUGUI Row(RectTransform card, string label, float y, bool isTotal)
    {
        Color bgCol = isTotal ? TCUI.TanLight : TCUI.CreamLight;
        var row = TCUI.Img(label, card, bgCol);
        TCUI.Place(row.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(740, isTotal ? 94 : 84));

        var l = TCUI.Label("L", row.transform, label, isTotal ? 36 : 32, TCUI.Cocoa, TextAlignmentOptions.Left);
        if (TCUI.Fresh(l)) l.enableWordWrapping = false;
        TCUI.Stretch(l.rectTransform, 32, 260, 0, 0);

        var ic = TCUI.Img("I", row.transform, Color.white, TCUI.Icon("coin"));
        TCUI.Place(ic.rectTransform, new Vector2(1, 0.5f), new Vector2(-42, 0), new Vector2(isTotal ? 54 : 46, isTotal ? 54 : 46));

        var v = TCUI.Label("V", row.transform, "0", isTotal ? 46 : 40, isTotal ? TCUI.Crimson : TCUI.Ink, TextAlignmentOptions.Right);
        if (TCUI.Fresh(v)) { v.enableWordWrapping = false; v.enableAutoSizing = true; v.fontSizeMin = 22; v.fontSizeMax = isTotal ? 46 : 40; }
        TCUI.Stretch(v.rectTransform, 300, 84, 0, 0);
        return v;
    }

    string GemSuffix() => gm.RunGems > 0 ? "  <color=#8C5C32>+" + gm.RunGems + " GEM" + (gm.RunGems > 1 ? "S" : "") + "</color>" : "";

    void ShowResults()
    {
        int prevBest = TCSave.Best;
        int total = gm.EndRun(false);
        resMetres.text = gm.Metres.ToString("N0") + " M";
        bool newBest = gm.Metres > prevBest;
        resBest.text = newBest ? "NEW BEST!" : "BEST  " + TCSave.Best.ToString("N0") + " M";
        resCoins.text = gm.coinsCollected.ToString("N0");
        resBonus.text = gm.DistanceBonus.ToString("N0") + GemSuffix();
        resTotal.text = total.ToString("N0");
        bool adReady = TCServices.IsRewardedReady();
        bool canDouble = adReady && total > 0 && !gm.DoubleClaimed;
        if (doubleBtn)
        {
            doubleBtn.gameObject.SetActive(canDouble);
            doubleBtn.transform.localScale = Vector3.one;
            doubleBtn.interactable = canDouble;
        }
        var dbRt = doubleBtn ? (RectTransform)doubleBtn.transform : null;
        float actionY = canDouble ? resultHomeRest.y : (dbRt ? dbRt.anchoredPosition.y : -165f);
        if (resultHomeRt) resultHomeRt.anchoredPosition = new Vector2(resultHomeRest.x, actionY);
        if (resultAgainRt) resultAgainRt.anchoredPosition = new Vector2(resultAgainRest.x, actionY);
        TCUI.Show(this, resultPanel);

        if (newBest)
        {
            // Triumphant High Score Celebration!
            TCSfx.Play("celebrate", 0.95f);
            StartCoroutine(TCUI.Punch(resBest.transform, 0.45f, 0.5f));
            StartCoroutine(TCUI.Punch(resMetres.transform, 0.35f, 0.4f));
            if (resultPanel) StartCoroutine(ConfettiCelebration(resultPanel.transform));
        }
    }

    IEnumerator HideDoubleAndSlideActions()
    {
        if (doubleBtn) doubleBtn.interactable = false;
        var dbRt = doubleBtn ? (RectTransform)doubleBtn.transform : null;
        Vector2 fromH = resultHomeRt ? resultHomeRt.anchoredPosition : Vector2.zero;
        Vector2 fromA = resultAgainRt ? resultAgainRt.anchoredPosition : Vector2.zero;
        // Slide Home / Restart downward into the x2 reward button's vertical position
        float targetY = dbRt ? dbRt.anchoredPosition.y : -409f;
        Vector2 toH = new Vector2(fromH.x, targetY);
        Vector2 toA = new Vector2(fromA.x, targetY);
        float dur = 0.4f;
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            if (dbRt) dbRt.localScale = Vector3.one * (1f - k);
            if (resultHomeRt) resultHomeRt.anchoredPosition = Vector2.Lerp(fromH, toH, k);
            if (resultAgainRt) resultAgainRt.anchoredPosition = Vector2.Lerp(fromA, toA, k);
            yield return null;
        }
        if (dbRt)
        {
            dbRt.localScale = Vector3.zero;
            if (doubleBtn) doubleBtn.gameObject.SetActive(false);
        }
        if (resultHomeRt) resultHomeRt.anchoredPosition = toH;
        if (resultAgainRt) resultAgainRt.anchoredPosition = toA;
    }

    void BuildCountdown()
    {
        countDownRoot = TCUI.Rect("Countdown", safe.parent);
        TCUI.LiftOverlay(countDownRoot, 90);
        TCUI.Stretch(countDownRoot);
        countDownGroup = TCUI.Add<CanvasGroup>(countDownRoot.gameObject);
        countDownGroup.alpha = 0f;
        countDownGroup.interactable = false;
        countDownGroup.blocksRaycasts = false;
        var bg = TCUI.Img("Dim", countDownRoot, new Color(TCUI.Ink.r, TCUI.Ink.g, TCUI.Ink.b, 0.38f));
        TCUI.Stretch(bg.rectTransform); bg.raycastTarget = false;
        // polish3: crimson medallion behind a big cream number (cream ring, deep-brown drop)
        var medal = TCUI.Img("Medal", countDownRoot, TCUI.Crimson, TCUI.Circle);
        TCUI.Place(medal.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 330));
        { var o = TCUI.Add<Outline>(medal.gameObject); o.effectColor = TCUI.Cream; o.effectDistance = new Vector2(9, -9); var sh = medal.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(TCUI.Ink.r, TCUI.Ink.g, TCUI.Ink.b, 0.55f); sh.effectDistance = new Vector2(0, -16); }
        var mb = TCUI.Img("Gloss", medal.transform, new Color(TCUI.CreamLight.r, TCUI.CreamLight.g, TCUI.CreamLight.b, 0.16f), TCUI.Circle); mb.preserveAspect = false; mb.raycastTarget = false; TCUI.Place(mb.rectTransform, new Vector2(0.5f, 0.8f), Vector2.zero, new Vector2(220, 84));
        countDownText = TCUI.Label("N", countDownRoot, "3", 220, TCUI.Cream);
        TCUI.Place(countDownText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 400));
        TCTheme.StyleText(countDownText, TCUI.Cream, 0.3f, true);
        countDownRoot.gameObject.SetActive(false);
    }

    void BuildWebHud(Transform top)
    {
        var row = TCUI.Rect("WebLetters", top);
        webLettersRow = row;
        TCUI.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0, -92), new Vector2(280, 64));
        webLetterText = new TextMeshProUGUI[3];
        webLetterBg = new Image[3];
        char[] letters = { 'W', 'E', 'B' };
        for (int i = 0; i < 3; i++)
        {
            var bg = TCUI.Img("Letter" + letters[i], row, WebOff);
            TCUI.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 80f, 0), new Vector2(68, 58));
            if (TCUI.Fresh(bg)) { var o = bg.gameObject.AddComponent<Outline>(); o.effectColor = new Color(TCUI.Brown.r, TCUI.Brown.g, TCUI.Brown.b, 0.6f); o.effectDistance = new Vector2(3, -3); var sh = bg.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(TCUI.Ink.r, TCUI.Ink.g, TCUI.Ink.b, 0.35f); sh.effectDistance = new Vector2(0, -5); var bv = TCUI.Img("Bevel", bg.transform, new Color(1, 1, 1, 0.8f), TCTheme.Spr("ui_bevel")); TCUI.Stretch(bv.rectTransform); bv.type = Image.Type.Sliced; }
            var lab = TCUI.Label("T", bg.transform, letters[i].ToString(), 40, WebOffText);
            TCUI.Stretch(lab.rectTransform);
            webLetterBg[i] = bg;
            webLetterText[i] = lab;
        }
        lastWebHud = "";
    }

    void RefreshWebHud()
    {
        var w = TCWebBonus.Instance;
        // Hide and remove the WEB letters panel once the web boost has been used
        if (w != null && w.SpentThisRun)
        {
            if (webLettersRow != null && webLettersRow.gameObject.activeSelf)
                webLettersRow.gameObject.SetActive(false);
            return;
        }

        if (webLetterText == null) return;
        bool hasW = w && w.HasW, hasE = w && w.HasE, hasB = w && w.HasB;
        string key = (hasW ? "W" : ".") + (hasE ? "E" : ".") + (hasB ? "B" : ".");
        if (key == lastWebHud) return;
        bool first = lastWebHud == "";
        lastWebHud = key;
        ApplyWebLetter(0, hasW, !first);
        ApplyWebLetter(1, hasE, !first);
        ApplyWebLetter(2, hasB, !first);
    }

    static Color WebOff => new Color(TCUI.Cream.r, TCUI.Cream.g, TCUI.Cream.b, 0.72f);
    static Color WebOffText => new Color(TCUI.Brown.r, TCUI.Brown.g, TCUI.Brown.b, 0.6f);
    void ApplyWebLetter(int i, bool on, bool punch)
    {
        if (webLetterBg == null || i >= webLetterBg.Length || !webLetterBg[i]) return;
        if (on)
        {
            webLetterBg[i].color = TCUI.Crimson;
            foreach (var o in webLetterBg[i].GetComponents<Outline>()) o.effectColor = TCUI.Cream;
            if (webLetterText[i]) TCTheme.StyleText(webLetterText[i], TCUI.Cream, 0.26f, true);
            if (punch) StartCoroutine(TCUI.Punch(webLetterBg[i].transform, 0.28f, 0.25f));
        }
        else
        {
            webLetterBg[i].color = WebOff;
            foreach (var o in webLetterBg[i].GetComponents<Outline>()) o.effectColor = new Color(TCUI.Brown.r, TCUI.Brown.g, TCUI.Brown.b, 0.6f);
            if (webLetterText[i]) webLetterText[i].color = WebOffText;
        }
    }

    IEnumerator ConfettiCelebration(Transform parent)
    {
        // Burst vibrant celebratory confetti pieces across the finish card
        Color[] confettiColors = new Color[]
        {
            // polish3 palette confetti
            TCUI.Crimson, TCUI.Coral, TCUI.Tan, TCUI.Cream, TCUI.Brown, TCUI.TanLight, TCUI.CreamLight
        };

        var container = new GameObject("ConfettiContainer").AddComponent<RectTransform>();
        container.SetParent(parent, false);
        container.anchorMin = Vector2.zero; container.anchorMax = Vector2.one;
        container.offsetMin = container.offsetMax = Vector2.zero;

        int count = 55;
        List<Image> pieces = new List<Image>();
        List<Vector2> velocities = new List<Vector2>();
        List<float> rotSpeeds = new List<float>();

        for (int i = 0; i < count; i++)
        {
            var pImg = TCUI.Img("C" + i, container, confettiColors[Random.Range(0, confettiColors.Length)], TCUI.Round);
            var prt = pImg.rectTransform;
            prt.sizeDelta = new Vector2(Random.Range(18f, 32f), Random.Range(10f, 18f));
            prt.anchoredPosition = new Vector2(Random.Range(-280f, 280f), Random.Range(180f, 440f));
            prt.localEulerAngles = new Vector3(0, 0, Random.Range(0f, 360f));
            pImg.raycastTarget = false;

            float angle = Random.Range(30f, 150f) * Mathf.Deg2Rad;
            float speed = Random.Range(350f, 850f);
            velocities.Add(new Vector2(Mathf.Cos(angle) * speed * (Random.value < 0.5f ? -1f : 1f), Mathf.Sin(angle) * speed));
            rotSpeeds.Add(Random.Range(-450f, 450f));
            pieces.Add(pImg);
        }

        float duration = 2.4f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float dt = Time.unscaledDeltaTime;
            float fade = elapsed > 1.6f ? (duration - elapsed) / 0.8f : 1f;

            for (int i = 0; i < pieces.Count; i++)
            {
                if (!pieces[i]) continue;
                var prt = pieces[i].rectTransform;
                // Update velocity with gravity and aerodynamic sway
                Vector2 vel = velocities[i];
                vel.y -= 750f * dt; // gravity
                vel.x *= Mathf.Pow(0.92f, dt * 60f); // air drag
                velocities[i] = vel;

                prt.anchoredPosition += vel * dt;
                prt.Rotate(0, 0, rotSpeeds[i] * dt);

                // Gentle fluttering stretch
                float flutter = Mathf.Sin((elapsed + i) * 8f);
                prt.localScale = new Vector3(Mathf.Abs(flutter), 1f, 1f);

                Color c = pieces[i].color;
                c.a = fade;
                pieces[i].color = c;
            }
            yield return null;
        }

        if (container) Destroy(container.gameObject);
    }
}
