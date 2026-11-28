using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SillyCon company logo splash screen.
/// 1. White background covers the screen immediately with zero artifacts.
/// 2. Logo sweeps into the scene with a painterly brushed-in effect.
/// 3. Logo settles and smiles in full clarity over the clean white background.
/// 4. Directional brush stroke sweeps across and brushes off the PNG.
/// 5. White background smoothly fades out into the main menu.
/// </summary>
public class TCSplashScreen : MonoBehaviour
{
    public static bool HasPlayedThisSession { get; private set; }

    [Header("Timings (seconds)")]
    public float brushInDuration = 0.85f;
    public float waitAfterBrushIn = 0.30f;
    public float rattleDuration = 0.20f;
    public float waitAfterRattle = 0.50f;
    public float brushOutDuration = 0.55f;
    public float fadeOutDuration = 0.40f;

    Material splashMat;
    CanvasGroup canvasGroup;
    RectTransform logoRect;
    RawImage logoImage;
    bool skipped;

    public static IEnumerator Play(Transform parent, Action onComplete = null)
    {
        var splashObj = new GameObject("TC_SplashScreen", typeof(RectTransform), typeof(CanvasGroup));
        splashObj.transform.SetParent(parent, false);
        var rt = splashObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        splashObj.transform.SetAsLastSibling();

        var splash = splashObj.AddComponent<TCSplashScreen>();
        // Failsafe: if anything ever stalls the splash coroutine, force-close after 5s
        splash.StartCoroutine(splash.FailsafeTimeout(5f));
        yield return splash.RunRoutine();
        onComplete?.Invoke();
    }

    IEnumerator FailsafeTimeout(float maxSeconds)
    {
        yield return new WaitForSecondsRealtime(maxSeconds);
        if (this && gameObject)
        {
            if (splashMat) Destroy(splashMat);
            Destroy(gameObject);
        }
    }

    IEnumerator RunRoutine()
    {
        HasPlayedThisSession = true;

        canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        // 1. Pure white background (immediately covers full screen, blocks touches)
        var bgObj = new GameObject("WhiteBG", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(transform, false);
        var bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var bgImg = bgObj.GetComponent<Image>();
        bgImg.color = Color.white;
        bgImg.raycastTarget = true;

        // Tap to fast-forward
        var tapBtn = bgObj.AddComponent<Button>();
        tapBtn.transition = Selectable.Transition.None;
        tapBtn.onClick.AddListener(() => skipped = true);

        // 2. Company logo (RawImage ensures pixel-perfect UVs 0..1 for procedural shader)
        Texture2D logoTex = Resources.Load<Texture2D>("TCUI/sillycon_logo");
        if (!logoTex) logoTex = Resources.Load<Texture2D>("sillycon_logo");

        var logoObj = new GameObject("SillyConLogo", typeof(RectTransform), typeof(RawImage));
        logoObj.transform.SetParent(transform, false);
        logoRect = logoObj.GetComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0.5f, 0.5f);
        logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = new Vector2(0, 30);
        logoRect.sizeDelta = new Vector2(530, 647); // 839x1024 aspect ratio

        logoImage = logoObj.GetComponent<RawImage>();
        if (logoTex) logoImage.texture = logoTex;
        logoImage.color = Color.clear; // Strictly transparent at first
        logoImage.raycastTarget = false;
        logoObj.SetActive(false); // Strictly hidden at first

        // 3. Brushed in / brushed out material (guaranteed 0 artifacts at start)
        var baseMat = Resources.Load<Material>("Materials/M_SplashInkSpread");
        var shader = baseMat ? baseMat.shader : Shader.Find("TallCrawl/SplashInkSpread");
        if (shader)
        {
            splashMat = new Material(shader);
            if (logoTex) splashMat.mainTexture = logoTex;
            splashMat.SetFloat("_BrushIn", 0f);
            splashMat.SetFloat("_BrushOut", 0f);
            splashMat.SetFloat("_EdgeSoftness", 0.14f);
            logoImage.material = splashMat;
            logoImage.SetMaterialDirty();
        }

        Vector2 baseLogoPos = logoRect.anchoredPosition;

        // --- Phase 0: Pure Clean White Screen (logo strictly hidden at first) ---
        float initWait = 0.20f;
        while (initWait > 0f && !skipped)
        {
            initWait -= Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.033f);
            yield return null;
        }

        // Reveal logo object ready for brush-in sweep
        logoObj.SetActive(true);
        logoImage.color = Color.white;
        if (splashMat)
        {
            splashMat.SetFloat("_BrushIn", 0f);
            splashMat.SetFloat("_BrushOut", 0f);
        }
        logoImage.SetMaterialDirty();
        yield return null;

        // --- Phase 1: Brushed In Effect ---
        float elapsed = 0f;
        while (elapsed < brushInDuration && !skipped)
        {
            // Clamp dt to prevent frame hitch from skipping the sweep
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.033f);
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / brushInDuration);
            // Smooth painterly curve
            float curve = t * t * (3f - 2f * t);

            if (splashMat)
            {
                splashMat.SetFloat("_BrushIn", curve);
                splashMat.SetFloat("_BrushOut", 0f);
            }
            if (logoImage) logoImage.SetMaterialDirty();
            logoRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.0f, curve);

            yield return null;
        }

        if (splashMat)
        {
            splashMat.SetFloat("_BrushIn", 1f);
            splashMat.SetFloat("_BrushOut", 0f);
        }
        if (logoImage) logoImage.SetMaterialDirty();
        logoRect.localScale = Vector3.one;

        // --- Phase 2: Settle 0.3s after brushing in ---
        elapsed = 0f;
        while (elapsed < waitAfterBrushIn && !skipped)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        logoRect.anchoredPosition = baseLogoPos;
        logoRect.localRotation = Quaternion.identity;
        logoRect.localScale = Vector3.one;

        // --- Phase 3: Quick 2-Oscillation Logo Rattle ---
        if (!skipped)
        {
            try { TCServices.LightHaptic(); } catch (Exception) { }
            try { TCSfx.Play("salsa", 0.85f); } catch (Exception) { }
        }
        elapsed = 0f;
        const float rattleCycles = 2f;
        while (elapsed < rattleDuration && !skipped)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / rattleDuration);
            float angle = t * rattleCycles * Mathf.PI * 2f;

            float shakeX = Mathf.Sin(angle) * 14f;
            float shakeRot = Mathf.Sin(angle) * 4.5f;
            float squash = Mathf.Sin(angle * 2f) * 0.035f;

            logoRect.anchoredPosition = baseLogoPos + new Vector2(shakeX, 0);
            logoRect.localRotation = Quaternion.Euler(0, 0, shakeRot);
            logoRect.localScale = new Vector3(1f + squash, 1f - squash, 1f);

            yield return null;
        }
        logoRect.anchoredPosition = baseLogoPos;
        logoRect.localRotation = Quaternion.identity;
        logoRect.localScale = Vector3.one;

        // --- Phase 4: Wait for 0.5s after rattling ---
        elapsed = 0f;
        while (elapsed < waitAfterRattle && !skipped)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / waitAfterRattle);
            float pulse = Mathf.Sin(t * Mathf.PI) * 0.018f;
            logoRect.localScale = Vector3.one * (1.0f + pulse);
            yield return null;
        }
        logoRect.localScale = Vector3.one;

        // --- Phase 5: Brushed Off the PNG ---
        elapsed = 0f;
        while (elapsed < brushOutDuration && !skipped)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / brushOutDuration);
            float curve = t * t * (3f - 2f * t);

            if (splashMat)
            {
                splashMat.SetFloat("_BrushIn", 1f);
                splashMat.SetFloat("_BrushOut", curve);
            }
            if (logoImage) logoImage.SetMaterialDirty();
            logoRect.anchoredPosition = baseLogoPos + new Vector2(curve * 18f, 0f);

            yield return null;
        }

        if (splashMat) splashMat.SetFloat("_BrushOut", 1f);
        if (logoImage) { logoImage.SetMaterialDirty(); logoImage.enabled = false; }

        // --- Phase 6: Fade Out White BG into Main Menu ---
        canvasGroup.blocksRaycasts = false;
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            canvasGroup.alpha = 1f - (t * t * (3f - 2f * t));
            yield return null;
        }

        canvasGroup.alpha = 0f;

        // Cleanup
        if (splashMat) Destroy(splashMat);
        Destroy(gameObject);
    }
}
