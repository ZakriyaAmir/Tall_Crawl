using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Common;
using Firebase.Analytics;
using GoogleMobileAds;
using GoogleMobileAds.Api;
//using static MaxSdkCallbacks;

public class AdmobManager : MonoBehaviour
{
    private BannerView _bannerView;
    private InterstitialAd _interstitialAd;
    private RewardedAd _rewardedAd;
    public bool isRewardedAdReady = false;
    [System.NonSerialized] public bool isInitialized = false;
    private RewardedInterstitialAd _rewardedInterstitialAd;
    private AppOpenAd appOpenAd;
    public static AdmobManager Instance;

    public const string OFFICIAL_BANNER_TEST_ID = "ca-app-pub-3940256099942544/6300978111";
    public const string OFFICIAL_INTERSTITIAL_TEST_ID = "ca-app-pub-3940256099942544/1033173712";
    public const string OFFICIAL_REWARDED_TEST_ID = "ca-app-pub-3940256099942544/5224354917";

    public const string REGISTERED_BANNER_ID = "ca-app-pub-4987175712306793/3348628581";
    public const string REGISTERED_INTERSTITIAL_ID = "ca-app-pub-4987175712306793/9597523925";
    public const string REGISTERED_REWARDED_ID = "ca-app-pub-4987175712306793/6984020172";

    // These ad units are configured to serve test ads.
    private string _adBannerUnitId;
    private string _adInterstitialUnitId;
    private string _adRewardedUnitId;
    private string _adRewardedInterstitialUnitId;
    private string _adAppOpenAdUnitId;

    private AdsManager _AdsManager;

    private bool _bannerRequestedVisible = false;
    private bool _isBannerLoading = false;
    private bool _isBannerLoaded = false;
    private bool _isInterstitialLoading = false;
    private bool _isRewardedLoading = false;

    void LogFirebaseEvent(string eventName)
    {
        TCServices.Log(eventName); // was infinite self-recursion (stack overflow on device)
    }

    public void RefreshUnitIds(bool forceReset = false)
    {
        if (_AdsManager == null) _AdsManager = GetComponent<AdsManager>();
        if (_AdsManager == null) _AdsManager = AdsManager.Instance;
        if (forceReset || string.IsNullOrEmpty(_adRewardedUnitId))
        {
            if (_AdsManager != null)
            {
                _AdsManager.ApplyOfficialTestIds();
                _adRewardedUnitId = _AdsManager.AdmobRewardedUnitId;
                _adBannerUnitId = _AdsManager.AdmobBannerUnitId;
                _adInterstitialUnitId = _AdsManager.AdmobInterstitialUnitId;
                _adRewardedInterstitialUnitId = _AdsManager.AdmobRewardedInterstitialUnitId;
                _adAppOpenAdUnitId = _AdsManager.AdmobAppOpenAdUnitId;
            }
            else
            {
                _adBannerUnitId = OFFICIAL_BANNER_TEST_ID;
                _adInterstitialUnitId = OFFICIAL_INTERSTITIAL_TEST_ID;
                _adRewardedUnitId = OFFICIAL_REWARDED_TEST_ID;
            }
        }
    }

    public void Awake()
    {
        Instance = this;
        RefreshUnitIds(true);
    }

    public void Start()
    {
        if (!Application.isMobilePlatform && !Application.isEditor) return; // SDK has no desktop client

        RequestConfiguration requestConfiguration = new RequestConfiguration
        {
            TestDeviceIds = new List<string>
            {
                AdRequest.TestDeviceSimulator,
                "AAF6E9CA381F9FB97840D63DB7169163"
            }
        };
        MobileAds.SetRequestConfiguration(requestConfiguration);

        Debug.Log("[AdmobManager] Initializing Google Mobile Ads SDK...");
        MobileAds.Initialize((InitializationStatus initStatus) =>
        {
            isInitialized = true;
            Debug.Log("[AdmobManager] Google Mobile Ads Initialized.");
            RefreshUnitIds(false);

            if (_bannerRequestedVisible)
            {
                showBannerAd();
            }
            else
            {
                LoadBannerAd();
            }

            LoadRewardedAd();
            LoadInterstitialAd();
        });
    }
    /// <summary>
    /// Loads the app open ad.
    /// </summary>
    public void LoadAppOpenAd()
    {
        // Clean up the old ad before loading a new one.
        if (appOpenAd != null)
        {
            appOpenAd.Destroy();
            appOpenAd = null;
        }

        Debug.Log("Loading the app open ad.");

        // Create our request used to load the ad.
        var adRequest = new AdRequest.Builder().Build();

        // send the request to load the ad.
        AppOpenAd.Load(_adAppOpenAdUnitId,ScreenOrientation.LandscapeLeft, adRequest,
            (AppOpenAd ad, LoadAdError error) =>
            {
                // if error is not null, the load request failed.
                if (error != null || ad == null)
                {
                    Debug.LogError("app open ad failed to load an ad " +
                                   "with error : " + error);
                    // send ad event
                    LogFirebaseEvent("Admob" + "_OpenApp" + "_success");
                    return;
                }

                Debug.Log("App open ad loaded with response : "
                          + ad.GetResponseInfo());
                // send ad event
                LogFirebaseEvent("Admob" + "_OpenApp" + "_failed");


                appOpenAd = ad;
            });
    }
    public bool IsAdAvailable
    {
        get
        {
            return appOpenAd != null;
        }
    }
    private void OnAppStateChanged(AppState state)
    {
        Debug.Log("App State changed to : " + state);

        // if the app is Foregrounded and the ad is available, show it.
        if (state == AppState.Foreground)
        {
            if (IsAdAvailable)
            {
                Debug.Log("You can run Open Ad here");
                LoadAppOpenAd();
            }
        }
    }
    /// <summary>
    /// Shows the app open ad.
    /// </summary>
    public void ShowAppOpenAd()
    {
        if (appOpenAd != null && appOpenAd.CanShowAd())
        {
            Debug.Log("Showing app open ad.");
            appOpenAd.Show();
        }
        else
        {
            Debug.LogError("App open ad is not ready yet.");
        }
    }
    /// <summary>
    /// Loads the rewarded interstitial ad.
    /// </summary>
    public void LoadRewardedInterstitialAd()
    {
        // Clean up the old ad before loading a new one.
        if (_rewardedInterstitialAd != null)
        {
            _rewardedInterstitialAd.Destroy();
            _rewardedInterstitialAd = null;
        }

        Debug.Log("Loading the rewarded interstitial ad.");

        // create our request used to load the ad.
        var adRequest = new AdRequest.Builder().Build();
        adRequest.Keywords.Add("unity-admob-sample");

        // send the request to load the ad.
        RewardedInterstitialAd.Load(_adRewardedInterstitialUnitId, adRequest,
            (RewardedInterstitialAd ad, LoadAdError error) =>
            {
                // if error is not null, the load request failed.
                if (error != null || ad == null)
                {
                    Debug.LogError("rewarded interstitial ad failed to load an ad " +
                                   "with error : " + error);
                    return;
                }

                Debug.Log("Rewarded interstitial ad loaded with response : "
                          + ad.GetResponseInfo());

                _rewardedInterstitialAd = ad;
            });
    }
    public bool ShowRewardedInterstitialAd(Action rewardCallback)
    {
        bool available = false;
        if (_rewardedInterstitialAd != null)
        {
            available = true;
            _rewardedInterstitialAd.Show((Reward reward) =>
            {
                _rewardedInterstitialAd.Destroy();
                LoadRewardedInterstitialAd();
                rewardCallback();
                // TODO: Reward the user.
                //Debug.Log(String.Format(rewardMsg, reward.Type, reward.Amount));
                // send ad event
                LogFirebaseEvent("Admob" + "_RewardedInterstitial" + "_success");
            });
        }
        else
        {
            available = false;
            // send ad event
            LogFirebaseEvent("Admob" + "_RewardedInterstitial" + "_failed");
        }
        return available;
    }
    /// <summary>
    /// Loads the rewarded ad with auto-retry on failure.
    /// </summary>
    public void LoadRewardedAd()
    {
        if (_isRewardedLoading) return;
        if (_rewardedAd != null && _rewardedAd.CanShowAd())
        {
            isRewardedAdReady = true;
            return;
        }

        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }
        isRewardedAdReady = false;
        _isRewardedLoading = true;

        if (string.IsNullOrEmpty(_adRewardedUnitId)) RefreshUnitIds(false);
        Debug.Log("[AdmobManager] Loading rewarded ad with unit: " + _adRewardedUnitId);

        var adRequest = AdRequestBuild();
        RewardedAd.Load(_adRewardedUnitId, adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                _isRewardedLoading = false;
                if (error != null || ad == null)
                {
                    Debug.LogError("[AdmobManager] Rewarded ad failed to load: " + error);
                    isRewardedAdReady = false;

                    if (error != null && error.ToString().Contains("403"))
                    {
                        if (_adRewardedUnitId == OFFICIAL_REWARDED_TEST_ID)
                        {
                            Debug.LogWarning("[AdmobManager] Rewarded 403 on official test ID — switching to registered unit ID: " + REGISTERED_REWARDED_ID);
                            _adRewardedUnitId = REGISTERED_REWARDED_ID;
                            if (gameObject.activeInHierarchy) StartCoroutine(RetryRewardedRoutine(0.5f));
                            return;
                        }
                        else if (_adRewardedUnitId == REGISTERED_REWARDED_ID)
                        {
                            Debug.LogWarning("[AdmobManager] Rewarded 403 on registered ID — switching to official test ID: " + OFFICIAL_REWARDED_TEST_ID);
                            _adRewardedUnitId = OFFICIAL_REWARDED_TEST_ID;
                            if (gameObject.activeInHierarchy) StartCoroutine(RetryRewardedRoutine(0.5f));
                            return;
                        }
                    }

                    if (gameObject.activeInHierarchy)
                    {
                        StartCoroutine(RetryRewardedRoutine(5f));
                    }
                    return;
                }

                Debug.Log("[AdmobManager] Rewarded ad loaded successfully.");
                _rewardedAd = ad;
                isRewardedAdReady = true;
                RegisterReloadHandler(ad);
            });
    }

    public bool ShowRewardedAd(Action rewardCallback)
    {
        bool ready = _rewardedAd != null && _rewardedAd.CanShowAd();
        if (!ready)
        {
            isRewardedAdReady = false;
            LogFirebaseEvent("Admob_Rewarded_failed");
            Debug.LogWarning("[AdmobManager] Rewarded ad not ready — preloading now.");
            LoadRewardedAd();
            return false;
        }

        isRewardedAdReady = false;
        var ad = _rewardedAd;
        ad.Show((Reward reward) =>
        {
            Debug.Log("[AdmobManager] OnUserEarnedReward type=" + reward.Type + " amount=" + reward.Amount);
            rewardCallback?.Invoke();
            LogFirebaseEvent("Admob_Rewarded_success");
        });
        return true;
    }

    private void RegisterReloadHandler(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AdmobManager] Rewarded ad closed full screen content.");
            _rewardedAd = null;
            isRewardedAdReady = false;
            LoadRewardedAd();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("[AdmobManager] Rewarded ad failed to open full screen content: " + error);
            _rewardedAd = null;
            isRewardedAdReady = false;
            LoadRewardedAd();
        };
    }

    /// <summary>
    /// Loads the interstitial ad with auto-retry on failure.
    /// </summary>
    public void LoadInterstitialAd()
    {
        if (_isInterstitialLoading) return;
        if (_interstitialAd != null && _interstitialAd.CanShowAd()) return; // Already cached and ready

        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        _isInterstitialLoading = true;
        if (string.IsNullOrEmpty(_adInterstitialUnitId)) RefreshUnitIds(false);
        Debug.Log("[AdmobManager] Loading interstitial ad with unit: " + _adInterstitialUnitId);

        var adRequest = AdRequestBuild();
        InterstitialAd.Load(_adInterstitialUnitId, adRequest,
            (InterstitialAd ad, LoadAdError error) =>
            {
                _isInterstitialLoading = false;
                if (error != null || ad == null)
                {
                    Debug.LogError("[AdmobManager] Interstitial ad failed to load: " + error);

                    if (error != null && error.ToString().Contains("403"))
                    {
                        if (_adInterstitialUnitId == OFFICIAL_INTERSTITIAL_TEST_ID)
                        {
                            Debug.LogWarning("[AdmobManager] Interstitial 403 on official test ID — switching to registered unit ID: " + REGISTERED_INTERSTITIAL_ID);
                            _adInterstitialUnitId = REGISTERED_INTERSTITIAL_ID;
                            if (gameObject.activeInHierarchy) StartCoroutine(RetryInterstitialRoutine(0.5f));
                            return;
                        }
                        else if (_adInterstitialUnitId == REGISTERED_INTERSTITIAL_ID)
                        {
                            Debug.LogWarning("[AdmobManager] Interstitial 403 on registered ID — switching to official test ID: " + OFFICIAL_INTERSTITIAL_TEST_ID);
                            _adInterstitialUnitId = OFFICIAL_INTERSTITIAL_TEST_ID;
                            if (gameObject.activeInHierarchy) StartCoroutine(RetryInterstitialRoutine(0.5f));
                            return;
                        }
                    }

                    if (gameObject.activeInHierarchy)
                    {
                        StartCoroutine(RetryInterstitialRoutine(5f));
                    }
                    return;
                }

                Debug.Log("[AdmobManager] Interstitial ad loaded successfully.");
                _interstitialAd = ad;
                RegisterInterstitialReloadHandler(ad);
            });
    }

    /// <summary>
    /// Shows the interstitial ad if ready; otherwise triggers preload immediately.
    /// </summary>
    public bool ShowInterstitialAd()
    {
        if (_interstitialAd != null && _interstitialAd.CanShowAd())
        {
            Debug.Log("[AdmobManager] Showing interstitial ad.");
            _interstitialAd.Show();
            LogFirebaseEvent("Admob_Interstitial_success");
            return true;
        }
        else
        {
            Debug.LogWarning("[AdmobManager] Interstitial ad not ready yet — preloading now.");
            LoadInterstitialAd();
            LogFirebaseEvent("Admob_Interstitial_failed");
            return false;
        }
    }

    private void RegisterInterstitialReloadHandler(InterstitialAd interstitialAd)
    {
        interstitialAd.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AdmobManager] Interstitial ad closed full screen content.");
            _interstitialAd = null;
            LoadInterstitialAd();
        };

        interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("[AdmobManager] Interstitial ad failed to open full screen content: " + error);
            _interstitialAd = null;
            LoadInterstitialAd();
        };
    }

    public void CreateBannerView()
    {
        Debug.Log("[AdmobManager] Creating banner view");

        if (_bannerView != null)
        {
            DestroyBannerView();
        }

        if (string.IsNullOrEmpty(_adBannerUnitId)) RefreshUnitIds(false);

        AdPosition pos = (AdsManager.Instance != null && AdsManager.Instance.bannerPosition == AdsManager.BannerPosition.Top)
            ? AdPosition.Top
            : AdPosition.Bottom;

        AdSize adaptiveSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
        _bannerView = new BannerView(_adBannerUnitId, adaptiveSize, pos);

        _bannerView.OnBannerAdLoaded += () =>
        {
            _isBannerLoading = false;
            _isBannerLoaded = true;
            Debug.Log("[AdmobManager] Banner ad loaded successfully.");
            if (_bannerRequestedVisible)
            {
                _bannerView.Show();
            }
            else
            {
                _bannerView.Hide();
            }
        };

        _bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            _isBannerLoading = false;
            _isBannerLoaded = false;
            Debug.LogError("[AdmobManager] Banner ad failed to load: " + error);

            if (error != null && error.ToString().Contains("403"))
            {
                if (_adBannerUnitId == OFFICIAL_BANNER_TEST_ID)
                {
                    Debug.LogWarning("[AdmobManager] Banner 403 on official test ID — switching to registered unit ID: " + REGISTERED_BANNER_ID);
                    _adBannerUnitId = REGISTERED_BANNER_ID;
                    DestroyBannerView();
                    if (_bannerRequestedVisible && gameObject.activeInHierarchy)
                    {
                        StartCoroutine(RetryBannerRoutine(0.5f));
                    }
                    return;
                }
                else if (_adBannerUnitId == REGISTERED_BANNER_ID)
                {
                    Debug.LogWarning("[AdmobManager] Banner 403 on registered ID — switching to official test ID: " + OFFICIAL_BANNER_TEST_ID);
                    _adBannerUnitId = OFFICIAL_BANNER_TEST_ID;
                    DestroyBannerView();
                    if (_bannerRequestedVisible && gameObject.activeInHierarchy)
                    {
                        StartCoroutine(RetryBannerRoutine(0.5f));
                    }
                    return;
                }
            }

            if (_bannerRequestedVisible && gameObject.activeInHierarchy)
            {
                StartCoroutine(RetryBannerRoutine(6f));
            }
        };
    }

    /// <summary>
    /// Creates the banner view and loads a banner ad.
    /// </summary>
    public void LoadBannerAd()
    {
        if (_bannerView == null)
        {
            CreateBannerView();
        }

        if (_bannerView != null && !_isBannerLoading)
        {
            Debug.Log("[AdmobManager] Loading banner ad with unit ID: " + _adBannerUnitId);
            _isBannerLoading = true;
            AdRequest request = AdRequestBuild();
            _bannerView.LoadAd(request);
        }
    }

    public bool showBannerAd()
    {
        _bannerRequestedVisible = true;
        if (!isInitialized)
        {
            Debug.Log("[AdmobManager] showBannerAd requested before Admob initialization finished — queued.");
            return true;
        }

        if (_bannerView == null)
        {
            CreateBannerView();
        }

        if (_bannerView != null)
        {
            Debug.Log("[AdmobManager] Showing banner view.");
            _bannerView.Show();
            if (!_isBannerLoaded && !_isBannerLoading)
            {
                LoadBannerAd();
            }
            LogFirebaseEvent("Admob_Banner_success");
            return true;
        }
        else
        {
            LogFirebaseEvent("Admob_Banner_failed");
            return false;
        }
    }

    AdRequest AdRequestBuild()
    {
        return new AdRequest.Builder().Build();
    }

    public void HideBannerAd()
    {
        _bannerRequestedVisible = false;
        if (_bannerView != null)
        {
            Debug.Log("[AdmobManager] Hiding banner view.");
            _bannerView.Hide();
        }
    }

    public void DestroyBannerView()
    {
        if (_bannerView != null)
        {
            Debug.Log("[AdmobManager] Destroying banner view.");
            _bannerView.Destroy();
            _bannerView = null;
            _isBannerLoaded = false;
            _isBannerLoading = false;
        }
    }

    IEnumerator RetryBannerRoutine(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (_bannerRequestedVisible && !_isBannerLoaded && !_isBannerLoading)
        {
            LoadBannerAd();
        }
    }

    IEnumerator RetryInterstitialRoutine(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if ((_interstitialAd == null || !_interstitialAd.CanShowAd()) && !_isInterstitialLoading)
        {
            LoadInterstitialAd();
        }
    }

    IEnumerator RetryRewardedRoutine(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if ((_rewardedAd == null || !_rewardedAd.CanShowAd()) && !_isRewardedLoading)
        {
            LoadRewardedAd();
        }
    }
}
