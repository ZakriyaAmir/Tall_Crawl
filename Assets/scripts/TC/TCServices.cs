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

#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _TC_PlayLightHaptic();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject vibratorInstance;
    static int androidSdkVersion = -1;
    static bool androidHapticsInit;

    static void EnsureAndroidVibrator()
    {
        if (androidHapticsInit) return;
        androidHapticsInit = true;
        try
        {
            using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                androidSdkVersion = versionClass.GetStatic<int>("SDK_INT");
            }
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity != null)
                {
                    vibratorInstance = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TCServices] Android haptics init: " + ex.Message);
        }
    }

    private static void PlayAndroidLightHaptic()
    {
        try
        {
            EnsureAndroidVibrator();
            if (vibratorInstance == null)
            {
                Handheld.Vibrate();
                return;
            }

            bool hasVib = vibratorInstance.Call<bool>("hasVibrator");
            if (!hasVib) return;

            // API 29+ (Android 10+): Subtle native tick
            if (androidSdkVersion >= 29)
            {
                try
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        int effectTick = effectClass.GetStatic<int>("EFFECT_TICK");
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>("createPredefined", effectTick))
                        {
                            vibratorInstance.Call("vibrate", effect);
                            return;
                        }
                    }
                }
                catch { }
            }

            // API 26+ (Android 8.0+): 20ms at light amplitude (70 / 255)
            if (androidSdkVersion >= 26)
            {
                try
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)20, 70))
                    {
                        vibratorInstance.Call("vibrate", effect);
                        return;
                    }
                }
                catch { }
            }

            // Legacy Android: short 20ms pulse
            vibratorInstance.Call("vibrate", (long)20);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TCServices] Android LightHaptic error: " + ex.Message);
            Handheld.Vibrate();
        }
    }
#endif

    public static void Haptic()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (TCSave.Vibration && !Application.isEditor) Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// Plays a very subtle/little vibration compatible with both Android (via VibrationEffect tick/short pulse)
    /// and iOS (via UIImpactFeedbackGenerator light style).
    /// </summary>
    public static void LightHaptic()
    {
        if (!TCSave.Vibration) return;

#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            _TC_PlayLightHaptic();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TCServices] iOS LightHaptic error: " + ex.Message);
            Handheld.Vibrate();
        }
#elif UNITY_ANDROID && !UNITY_EDITOR
        PlayAndroidLightHaptic();
#else
        if (Application.isEditor)
        {
            // Subtle indicator in editor if desired
        }
#endif
    }

    /// <summary>
    /// Detects the highest refresh rate supported by the current display / device
    /// (e.g. 90Hz, 120Hz ProMotion, 144Hz, 165Hz, 240Hz) and sets Application.targetFrameRate accordingly.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ApplyMaxDeviceRefreshRate()
    {
        int maxHz = 60;

        try
        {
            // 1. Query all display modes supported by the current resolution / display:
            var resolutions = Screen.resolutions;
            if (resolutions != null && resolutions.Length > 0)
            {
                for (int i = 0; i < resolutions.Length; i++)
                {
                    int hz = Mathf.RoundToInt((float)resolutions[i].refreshRateRatio.value);
                    if (hz > maxHz) maxHz = hz;
                }
            }

            int curHz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            if (curHz > maxHz) maxHz = curHz;
        }
        catch (Exception) { }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var windowManager = activity.Call<AndroidJavaObject>("getWindowManager"))
            using (var display = windowManager.Call<AndroidJavaObject>("getDefaultDisplay"))
            {
                // Query hardware display modes on Android:
                var modes = display.Call<AndroidJavaObject[]>("getSupportedModes");
                if (modes != null)
                {
                    for (int i = 0; i < modes.Length; i++)
                    {
                        var m = modes[i];
                        if (m != null)
                        {
                            float rate = m.Call<float>("getRefreshRate");
                            int rounded = Mathf.RoundToInt(rate);
                            if (rounded > maxHz) maxHz = rounded;
                        }
                    }
                }
                float curRate = display.Call<float>("getRefreshRate");
                int curRounded = Mathf.RoundToInt(curRate);
                if (curRounded > maxHz) maxHz = curRounded;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TCServices] Android display mode check: " + ex.Message);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // On iOS devices with ProMotion (iPhone 13 Pro+, iPad Pro), Apple requires requesting
        // 120fps via Application.targetFrameRate to unlock full 120Hz display refresh.
        // Fixed 60Hz iOS screens will naturally sync to their 60Hz hardware limit.
        if (maxHz < 120) maxHz = 120;
#endif

        // Ensure we never drop below 60fps
        maxHz = Mathf.Max(60, maxHz);

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = maxHz;
        Debug.Log($"[TCServices] Display max refresh rate applied: targetFrameRate = {maxHz} fps (vSyncCount = 0)");
    }
}
