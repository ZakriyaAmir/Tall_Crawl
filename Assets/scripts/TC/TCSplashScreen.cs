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
    public float brushInDuration = 1.15f;
    public float holdDuration = 0.85f;
    public float brushOutDuration = 0.80f;
    public float fadeOutDuration = 0.50f;

    Material splashMat;
    CanvasGroup canvasGroup;
    RectTransform logoRect;
    Image logoImage;
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
        yield return splash.RunRoutine();
        onComplete?.Invoke();
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

        // 2. Company logo
        Sprite logoSprite = Resources.Load<Sprite>("sillycon_logo");
        if (!logoSprite) logoSprite = Resources.Load<Sprite>("TCUI/sillycon_logo");
        Texture2D logoTex = Resources.Load<Texture2D>("sillycon_logo");
        if (!logoTex) logoTex = Resources.Load<Texture2D>("TCUI/sillycon_logo");

        var logoObj = new GameObject("SillyConLogo", typeof(RectTransform), typeof(Image));
        logoObj.transform.SetParent(transform, false);
        logoRect = logoObj.GetComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0.5f, 0.5f);
        logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = new Vector2(0, 30);
        logoRect.sizeDelta = new Vector2(530, 647); // 839x1024 aspect ratio

        logoImage = logoObj.GetComponent<Image>();
        if (logoSprite) logoImage.sprite = logoSprite;
        logoImage.raycastTarget = false;

        // 3. Brushed in / brushed out material (guaranteed 0 artifacts at start)
        var baseMat = Resources.Load<Material>("Materials/M_SplashInkSpread");
        var shader = baseMat ? baseMat.shader : Shader.Find("TallCrawl/SplashInkSpread");
        if (shader)
        {
            splashMat = new Material(shader);
            if (logoTex) splashMat.mainTexture = logoTex;
            else if (logoSprite) splashMat.mainTexture = logoSprite.texture;
            splashMat.SetFloat("_BrushIn", 0f);
            splashMat.SetFloat("_BrushOut", 0f);
            splashMat.SetFloat("_EdgeSoftness", 0.055f);
            logoImage.material = splashMat;
        }

        Vector2 baseLogoPos = logoRect.anchoredPosition;

        // --- Phase 1: Brushed In Effect ---
        float elapsed = 0f;
        while (elapsed < brushInDuration && !skipped)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / brushInDuration);
            // Smooth painterly curve
            float curve = t * t * (3f - 2f * t);

            if (splashMat)
            {
                splashMat.SetFloat("_BrushIn", curve);
                splashMat.SetFloat("_BrushOut", 0f);
            }
            logoRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.0f, curve);

            yield return null;
        }

        if (splashMat)
        {
            splashMat.SetFloat("_BrushIn", 1f);
            splashMat.SetFloat("_BrushOut", 0f);
        }
        logoRect.localScale = Vector3.one;

        // --- Phase 2: Hold & Cute Breathing Settle ---
        elapsed = 0f;
        while (elapsed < holdDuration && !skipped)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / holdDuration);
            float pulse = Mathf.Sin(t * Mathf.PI) * 0.022f;
            logoRect.localScale = Vector3.one * (1.0f + pulse);
            yield return null;
        }
        logoRect.localScale = Vector3.one;

        // --- Phase 3: Brushed Off the PNG ---
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
            logoRect.anchoredPosition = baseLogoPos + new Vector2(curve * 18f, 0f);

            yield return null;
        }

        if (splashMat) splashMat.SetFloat("_BrushOut", 1f);
        logoImage.enabled = false;

        // --- Phase 4: Fade Out White BG into Main Menu ---
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
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
