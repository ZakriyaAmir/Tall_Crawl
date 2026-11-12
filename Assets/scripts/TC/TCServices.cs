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

    /// Rewarded ads always play, even when NoAds is on (NoAds only hides banner + interstitial).
    /// On device this shows the official Google AdMob test rewarded unit full-screen and grants
    /// the reward only from AdmobManager's OnUserEarnedReward callback. In the Editor there is
    /// no visible panel: we log and grant so revive / x2 stay testable.
    public static void Rewarded(Action onReward, string placement)
    {
        Log("rewarded_request", "placement_" + placement, 1);
        if (Application.isEditor)
        {
            Debug.Log("[TCServices] Rewarded '" + placement + "' — editor: grant (no ad panel). NoAds=" + TCSave.NoAds);
            onReward?.Invoke();
            return;
        }
        try
        {
            if (AdsManager.Instance != null && AdsManager.Instance.RunRewardedAd(onReward))
            {
                Debug.Log("[TCServices] Rewarded '" + placement + "' — showing AdMob test rewarded ad");
                return;
            }
        }
        catch (Exception e) { Debug.LogWarning("[TCServices] Rewarded show failed: " + e.Message); }
        // Do not fall back to a custom overlay, and do not grant if the ad did not play.
        Debug.LogWarning("[TCServices] Rewarded '" + placement + "' not ready — reward not granted");
    }

    public static void Interstitial()
    {
        if (TCSave.NoAds) return;
        if (Application.isEditor)
        {
            Debug.Log("[TCServices] Interstitial — editor: skip (no ad panel)");
            return;
        }
        try
        {
            if (AdsManager.Instance != null)
                AdsManager.Instance.RunInterstitialAd();
        }
        catch (Exception e) { Debug.LogWarning("[TCServices] Interstitial failed: " + e.Message); }
    }

    public static void Banner(bool show)
    {
        if (TCSave.NoAds) show = false;
        TCSafeArea.BannerVisible = show;
        if (Application.isEditor) return;
        try
        {
            if (AdsManager.Instance != null)
            {
                if (show) AdsManager.Instance.RunBannerAd();
                else AdsManager.Instance.HideBanner();
            }
        }
        catch (Exception e) { Debug.LogWarning("[TCServices] Banner failed: " + e.Message); }
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
