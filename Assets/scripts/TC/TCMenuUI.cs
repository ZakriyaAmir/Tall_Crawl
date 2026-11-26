using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// Main menu: currencies, best score, 3D spider preview, Play, Skins, Shop, Daily reward, Settings, consent.
public class TCMenuUI : MonoBehaviour
{
    public static TCMenuUI Instance;
    RectTransform safe; Canvas canvas;
    TextMeshProUGUI coinsText, gemsText, bestText; Image dailyBadge;
    CanvasGroup skinsPanel, shopPanel, settingsPanel, dailyPanel, consentPanel, toastGroup; TextMeshProUGUI toastText;
    GameObject previewSpider; RenderTexture rt; [SerializeField] Camera previewCam; RawImage previewImg, skinPreviewImg; int previewSkin = -1;
    readonly List<System.Action> refreshers = new List<System.Action>();
    readonly TCCheatCode cheat = new TCCheatCode();
    public bool CheatActivated { get; private set; }
    TextMeshProUGUI cheatHint;
    bool splashFinished;

    void Awake()
    {
        Instance = this;
        StopMenuMusic();
    }

    void Start()
    {
        TCServices.ApplyMaxDeviceRefreshRate(); Time.timeScale = 1; TCSave.Apply();
        BuildPreview();
        BuildUI();
        UnityIAP.OnIAPInitialized += RefreshShopPrices;
        TCSave.Changed += Refresh; Refresh();
        StartCoroutine(PlaySplashAndEntrance());
        TCSkins.CheckAchievements();
        if (PlayerPrefs.GetInt("consent", 0) == 0) PlayerPrefs.SetInt("consent", 1);
        bool isFirstBootEver = PlayerPrefs.GetInt("tc_booted_before", 0) == 0;
        if (isFirstBootEver)
        {
            PlayerPrefs.SetInt("tc_booted_before", 1);
            PlayerPrefs.Save();
        }
        else if (TCSave.DailyAvailable(out _))
        {
            StartCoroutine(Delay(4.2f, OpenDaily));
        }
        // Note: Do not change text colors at runtime so baked scene text styling is preserved
        HookPreviewTap(previewImg);
        HookPreviewTap(skinPreviewImg);
    }

    IEnumerator PlaySplashAndEntrance()
    {
        // Only play the splash screen when player starts the game (first time in the session),
        // not when returning to the main menu from the gameplay scene.
        if (!TCSplashScreen.HasPlayedThisSession)
        {
            // 1. Hide spider preview and ensure BGM is silent while splash screen is going on
            if (previewSpider) previewSpider.SetActive(false);
            if (previewImg) previewImg.gameObject.SetActive(false);
            StopMenuMusic();

            // 2. Play the SillyCon splash screen (brushed in -> settle -> brushed off -> fade out white BG)
            if (canvas)
            {
                yield return TCSplashScreen.Play(canvas.transform);
            }

            // 3. Wait exactly 0.1s after the splash screen is completely gone
            yield return new WaitForSecondsRealtime(0.1f);
        }

        // Reveal spider preview and start menu music and banner ad only then
        splashFinished = true;
        if (previewSpider)
        {
            previewSpider.transform.localRotation = Quaternion.identity;
            previewSpider.SetActive(true);
        }
        if (previewImg) previewImg.gameObject.SetActive(true);
        ApplyMenuMusic();
        TCServices.Banner(true);

        // Run spider entrance animation
        yield return Entrance();
    }
    /// Builds (or, when baked into the scene, binds to) every menu screen. Also run in edit mode by TCUIBake.
    public void BuildUI()
    {
        canvas = TCUI.MakeCanvas("TC_MenuUI", 10);
        BuildBackground();
        safe = TCUI.SafeRoot(canvas.transform);
        BuildMain(); BuildSkins(); BuildShop(); BuildSettings(); BuildDaily(); BuildConsent(); BuildToast();
        // polish3 palette theme: styles anything not already themed in the baked scene (never overrides editor edits)
        TCTheme.ThemeTree(canvas.transform);
        TCTheme.MenuBackground(canvas.transform.Find("BG"));
    }
    void Co(IEnumerator e) { if (Application.isPlaying) StartCoroutine(e); }
    void OnDestroy() { TCSave.Changed -= Refresh; UnityIAP.OnIAPInitialized -= RefreshShopPrices; if (rt) rt.Release(); }
    static void StopMenuMusic()
    {
        var m = GameObject.Find("bgm");
        if (m)
        {
            var a = m.GetComponent<AudioSource>();
            if (a) { a.mute = true; a.Stop(); }
        }
    }

    static void ApplyMenuMusic()
    {
        var m = GameObject.Find("bgm");
        if (m)
        {
            var a = m.GetComponent<AudioSource>();
            if (a)
            {
                a.mute = !TCSave.Music;
                if (TCSave.Music && !a.isPlaying) a.Play();
            }
        }
    }

    // Android back button: close the top-most open panel (it did nothing before).
    void Update()
    {
        // Continuously shift Rainbow card color in skins panel
        if (skinsPanel && skinsPanel.gameObject.activeSelf && skinsPanel.alpha > 0.1f)
        {
            var db = skinDatabase ? skinDatabase : SpiderSkinDatabase.Instance;
            var skins = db != null && db.skins != null && db.skins.Length > 0 ? db.skins : TCSkins.All;
            foreach (var c in skinCards)
            {
                if (c.idx < skins.Length && skins[c.idx].name == "Rainbow" && c.card)
                {
                    float hue = Mathf.Repeat(Time.unscaledTime * 0.65f, 1f);
                    Color rainbowCol = Color.HSVToRGB(hue, 0.85f, 1f);
                    c.card.color = c.idx == selectedCard ? Color.Lerp(rainbowCol, Color.white, 0.35f) : rainbowCol;
                }
            }
        }

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
        RefreshShopPrices();
        foreach (var r in refreshers) r();
        if (splashFinished) ApplyMenuMusic(); // keep bgm in sync with the saved setting whatever changed it
    }

    // ---------- Background (stylized toon spider forest) ----------
    void BuildBackground()
    {
        var bg = TCUI.Rect("BG", canvas.transform); TCUI.Stretch(bg);
        // Hide legacy flat bubbles so the toonish background artwork shines cleanly
        for (int i = 0; i < bg.childCount; i++)
        {
            var ch = bg.GetChild(i);
            if (ch.name.StartsWith("Bubble")) ch.gameObject.SetActive(false);
        }
        if (TCUI.Fresh(bg))
        {
            var tex = Resources.Load<Texture2D>("TCUI/menu_gradient");
            var ri = bg.gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false;
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
        rt = new RenderTexture(768, 768, 16, RenderTextureFormat.ARGB32) { antiAliasing = 2 };

        // Use preview camera already present in the scene, or locate by name
        if (!previewCam)
        {
            var existingCam = GameObject.Find("PreviewCam");
            if (existingCam) previewCam = existingCam.GetComponent<Camera>();
        }

        Transform stageParent = null;
        if (previewCam)
        {
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.backgroundColor = new Color(0, 0, 0, 0);
            previewCam.targetTexture = rt;
            stageParent = previewCam.transform.parent;
        }

        var root = GameObject.Find("SkinPreviewStage");
        if (!root)
        {
            root = new GameObject("SkinPreviewStage");
            root.transform.position = new Vector3(0, -500, 0);
            if (stageParent) root.transform.SetParent(stageParent, true);
        }

        if (!GameObject.Find("PreviewLight"))
        {
            var light = new GameObject("PreviewLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.SetParent(root.transform);
            light.transform.rotation = Quaternion.Euler(35, -25, 0);
            light.intensity = 1.25f;
            light.shadows = LightShadows.None;
        }

        if (a.spiderModel)
        {
            previewSpider = Instantiate(a.spiderModel, root.transform);
            previewSpider.transform.localPosition = Vector3.zero;
            previewSpider.transform.localRotation = Quaternion.identity;
            var anim = previewSpider.GetComponentInChildren<Animator>(); if (!anim) anim = previewSpider.AddComponent<Animator>();
            if (a.spiderAnimator) { anim.runtimeAnimatorController = a.spiderAnimator; anim.SetBool("started", false); }
            var smr = previewSpider.GetComponentInChildren<SkinnedMeshRenderer>();
            var b = smr ? smr.bounds : new Bounds(previewSpider.transform.position, Vector3.one);
            float size = b.size.magnitude;
            previewSize = size;
        }
        ApplyPreview(TCSave.SelectedSkin);
        StripPreviewTrails();
        if (previewSpider)
        {
            previewSpider.transform.localRotation = Quaternion.identity;
            previewSpider.SetActive(false);
        }
    }
    void ApplyPreview(int i)
    {
        if (previewSpider && previewSkin != i)
        {
            previewSkin = i;
            TCSkins.Apply(previewSpider, i, enableTrail: false);
            StripPreviewTrails();
        }
    }
    void StripPreviewTrails()
    {
        if (!previewSpider) return;
        foreach (var tr in previewSpider.GetComponentsInChildren<TrailRenderer>(true))
        {
            if (!tr) continue;
            tr.enabled = false;
            tr.Clear();
            Destroy(tr);
        }
    }
    float swayBlend = 0f;
    void LateUpdate()
    {
        if (splashFinished && previewSpider && !entering)
        {
            swayBlend = Mathf.MoveTowards(swayBlend, 1f, Time.unscaledDeltaTime * 1.25f);
            previewSpider.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.unscaledTime * 1.3f) * 28f * swayBlend, 0);
        }
    } // gentle sway, face stays visible

    // ---------- Main menu entrance: 4 random / rotating animations ----------
    // 1: Drop on a silk thread, somersault, squash landing, dust, wave
    // 2: Zoom from left to right side, then enter center and wink with sounds & voice
    // 3: Come from far away, bonk / hit face to camera, recover, wave at camera with sounds & voice
    // 4: Fall from top, bounce/land dizzy, shake head / regain consciousness, squint eyes, happy smile with sounds & voice
    bool entering; LineRenderer silk; float previewSize = 1;

    IEnumerator Entrance()
    {
        if (!previewSpider) yield break;
        previewSpider.SetActive(true);
        if (previewImg) previewImg.gameObject.SetActive(true);
        entering = true;
        swayBlend = 0f;
        float S = previewSize;
        float h = S * 0.22f;
        Transform pivot = previewSpider.transform.parent;
        if (!pivot || pivot.name != "DropPivot")
        {
            var pGo = new GameObject("DropPivot").transform;
            pGo.SetParent(previewSpider.transform.parent, false);
            pGo.localPosition = new Vector3(0, h, 0);
            pGo.localRotation = Quaternion.identity;
            previewSpider.transform.localRotation = Quaternion.identity;
            previewSpider.transform.SetParent(pGo, true);
            pivot = pGo;
        }
        var st = pivot;
        Vector3 rest = new Vector3(0, h, 0);
        var cs = previewSpider.GetComponent<TCCuteSpider>();

        // Pick next entrance style (0..3) sequentially so all 4 are seen cleanly in order
        int style = PlayerPrefs.GetInt("tc_menu_entrance_style", 0) % 4;
        PlayerPrefs.SetInt("tc_menu_entrance_style", (style + 1) % 4);
        PlayerPrefs.Save();

        switch (style)
        {
            case 0:
                yield return EntranceDropWave(st, rest, S, cs);
                break;
            case 1:
                yield return EntranceZoomWink(st, rest, S, cs);
                break;
            case 2:
                yield return EntranceHitCameraWave(st, rest, S, cs);
                break;
            case 3:
                yield return EntranceFallDizzySmile(st, rest, S, cs);
                break;
        }

        if (cs)
        {
            cs.wave = 0;
            cs.headExtraRot = Quaternion.identity;
            cs.headExtraPos = Vector3.zero;
            cs.ClearEyeOverride();
        }
        st.localPosition = rest;
        st.localRotation = Quaternion.identity;
        st.localScale = Vector3.one;
        previewSpider.transform.localRotation = Quaternion.identity;

        entering = false;
    }

    // --- Style 1: Classic silk drop, somersault, squash, wave ---
    IEnumerator EntranceDropWave(Transform st, Vector3 rest, float S, TCCuteSpider cs)
    {
        Vector3 top = new Vector3(0, rest.y + S * 1.6f, 0);
        silk = new GameObject("Silk").AddComponent<LineRenderer>();
        silk.transform.SetParent(st.parent, false);
        silk.useWorldSpace = false;
        var silkMat = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default"));
        var silkTex = Resources.Load<Texture2D>("SilkWeb");
        if (silkTex) silkMat.mainTexture = silkTex;
        silk.material = silkMat;
        silk.textureMode = LineTextureMode.Tile;
        silk.startColor = silk.endColor = new Color(1, 1, 1, 0.95f);
        silk.widthMultiplier = S * 0.022f;
        silk.positionCount = 2;
        silk.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        st.localPosition = top;
        st.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(0.25f);
        TCSfx.Play("menu_drop", 0.6f);
        bool flipSfx = false;
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.95f)
        {
            float e = 1 - Mathf.Pow(1 - x, 3);
            st.localPosition = Vector3.Lerp(top, rest, e);
            float flip = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.25f, 0.8f, x));
            if (!flipSfx && x > 0.25f) { flipSfx = true; TCSfx.Play("menu_flip", 0.6f); }
            st.localRotation = Quaternion.Euler(-360f * flip, 0, 0) * Quaternion.Euler(0, 0, Mathf.Sin(x * 9) * 8 * (1 - x));
            float str = 1 + 0.18f * Mathf.Sin(x * Mathf.PI);
            st.localScale = new Vector3(1 / Mathf.Sqrt(str), str, 1 / Mathf.Sqrt(str));
            Silk(st, S);
            yield return null;
        }
        st.localPosition = rest;
        st.localRotation = Quaternion.identity;
        TCSfx.Play("land", 0.7f);
        Dust(st.parent, S);

        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.45f)
        {
            float k = Mathf.Sin(x * Mathf.PI * 3) * (1 - x) * 0.3f;
            st.localScale = new Vector3(1 + k, 1 - k, 1 + k);
            Silk(st, S);
            silk.startColor = silk.endColor = new Color(1, 1, 1, 0.95f * (1 - x));
            yield return null;
        }
        st.localScale = Vector3.one;
        if (silk) Destroy(silk.gameObject);

        TCSfx.PlayAction("chime", 0.7f, 1.05f);
        for (float x = 0; x < 1.4f; x += Time.unscaledDeltaTime)
        {
            if (cs) cs.wave = Mathf.Clamp01(Mathf.Min(x * 5, (1.4f - x) * 5));
            yield return null;
        }
        if (cs) cs.wave = 0;
    }

    // --- Style 2: Zoom left to right, swoop in to center, cute wink with sounds & happy voice ---
    IEnumerator EntranceZoomWink(Transform st, Vector3 rest, float S, TCCuteSpider cs)
    {
        Vector3 offLeft = rest + new Vector3(-S * 3.4f, 0.15f * S, 0.2f * S);
        Vector3 offRight = rest + new Vector3(S * 3.4f, 0.15f * S, 0.2f * S);
        st.localPosition = offLeft;
        st.localScale = Vector3.one;
        st.localRotation = Quaternion.identity;

        yield return new WaitForSecondsRealtime(0.2f);

        // Step 1: Fast high-speed zoom from offscreen left across to offscreen right
        TCSfx.PlayAction("whoosh", 0.85f, 1.25f);
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.48f)
        {
            float e = Mathf.SmoothStep(0, 1, x);
            st.localPosition = Vector3.Lerp(offLeft, offRight, e);
            st.localRotation = Quaternion.Euler(0, 90f, Mathf.Sin(x * Mathf.PI) * 12f);
            st.localScale = new Vector3(1.35f, 0.85f, 0.85f);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.12f);

        // Step 2: Swoop gracefully back from right into dead center
        TCSfx.Play("thrust", 0.7f, 1.15f);
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.58f)
        {
            float e = 1 - Mathf.Pow(1 - x, 3);
            st.localPosition = Vector3.Lerp(offRight, rest, e);
            float rotY = Mathf.Lerp(-90f, 0f, e);
            st.localRotation = Quaternion.Euler(0, rotY, Mathf.Sin(x * Mathf.PI * 2f) * 6f * (1 - x));
            float k = 1f + 0.15f * Mathf.Sin(x * Mathf.PI);
            st.localScale = new Vector3(k, 1f / k, 1f);
            yield return null;
        }

        // Skid / settle dust in center
        st.localPosition = rest;
        st.localRotation = Quaternion.identity;
        st.localScale = Vector3.one;
        TCSfx.Play("land", 0.65f);
        Dust(st.parent, S * 0.8f);

        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.35f)
        {
            float k = Mathf.Sin(x * Mathf.PI * 3f) * (1 - x) * 0.22f;
            st.localScale = new Vector3(1 + k, 1 - k, 1 + k);
            yield return null;
        }
        st.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(0.15f);

        // Step 3: Friendly wave + happy perky breath (no wink)
        TCSfx.PlayAction("chime", 0.75f, 1.15f);
        st.localRotation = Quaternion.identity;
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.85f)
        {
            if (cs) cs.wave = Mathf.Clamp01(Mathf.Min(x * 5f, (1f - x) * 5f));
            float happyPulse = Mathf.Sin(x * Mathf.PI) * 0.05f;
            st.localScale = new Vector3(1f - happyPulse * 0.4f, 1f + happyPulse, 1f - happyPulse * 0.4f);
            st.localRotation = Quaternion.identity;
            yield return null;
        }
        if (cs) { cs.wave = 0; cs.ClearEyeOverride(); }
        st.localScale = Vector3.one;
        st.localRotation = Quaternion.identity;

        yield return new WaitForSecondsRealtime(0.2f);
    }

    // --- Style 3: Come from far away, bonk / hit camera, bounce back & wave ---
    IEnumerator EntranceHitCameraWave(Transform st, Vector3 rest, float S, TCCuteSpider cs)
    {
        // PreviewCam is at z ~ -4.25, looking at (0, -500, 0).
        // Rest is z = 0. Far is z = +8.0. Camera close hit is z = -3.2 (right in front of lens).
        Vector3 farPos = rest + new Vector3(0, 0.4f * S, 7.5f * S);
        Vector3 camBonkPos = rest + new Vector3(0, 0.05f * S, -2.55f * S);
        st.localPosition = farPos;
        st.localRotation = Quaternion.identity;
        st.localScale = Vector3.one * 0.25f;

        yield return new WaitForSecondsRealtime(0.2f);

        // Rush from far distance towards camera
        TCSfx.PlayAction("whoosh", 0.8f, 1.1f);
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.65f)
        {
            float e = x * x; // accelerate towards screen
            st.localPosition = Vector3.Lerp(farPos, camBonkPos, e);
            st.localScale = Vector3.Lerp(Vector3.one * 0.25f, Vector3.one * 1.55f, e);
            st.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(x * 12f) * 6f * (1 - x));
            yield return null;
        }

        // BONK! Face hits the camera lens glass
        st.localPosition = camBonkPos;
        TCSfx.PlayAction("hit", 0.95f, 1.28f, dizzy: true, voiceVol: 0.6f);
        if (cs) cs.SetEyeOverride(0.2f); // eyes squished wide on impact

        // Squash flat against screen
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.18f)
        {
            float k = Mathf.Sin(x * Mathf.PI);
            st.localScale = new Vector3(1.8f + k * 0.4f, 1.8f + k * 0.4f, 0.35f);
            yield return null;
        }

        // Bounce back from camera to rest position
        TCSfx.Play("boop", 0.75f, 1.15f);
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.6f)
        {
            float e = 1 - Mathf.Pow(1 - x, 3);
            st.localPosition = Vector3.Lerp(camBonkPos, rest, e);
            st.localScale = Vector3.Lerp(new Vector3(1.6f, 1.6f, 0.5f), Vector3.one, e);
            st.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(x * Mathf.PI * 4f) * 14f * (1 - x));
            yield return null;
        }

        st.localPosition = rest;
        st.localScale = Vector3.one;
        st.localRotation = Quaternion.identity;
        if (cs) cs.ClearEyeOverride();

        // Shake head only to recover balance (keep body planted)
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.45f)
        {
            if (cs) cs.headExtraRot = Quaternion.Euler(0, Mathf.Sin(x * Mathf.PI * 8f) * 28f * (1 - x), Mathf.Sin(x * Mathf.PI * 6f) * 14f * (1 - x));
            yield return null;
        }
        if (cs) cs.headExtraRot = Quaternion.identity;

        // Friendly wave at camera with happy chatter voice
        TCSfx.PlayAction("chime", 0.75f, 1.05f);
        for (float x = 0; x < 1.4f; x += Time.unscaledDeltaTime)
        {
            if (cs) cs.wave = Mathf.Clamp01(Mathf.Min(x * 5, (1.4f - x) * 5));
            yield return null;
        }
        if (cs) cs.wave = 0;
    }

    // --- Style 4: Fall from top, dizzy bounce, shake/regain consciousness, squint & cute smile ---
    IEnumerator EntranceFallDizzySmile(Transform st, Vector3 rest, float S, TCCuteSpider cs)
    {
        Vector3 highTop = rest + new Vector3(0, S * 2.8f, 0);
        st.localPosition = highTop;
        st.localRotation = Quaternion.Euler(180f, 0, 45f); // tumbling down
        st.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(0.2f);

        // Fall fast without silk
        TCSfx.Play("menu_drop", 0.75f, 1.15f);
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.55f)
        {
            float e = x * x; // accelerating gravity drop
            st.localPosition = Vector3.Lerp(highTop, rest, e);
            st.localRotation = Quaternion.Euler(Mathf.Lerp(180f, 0f, e), 0, Mathf.Sin(x * 10f) * 18f);
            float str = 1f + 0.3f * e;
            st.localScale = new Vector3(1f / Mathf.Sqrt(str), str, 1f / Mathf.Sqrt(str));
            yield return null;
        }

        // Hard comical crash landing + dust
        st.localPosition = rest;
        st.localRotation = Quaternion.identity;
        TCSfx.PlayAction("land", 0.9f, 1.05f, dizzy: true, voiceVol: 0.65f);
        Dust(st.parent, S * 1.2f);

        // Big squash & wobble
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / 0.45f)
        {
            float k = Mathf.Sin(x * Mathf.PI * 3f) * (1 - x) * 0.45f;
            st.localScale = new Vector3(1 + k * 1.2f, Mathf.Max(0.4f, 1 - k * 1.4f), 1 + k * 1.2f);
            yield return null;
        }
        st.localScale = Vector3.one;

        // Shake head violently to regain balance with a bold spinning spiral dizzy VFX over its head
        TCSfx.Play("wiggle", 0.85f, 1.25f);
        st.localRotation = Quaternion.identity;
        var dizzyVfx = CreateDizzySpiralVFX(st, S, cs);
        const float shakeDur = 1.55f;
        for (float x = 0; x < 1f; x += Time.unscaledDeltaTime / shakeDur)
        {
            float endFade = Mathf.Clamp01((1f - x) / 0.16f);
            float damp = (1f - x * 0.40f) * endFade;
            float yawShake = Mathf.Sin(x * Mathf.PI * 20f) * 48f * damp;
            float rollShake = Mathf.Sin(x * Mathf.PI * 14f) * 28f * damp;
            float pitchShake = Mathf.Sin(x * Mathf.PI * 10f) * 16f * damp;
            if (cs)
            {
                cs.headExtraRot = Quaternion.Euler(pitchShake, yawShake, rollShake);
                cs.headExtraPos = new Vector3(Mathf.Sin(x * Mathf.PI * 20f) * 0.09f * damp, 0f, 0f);
            }
            if (dizzyVfx)
            {
                dizzyVfx.transform.localRotation = Quaternion.Euler(34f, -x * 1260f, 0f);
                float pop = Mathf.Clamp01(x / 0.10f) * endFade;
                dizzyVfx.transform.localScale = Vector3.one * pop;
            }
            yield return null;
        }
        if (dizzyVfx) Destroy(dizzyVfx);
        if (cs)
        {
            cs.headExtraRot = Quaternion.identity;
            cs.headExtraPos = Vector3.zero;
            cs.ClearEyeOverride();
        }
        st.localRotation = Quaternion.identity;
        st.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(0.2f);
    }

    GameObject CreateDizzySpiralVFX(Transform st, float S, TCCuteSpider cs)
    {
        int layer = (cs && cs.gameObject) ? cs.gameObject.layer : st.gameObject.layer;
        var root = new GameObject("DizzySpiralVFX") { layer = layer };
        root.transform.SetParent(st.parent, false);
        root.transform.localPosition = st.localPosition + new Vector3(0f, S * 0.88f, -S * 0.05f);
        root.transform.localRotation = Quaternion.Euler(34f, 0f, 0f);

        var sh = Shader.Find("Universal Render Pipeline/Unlit")
              ?? Shader.Find("Unlit/Color")
              ?? Shader.Find("Sprites/Default");

        var goldMat = new Material(sh);
        Color goldCol = new Color(1f, 0.88f, 0.18f, 1f);
        if (goldMat.HasProperty("_BaseColor")) goldMat.SetColor("_BaseColor", goldCol);
        goldMat.color = goldCol;

        var whiteMat = new Material(sh);
        Color whiteCol = new Color(1f, 0.99f, 0.85f, 1f);
        if (whiteMat.HasProperty("_BaseColor")) whiteMat.SetColor("_BaseColor", whiteCol);
        whiteMat.color = whiteCol;

        // Bold outer 2.2-turn golden spiral
        var lr = root.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = goldMat;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.startWidth = S * 0.11f;
        lr.endWidth = S * 0.045f;
        lr.startColor = goldCol;
        lr.endColor = new Color(1f, 0.72f, 0.08f, 1f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        const int pts = 48;
        lr.positionCount = pts;
        for (int i = 0; i < pts; i++)
        {
            float u = i / (float)(pts - 1);
            float ang = u * Mathf.PI * 4.4f; // ~2.2 spiral turns
            float rad = Mathf.Lerp(S * 0.10f, S * 0.56f, u);
            float y = u * S * 0.16f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad));
        }

        // Crisp inner highlight spiral (counter-offset for bold cartoon readability)
        var innerGo = new GameObject("InnerSpiral") { layer = layer };
        innerGo.transform.SetParent(root.transform, false);
        var lr2 = innerGo.AddComponent<LineRenderer>();
        lr2.useWorldSpace = false;
        lr2.material = whiteMat;
        lr2.numCapVertices = 4;
        lr2.startWidth = S * 0.055f;
        lr2.endWidth = S * 0.025f;
        lr2.startColor = whiteCol;
        lr2.endColor = goldCol;
        lr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr2.receiveShadows = false;
        lr2.positionCount = pts;
        for (int i = 0; i < pts; i++)
        {
            float u = i / (float)(pts - 1);
            float ang = u * Mathf.PI * 4.4f + Mathf.PI;
            float rad = Mathf.Lerp(S * 0.08f, S * 0.46f, u);
            float y = u * S * 0.16f + S * 0.03f;
            lr2.SetPosition(i, new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad));
        }

        // 4 bold orbiting dizzy spheres around the spiral rim
        for (int i = 0; i < 4; i++)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.layer = layer;
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(root.transform, false);
            float a = i * (Mathf.PI * 2f / 4f);
            dot.transform.localPosition = new Vector3(Mathf.Cos(a) * S * 0.54f, S * 0.12f, Mathf.Sin(a) * S * 0.54f);
            dot.transform.localScale = Vector3.one * S * 0.15f;
            var r = dot.GetComponent<Renderer>();
            r.sharedMaterial = (i % 2 == 0) ? goldMat : whiteMat;
        }
        return root;
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

        // How To Play button exists in MainMenu scene directly so user can edit visuals/transform in the Inspector
        var htpTr = safe ? safe.Find("HowToPlay") : null;
        if (htpTr)
        {
            var htpBtn = htpTr.GetComponent<Button>();
            if (htpBtn)
            {
                htpBtn.onClick.RemoveListener(StartTutorialGame);
                htpBtn.onClick.AddListener(StartTutorialGame);
            }
            var htpLblTr = htpTr.Find("HTPLabel");
            if (htpLblTr)
            {
                var lblBtn = htpLblTr.GetComponent<Button>();
                if (lblBtn)
                {
                    lblBtn.onClick.RemoveAllListeners();
                    lblBtn.onClick.AddListener(() =>
                    {
                        if (htpBtn) htpBtn.onClick.Invoke();
                        else StartTutorialGame();
                    });
                }
            }
        }

        if (TCAssets.I.logo)
        {
            var logo = TCUI.Img("Logo", safe, Color.white, TCAssets.I.logo); logo.preserveAspect = true; TCUI.Place(logo.rectTransform, new Vector2(0.5f, 0.83f), Vector2.zero, new Vector2(760, 428)); // cute & awesome 3D title logo
        }
        else { var t = TCUI.Label("Title", safe, "TALL CRAWL", 120, TCUI.Cocoa); TCUI.Place(t.rectTransform, new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(1000, 200)); }

        previewImg = TCUI.Add<RawImage>(TCUI.Rect("Preview", safe).gameObject);
        previewImg.texture = rt; previewImg.raycastTarget = false; TCUI.Place(previewImg.rectTransform, new Vector2(0.5f, 0.50f), Vector2.zero, new Vector2(740, 740));
        previewImg.gameObject.SetActive(false);

        var bestPill = TCUI.Img("Best", safe, new Color(1, 1, 1, 0.85f)); TCUI.Place(bestPill.rectTransform, new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(480, 84));
        bestText = TCUI.Label("Value", bestPill.transform, "", 44, TCUI.Cocoa); TCUI.Stretch(bestText.rectTransform);

        var play = TCUI.Btn("Play", safe, "PLAY", TCUI.Mint, () => StartCoroutine(LoadGame()), 96, TCUI.Icon("play")); // no game-start sound
        TCUI.Place((RectTransform)play.transform, new Vector2(0.5f, 0), new Vector2(0, 310), new Vector2(640, 190));
        { var glow = TCUI.Img("Glow", safe, new Color(TCUI.CreamLight.r, TCUI.CreamLight.g, TCUI.CreamLight.b, 0.55f), TCUI.Circle); glow.raycastTarget = false;
          TCUI.Place(glow.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 310), new Vector2(820, 290)); if (TCUI.Fresh(glow)) glow.transform.SetSiblingIndex(play.transform.GetSiblingIndex() - 1); }

        SmallBtn("Skins", "SKINS", TCUI.Blush, "shirt", new Vector2(-330, 110), () => { RefreshSkinGrid(); TCUI.Show(this, skinsPanel, pop: true, animateContents: false); });
        SmallBtn("Shop", "SHOP", TCUI.Peach, "bag", new Vector2(0, 110), () => { RefreshShopPrices(); TCUI.Show(this, shopPanel, pop: true, animateContents: false); });
        var daily = SmallBtn("Daily", "DAILY", TCUI.Sky, "gift", new Vector2(330, 110), OpenDaily);
        dailyBadge = TCUI.Img("Badge", daily.transform, TCUI.Crimson, TCUI.Circle); TCUI.Place(dailyBadge.rectTransform, new Vector2(1, 1), new Vector2(-10, -10), new Vector2(46, 46));
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
    IEnumerator Glow(Image g) { while (g) { float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f); g.color = new Color(TCUI.CreamLight.r, TCUI.CreamLight.g, TCUI.CreamLight.b, 0.25f + 0.35f * k); g.transform.localScale = Vector3.one * (0.95f + 0.1f * k); yield return null; } }
    IEnumerator Breathe(Transform t) { yield return new WaitForSecondsRealtime(1.6f); while (t) { float k = Mathf.Sin(Time.unscaledTime * 3f) * 0.04f; t.localScale = new Vector3(1 + k, 1 - k * 0.6f, 1); yield return null; } }
    IEnumerator Pulse(Transform t) { var ic = t.Find("Icon"); while (ic) { ic.localScale = Vector3.one * (1 + 0.12f * Mathf.Sin(Time.unscaledTime * 5)); yield return null; } }
    bool loadingGame;
    void StartTutorialGame()
    {
        TCSave.TutorialRequested = true;
        StartCoroutine(LoadGame());
    }
    IEnumerator LoadGame()
    {
        if (loadingGame) yield break;
        loadingGame = true;
        var fade = TCUI.Rect("Fade", canvas.transform); TCUI.Stretch(fade); var img = fade.gameObject.AddComponent<Image>(); img.color = new Color(TCUI.Cream.r, TCUI.Cream.g, TCUI.Cream.b, 0);
        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime) { img.color = new Color(TCUI.Cream.r, TCUI.Cream.g, TCUI.Cream.b, t / 0.3f); yield return null; }
        SceneManager.LoadScene("gameplay");
    }

    // ---------- Skins ----------
    [Header("Skins")]
    public SpiderSkinDatabase skinDatabase;
    public GameObject spiderSkinListObjPrefab;
    public RectTransform skinsListParent;
    public ScrollRect skinsScrollRect;
    public Scrollbar skinsScrollbar;
    readonly List<(Image card, TextMeshProUGUI label, Image lockIc, Image check, int idx)> skinCards = new List<(Image, TextMeshProUGUI, Image, Image, int)>();
    TextMeshProUGUI skinName; Button skinAction; TextMeshProUGUI skinActionText; int selectedCard;
    void BuildSkins()
    {
        skinsPanel = TCUI.Panel("Skins", safe, out var card, new Vector2(980, 1500), "SKINS");
        skinPreviewImg = TCUI.Add<RawImage>(TCUI.Rect("Preview", card).gameObject);
        skinPreviewImg.texture = rt; skinPreviewImg.raycastTarget = false; TCUI.Place(skinPreviewImg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(460, 460));
        skinPreviewImg.rectTransform.localScale = new Vector3(3f, 3f, 3f);
        skinName = TCUI.Label("Name", card, "", 56, TCUI.Cocoa); TCUI.Place(skinName.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -580), new Vector2(800, 80));

        // Locate existing hierarchy if already configured in scene/inspector
        if (!skinsListParent && card)
        {
            var found = card.Find("SkinsScrollView/Viewport/SkinsList")
                     ?? card.Find("Viewport/SkinsList")
                     ?? card.Find("SkinsList");
            if (found) skinsListParent = (RectTransform)found;
        }

        if (!skinsScrollRect && card)
        {
            skinsScrollRect = card.GetComponentInChildren<ScrollRect>(true);
        }

        if (!skinsScrollbar && card)
        {
            skinsScrollbar = card.GetComponentInChildren<Scrollbar>(true);
        }

        foreach (var c in skinCards)
        {
            if (c.card && c.card.gameObject)
            {
                if (Application.isPlaying) Destroy(c.card.gameObject);
                else DestroyImmediate(c.card.gameObject);
            }
        }
        skinCards.Clear();

        if (!TCUI.Baking)
        {
            var db = skinDatabase ? skinDatabase : SpiderSkinDatabase.Instance;
            var prefab = spiderSkinListObjPrefab;
            if (!prefab && db) prefab = db.spiderSkinListObjPrefab;
            if (!prefab && TCAssets.I) prefab = TCAssets.I.spiderSkinListObj;
            if (!prefab) prefab = Resources.Load<GameObject>("spiderSkinListObj");

            var skins = db != null && db.skins != null && db.skins.Length > 0 ? db.skins : TCSkins.All;
            var parent = skinsListParent ? skinsListParent : card;
            var order = TCSkins.DisplayOrder(skins);

            for (int n = 0; n < order.Length; n++)
            {
                int i = order[n];
                int idx = i; var def = skins[i];

                GameObject itemObj;
                if (prefab)
                {
                    itemObj = Instantiate(prefab, parent);
                }
                else
                {
                    var bGo = TCUI.Btn("Skin" + i, parent, "", def.body, null, 30);
                    itemObj = bGo.gameObject;
                }
                itemObj.name = "Skin" + i;

                var listObj = itemObj.GetComponent<SpiderSkinListObj>();
                if (listObj)
                {
                    listObj.Setup(idx, def, () => { selectedCard = idx; ApplyPreview(idx); RefreshSkinGrid(); });
                    skinCards.Add((listObj.cardImage, listObj.priceLabel, listObj.lockIcon, listObj.checkIcon, idx));
                }
                else
                {
                    var b = itemObj.GetComponent<Button>();
                    if (!b) b = itemObj.AddComponent<Button>();
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(() => { TCAudio.Click(); selectedCard = idx; ApplyPreview(idx); RefreshSkinGrid(); });

                    var cardImg = itemObj.GetComponent<Image>();
                    var accent = itemObj.transform.Find("Accent")?.GetComponent<Image>();
                    var lbl = itemObj.transform.Find("Price")?.GetComponent<TextMeshProUGUI>();
                    var lk = itemObj.transform.Find("Lock")?.GetComponent<Image>();
                    var ck = itemObj.transform.Find("Check")?.GetComponent<Image>();

                    if (lbl) lbl.color = Color.black;
                    if (accent) accent.color = def.accent;
                    if (cardImg) cardImg.color = def.body;
                    skinCards.Add((cardImg, lbl, lk, ck, idx));
                }
            }
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
        var db = skinDatabase ? skinDatabase : SpiderSkinDatabase.Instance;
        var skins = db != null && db.skins != null && db.skins.Length > 0 ? db.skins : TCSkins.All;

        foreach (var c in skinCards)
        {
            if (c.idx >= skins.Length) continue;
            bool owned = TCSave.SkinOwned(c.idx); var def = skins[c.idx];
            if (c.lockIc) c.lockIc.gameObject.SetActive(!owned);
            if (c.check) { c.check.gameObject.SetActive(TCSave.SelectedSkin == c.idx); c.check.color = TCUI.Crimson; }
            if (c.label)
            {
                c.label.color = Color.black;
                if (owned)
                {
                    c.label.text = "";
                }
                else
                {
                    c.label.text = def.unlock switch
                    {
                        SkinUnlock.Coins => def.amount.ToString(),
                        SkinUnlock.Ads => "AD " + TCSave.SkinAdsWatched(c.idx) + "/" + def.amount,
                        SkinUnlock.BestScore => def.amount + "M",
                        SkinUnlock.Runs => def.amount + " runs",
                        SkinUnlock.KillBees => Mathf.Min(TCSave.BeesKnocked, def.amount) + "/" + def.amount + " knocked",
                        SkinUnlock.KillWasps => Mathf.Min(TCSave.WaspsKnocked, def.amount) + "/" + def.amount + " knocked",
                        SkinUnlock.KillSquirrels => Mathf.Min(TCSave.SquirrelsKnocked, def.amount) + "/" + def.amount + " knocked",
                        SkinUnlock.KnockBats => Mathf.Min(TCSave.BatsKnocked, def.amount) + "/" + def.amount + " knocked",
                        SkinUnlock.KnockSnakes => Mathf.Min(TCSave.SnakesKnocked, def.amount) + "/" + def.amount + " knocked",
                        _ => "Free"
                    };
                }
            }
            if (c.card) c.card.color = c.idx == selectedCard ? Color.Lerp(skins[c.idx].body, Color.white, 0.35f) : skins[c.idx].body;
        }
        if (selectedCard >= 0 && selectedCard < skins.Length)
        {
            var d = skins[selectedCard];
            if (skinName) skinName.text = d.name;
            bool own = TCSave.SkinOwned(selectedCard);
            if (skinAction)
            {
                skinAction.interactable = true;
                if (own) skinActionText.text = TCSave.SelectedSkin == selectedCard ? "EQUIPPED" : "SELECT";
                else if (d.unlock == SkinUnlock.Coins) { skinActionText.text = "BUY " + d.amount; skinAction.interactable = true; }
                else if (d.unlock == SkinUnlock.Ads) skinActionText.text = "WATCH AD (" + TCSave.SkinAdsWatched(selectedCard) + "/" + d.amount + ")";
                else { skinActionText.text = d.UnlockText; skinAction.interactable = false; }
            }
        }
    }
    void SkinAction()
    {
        var db = skinDatabase ? skinDatabase : SpiderSkinDatabase.Instance;
        var skins = db != null && db.skins != null && db.skins.Length > 0 ? db.skins : TCSkins.All;
        if (selectedCard < 0 || selectedCard >= skins.Length) return;
        int i = selectedCard; var d = skins[i];
        if (TCSave.SkinOwned(i)) { TCSave.SelectedSkin = i; Toast(d.name + " equipped!"); TCServices.Log("skin_equip", ("skin_id", i), ("skin_name", d.name)); }
        else if (d.unlock == SkinUnlock.Coins)
        {
            if (TCSave.Coins >= d.amount)
            {
                TCSave.Coins -= d.amount; TCSave.UnlockSkin(i); TCSave.SelectedSkin = i;
                TCAudio.Play(TCAssets.I.purchase); Toast("Unlocked " + d.name + "!"); TCServices.Log("skin_buy", "skin", i);
            }
            else
            {
                Toast("Insufficient coins");
                if (skinAction) StartCoroutine(TCUI.Punch(skinAction.transform, 0.3f, 0.22f));
            }
        }
        else if (d.unlock == SkinUnlock.Ads) TCServices.Rewarded(() => { TCSave.AddSkinAd(i); TCSkins.CheckAchievements(); if (TCSave.SkinOwned(i)) { TCSave.SelectedSkin = i; Toast("Unlocked " + d.name + "!"); TCServices.Log("skin_ad_unlock", ("skin_id", i), ("skin_name", d.name)); } RefreshSkinGrid(); }, "skin");
        RefreshSkinGrid();
    }

    // ---------- Shop ----------
    static readonly int[] activeUpgradeIds = { 1, 2, 5, 6 };
    static readonly string[] upgNames = { "", "Boost Time", "Extra Heart", "Climb Bonus", "Head Start", "Freeze Powerup", "Coin Magnet" };
    static readonly int[] upgMax = { 0, 5, 2, 0, 0, 5, 5 };
    public CanvasGroup GetPanel(string n) => n == "Skins" ? skinsPanel : n == "Shop" ? shopPanel : n == "Settings" ? settingsPanel : n == "Daily" ? dailyPanel : consentPanel;

    static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        if (!rt) return;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void SetStretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
    {
        if (!rt) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    static readonly string[] packFallbackPrices = { "$0.29", "$2.99", "$4.99" };
    static readonly string[] gemFallbackPrices = { "$0.50", "$2.49", "$4.99", "$8.99" };

    void BuildShop()
    {
        shopPanel = TCUI.Panel("Shop", safe, out var card, new Vector2(980, 1680), "SHOP");

        // Hide obsolete upgrade rows if present in baked scene
        var oldUpg3 = card.Find("Upg3"); if (oldUpg3) oldUpg3.gameObject.SetActive(false);
        var oldUpg4 = card.Find("Upg4"); if (oldUpg4) oldUpg4.gameObject.SetActive(false);

        // Upgrades Section
        for (int i = 0; i < activeUpgradeIds.Length; i++)
        {
            int id = activeUpgradeIds[i];
            int u = id;
            var row = card.Find("Upg" + id);
            if (!row) continue;

            var name = row.Find("N")?.GetComponent<TextMeshProUGUI>();
            if (name) name.text = upgNames[id];

            var pips = row.Find("P")?.GetComponent<TextMeshProUGUI>();
            var buy = row.Find("Buy")?.GetComponent<Button>();
            var bt = buy ? buy.GetComponentInChildren<TextMeshProUGUI>() : null;

            if (buy)
            {
                buy.onClick.RemoveAllListeners();
                buy.onClick.AddListener(() =>
                {
                    TCAudio.Click();
                    int lvl = TCSave.Upgrade(u); if (lvl >= upgMax[u]) return; int cost = TCConfig.I.upgradeCosts[Mathf.Min(lvl, TCConfig.I.upgradeCosts.Length - 1)];
                    if (TCSave.Gems < cost) { Toast("Not enough gems"); return; }
                    TCSave.Gems -= cost; TCSave.SetUpgrade(u, lvl + 1); TCAudio.Play(TCAssets.I.purchase); Toast(upgNames[u] + " upgraded!"); TCServices.Log("upgrade", "id", u);
                });
            }

            refreshers.Add(() =>
            {
                int lvl = TCSave.Upgrade(u);
                if (pips) pips.text = "Level " + lvl + " / " + upgMax[u];
                bool max = lvl >= upgMax[u];
                int cost = TCConfig.I.upgradeCosts[Mathf.Min(lvl, TCConfig.I.upgradeCosts.Length - 1)];
                if (bt) bt.text = max ? "MAX" : cost.ToString();
                if (buy) buy.interactable = !max && TCSave.Gems >= cost;
            });
        }

        // Gems Section
        for (int g = 0; g < UnityIAP.GemProductIDs.Length; g++)
        {
            int pack = g, gems = UnityIAP.GemPackAmounts[g];
            var gb = card.Find("Gems" + g)?.GetComponent<Button>();
            if (!gb) continue;

            gb.onClick.RemoveAllListeners();
            gb.onClick.AddListener(() =>
            {
                TCAudio.Click();
                TCServices.BuyGems(pack, () =>
                {
                    TCSave.Gems += gems; TCAudio.Play(TCAssets.I.purchase); Toast("+" + gems + " gems!"); TCServices.Log("iap_gems", "gems", gems);
                });
            });
        }

        // Coin Packs & More Section
        Pack(card, 0, () => TCSave.Coins += 1000);
        Pack(card, 1, () => { TCSave.NoAds = true; TCSave.Gems += 20; TCServices.Banner(false); });
        Pack(card, 2, () => { TCSave.Coins += 5000; TCSave.Gems += 50; });

        var free = card.Find("FreeCoins")?.GetComponent<Button>();
        if (free)
        {
            free.onClick.RemoveAllListeners();
            free.onClick.AddListener(() =>
            {
                TCAudio.Click();
                int adCoins = TCConfig.I.adCoinReward;
                TCServices.Rewarded(() => { TCSave.Coins += adCoins; Toast("+" + adCoins + " coins"); }, "shop_coins");
            });
        }

        // Bottom Actions (Restore & Close)
        var restore = card.Find("Restore")?.GetComponent<Button>();
        if (restore)
        {
            restore.onClick.RemoveAllListeners();
            restore.onClick.AddListener(() => { TCAudio.Click(); OnRestoreClicked(); });
        }

        var close = card.Find("Close")?.GetComponent<Button>();
        if (close)
        {
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(() => { TCAudio.Click(); TCUI.Hide(this, shopPanel); });
        }

        RefreshShopPrices();
    }

    public void RefreshShopPrices()
    {
        if (shopPanel == null) return;
        var card = shopPanel.transform.Find("Card");
        if (!card) return;

        // Gems 0..3
        for (int g = 0; g < UnityIAP.GemProductIDs.Length; g++)
        {
            var gb = card.Find("Gems" + g);
            if (!gb) continue;
            var pt = gb.Find("PriceBanner/PriceText")?.GetComponent<TextMeshProUGUI>();
            if (!pt) pt = gb.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
            if (pt)
            {
                var lp = UnityIAP.Instance ? UnityIAP.Instance.LocalizedPrice(UnityIAP.GemProductIDs[g]) : null;
                pt.text = !string.IsNullOrEmpty(lp) ? lp : (g < gemFallbackPrices.Length ? gemFallbackPrices[g] : pt.text);
            }
        }

        // Coin Packs 0..2
        for (int p = 0; p < 3; p++)
        {
            var pb = card.Find("Pack" + p);
            if (!pb) continue;
            var pt = pb.Find("PriceBanner/PriceText")?.GetComponent<TextMeshProUGUI>();
            if (!pt) pt = pb.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
            if (pt)
            {
                var lp = (UnityIAP.Instance != null && UnityIAP.Instance.kProductIDs != null && p < UnityIAP.Instance.kProductIDs.Length)
                    ? UnityIAP.Instance.LocalizedPrice(UnityIAP.Instance.kProductIDs[p]) : null;
                pt.text = !string.IsNullOrEmpty(lp) ? lp : (p < packFallbackPrices.Length ? packFallbackPrices[p] : pt.text);
            }
        }
    }

    void OnRestoreClicked()
    {
        TCUI.Confirm(
            this,
            safe,
            "RESTORE PURCHASES",
            "Do you want to restore previously purchased items?",
            "RESTORE",
            () =>
            {
                try
                {
                    UnityIAP.Instance?.RestorePurchases();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("RestorePurchases failed: " + ex.Message);
                }
                Toast("Restoring purchases...");
            },
            "CANCEL",
            null
        );
    }

    void Pack(RectTransform card, int productIndex, System.Action grant)
    {
        var b = card.Find("Pack" + productIndex)?.GetComponent<Button>();
        if (!b) return;

        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() =>
        {
            TCAudio.Click();
            TCServices.Buy(productIndex, () =>
            {
                grant(); TCAudio.Play(TCAssets.I.purchase); Toast("Purchase complete!"); TCServices.Log("iap_purchase", "index", productIndex);
            });
        });
    }

    // ---------- Settings ----------
    void BuildSettings()
    {
        settingsPanel = TCUI.Panel("Settings", safe, out var card, new Vector2(900, 960), "SETTINGS");
        TCGameUI.Toggle(card, "Sound", 335, () => TCSave.Sound, v => { TCSave.Sound = v; if (FeedCheat(TCCheatCode.Kind.Sound)) ActivateCheat(); }, 700);
        TCGameUI.Toggle(card, "Music", 230, () => TCSave.Music, v => { TCSave.Music = v; ApplyMenuMusic(); FeedCheat(TCCheatCode.Kind.Music); }, 700);
        TCGameUI.Toggle(card, "Vibration", 125, () => TCSave.Vibration, v => { TCSave.Vibration = v; if (FeedCheat(TCCheatCode.Kind.Vibration)) ActivateCheat(); }, 700);
        var sl = TCUI.Label("SensL", card, "Control sensitivity", 44, TCUI.Cocoa, TextAlignmentOptions.Left); TCUI.Place(sl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(700, 70));
        var slider = BuildSlider(card, new Vector2(0, -45)); slider.minValue = 0.4f; slider.maxValue = 2.2f; slider.value = TCSave.Sensitivity;
        slider.onValueChanged.RemoveAllListeners();
        System.Action<float> feedSlider = v =>
        {
            TCSave.Sensitivity = v;
            if (FeedCheat(TCCheatCode.ClassifySlider(v, slider.minValue, slider.maxValue))) ActivateCheat();
        };
        slider.onValueChanged.AddListener(v => feedSlider(v));
        // Seed the current extreme on finger-down so a continuous sweep counts
        // even if the knob was already parked at an end (no onValueChanged yet).
        var hook = slider.GetComponent<TCCheatSliderHook>() ?? slider.gameObject.AddComponent<TCCheatSliderHook>();
        hook.Bind(slider, feedSlider);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!card.Find("CheatHint"))
        {
            cheatHint = TCUI.Label("CheatHint", card, "", 22, new Color(TCUI.Brown.r, TCUI.Brown.g, TCUI.Brown.b, 0.45f));
            TCUI.Place(cheatHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -105), new Vector2(700, 32));
            cheatHint.raycastTarget = false;
        }
        else cheatHint = card.Find("CheatHint").GetComponent<TextMeshProUGUI>();
#endif
        TCUI.Place((RectTransform)TCUI.Btn("Privacy", card, "PRIVACY POLICY", TCUI.Sky, () => { FeedCheat(TCCheatCode.Kind.Other); Application.OpenURL(TCAssets.I.privacyUrl); }, 42).transform, new Vector2(0.5f, 0.5f), new Vector2(0, -175), new Vector2(620, 110));
        var tutBtn = card.Find("Tutorial");
        if (tutBtn) tutBtn.gameObject.SetActive(false);
        TCUI.Place((RectTransform)TCUI.Btn("Close", card, "DONE", TCUI.Mint, () => { FeedCheat(TCCheatCode.Kind.Other); TCUI.Hide(this, settingsPanel); }, 50).transform, new Vector2(0.5f, 0), new Vector2(0, 95), new Vector2(420, 120));
    }
    Slider BuildSlider(RectTransform parent, Vector2 pos)
    {
        var root = TCUI.Rect("Slider", parent); TCUI.Place(root, new Vector2(0.5f, 0.5f), pos, new Vector2(700, 60));
        var bg = TCUI.Img("BG", root, TCUI.Grey); TCUI.Stretch(bg.rectTransform, 0, 0, 18, 18);
        if (bg) bg.raycastTarget = true;
        var fillArea = TCUI.Rect("FillArea", root); TCUI.Stretch(fillArea, 0, 0, 18, 18);
        var fill = TCUI.Img("Fill", fillArea, TCUI.Mint); TCUI.Stretch(fill.rectTransform);
        var handleArea = TCUI.Rect("HandleArea", root); TCUI.Stretch(handleArea, 30, 30, 0, 0);
        var handle = TCUI.Img("Handle", handleArea, TCUI.Peach, TCUI.Circle);
        if (TCUI.Fresh(handle)) handle.rectTransform.sizeDelta = new Vector2(70, 70);
        if (handle) handle.raycastTarget = true;
        var s = TCUI.Add<Slider>(root.gameObject); s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
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
            var v = TCUI.Label("V", c.transform, r[i].ToString(), 40, TCUI.Cocoa); TCUI.Place(v.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(190, 50)); v.text = r[i].ToString();
            dailyCells.Add(c);
        }
        claimBtn = TCUI.Btn("Claim", card, "CLAIM", TCUI.Mint, () => ClaimDaily(false), 56);
        claimBtnText = claimBtn.GetComponentInChildren<TextMeshProUGUI>();
        TCUI.Place((RectTransform)claimBtn.transform, new Vector2(0.5f, 0), new Vector2(-160, 260), new Vector2(380, 140));
        dblBtn = TCUI.Btn("Double", card, "x2 (AD)", TCUI.Butter, () => TCServices.Rewarded(() => ClaimDaily(true), "daily", () => Toast("Ad is still loading. Please try again in a moment!")), 48, TCUI.Icon("play"));
        dblBtnText = dblBtn.GetComponentInChildren<TextMeshProUGUI>();
        TCUI.Place((RectTransform)dblBtn.transform, new Vector2(0.5f, 0), new Vector2(230, 260), new Vector2(340, 140));
        TCUI.Place((RectTransform)TCUI.Btn("Close", card, "LATER", TCUI.Grey, () => TCUI.Hide(this, dailyPanel), 42).transform, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(300, 100));
        refreshers.Add(() =>
        {
            bool avail = TCSave.DailyAvailable(out int day);
            for (int i = 0; i < 7; i++)
            {
                // polish3: claimed = tan, today's reward = crimson highlight, upcoming = cream; labels stay readable
                var face = i < day ? TCUI.Tan : i == day && avail ? TCUI.Crimson : TCUI.CreamLight;
                dailyCells[i].color = face; TCTheme.TextOnFace(dailyCells[i].transform, face);
                foreach (var o in dailyCells[i].GetComponents<UnityEngine.UI.Outline>()) o.effectColor = i == day && avail ? TCUI.CrimsonDark : TCUI.Tan;
            }
            var crt = (RectTransform)claimBtn.transform;
            if (avail)
            {
                bool canDouble = TCServices.IsRewardedReady();
                dblBtn.gameObject.SetActive(canDouble);
                dblBtn.interactable = canDouble;
                crt.anchoredPosition = new Vector2(canDouble ? -160 : 0, 260);
                claimBtn.interactable = true;
                if (claimBtnText) claimBtnText.text = "CLAIM";
                if (dblBtnText) dblBtnText.text = "x2 (AD)";
                TCTheme.SetShine(claimBtn, true);
            }
            else
            {
                dblBtn.gameObject.SetActive(false);
                crt.anchoredPosition = new Vector2(0, 260);
                claimBtn.interactable = false;
                if (claimBtnText) claimBtnText.text = "CLAIMED";
                TCTheme.SetShine(claimBtn, false);
            }
        });
    }
    void OpenDaily() { Refresh(); TCUI.Show(this, dailyPanel); }
    void ClaimDaily(bool doubled)
    {
        if (!TCSave.DailyAvailable(out int day)) return;
        claimBtn.interactable = false;
        if (dblBtn) dblBtn.interactable = false;
        TCTheme.SetShine(claimBtn, false);
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
        TCUI.Place((RectTransform)TCUI.Btn("Accept", card, "ACCEPT", TCUI.Mint, () => { PlayerPrefs.SetInt("consent", 1); TCUI.Hide(this, consentPanel); TCServices.Banner(true); }, 56).transform, new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(560, 140));
        TCUI.Place((RectTransform)TCUI.Btn("Privacy", card, "PRIVACY POLICY", TCUI.Sky, () => Application.OpenURL(TCAssets.I.privacyUrl), 40).transform, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(560, 110));
    }

    // ---------- Toast ----------
    void BuildToast()
    {
        var r = TCUI.Img("Toast", canvas.transform, TCUI.Cocoa); TCUI.Place(r.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(760, 110));
        toastGroup = TCUI.Add<CanvasGroup>(r.gameObject); toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        toastText = TCUI.Label("T", r.transform, "", 44, TCUI.Cream); TCUI.Stretch(toastText.rectTransform);
    }
    Coroutine toastCo;
    public void ShowToast(string msg, bool flashy = false) { Toast(msg, flashy); }
    void Toast(string msg) { Toast(msg, false); }
    void Toast(string msg, bool flashy)
    {
        if (toastCo != null) StopCoroutine(toastCo);
        toastCo = StartCoroutine(ToastCo(msg, flashy));
    }
    IEnumerator ToastCo(string msg, bool flashy = false)
    {
        toastText.text = msg;
        toastText.margin = new Vector4(22, 6, 22, 6); toastText.enableAutoSizing = true; toastText.fontSizeMin = 16; toastText.fontSizeMax = Mathf.Max(toastText.fontSizeMax, 26);
        toastGroup.transform.SetAsLastSibling();
        var img = toastGroup.GetComponent<Image>();
        var original = img ? img.color : TCUI.Cocoa;
        if (img && flashy) img.color = TCUI.Crimson;
        toastText.color = TCUI.Cream;
        if (flashy) StartCoroutine(TCUI.Punch(toastGroup.transform, 0.45f, 0.4f));
        if (flashy) TCSfx.Play("celebrate", 0.9f);
        for (float t = 0; t < 0.2f; t += Time.unscaledDeltaTime) { toastGroup.alpha = t / 0.2f; yield return null; }
        toastGroup.alpha = 1;
        yield return new WaitForSecondsRealtime(flashy ? 2.2f : 1.4f);
        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime) { toastGroup.alpha = 1 - t / 0.3f; yield return null; }
        toastGroup.alpha = 0;
        if (img) img.color = original;
        toastText.color = TCUI.Cream;
    }

    bool FeedCheat(TCCheatCode.Kind kind)
    {
        bool done = cheat.Feed(kind, Time.unscaledTime);
        UpdateCheatHint();
        return done;
    }

    void UpdateCheatHint()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!cheatHint) return;
        if (cheat.Progress <= 0) { cheatHint.text = ""; return; }
        // Subtle dots only — never shown in release builds.
        cheatHint.text = new string('·', cheat.Progress);
#endif
    }

    public void ActivateCheat()
    {
        CheatActivated = true;
        TCCheatCode.Grant();
        Refresh();
        RefreshSkinGrid();
        Toast("Cheat code activated!", true);
    }

    /// Used by the real-event playthrough to run a second case on the same menu.
    public void ClearCheatActivated() { CheatActivated = false; }

    void HookPreviewTap(RawImage img)
    {
        if (!img) return;
        img.raycastTarget = true;
        var tap = img.GetComponent<TCPreviewTap>();
        if (!tap) tap = img.gameObject.AddComponent<TCPreviewTap>();
        tap.onTap = OnPreviewTapped;
    }

    void OnPreviewTapped()
    {
        if (entering || !previewSpider) return;
        TCServices.LightHaptic();
        var cs = previewSpider.GetComponent<TCCuteSpider>();
        if (cs) cs.PlayRandomTouch();
    }
}

public class TCPreviewTap : MonoBehaviour, IPointerClickHandler
{
    public System.Action onTap;
    public void OnPointerClick(PointerEventData e) { onTap?.Invoke(); }
}
