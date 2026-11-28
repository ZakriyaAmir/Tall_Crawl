using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Google Play Games UI on the main menu (ported from Wordwise Solitaire StoreUI.Leaderboard):
///  - Leaderboard button beside the BEST pill, opening a Top 20 worldwide panel that ranks ONLY best distance (m).
///    Layout matches Wordwise: my-rank card (avatar, name, worldwide rank, my best, sign-in button), RANK / PLAYER /
///    DISTANCE header, scrolling Top 20 rows tinted gold / silver / bronze / me, Refresh + Back.
///  - "Sign in with Google Play" button in Settings (and inside the leaderboard card when signed out).
/// Built with the same TCUI builders as every other Tall Crawl panel, so it binds to baked scene objects by name.
/// </summary>
public partial class TCMenuUI
{
    CanvasGroup leaderboardPanel;
    RectTransform lbContent;
    TextMeshProUGUI lbMyName, lbMyRank, lbMyScore, lbStatus, lbAvatarLetter, gpgsSettingsText;
    Button lbSignInBtn, gpgsSettingsBtn;
    readonly List<GameObject> lbRows = new List<GameObject>();

    static readonly Color RowGold = new Color32(255, 232, 145, 255), RowSilver = new Color32(236, 240, 246, 255),
        RowBronze = new Color32(246, 214, 184, 255), RowMe = new Color32(255, 240, 185, 255), RowDefault = new Color32(255, 252, 244, 255);

    void BuildLeaderboardButton()
    {
        var b = TCUI.Btn("Leaderboard", safe, "", TCUI.Butter, OpenLeaderboard, 40, TCUI.Icon("star"));
        TCUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0.28f), new Vector2(310, 0), new Vector2(110, 110));
    }

    void BuildGpgsSettingsButton(RectTransform card)
    {
        gpgsSettingsBtn = TCUI.Btn("GooglePlay", card, "SIGN IN WITH GOOGLE PLAY", TCUI.Mint, OnGpgsSettingsTapped, 34, TCUI.Icon("play"));
        TCUI.Place((RectTransform)gpgsSettingsBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -278), new Vector2(620, 84));
        gpgsSettingsText = gpgsSettingsBtn.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
    }

    void OnGpgsSettingsTapped()
    {
        if (TCPlayGames.IsSignedIn) { Toast("Signed in as " + TCPlayGames.DisplayName); return; }
        SignInWithGooglePlay(null);
    }

    void SignInWithGooglePlay(System.Action after)
    {
        Toast("Connecting to Google Play...");
        TCPlayGames.SignInInteractive((ok, info) =>
        {
            RefreshGpgsUI();
            Toast(ok ? "Signed in as " + info : info);
            if (ok) after?.Invoke();
        });
    }

    void RefreshGpgsUI()
    {
        if (gpgsSettingsText) gpgsSettingsText.text = TCPlayGames.IsSignedIn ? "GOOGLE PLAY: " + TCPlayGames.DisplayName.ToUpperInvariant() : "SIGN IN WITH GOOGLE PLAY";
        if (lbSignInBtn) lbSignInBtn.gameObject.SetActive(!TCPlayGames.IsSignedIn);
        if (lbMyName) lbMyName.text = TCPlayGames.IsSignedIn ? TCPlayGames.DisplayName : "You";
        if (lbAvatarLetter) lbAvatarLetter.text = (lbMyName && lbMyName.text.Length > 0 ? lbMyName.text.Substring(0, 1) : "?").ToUpperInvariant();
    }

    void BuildLeaderboard()
    {
        leaderboardPanel = TCUI.Panel("Leaderboard", safe, out var card, new Vector2(980, 1640), "LEADERBOARD");

        // My rank card (avatar, name, worldwide rank, my best distance, sign-in)
        var my = TCUI.Img("MyRankCard", card, TCUI.CreamLight);
        TCUI.Place(my.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -215), new Vector2(900, 230));
        var av = TCUI.Img("Avatar", my.transform, TCUI.Tan, TCUI.Circle);
        TCUI.Place(av.rectTransform, new Vector2(0, 1), new Vector2(80, -70), new Vector2(104, 104));
        lbAvatarLetter = TCUI.Label("Letter", av.transform, "?", 56, TCUI.Cream); TCUI.Stretch(lbAvatarLetter.rectTransform);
        lbMyName = TCUI.Label("MyName", my.transform, "You", 42, TCUI.Cocoa, TextAlignmentOptions.MidlineLeft);
        TCUI.Place(lbMyName.rectTransform, new Vector2(0, 1), new Vector2(150 + 230, -45), new Vector2(460, 60));
        lbMyName.enableWordWrapping = false; lbMyName.overflowMode = TextOverflowModes.Ellipsis;
        lbMyRank = TCUI.Label("MyRank", my.transform, "Worldwide Rank: --", 32, TCUI.Brown, TextAlignmentOptions.MidlineLeft);
        TCUI.Place(lbMyRank.rectTransform, new Vector2(0, 1), new Vector2(150 + 230, -100), new Vector2(460, 50));
        lbMyRank.enableAutoSizing = true; lbMyRank.fontSizeMin = 22; lbMyRank.fontSizeMax = 34;
        lbMyScore = TCUI.Label("MyScore", my.transform, "0 m", 46, TCUI.Crimson, TextAlignmentOptions.MidlineRight);
        TCUI.Place(lbMyScore.rectTransform, new Vector2(1, 1), new Vector2(-150, -72), new Vector2(260, 90));
        lbMyScore.enableAutoSizing = true; lbMyScore.fontSizeMin = 26; lbMyScore.fontSizeMax = 46;
        lbSignInBtn = TCUI.Btn("GpgsSignInBtn", my.transform, "SIGN IN WITH GOOGLE PLAY", TCUI.Mint, () => SignInWithGooglePlay(RefreshLeaderboard), 30, TCUI.Icon("play"));
        TCUI.Place((RectTransform)lbSignInBtn.transform, new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(760, 76));

        // Column header
        var hdr = TCUI.Rect("ListHeader", card); TCUI.Place(hdr, new Vector2(0.5f, 1), new Vector2(0, -372), new Vector2(880, 52));
        var hr = TCUI.Label("HRank", hdr, "RANK", 28, TCUI.Brown, TextAlignmentOptions.MidlineLeft); TCUI.Place(hr.rectTransform, new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(160, 46));
        var hp = TCUI.Label("HPlayer", hdr, "PLAYER", 28, TCUI.Brown, TextAlignmentOptions.MidlineLeft); TCUI.Place(hp.rectTransform, new Vector2(0, 0.5f), new Vector2(380, 0), new Vector2(380, 46));
        var hs = TCUI.Label("HScore", hdr, "DISTANCE", 28, TCUI.Brown, TextAlignmentOptions.MidlineRight); TCUI.Place(hs.rectTransform, new Vector2(1, 0.5f), new Vector2(-130, 0), new Vector2(240, 46));

        // Scrolling Top 20 list
        var scroll = TCUI.Img("Scroll", card, new Color(TCUI.CreamLight.r, TCUI.CreamLight.g, TCUI.CreamLight.b, 0.92f));
        var srt = scroll.rectTransform;
        if (TCUI.Fresh(scroll)) { srt.anchorMin = new Vector2(0.5f, 0); srt.anchorMax = new Vector2(0.5f, 1); srt.pivot = new Vector2(0.5f, 0.5f); srt.offsetMin = new Vector2(-450, 200); srt.offsetMax = new Vector2(450, -410); }
        scroll.raycastTarget = true;
        TCUI.Add<RectMask2D>(scroll.gameObject);
        var sr = TCUI.Add<ScrollRect>(scroll.gameObject);
        lbContent = TCUI.Rect("Content", srt);
        if (TCUI.Fresh(lbContent)) { lbContent.anchorMin = new Vector2(0, 1); lbContent.anchorMax = new Vector2(1, 1); lbContent.pivot = new Vector2(0.5f, 1); lbContent.anchoredPosition = Vector2.zero; lbContent.sizeDelta = Vector2.zero; }
        var vlg = TCUI.Add<VerticalLayoutGroup>(lbContent.gameObject);
        vlg.spacing = 12; vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.childControlWidth = true; vlg.childControlHeight = false; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        TCUI.Add<ContentSizeFitter>(lbContent.gameObject).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = srt; sr.content = lbContent; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;
        lbStatus = TCUI.Label("StatusText", srt, "Loading Top 20...", 36, TCUI.Brown); TCUI.Stretch(lbStatus.rectTransform, 40, 40, 40, 40);

        // Bottom buttons
        TCUI.Place((RectTransform)TCUI.Btn("Refresh", card, "REFRESH", TCUI.Sky, RefreshLeaderboard, 44).transform, new Vector2(0.5f, 0), new Vector2(-200, 100), new Vector2(340, 120));
        TCUI.Place((RectTransform)TCUI.Btn("Close", card, "BACK", TCUI.Grey, () => TCUI.Hide(this, leaderboardPanel), 44).transform, new Vector2(0.5f, 0), new Vector2(200, 100), new Vector2(340, 120));
    }

    public void OpenLeaderboard()
    {
        if (!leaderboardPanel) return;
        TCServices.Log("menu_leaderboard", "signed_in", TCPlayGames.IsSignedIn ? 1 : 0);
        TCUI.Show(this, leaderboardPanel, pop: true, animateContents: false);
        RefreshLeaderboard();
    }

    public void RefreshLeaderboard()
    {
        RefreshGpgsUI();
        if (lbMyScore) lbMyScore.text = TCLeaderboard.FormatMetres(TCSave.Best);
        if (lbMyRank) lbMyRank.text = TCPlayGames.IsSignedIn ? "Worldwide Rank: loading..." : "Worldwide Rank: sign in required";
        SetLbStatus(TCPlayGames.IsSignedIn ? "Fetching Top 20 worldwide..." : "Sign in with Google Play to see the Top 20 climbers.");
        ClearLbRows();
        if (!TCPlayGames.IsSignedIn) return;

        TCLeaderboard.FetchTop20AndMyRank(res =>
        {
            if (!this || !leaderboardPanel) return;
            RefreshGpgsUI();
            if (lbMyScore) lbMyScore.text = TCLeaderboard.FormatMetres(res.localPlayerMetres);
            if (lbMyRank)
                lbMyRank.text = !res.isAuthenticated ? "Worldwide Rank: sign in required"
                    : res.localPlayerRank > 0 ? "Worldwide Rank: #" + res.localPlayerRank : "Worldwide Rank: unranked";
            ClearLbRows();
            if (!res.success || res.entries == null || res.entries.Count == 0)
            {
                SetLbStatus(string.IsNullOrEmpty(res.statusMessage) ? "No distances yet. Climb to claim #1!" : res.statusMessage);
                return;
            }
            SetLbStatus(null);
            foreach (var e in res.entries) SpawnLbRow(e);
        });
    }

    void SetLbStatus(string msg)
    {
        if (!lbStatus) return;
        lbStatus.gameObject.SetActive(!string.IsNullOrEmpty(msg));
        lbStatus.text = msg ?? "";
    }

    void ClearLbRows()
    {
        foreach (var r in lbRows) if (r) Destroy(r);
        lbRows.Clear();
    }

    void SpawnLbRow(TCLeaderboard.Entry e)
    {
        if (!lbContent) return;
        var go = new GameObject("Row_" + e.rank, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(lbContent, false);
        var img = go.GetComponent<Image>(); img.sprite = TCUI.Round; img.type = Image.Type.Sliced; img.raycastTarget = false;
        img.color = e.isLocalPlayer ? RowMe : e.rank == 1 ? RowGold : e.rank == 2 ? RowSilver : e.rank == 3 ? RowBronze : RowDefault;
        var le = go.GetComponent<LayoutElement>(); le.preferredHeight = 96; le.minHeight = 96;
        ((RectTransform)go.transform).sizeDelta = new Vector2(0, 96);
        var ol = go.AddComponent<UnityEngine.UI.Outline>(); ol.effectColor = new Color(TCUI.Tan.r, TCUI.Tan.g, TCUI.Tan.b, 0.9f); ol.effectDistance = new Vector2(3, -3);

        var rank = RowLabel("Rank", go.transform, "#" + e.rank, 40, e.rank <= 3 ? TCUI.Crimson : TCUI.Cocoa, TextAlignmentOptions.MidlineLeft);
        Anchor(rank.rectTransform, 0, 0, 24, 150);
        var name = RowLabel("Name", go.transform, e.playerName, 36, TCUI.Cocoa, TextAlignmentOptions.MidlineLeft);
        Anchor(name.rectTransform, 0, 1, 170, -260);
        name.fontStyle = e.isLocalPlayer ? FontStyles.Bold | FontStyles.Underline : FontStyles.Bold;
        name.overflowMode = TextOverflowModes.Ellipsis; name.enableWordWrapping = false;
        var score = RowLabel("Score", go.transform, TCLeaderboard.FormatMetres(e.metres), 38, TCUI.Ink, TextAlignmentOptions.MidlineRight);
        Anchor(score.rectTransform, 1, 1, -250, -24);
        lbRows.Add(go);
    }

    static TextMeshProUGUI RowLabel(string n, Transform parent, string text, float size, Color c, TextAlignmentOptions align)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (TCAssets.I.font) t.font = TCAssets.I.font;
        t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.raycastTarget = false; t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.55f;
        return t;
    }

    /// Horizontal anchors xMin/xMax (0..1) with left/right pixel offsets; full row height.
    static void Anchor(RectTransform rt, float xMin, float xMax, float left, float right)
    {
        rt.anchorMin = new Vector2(xMin, 0); rt.anchorMax = new Vector2(xMax, 1);
        rt.offsetMin = new Vector2(left, 6); rt.offsetMax = new Vector2(right, -6);
    }
}
