using System;
using UnityEngine;

/// Safe wrappers around the existing Ads / IAP / Firebase managers, so gameplay never throws
/// when a manager is missing (e.g. starting the gameplay scene directly in the editor).
public static class TCServices
{
    public static void Log(string evt, string param = null, long value = 0)
    {
#if !UNITY_EDITOR
        try
        {
            if (param == null) Firebase.Analytics.FirebaseAnalytics.LogEvent(evt);
            else Firebase.Analytics.FirebaseAnalytics.LogEvent(evt, param, value);
        }
        catch (Exception) { }
#endif
    }

    /// Shows a rewarded ad; reward is granted on completion.
    /// When ads are removed (IAP or cheat) the reward is granted directly, no ad.
    /// In the Editor, or when the Mobile Ads SDK cannot show, a visible placeholder
    /// overlay stands in so the flow is testable. Official Google test unit IDs are
    /// used on device.
    public static void Rewarded(Action onReward, string placement)
    {
        Log("rewarded_request", "placement_" + placement, 1);
        if (TCSave.NoAds)
        {
            onReward?.Invoke();
            return;
        }
        try
        {
            if (!Application.isEditor && AdsManager.Instance != null && AdsManager.Instance.RunRewardedAd(onReward))
                return;
        }
        catch (Exception e) { Debug.LogWarning(e.Message); }
        TCAdOverlay.ShowRewarded(onReward, placement);
    }

    public static void Interstitial()
    {
        if (TCSave.NoAds) return;
        try
        {
            if (!Application.isEditor && AdsManager.Instance != null)
            {
                AdsManager.Instance.RunInterstitialAd();
                return;
            }
        }
        catch (Exception) { }
        TCAdOverlay.ShowInterstitial();
    }

    public static void Banner(bool show)
    {
        if (TCSave.NoAds) show = false;
        TCSafeArea.BannerVisible = show;
        try
        {
            if (!Application.isEditor && AdsManager.Instance != null)
            {
                if (show) AdsManager.Instance.RunBannerAd();
                else AdsManager.Instance.HideBanner();
                return;
            }
        }
        catch (Exception) { }
        TCAdOverlay.ShowBanner(show);
    }

    /// productIndex maps to UnityIAP.kProductIDs (dummy IDs, see README).
    public static void Buy(int productIndex, Action onSuccess)
    {
        if (UnityIAP.Instance == null) { Debug.Log("IAP not initialised – granting in editor"); if (Application.isEditor) onSuccess?.Invoke(); return; }
        UnityIAP.Instance.onPurchaseComplete = onSuccess;
        UnityIAP.Instance.GeneralFunctionl(productIndex);
    }

    /// Standalone gem pack purchase (pack index into UnityIAP.GemProductIDs, 10 gems per USD).
    public static void BuyGems(int pack, Action onSuccess)
    {
        if (UnityIAP.Instance == null) { Debug.Log("IAP not initialised – granting in editor"); if (Application.isEditor) onSuccess?.Invoke(); return; }
        UnityIAP.Instance.onPurchaseComplete = onSuccess;
        UnityIAP.Instance.BuyGemPack(pack);
    }

    public static void Haptic()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (TCSave.Vibration && !Application.isEditor) Handheld.Vibrate();
#endif
    }
}
