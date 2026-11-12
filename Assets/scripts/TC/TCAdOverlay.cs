using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Visible placeholder ads for the Editor (and any time the Mobile Ads SDK
/// cannot show a real ad). Lets the rewarded / interstitial / banner flow be
/// play-tested without a device. On device, official Google test unit IDs are
/// used by AdsManager.
public class TCAdOverlay : MonoBehaviour
{
    public static TCAdOverlay Instance { get; private set; }

    Canvas canvas;
    CanvasGroup interstitialGroup, rewardedGroup, bannerGroup;
    TextMeshProUGUI interstitialTitle, rewardedTitle, bannerLabel, countdownText;
    Action pendingReward;
    bool rewardedOpen, interstitialOpen;
    Coroutine lifeCo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance) return;
        var g = new GameObject("TCAdOverlay");
        DontDestroyOnLoad(g);
        g.AddComponent<TCAdOverlay>();
    }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240; // above pause / results / HUD
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();

        interstitialGroup = MakeScreen("Interstitial", "TEST INTERSTITIAL", TCUI.Sky,
            "This is a Google AdMob test interstitial placeholder.\nIt stands in when a real test ad cannot play (Editor, or the SDK is not ready).",
            "CLOSE", HideInterstitial, out interstitialTitle);
        rewardedGroup = MakeScreen("Rewarded", "TEST REWARDED AD", TCUI.Butter,
            "Watch this placeholder to earn the reward.\nOn a device, the official AdMob test rewarded unit is used instead.",
            "CLAIM REWARD", ClaimRewarded, out rewardedTitle);
        countdownText = TCUI.Label("Count", rewardedGroup.transform.Find("Card"), "3", 72, TCUI.Cocoa);
        TCUI.Place(countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(200, 90));

        // Bottom banner placeholder (editor / fallback only)
        var ban = TCUI.Rect("Banner", transform);
        TCUI.Stretch(ban, 0, 0, 0, 0);
        ban.anchorMin = new Vector2(0, 0);
        ban.anchorMax = new Vector2(1, 0);
        ban.pivot = new Vector2(0.5f, 0);
        ban.sizeDelta = new Vector2(0, 150);
        var bg = ban.gameObject.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.14f, 0.94f);
        bg.raycastTarget = false;
        bannerLabel = TCUI.Label("L", ban, "TEST BANNER  ·  AdMob  ca-app-pub-3940256099942544", 32, Color.white);
        TCUI.Stretch(bannerLabel.rectTransform, 20, 20, 0, 0);
        bannerGroup = ban.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.alpha = 0;
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;
        ban.gameObject.SetActive(false);
    }

    CanvasGroup MakeScreen(string name, string title, Color accent, string body, string cta, Action onCta, out TextMeshProUGUI titleLabel)
    {
        var root = TCUI.Rect(name, transform);
        TCUI.Stretch(root);
        var cg = root.gameObject.AddComponent<CanvasGroup>();
        var dim = TCUI.Img("Dim", root, new Color(0.08f, 0.06f, 0.07f, 0.82f));
        dim.sprite = null; dim.type = Image.Type.Simple; dim.raycastTarget = true;
        TCUI.Stretch(dim.rectTransform);
        var card = TCUI.Img("Card", root, TCUI.Cream).rectTransform;
        TCUI.Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 980));
        var head = TCUI.Img("Head", card, accent);
        TCUI.Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(700, 120));
        titleLabel = TCUI.Label("Title", head.transform, title, 52, Color.white);
        TCUI.Stretch(titleLabel.rectTransform, 16, 16, 0, 0);
        TCUI.ApplyReadable(head.transform, accent);
        var bodyT = TCUI.Label("Body", card, body, 36, TCUI.Cocoa);
        TCUI.Place(bodyT.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(740, 280));
        var badge = TCUI.Label("Badge", card, "TEST AD", 28, TCUI.Peach);
        TCUI.Place(badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(400, 50));
        var btn = TCUI.Btn("Cta", card, cta, TCUI.Mint, onCta, 48);
        TCUI.Place((RectTransform)btn.transform, new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(560, 130));
        cg.alpha = 0; cg.interactable = false; cg.blocksRaycasts = false;
        root.gameObject.SetActive(false);
        return cg;
    }

    public static void ShowRewarded(Action onReward, string placement)
    {
        Ensure().ShowRewardedInternal(onReward, placement);
    }

    public static void ShowInterstitial(Action onClosed = null)
    {
        Ensure().ShowInterstitialInternal(onClosed);
    }

    public static void ShowBanner(bool show)
    {
        var i = Ensure();
        if (!i) return;
        // Only draw the placeholder bar in the editor (or when no mobile SDK is active).
        bool placeholder = Application.isEditor || !Application.isMobilePlatform;
        i.bannerGroup.gameObject.SetActive(placeholder && show);
        i.bannerGroup.alpha = placeholder && show ? 1f : 0f;
        TCSafeArea.BannerVisible = show && !TCSave.NoAds;
    }

    public static bool BannerPlaceholderVisible =>
        Instance && Instance.bannerGroup && Instance.bannerGroup.gameObject.activeSelf && Instance.bannerGroup.alpha > 0.01f;

    static TCAdOverlay Ensure()
    {
        if (Instance) return Instance;
        Boot();
        return Instance;
    }

    void ShowRewardedInternal(Action onReward, string placement)
    {
        pendingReward = onReward;
        rewardedOpen = true;
        if (rewardedTitle) rewardedTitle.text = "TEST REWARDED AD";
        var sub = rewardedGroup.transform.Find("Card/Body")?.GetComponent<TextMeshProUGUI>();
        if (sub) sub.text = "Placement: " + placement + "\nThis placeholder stands in for a Google AdMob test rewarded ad.";
        Open(rewardedGroup);
        if (lifeCo != null) StopCoroutine(lifeCo);
        lifeCo = StartCoroutine(RewardedCountdown());
    }

    IEnumerator RewardedCountdown()
    {
        for (int i = 3; i >= 1; i--)
        {
            if (countdownText) countdownText.text = i.ToString();
            yield return new WaitForSecondsRealtime(0.7f);
        }
        if (countdownText) countdownText.text = "READY";
    }

    void ClaimRewarded()
    {
        if (!rewardedOpen) return;
        rewardedOpen = false;
        var cb = pendingReward; pendingReward = null;
        Close(rewardedGroup);
        cb?.Invoke();
    }

    void ShowInterstitialInternal(Action onClosed)
    {
        interstitialOpen = true;
        Open(interstitialGroup);
        if (lifeCo != null) StopCoroutine(lifeCo);
        lifeCo = StartCoroutine(AutoCloseInterstitial(onClosed));
    }

    IEnumerator AutoCloseInterstitial(Action onClosed)
    {
        yield return new WaitForSecondsRealtime(2.2f);
        HideInterstitial();
        onClosed?.Invoke();
    }

    void HideInterstitial()
    {
        if (!interstitialOpen) return;
        interstitialOpen = false;
        Close(interstitialGroup);
    }

    void Open(CanvasGroup cg)
    {
        if (!cg) return;
        cg.gameObject.SetActive(true);
        cg.alpha = 1; cg.interactable = true; cg.blocksRaycasts = true;
        cg.transform.SetAsLastSibling();
    }

    void Close(CanvasGroup cg)
    {
        if (!cg) return;
        cg.alpha = 0; cg.interactable = false; cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }
}
