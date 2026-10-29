using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Main menu: currencies, best score, 3D spider preview, Play, Skins, Shop, Daily reward, Settings, consent.
public class TCMenuUI : MonoBehaviour
{
    RectTransform safe; Canvas canvas;
    TextMeshProUGUI coinsText, gemsText, bestText; Image dailyBadge;
    CanvasGroup skinsPanel, shopPanel, settingsPanel, dailyPanel, consentPanel, toastGroup; TextMeshProUGUI toastText;
    GameObject previewSpider; RenderTexture rt; Camera previewCam; RawImage previewImg, skinPreviewImg; int previewSkin = -1;
    readonly List<System.Action> refreshers = new List<System.Action>();

    void Start()
    {
        Application.targetFrameRate = 60; Time.timeScale = 1; TCSave.Apply();
        canvas = TCUI.MakeCanvas("TC_MenuUI", 10);
        BuildBackground();
        safe = TCUI.SafeRoot(canvas.transform);
        BuildPreview();
        BuildMain(); BuildSkins(); BuildShop(); BuildSettings(); BuildDaily(); BuildConsent(); BuildToast();
        TCSave.Changed += Refresh; Refresh();
        StartCoroutine(Entrance());
        { float d = 0.9f; foreach (Transform c in safe) { if (c.GetComponent<CanvasGroup>() || c.name == "Preview" || c.name.StartsWith("Shadow")) continue; StartCoroutine(PopIn(c, d)); d += 0.06f; } }
        ApplyMenuMusic();
        TCSkins.CheckAchievements();
        if (PlayerPrefs.GetInt("consent", 0) == 0) PlayerPrefs.SetInt("consent", 1);
        TCServices.Banner(true);
    }
    void OnDestroy() { TCSave.Changed -= Refresh; if (rt) rt.Release(); }
    // The saved Music setting was only applied when toggled, so menu music always played on launch.
    static void ApplyMenuMusic() { var m = GameObject.Find("bgm"); if (m) { var a = m.GetComponent<AudioSource>(); if (a) a.mute = !TCSave.Music; } }

    // Android back button: close the top-most open panel (it did nothing before).
    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        foreach (var cg in new[] { dailyPanel, skinsPanel, shopPanel, settingsPanel })
            if (cg && cg.gameObject.activeSelf && cg.alpha > 0.5f) { TCUI.Hide(this, cg); return; }
    }
    IEnumerator Delay(float t, System.Action a) { yield return new WaitForSecondsRealtime(t); a(); }

    void Refresh()
    {
        coinsText.text = TCSave.Coins.ToString("N0"); gemsText.text = TCSave.Gems.ToString();
        bestText.text = "BEST  " + TCSave.Best + "M";
        dailyBadge.gameObject.SetActive(TCSave.DailyAvailable(out _));
        foreach (var r in refreshers) r();
        ApplyMenuMusic(); // keep bgm in sync with the saved setting whatever changed it
    }

    // ---------- Background (pastel gradient + floating leaves) ----------
    void BuildBackground()
    {
        var bg = TCUI.Rect("BG", canvas.transform); TCUI.Stretch(bg);
        var tex = new Texture2D(2, 64) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) { var c = Color.Lerp(TCUI.Hex("#FFD3B8"), TCUI.Hex("#7FD0FF"), y / 63f); tex.SetPixel(0, y, c); tex.SetPixel(1, y, c); }
        tex.Apply();
        var ri = bg.gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false;
        for (int i = 0; i < 14; i++)
        {
            var dot = TCUI.Img("Bubble", bg, new Color(1, 1, 1, Random.Range(0.18f, 0.4f)), TCUI.Circle);
            float s = Random.Range(40, 160); dot.rectTransform.sizeDelta = new Vector2(s, s);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(Random.value, Random.value);
            StartCoroutine(Float(dot.rectTransform, Random.Range(0.2f, 0.6f)));
        }
    }
    IEnumerator Float(RectTransform r, float speed)
    {
        float phase = Random.value * 10;
        while (r)
        {
            var a = r.anchorMin; a.y += speed * 0.02f * Time.unscaledDeltaTime; if (a.y > 1.1f) { a.y = -0.1f; a.x = Random.value; }
            r.anchorMin = r.anchorMax = a; r.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * speed + phase) * 30, 0);
            yield return null;
        }
    }

    // ---------- 3D spider preview (render texture) ----------
    void BuildPreview()
    {
        var a = TCAssets.I;
        rt = new RenderTexture(768, 768, 16) { antiAliasing = 2 };
        var root = new GameObject("SkinPreviewStage"); root.transform.position = new Vector3(0, -500, 0);
        var camGo = new GameObject("PreviewCam"); camGo.transform.SetParent(root.transform, false);
        previewCam = camGo.AddComponent<Camera>(); previewCam.clearFlags = CameraClearFlags.SolidColor; previewCam.backgroundColor = new Color(0, 0, 0, 0);
        previewCam.targetTexture = rt; previewCam.fieldOfView = 30; previewCam.cullingMask = 1 << 0; previewCam.nearClipPlane = 0.01f;
        var light = new GameObject("PreviewLight").AddComponent<Light>(); light.type = LightType.Directional; light.transform.SetParent(root.transform); light.transform.rotation = Quaternion.Euler(35, -25, 0); light.intensity = 1.25f; // key light from the camera side (face lit) light.shadows = LightShadows.None;
        if (a.spiderModel)
        {
            previewSpider = Instantiate(a.spiderModel, root.transform);
            previewSpider.transform.localPosition = Vector3.zero;
            var anim = previewSpider.GetComponentInChildren<Animator>(); if (!anim) anim = previewSpider.AddComponent<Animator>();
            if (a.spiderAnimator) { anim.runtimeAnimatorController = a.spiderAnimator; anim.SetBool("started", false); }
            var smr = previewSpider.GetComponentInChildren<SkinnedMeshRenderer>();
            var b = smr ? smr.bounds : new Bounds(previewSpider.transform.position, Vector3.one);
            float size = b.size.magnitude;
            var aim = b.center + Vector3.up * size * 0.22f; // new chibi spider sits taller: frame its full body
            previewCam.transform.position = aim + new Vector3(0, size * 0.6f, -size * 1.7f);
            previewCam.transform.LookAt(aim); previewSize = size;
        }
        ApplyPreview(TCSave.SelectedSkin);
    }
    void ApplyPreview(int i) { if (previewSpider && previewSkin != i) { previewSkin = i; TCSkins.Apply(previewSpider, i); } }
    float swayBlend = 0f;
    void LateUpdate()
    {
        if (previewSpider && !entering)
        {
            swayBlend = Mathf.MoveTowards(swayBlend, 1f, Time.unscaledDeltaTime * 1.25f);
            previewSpider.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.unscaledTime * 1.3f) * 28f * swayBlend, 0);
        }
    } // gentle sway, face stays visible

    // ---------- Main menu entrance: drop on a silk thread, somersault, squash landing, dust, wave ----------
    bool entering; LineRenderer silk; float previewSize = 1;
    IEnumerator Entrance()
    {
        if (!previewSpider) yield break;
        entering = true;
        swayBlend = 0f;
        var cs = previewSpider.GetComponent<TCCuteSpider>();
        float S = previewSize;
        // Animate a pivot above the spider (its own Animator owns the spider transform).
        var pivot = new GameObject("DropPivot").transform; pivot.SetParent(previewSpider.transform.parent, false);
        float h = S * 0.22f; pivot.localPosition = new Vector3(0, h, 0);
        previewSpider.transform.SetParent(pivot, true);
        var st = pivot; Vector3 rest = new Vector3(0, h, 0), top = new Vector3(0, h + S * 1.6f, 0);
        silk = new GameObject("Silk").AddComponent<LineRenderer>(); silk.transform.SetParent(pivot.parent, false); silk.useWorldSpace = false;
        var silkMat = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default"));
        var silkTex = Resources.Load<Texture2D>("SilkWeb");
        if (silkTex) silkMat.mainTexture = silkTex;
        silk.material = silkMat;
        silk.textureMode = LineTextureMode.Tile;
        silk.startColor = silk.endColor = new Color(1, 1, 1, 0.95f);
        silk.widthMultiplier = S * 0.022f; silk.positionCount = 2; silk.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        st.localPosition = top; st.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(0.25f);
        TCSfx.Play("menu_drop", 0.6f);
        bool flipSfx = false;
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.95f)
        {
            float e = 1 - Mathf.Pow(1 - x, 3);                                 // ease-out drop
            st.localPosition = Vector3.Lerp(top, rest, e);
            float flip = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.25f, 0.8f, x));
            if (!flipSfx && x > 0.25f) { flipSfx = true; TCSfx.Play("menu_flip", 0.6f); }
            st.localRotation = Quaternion.Euler(-360f * flip, 0, 0) * Quaternion.Euler(0, 0, Mathf.Sin(x * 9) * 8 * (1 - x)); // somersault on the thread
            float str = 1 + 0.18f * Mathf.Sin(x * Mathf.PI);                    // stretch while falling
            st.localScale = new Vector3(1 / Mathf.Sqrt(str), str, 1 / Mathf.Sqrt(str));
            Silk(st, S); yield return null;
        }
        st.localPosition = rest; st.localRotation = Quaternion.identity;
        TCSfx.Play("land", 0.7f); Dust(pivot.parent, S);
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.45f)       // squash + wobble settle
        {
            float k = Mathf.Sin(x * Mathf.PI * 3) * (1 - x) * 0.3f;
            st.localScale = new Vector3(1 + k, 1 - k, 1 + k); Silk(st, S);
            silk.startColor = silk.endColor = new Color(1, 1, 1, 0.95f * (1 - x)); yield return null;
        }
        st.localScale = Vector3.one; Destroy(silk.gameObject);
        TCSfx.Play("wave", 0.6f);
        cs = previewSpider.GetComponent<TCCuteSpider>();
        for (float x = 0; x < 1.4f; x += Time.unscaledDeltaTime) { if (cs) cs.wave = Mathf.Clamp01(Mathf.Min(x * 5, (1.4f - x) * 5)); yield return null; }
        if (cs) cs.wave = 0;
        entering = false;
    }
    void Silk(Transform st, float S) { if (!silk) return; silk.SetPosition(0, st.localPosition + new Vector3(0, S * 0.5f, 0)); silk.SetPosition(1, new Vector3(0, S * 4f, 0)); }
    void Dust(Transform parent, float S)
    {
        var m = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < 10; i++)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(g.GetComponent<Collider>()); g.transform.SetParent(parent, false);
            float a = i / 10f * Mathf.PI * 2; var dir = new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a) * 0.6f);
            g.GetComponent<Renderer>().sharedMaterial = m; g.GetComponent<Renderer>().material.color = i % 3 == 0 ? new Color(1, 0.95f, 0.6f, 0.9f) : new Color(1, 1, 1, 0.8f);
            StartCoroutine(Puff(g.transform, dir, S));
        }
    }
    IEnumerator Puff(Transform t, Vector3 dir, float S)
    {
        var r = t.GetComponent<Renderer>(); var c = r.material.color;
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.5f)
        {
            t.localPosition = dir * S * (0.25f + 0.45f * x); t.localScale = Vector3.one * S * 0.07f * (1 - x * 0.6f);
            r.material.color = new Color(c.r, c.g, c.b, c.a * (1 - x)); yield return null;
        }
        Destroy(t.gameObject);
    }
    // UI pop-in around the spider (staggered squash pop)
    IEnumerator PopIn(Transform t, float delay)
    {
        if (!t) yield break; var baseS = t.localScale; t.localScale = Vector3.zero;
        yield return new WaitForSecondsRealtime(delay);
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.35f)
        {
            if (!t) yield break;
            float k = 1 + Mathf.Sin(x * Mathf.PI) * 0.25f * (1 - x) ; float e = 1 - Mathf.Pow(1 - x, 3);
            t.localScale = new Vector3(baseS.x * e * k, baseS.y * e / k, baseS.z); yield return null;
        }
        if (t) t.localScale = baseS;
    }
    public void PreviewSkin(int i) => ApplyPreview(i); // used by the screenshot harness

    // ---------- Main ----------
    void BuildMain()
    {
        // Top bar
        coinsText = Currency("Coins", "coin", new Vector2(190, -90));
        gemsText = Currency("Gems", "gem", new Vector2(480, -90));
        var gear = TCUI.Btn("Settings", safe, "", TCUI.Butter, () => TCUI.Show(this, settingsPanel), 40, TCUI.Icon("gear"));
        TCUI.Place((RectTransform)gear.transform, new Vector2(1, 1), new Vector2(-100, -90), new Vector2(120, 120));

        if (TCAssets.I.logo)
        {
            var logo = TCUI.Img("Logo", safe, Color.white, TCAssets.I.logo); logo.preserveAspect = true; TCUI.Place(logo.rectTransform, new Vector2(0.5f, 0.83f), Vector2.zero, new Vector2(760, 428)); // cute & awesome 3D title logo
            StartCoroutine(Bob(logo.rectTransform, 10, 1.8f));
        }
        else { var t = TCUI.Label("Title", safe, "TALL CRAWL", 120, TCUI.Cocoa); TCUI.Place(t.rectTransform, new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(1000, 200)); }

        previewImg = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>(); previewImg.transform.SetParent(safe, false);
        previewImg.texture = rt; previewImg.raycastTarget = false; TCUI.Place(previewImg.rectTransform, new Vector2(0.5f, 0.50f), Vector2.zero, new Vector2(740, 740));

        var bestPill = TCUI.Img("Best", safe, new Color(1, 1, 1, 0.85f)); TCUI.Place(bestPill.rectTransform, new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(480, 84));
        bestText = TCUI.Label("Value", bestPill.transform, "", 44, TCUI.Cocoa); TCUI.Stretch(bestText.rectTransform);

        var play = TCUI.Btn("Play", safe, "PLAY", TCUI.Mint, () => StartCoroutine(LoadGame()), 96, TCUI.Icon("play")); // no game-start sound
        TCUI.Place((RectTransform)play.transform, new Vector2(0.5f, 0), new Vector2(0, 310), new Vector2(640, 190));
        { var glow = TCUI.Img("Glow", safe, new Color(1f, 0.95f, 0.55f, 0.55f), TCUI.Circle); glow.raycastTarget = false;
          TCUI.Place(glow.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 310), new Vector2(820, 290)); glow.transform.SetSiblingIndex(play.transform.GetSiblingIndex() - 1);
          StartCoroutine(Glow(glow)); }
        StartCoroutine(Pulse(play.transform)); StartCoroutine(Breathe(play.transform));

        SmallBtn("Skins", "SKINS", TCUI.Blush, "shirt", new Vector2(-330, 110), () => { RefreshSkinGrid(); TCUI.Show(this, skinsPanel); });
        SmallBtn("Shop", "SHOP", TCUI.Peach, "bag", new Vector2(0, 110), () => TCUI.Show(this, shopPanel));
        var daily = SmallBtn("Daily", "DAILY", TCUI.Sky, "gift", new Vector2(330, 110), OpenDaily);
        dailyBadge = TCUI.Img("Badge", daily.transform, TCUI.Hex("#FF6F8E"), TCUI.Circle); TCUI.Place(dailyBadge.rectTransform, new Vector2(1, 1), new Vector2(-10, -10), new Vector2(46, 46));
    }
    Button SmallBtn(string n, string text, Color c, string icon, Vector2 pos, System.Action a)
    {
        var b = TCUI.Btn(n, safe, text, c, a, 46, TCUI.Icon(icon));
        TCUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0), pos, new Vector2(300, 130)); return b;
    }
    TextMeshProUGUI Currency(string n, string icon, Vector2 pos)
    {
        var pill = TCUI.Img(n, safe, new Color(1, 1, 1, 0.9f)); TCUI.Place(pill.rectTransform, new Vector2(0, 1), pos, new Vector2(260, 96));
        var ic = TCUI.Img("Icon", pill.transform, Color.white, TCUI.Icon(icon)); TCUI.Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(48, 0), new Vector2(70, 70));
        var t = TCUI.Label("Value", pill.transform, "0", 48, TCUI.Cocoa, TextAlignmentOptions.Right); TCUI.Stretch(t.rectTransform, 90, 24, 0, 0); return t;
    }
    IEnumerator Bob(RectTransform r, float amp, float speed) { var p = r.anchoredPosition; while (r) { r.anchoredPosition = p + new Vector2(0, Mathf.Sin(Time.unscaledTime * speed) * amp); yield return null; } }
    IEnumerator Glow(Image g) { while (g) { float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f); g.color = new Color(1f, 0.95f, 0.55f, 0.25f + 0.35f * k); g.transform.localScale = Vector3.one * (0.95f + 0.1f * k); yield return null; } }
    IEnumerator Breathe(Transform t) { yield return new WaitForSecondsRealtime(1.6f); while (t) { float k = Mathf.Sin(Time.unscaledTime * 3f) * 0.04f; t.localScale = new Vector3(1 + k, 1 - k * 0.6f, 1); yield return null; } }
    IEnumerator Pulse(Transform t) { var ic = t.Find("Icon"); while (ic) { ic.localScale = Vector3.one * (1 + 0.12f * Mathf.Sin(Time.unscaledTime * 5)); yield return null; } }
    IEnumerator LoadGame()
    {
        var fade = TCUI.Rect("Fade", canvas.transform); TCUI.Stretch(fade); var img = fade.gameObject.AddComponent<Image>(); img.color = new Color(1, 0.97f, 0.93f, 0);
        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime) { img.color = new Color(1, 0.97f, 0.93f, t / 0.3f); yield return null; }
        SceneManager.LoadScene("gameplay");
    }

    // ---------- Skins ----------
    readonly List<(Image card, TextMeshProUGUI label, Image lockIc, Image check, int idx)> skinCards = new List<(Image, TextMeshProUGUI, Image, Image, int)>();
    TextMeshProUGUI skinName; Button skinAction; TextMeshProUGUI skinActionText; int selectedCard;
    void BuildSkins()
    {
        skinsPanel = TCUI.Panel("Skins", safe, out var card, new Vector2(980, 1500), "SKINS");
        skinPreviewImg = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>(); skinPreviewImg.transform.SetParent(card, false);
        skinPreviewImg.texture = rt; skinPreviewImg.raycastTarget = false; TCUI.Place(skinPreviewImg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(460, 460));
        skinName = TCUI.Label("Name", card, "", 56, TCUI.Cocoa); TCUI.Place(skinName.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -580), new Vector2(800, 80));
        for (int i = 0; i < TCSkins.All.Length; i++)
        {
            int idx = i; var def = TCSkins.All[i];
            int col = i % 5, row = i / 5;
            var b = TCUI.Btn("Skin" + i, card, "", def.body, () => { selectedCard = idx; ApplyPreview(idx); RefreshSkinGrid(); }, 30);
            TCUI.Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(-376 + col * 188, -660 - row * 175), new Vector2(140, 140));
            var accent = TCUI.Img("Accent", b.transform, def.accent, TCUI.Circle); TCUI.Place(accent.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            var lbl = TCUI.Label("Price", b.transform, "", 28, TCUI.Cocoa); TCUI.Place(lbl.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -22), new Vector2(190, 36));
            var lk = TCUI.Img("Lock", b.transform, Color.white, TCUI.Icon("lock")); TCUI.Place(lk.rectTransform, new Vector2(1, 1), new Vector2(-22, -22), new Vector2(44, 44));
            var ck = TCUI.Img("Check", b.transform, TCUI.Mint, TCUI.Circle); TCUI.Place(ck.rectTransform, new Vector2(1, 1), new Vector2(-18, -18), new Vector2(52, 52));
            var ckI = TCUI.Img("I", ck.transform, Color.white, TCUI.Icon("check")); TCUI.Stretch(ckI.rectTransform, 8, 8, 8, 8);
            skinCards.Add((b.GetComponent<Image>(), lbl, lk, ck, idx));
        }
        skinAction = TCUI.Btn("Action", card, "SELECT", TCUI.Mint, SkinAction, 52);
        TCUI.Place((RectTransform)skinAction.transform, new Vector2(0.5f, 0), new Vector2(-160, 100), new Vector2(460, 120));
        skinActionText = skinAction.GetComponentInChildren<TextMeshProUGUI>();
        TCUI.Place((RectTransform)TCUI.Btn("Close", card, "BACK", TCUI.Grey, () => { ApplyPreview(TCSave.SelectedSkin); TCUI.Hide(this, skinsPanel); }, 44).transform, new Vector2(0.5f, 0), new Vector2(260, 100), new Vector2(260, 120));
        selectedCard = TCSave.SelectedSkin;
        refreshers.Add(() => { if (skinsPanel.gameObject.activeSelf) RefreshSkinGrid(); });
        RefreshSkinGrid(); // grid showed every skin as equipped/unlocked until first tap
    }
    void RefreshSkinGrid()
    {
        TCSkins.CheckAchievements();
        foreach (var c in skinCards)
        {
            bool owned = TCSave.SkinOwned(c.idx); var def = TCSkins.All[c.idx];
            c.lockIc.gameObject.SetActive(!owned); c.check.gameObject.SetActive(TCSave.SelectedSkin == c.idx);
            c.label.text = owned ? "" : def.unlock == SkinUnlock.Coins ? def.amount.ToString() : def.unlock == SkinUnlock.Ads ? "AD " + TCSave.SkinAdsWatched(c.idx) + "/" + def.amount : def.unlock == SkinUnlock.BestScore ? def.amount + "M" : def.amount + " runs";
            c.card.color = c.idx == selectedCard ? Color.Lerp(TCSkins.All[c.idx].body, Color.white, 0.35f) : TCSkins.All[c.idx].body;
        }
        var d = TCSkins.All[selectedCard]; skinName.text = d.name;
        bool own = TCSave.SkinOwned(selectedCard);
        skinAction.interactable = true;
        if (own) skinActionText.text = TCSave.SelectedSkin == selectedCard ? "EQUIPPED" : "SELECT";
        else if (d.unlock == SkinUnlock.Coins) { skinActionText.text = "BUY " + d.amount; skinAction.interactable = TCSave.Coins >= d.amount; }
        else if (d.unlock == SkinUnlock.Ads) skinActionText.text = "WATCH AD (" + TCSave.SkinAdsWatched(selectedCard) + "/" + d.amount + ")";
        else { skinActionText.text = d.UnlockText; skinAction.interactable = false; }
    }
    void SkinAction()
    {
        int i = selectedCard; var d = TCSkins.All[i];
        if (TCSave.SkinOwned(i)) { TCSave.SelectedSkin = i; Toast(d.name + " equipped!"); }
        else if (d.unlock == SkinUnlock.Coins && TCSave.Coins >= d.amount) { TCSave.Coins -= d.amount; TCSave.UnlockSkin(i); TCSave.SelectedSkin = i; TCAudio.Play(TCAssets.I.purchase); Toast("Unlocked " + d.name + "!"); TCServices.Log("skin_buy", "skin", i); }
        else if (d.unlock == SkinUnlock.Ads) TCServices.Rewarded(() => { TCSave.AddSkinAd(i); TCSkins.CheckAchievements(); if (TCSave.SkinOwned(i)) { TCSave.SelectedSkin = i; Toast("Unlocked " + d.name + "!"); } RefreshSkinGrid(); }, "skin");
        RefreshSkinGrid();
    }

    // ---------- Shop ----------
    static readonly string[] upgNames = { "", "Boost Time", "Extra Heart", "Climb Bonus", "Head Start" };
    static readonly int[] upgMax = { 0, 5, 2, 3, 3 };
    public CanvasGroup GetPanel(string n) => n == "Skins" ? skinsPanel : n == "Shop" ? shopPanel : n == "Settings" ? settingsPanel : n == "Daily" ? dailyPanel : consentPanel;
    void BuildShop()
    {
        shopPanel = TCUI.Panel("Shop", safe, out var card, new Vector2(980, 1680), "SHOP");
        var h1 = TCUI.Label("H", card, "UPGRADES", 46, TCUI.Peach); TCUI.Place(h1.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(800, 60));
        for (int id = 1; id <= 4; id++)
        {
            int u = id; float y = -220 - (id - 1) * 140;
            var row = TCUI.Img("Upg" + id, card, Color.white); TCUI.Place(row.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(880, 120));
            var name = TCUI.Label("N", row.transform, upgNames[id], 44, TCUI.Cocoa, TextAlignmentOptions.Left); TCUI.Stretch(name.rectTransform, 30, 400, 0, 40);
            var pips = TCUI.Label("P", row.transform, "", 34, TCUI.Mint, TextAlignmentOptions.Left); TCUI.Stretch(pips.rectTransform, 30, 400, 60, 0);
            var buy = TCUI.Btn("Buy", row.transform, "0", TCUI.Butter, () =>
            {
                int lvl = TCSave.Upgrade(u); if (lvl >= upgMax[u]) return; int cost = TCConfig.I.upgradeCosts[Mathf.Min(lvl, TCConfig.I.upgradeCosts.Length - 1)];
                if (TCSave.Coins < cost) { Toast("Not enough coins"); return; }
                TCSave.Coins -= cost; TCSave.SetUpgrade(u, lvl + 1); TCAudio.Play(TCAssets.I.purchase); Toast(upgNames[u] + " upgraded!"); TCServices.Log("upgrade", "id", u);
            }, 40, TCUI.Icon("coin"));
            TCUI.Place((RectTransform)buy.transform, new Vector2(1, 0.5f), new Vector2(-170, 0), new Vector2(300, 96));
            var bt = buy.GetComponentInChildren<TextMeshProUGUI>();
            refreshers.Add(() =>
            {
                int lvl = TCSave.Upgrade(u); pips.text = "Level " + lvl + " / " + upgMax[u];
                bool max = lvl >= upgMax[u]; int cost = TCConfig.I.upgradeCosts[Mathf.Min(lvl, TCConfig.I.upgradeCosts.Length - 1)];
                if (bt) bt.text = max ? "MAX" : cost.ToString(); buy.interactable = !max && TCSave.Coins >= cost;
            });
        }
        // Standalone gem packs: exactly 10 gems per USD.
        var hg = TCUI.Label("HG", card, "GEMS  (10 GEMS = $1)", 46, TCUI.Peach); TCUI.Place(hg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -770), new Vector2(860, 60));
        for (int g = 0; g < UnityIAP.GemProductIDs.Length; g++)
        {
            int pack = g, gems = UnityIAP.GemPackAmounts[g];
            string price = "$" + UnityIAP.GemPackUsd[g].ToString("0");
            var gb = TCUI.Btn("Gems" + g, card, gems + "\n" + price, TCUI.Sky, () => TCServices.BuyGems(pack, () =>
            {
                TCSave.Gems += gems; TCAudio.Play(TCAssets.I.purchase); Toast("+" + gems + " gems!"); TCServices.Log("iap_gems", "gems", gems);
            }), 40, TCUI.Icon("gem"));
            TCUI.Place((RectTransform)gb.transform, new Vector2(0.5f, 1), new Vector2(-342 + g * 228, -880), new Vector2(212, 140));
            var gt = gb.GetComponentInChildren<TextMeshProUGUI>();
            refreshers.Add(() => { var lp = UnityIAP.Instance ? UnityIAP.Instance.LocalizedPrice(UnityIAP.GemProductIDs[pack]) : null; if (gt && !string.IsNullOrEmpty(lp)) gt.text = gems + "\n" + lp; });
        }
        var h2 = TCUI.Label("H2", card, "COIN PACKS & MORE", 46, TCUI.Peach); TCUI.Place(h2.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -1000), new Vector2(800, 60));
        Pack(card, new Vector2(-225, -1110), "1,000 coins", "BUY", 0, () => TCSave.Coins += 1000);
        Pack(card, new Vector2(225, -1110), "No Ads + 20 gems", "BUY", 1, () => { TCSave.NoAds = true; TCSave.Gems += 20; TCServices.Banner(false); });
        Pack(card, new Vector2(-225, -1260), "Premium: 5,000\ncoins + 50 gems", "BUY", 2, () => { TCSave.Coins += 5000; TCSave.Gems += 50; });
        int adCoins = TCConfig.I.adCoinReward;
        var free = TCUI.Btn("FreeCoins", card, "FREE " + adCoins + " (AD)", TCUI.Mint, () => TCServices.Rewarded(() => { TCSave.Coins += adCoins; Toast("+" + adCoins + " coins"); }, "shop_coins"), 40, TCUI.Icon("play"));
        TCUI.Place((RectTransform)free.transform, new Vector2(0.5f, 1), new Vector2(225, -1260), new Vector2(420, 130));
        var restore = TCUI.Btn("Restore", card, "RESTORE", TCUI.Lilac, () => { try { UnityIAP.Instance?.RestorePurchases(); } catch { } Toast("Restoring purchases..."); }, 38);
        TCUI.Place((RectTransform)restore.transform, new Vector2(0.5f, 0), new Vector2(-200, 110), new Vector2(340, 110));
        TCUI.Place((RectTransform)TCUI.Btn("Close", card, "BACK", TCUI.Grey, () => TCUI.Hide(this, shopPanel), 46).transform, new Vector2(0.5f, 0), new Vector2(200, 110), new Vector2(340, 110));
    }
    void Pack(RectTransform card, Vector2 pos, string title, string btn, int productIndex, System.Action grant)
    {
        var b = TCUI.Btn("Pack" + productIndex, card, title, TCUI.Sky, () => TCServices.Buy(productIndex, () => { grant(); TCAudio.Play(TCAssets.I.purchase); Toast("Purchase complete!"); TCServices.Log("iap_purchase", "index", productIndex); }), 38);
        TCUI.Place((RectTransform)b.transform, new Vector2(0.5f, 1), pos, new Vector2(420, 130));
    }

    // ---------- Settings ----------
    void BuildSettings()
    {
        settingsPanel = TCUI.Panel("Settings", safe, out var card, new Vector2(900, 1240), "SETTINGS");
        TCGameUI.Toggle(card, "Sound", 380, () => TCSave.Sound, v => TCSave.Sound = v, 700);
        TCGameUI.Toggle(card, "Music", 270, () => TCSave.Music, v => { TCSave.Music = v; ApplyMenuMusic(); }, 700);
        TCGameUI.Toggle(card, "Vibration", 160, () => TCSave.Vibration, v => TCSave.Vibration = v, 700);
        var sl = TCUI.Label("SensL", card, "Control sensitivity", 44, TCUI.Cocoa, TextAlignmentOptions.Left); TCUI.Place(sl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(700, 70));
        var slider = BuildSlider(card, new Vector2(0, -30)); slider.minValue = 0.4f; slider.maxValue = 2.2f; slider.value = TCSave.Sensitivity; slider.onValueChanged.AddListener(v => TCSave.Sensitivity = v);
        TCUI.Place((RectTransform)TCUI.Btn("Privacy", card, "PRIVACY POLICY", TCUI.Sky, () => Application.OpenURL(TCAssets.I.privacyUrl), 42).transform, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(620, 110));
        TCUI.Place((RectTransform)TCUI.Btn("Tutorial", card, "REPLAY TUTORIAL", TCUI.Blush, () => { TCSave.TutorialDone = false; Toast("Tutorial will show next run"); }, 42).transform, new Vector2(0.5f, 0.5f), new Vector2(0, -310), new Vector2(620, 110));
        TCUI.Place((RectTransform)TCUI.Btn("Close", card, "DONE", TCUI.Mint, () => TCUI.Hide(this, settingsPanel), 50).transform, new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(420, 120));
    }
    Slider BuildSlider(RectTransform parent, Vector2 pos)
    {
        var root = TCUI.Rect("Slider", parent); TCUI.Place(root, new Vector2(0.5f, 0.5f), pos, new Vector2(700, 60));
        var bg = TCUI.Img("BG", root, TCUI.Grey); TCUI.Stretch(bg.rectTransform, 0, 0, 18, 18);
        var fillArea = TCUI.Rect("FillArea", root); TCUI.Stretch(fillArea, 0, 0, 18, 18);
        var fill = TCUI.Img("Fill", fillArea, TCUI.Mint); TCUI.Stretch(fill.rectTransform);
        var handleArea = TCUI.Rect("HandleArea", root); TCUI.Stretch(handleArea, 30, 30, 0, 0);
        var handle = TCUI.Img("Handle", handleArea, TCUI.Peach, TCUI.Circle); handle.rectTransform.sizeDelta = new Vector2(70, 70); handle.raycastTarget = true;
        var s = root.gameObject.AddComponent<Slider>(); s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        return s;
    }

    // ---------- Daily reward ----------
    readonly List<Image> dailyCells = new List<Image>(); Button claimBtn, dblBtn;
    TextMeshProUGUI claimBtnText, dblBtnText;
    void BuildDaily()
    {
        dailyPanel = TCUI.Panel("Daily", safe, out var card, new Vector2(960, 1100), "DAILY REWARD");
        var r = TCConfig.I.dailyRewards;
        for (int i = 0; i < 7; i++)
        {
            int col = i < 4 ? i : i - 4; int row = i < 4 ? 0 : 1;
            float x = row == 0 ? -330 + col * 220 : -220 + col * 220;
            var c = TCUI.Img("Day" + i, card, Color.white); TCUI.Place(c.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 250 - row * 270), new Vector2(190, 240));
            var d = TCUI.Label("D", c.transform, "DAY " + (i + 1), 34, TCUI.Cocoa); TCUI.Place(d.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(190, 50));
            bool gem = i == 6;
            var ic = TCUI.Img("I", c.transform, Color.white, TCUI.Icon(gem ? "gem" : "coin")); TCUI.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(80, 80));
            var v = TCUI.Label("V", c.transform, r[i].ToString(), 40, TCUI.Cocoa); TCUI.Place(v.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(190, 50));
            dailyCells.Add(c);
        }
        claimBtn = TCUI.Btn("Claim", card, "CLAIM", TCUI.Mint, () => ClaimDaily(false), 56);
        claimBtnText = claimBtn.GetComponentInChildren<TextMeshProUGUI>();
        TCUI.Place((RectTransform)claimBtn.transform, new Vector2(0.5f, 0), new Vector2(-160, 260), new Vector2(380, 140));
        dblBtn = TCUI.Btn("Double", card, "x2 (AD)", TCUI.Butter, () => TCServices.Rewarded(() => ClaimDaily(true), "daily"), 48, TCUI.Icon("play"));
        dblBtnText = dblBtn.GetComponentInChildren<TextMeshProUGUI>();
        TCUI.Place((RectTransform)dblBtn.transform, new Vector2(0.5f, 0), new Vector2(230, 260), new Vector2(340, 140));
        TCUI.Place((RectTransform)TCUI.Btn("Close", card, "LATER", TCUI.Grey, () => TCUI.Hide(this, dailyPanel), 42).transform, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(300, 100));
        refreshers.Add(() =>
        {
            bool avail = TCSave.DailyAvailable(out int day);
            for (int i = 0; i < 7; i++) dailyCells[i].color = i < day ? TCUI.Mint : i == day && avail ? TCUI.Butter : Color.white;
            claimBtn.interactable = avail;
            dblBtn.interactable = avail;
            if (claimBtnText) claimBtnText.text = avail ? "CLAIM" : "CLAIMED";
            if (dblBtnText) dblBtnText.text = avail ? "x2 (AD)" : "CLAIMED";
        });
    }
    void OpenDaily() { Refresh(); TCUI.Show(this, dailyPanel); }
    void ClaimDaily(bool doubled)
    {
        if (!TCSave.DailyAvailable(out int day)) return;
        claimBtn.interactable = false;
        if (dblBtn) dblBtn.interactable = false;
        int amt = TCConfig.I.dailyRewards[day] * (doubled ? 2 : 1);
        if (day == 6) TCSave.Gems += amt; else TCSave.Coins += amt;
        TCSave.ClaimDaily(day); TCAudio.Play(TCAssets.I.purchase);
        Toast("+" + amt + (day == 6 ? " gems" : " coins"));
        StartCoroutine(Delay(0.4f, () => TCUI.Hide(this, dailyPanel)));
    }

    // ---------- Consent ----------
    void BuildConsent()
    {
        consentPanel = TCUI.Panel("Consent", safe, out var card, new Vector2(920, 900), "WELCOME!");
        var t = TCUI.Label("Body", card, "We use ads and analytics to keep Tall Crawl free. By tapping Accept you agree to our privacy policy. You can change ad choices in Settings.", 40, TCUI.Cocoa);
        TCUI.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(800, 400));
        TCUI.Place((RectTransform)TCUI.Btn("Accept", card, "ACCEPT", TCUI.Mint, () => { PlayerPrefs.SetInt("consent", 1); TCUI.Hide(this, consentPanel); TCServices.Banner(true); StartCoroutine(Delay(0.4f, OpenDaily)); }, 56).transform, new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(560, 140));
        TCUI.Place((RectTransform)TCUI.Btn("Privacy", card, "PRIVACY POLICY", TCUI.Sky, () => Application.OpenURL(TCAssets.I.privacyUrl), 40).transform, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(560, 110));
    }

    // ---------- Toast ----------
    void BuildToast()
    {
        var r = TCUI.Img("Toast", canvas.transform, TCUI.Cocoa); TCUI.Place(r.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(760, 110));
        toastGroup = r.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        toastText = TCUI.Label("T", r.transform, "", 44, Color.white); TCUI.Stretch(toastText.rectTransform);
    }
    Coroutine toastCo;
    void Toast(string msg) { if (toastCo != null) StopCoroutine(toastCo); toastCo = StartCoroutine(ToastCo(msg)); }
    IEnumerator ToastCo(string msg)
    {
        toastText.text = msg; toastGroup.transform.SetAsLastSibling();
        for (float t = 0; t < 0.2f; t += Time.unscaledDeltaTime) { toastGroup.alpha = t / 0.2f; yield return null; }
        toastGroup.alpha = 1; yield return new WaitForSecondsRealtime(1.4f);
        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime) { toastGroup.alpha = 1 - t / 0.3f; yield return null; }
        toastGroup.alpha = 0;
    }
}
