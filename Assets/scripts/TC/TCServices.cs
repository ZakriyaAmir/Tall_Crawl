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

    /// Shows a rewarded ad; reward is granted on completion. In the editor / when no ad is
    /// loaded we grant directly so the flow is testable (test IDs only).
    public static void Rewarded(Action onReward, string placement)
    {
        Log("rewarded_request", "placement_" + placement, 1);
        try
        {
            if (AdsManager.Instance != null && !Application.isEditor && AdsManager.Instance.RunRewardedAd(onReward)) return;
        }
        catch (Exception e) { Debug.LogWarning(e.Message); }
        onReward?.Invoke();
    }

    public static void Interstitial()
    {
        if (TCSave.NoAds) return;
        try { AdsManager.Instance?.RunInterstitialAd(); } catch (Exception) { }
    }

    public static void Banner(bool show)
    {
        if (TCSave.NoAds) show = false;
        try { if (show) AdsManager.Instance?.RunBannerAd(); else AdsManager.Instance?.HideBanner(); } catch (Exception) { }
    }

    /// productIndex maps to UnityIAP.kProductIDs (dummy IDs, see README).
    public static void Buy(int productIndex, Action onSuccess)
    {
        if (UnityIAP.Instance == null) { Debug.Log("IAP not initialised – granting in editor"); if (Application.isEditor) onSuccess?.Invoke(); return; }
        UnityIAP.Instance.onPurchaseComplete = onSuccess;
        UnityIAP.Instance.GeneralFunctionl(productIndex);
    }

    public static void Haptic()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (TCSave.Vibration && !Application.isEditor) Handheld.Vibrate();
#endif
    }
}
