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
    private RewardedInterstitialAd _rewardedInterstitialAd;
    private AppOpenAd appOpenAd;
    public static AdmobManager Instance;

    // These ad units are configured to always serve test ads.

    private string _adBannerUnitId;
    private string _adInterstitialUnitId;
    private string _adRewardedUnitId;
    private string _adRewardedInterstitialUnitId;
    private string _adAppOpenAdUnitId;

    private AdsManager _AdsManager;

    public void Awake()
    {
        Instance = this;

        _AdsManager = GetComponent<AdsManager>();

        _adRewardedUnitId = _AdsManager.AdmobRewardedUnitId;
        _adBannerUnitId = _AdsManager.AdmobBannerUnitId;
        _adInterstitialUnitId = _AdsManager.AdmobInterstitialUnitId;
        _adRewardedInterstitialUnitId = _AdsManager.AdmobRewardedInterstitialUnitId;
        _adAppOpenAdUnitId = _AdsManager.AdmobAppOpenAdUnitId;

        //AppStateEventNotifier.AppStateChanged += OnAppStateChanged;
    }
    private void OnDestroy()
    {
        // Always unlisten to events when complete.
        //AppStateEventNotifier.AppStateChanged -= OnAppStateChanged;
    }
    public void Start()
    {
        // Initialize the Google Mobile Ads SDK.
        MobileAds.Initialize((InitializationStatus initStatus) =>
        {
            Debug.Log("Admob Intitialized");
            LoadBannerAd();
            LoadRewardedAd();
            LoadInterstitialAd();
            //LoadRewardedInterstitialAd();
            //LoadAppOpenAd();
            // This callback is called once the MobileAds SDK is initialized.
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
                    FirebaseAnalytics.LogEvent("Admob" + "_OpenApp" + "_success");
                    return;
                }

                Debug.Log("App open ad loaded with response : "
                          + ad.GetResponseInfo());
                // send ad event
                FirebaseAnalytics.LogEvent("Admob" + "_OpenApp" + "_failed");


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
                FirebaseAnalytics.LogEvent("Admob" + "_RewardedInterstitial" + "_success");
            });
        }
        else
        {
            available = false;
            // send ad event
            FirebaseAnalytics.LogEvent("Admob" + "_RewardedInterstitial" + "_failed");
        }
        return available;
    }
    /// <summary>
    /// Loads the rewarded ad.
    /// </summary>
    public void LoadRewardedAd()
    {
        // Clean up the old ad before loading a new one.
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        Debug.Log("Loading the rewarded ad.");

        // create our request used to load the ad.
        var adRequest = new AdRequest.Builder().Build();

        // send the request to load the ad.
        RewardedAd.Load(_adRewardedUnitId, adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                // if error is not null, the load request failed.
                if (error != null || ad == null)
                {
                    Debug.LogError("Rewarded ad failed to load an ad " +
                                   "with error : " + error);
                    return;
                }

                Debug.Log("Rewarded ad loaded with response : "
                          + ad.GetResponseInfo());

                _rewardedAd = ad;
            });
    }

    public bool ShowRewardedAd(Action rewardCallback)
    {
        bool available = false;
        if (_rewardedAd != null)
        {
            available = true;
            _rewardedAd.Show((Reward reward) =>
            {
                _rewardedAd.Destroy();
                LoadRewardedAd();
                rewardCallback();
                // TODO: Reward the user.
                //Debug.Log(String.Format(rewardMsg, reward.Type, reward.Amount));

                // send ad event
                FirebaseAnalytics.LogEvent("Admob" + "_Rewarded" + "_success");
            });
        }
        else
        {
            available = false;
            // send ad event
            FirebaseAnalytics.LogEvent("Admob" + "_Rewarded" + "_failed");
        }
        return available;
    }

    private void RegisterReloadHandler(RewardedAd ad)
    {
        // Raised when the ad closed full screen content.
        ad.OnAdFullScreenContentClosed += () =>
    {
            Debug.Log("Rewarded Ad full screen content closed.");

            // Reload the ad so that we can show another as soon as possible.
            LoadRewardedAd();
        };
        // Raised when the ad failed to open full screen content.
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Rewarded ad failed to open full screen content " +
                           "with error : " + error);

            // Reload the ad so that we can show another as soon as possible.
            LoadRewardedAd();
        };
    }
    /// <summary>
    /// Loads the interstitial ad.
    /// </summary>
    public void LoadInterstitialAd()
    {
        // Clean up the old ad before loading a new one.
        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        Debug.Log("Loading the interstitial ad.");

        // create our request used to load the ad.
        var adRequest = new AdRequest.Builder().Build();

        // send the request to load the ad.
        InterstitialAd.Load(_adInterstitialUnitId, adRequest,
            (InterstitialAd ad, LoadAdError error) =>
            {
                // if error is not null, the load request failed.
                if (error != null || ad == null)
                {
                    Debug.LogError("interstitial ad failed to load an ad " +
                                   "with error : " + error);
                    return;
                }

                Debug.Log("Interstitial ad loaded with response : "
                          + ad.GetResponseInfo());

                _interstitialAd = ad;

                //ShowInterstitialAd();
            });
    }

    /// <summary>
    /// Shows the interstitial ad.
    /// </summary>
    public bool ShowInterstitialAd()
    {
        bool available = false;
        if (_interstitialAd != null && _interstitialAd.CanShowAd())
        {
            Debug.Log("Showing interstitial ad.");
            _interstitialAd.Show();
            RegisterReloadHandler(_interstitialAd);
            available = true;
            // send ad event
            FirebaseAnalytics.LogEvent("Admob" + "_Interstitial" + "_success");
        }
        else
        {
            Debug.LogError("Interstitial ad is not ready yet.");
            available = false;
            // send ad event
            FirebaseAnalytics.LogEvent("Admob" + "_Interstitial" + "_failed");
        }
        return available;
    }

    private void RegisterReloadHandler(InterstitialAd interstitialAd)
    {
        // Raised when the ad closed full screen content.
        interstitialAd.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Interstitial Ad full screen content closed.");

            // Reload the ad so that we can show another as soon as possible.
            LoadInterstitialAd();
        };
        // Raised when the ad failed to open full screen content.
        interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Interstitial ad failed to open full screen content " +
                           "with error : " + error);

            // Reload the ad so that we can show another as soon as possible.
            LoadInterstitialAd();
        };
    }

    public void CreateBannerView()
    {
        Debug.Log("Creating banner view");

        // If we already have a banner, destroy the old one.
        if (_bannerView != null)
        {
            DestroyBannerView();
        }

        if (AdsManager.Instance.bannerPosition == AdsManager.BannerPosition.Top)
        {
            //Customize banner ad size here
            Debug.Log("Top Banner");
            _bannerView = new BannerView(_adBannerUnitId, AdSize.Banner, AdPosition.Top);
        }
        else if (AdsManager.Instance.bannerPosition == AdsManager.BannerPosition.Bottom)
        {
            //Customize banner ad size here
            Debug.Log("Bottom Banner");
            _bannerView = new BannerView(_adBannerUnitId, AdSize.Banner, AdPosition.Bottom);
        }
    }


    /// <summary>
    /// Creates the banner view and loads a banner ad.
    /// </summary>
    public void LoadBannerAd()
    {
        // create an instance of a banner view first.
        if (_bannerView == null)
        {
            CreateBannerView();
        }

        Debug.Log("Loading banner ad.");
    }

    public bool showBannerAd()
    {
        bool available = false;
        if (_bannerView != null)
        {
            AdRequest request = AdRequestBuild();
            _bannerView.LoadAd(request);
            available = true;
            // send ad event
            FirebaseAnalytics.LogEvent("Admob" + "_Banner" + "_success");
        }
        else
        {
            available = false;
            // send ad event
            FirebaseAnalytics.LogEvent("Admob" + "_Banner" + "_failed");
        }
        return available;
    }

    AdRequest AdRequestBuild()
    {
        return new AdRequest.Builder().Build();
    }

    public void HideBannerAd()
    {
        if (_bannerView != null)
        {
            Debug.Log("Hiding banner view.");
            _bannerView.Hide();
        }
    }
    public void DestroyBannerView()
    {
        if (_bannerView != null)
        {
            Debug.Log("Destroying banner view.");
            _bannerView.Destroy();
            _bannerView = null;
        }
    }
}
