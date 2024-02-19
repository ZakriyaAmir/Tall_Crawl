/*using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using GoogleMobileAds.Api;
using static AdsManager;
using Firebase.Analytics;

public class MediationManager : MonoBehaviour
{
    private string MaxSdkKey;
    private string InterstitialAdUnitId;
    private string RewardedAdUnitId;
    private string BannerAdUnitId;
    private string MRecAdUnitId;
    private  string RewardedInterstitialAdUnitId;
    public UnityEvent OnwatchvideoAd;


    //public Button showInterstitialButton;
    //public Button showRewardedButton;
    //public Button showRewardedInterstitialButton;
    //public Button showBannerButton;
    //public Button showMRecButton;
    //public Button mediationDebuggerButton;
    //public Text interstitialStatusText;
    //public Text rewardedStatusText;
    //public Text rewardedInterstitialStatusText;

    //private bool isBannerShowing;
    //private bool isMRecShowing;

    private int interstitialRetryAttempt;
    private int rewardedRetryAttempt;
    private int rewardedInterstitialRetryAttempt;
    public static MediationManager instance;
    private AdsManager _AdsManager;

    private void Awake()
    {
        instance = this;
        _AdsManager = GetComponent<AdsManager>();

        MaxSdkKey = _AdsManager.MaxSdkKey;
        InterstitialAdUnitId = _AdsManager.MaxInterstitialAdUnitId;
        RewardedAdUnitId = _AdsManager.MaxRewardedAdUnitId;
        BannerAdUnitId = _AdsManager.MaxBannerAdUnitId;
        MRecAdUnitId = _AdsManager.MaxMRecAdUnitId;
        RewardedInterstitialAdUnitId = _AdsManager.MaxRewardedInterstitialAdUnitId;
    }
    void Start()
    {

        DontDestroyOnLoad(gameObject);
        //showInterstitialButton.onClick.AddListener(ShowInterstitial);
        //showRewardedButton.onClick.AddListener(ShowRewardedAd);
        //showRewardedInterstitialButton.onClick.AddListener(ShowRewardedInterstitialAd);
        //showBannerButton.onClick.AddListener(ToggleBannerVisibility);
        //showMRecButton.onClick.AddListener(ToggleMRecVisibility);
        //mediationDebuggerButton.onClick.AddListener(MaxSdk.ShowMediationDebugger);

        MaxSdkCallbacks.OnSdkInitializedEvent += sdkConfiguration =>
        {
            // AppLovin SDK is initialized, configure and start loading ads.
            Debug.Log("MAX SDK Initialized");
            
            InitializeInterstitialAds();
            InitializeRewardedAds();
            InitializeBannerAds();
            InitializeMRecAds();
            InitializeRewardedInterstitialAds();
            AdsManager.Instance.MRecPosition = (MaxSdkBase.AdViewPosition)AdsManager.Instance.MRecPos;
            // Initialize Adjust SDK
            //AdjustConfig adjustConfig = new AdjustConfig("YourAppToken", AdjustEnvironment.Sandbox);
            //Adjust.start(adjustConfig);
        };

        MaxSdk.SetSdkKey(MaxSdkKey);
        MaxSdk.InitializeSdk();
    }

    #region Interstitial Ad Methods

    private void InitializeInterstitialAds()
    {
        // Attach callbacks
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialFailedEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += InterstitialFailedToDisplayEvent;
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialDismissedEvent;
        MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialRevenuePaidEvent;

        // Load the first interstitial
        LoadInterstitialMax();
    }

    void LoadInterstitialMax()
    {
        //interstitialStatusText.text = "Loading...";
        MaxSdk.LoadInterstitial(InterstitialAdUnitId);
    }

    public void ShowInterstitialMax()
    {
        if (MaxSdk.IsInterstitialReady(InterstitialAdUnitId))
        {
            Debug.Log("Max Log = " + "Showing Interstitial");
            MaxSdk.ShowInterstitial(InterstitialAdUnitId);
            // send ad event
            FirebaseAnalytics.LogEvent("Applovin" + "_Intersitial" + "_success");
        }
        else
        {
            Debug.Log("Max Log = " + "Ad not ready");
            // send ad event
            FirebaseAnalytics.LogEvent("Applovin" + "_Intersitial" + "_failed");
        }
    }

    private void OnInterstitialLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Interstitial ad is ready to be shown. MaxSdk.IsInterstitialReady(interstitialAdUnitId) will now return 'true'
        //interstitialStatusText.text = "Loaded";
        Debug.Log("Interstitial loaded");

        // Reset retry attempt
        interstitialRetryAttempt = 0;
    }

    private void OnInterstitialFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        // Interstitial ad failed to load. We recommend retrying with exponentially higher delays up to a maximum delay (in this case 64 seconds).
        interstitialRetryAttempt++;
        double retryDelay = Math.Pow(2, Math.Min(6, interstitialRetryAttempt));

        //interstitialStatusText.text = "Load failed: " + errorInfo.Code + "\nRetrying in " + retryDelay + "s...";
        Debug.Log("Interstitial failed to load with error code: " + errorInfo.Code);

        Invoke("LoadInterstitial", (float)retryDelay);
    }

    private void InterstitialFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        // Interstitial ad failed to display. We recommend loading the next ad
        Debug.Log("Interstitial failed to display with error code: " + errorInfo.Code);
        LoadInterstitialMax();
    }

    private void OnInterstitialDismissedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Interstitial ad is hidden. Pre-load the next ad
        Debug.Log("Interstitial dismissed");
        LoadInterstitialMax();
    }

    private void OnInterstitialRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Interstitial ad revenue paid. Use this callback to track user revenue.
        Debug.Log("Interstitial revenue paid");

        // Ad revenue
        double revenue = adInfo.Revenue;

        // Miscellaneous data
        string countryCode = MaxSdk.GetSdkConfiguration().CountryCode; // "US" for the United States, etc - Note: Do not confuse this with currency code which is "USD" in most cases!
        string networkName = adInfo.NetworkName; // Display name of the network that showed the ad (e.g. "AdColony")
        string adUnitIdentifier = adInfo.AdUnitIdentifier; // The MAX Ad Unit ID
        string placement = adInfo.Placement; // The placement this ad's postbacks are tied to

        //TrackAdRevenue(adInfo);
    }

    #endregion

    #region Rewarded Ad Methods

    private void InitializeRewardedAds()
    {
        // Attach callbacks
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoadedEvent;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdFailedEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplayEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayedEvent;
        MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClickedEvent;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdDismissedEvent;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;
        MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaidEvent;

        // Load the first RewardedAd
        LoadRewardedAd();
    }

    private void LoadRewardedAd()
    {
        //rewardedStatusText.text = "Loading...";
        MaxSdk.LoadRewardedAd(RewardedAdUnitId);
    }

    public void ShowRewardedAdMax()
    {
        if (MaxSdk.IsRewardedAdReady(RewardedAdUnitId))
        {
            //rewardedStatusText.text = "Showing";
            MaxSdk.ShowRewardedAd(RewardedAdUnitId);
            // send ad event
            FirebaseAnalytics.LogEvent("Applovin" + "_Rewarded" + "_success");
        }
        else
        {
            Debug.Log("Max Interstitial Ad not ready");
            // send ad event
            FirebaseAnalytics.LogEvent("Applovin" + "_Rewarded" + "_failed");
        }
    }

    private void OnRewardedAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded ad is ready to be shown. MaxSdk.IsRewardedAdReady(rewardedAdUnitId) will now return 'true'
        //rewardedStatusText.text = "Loaded";
        Debug.Log("Rewarded ad loaded");

        // Reset retry attempt
        rewardedRetryAttempt = 0;
    }

    private void OnRewardedAdFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        // Rewarded ad failed to load. We recommend retrying with exponentially higher delays up to a maximum delay (in this case 64 seconds).
        rewardedRetryAttempt++;
        double retryDelay = Math.Pow(2, Math.Min(6, rewardedRetryAttempt));

        //rewardedStatusText.text = "Load failed: " + errorInfo.Code + "\nRetrying in " + retryDelay + "s...";
        Debug.Log("Rewarded ad failed to load with error code: " + errorInfo.Code);

        Invoke("LoadRewardedAd", (float)retryDelay);
    }

    private void OnRewardedAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded ad failed to display. We recommend loading the next ad
        Debug.Log("Rewarded ad failed to display with error code: " + errorInfo.Code);
        LoadRewardedAd();
    }

    private void OnRewardedAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("Rewarded ad displayed");
    }

    private void OnRewardedAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("Rewarded ad clicked");
    }

    private void OnRewardedAdDismissedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded ad is hidden. Pre-load the next ad
        Debug.Log("Rewarded ad dismissed");
        LoadRewardedAd();
    }

    private void OnRewardedAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded ad was displayed and user should receive the reward
        Debug.Log("Rewarded ad received reward");
        //PlayerPrefs.SetInt("TotalMoney", PlayerPrefs.GetInt("TotalMoney", 300) + 100);
        //FindObjectOfType<Menu>().UpdateEconomy();
    }

    private void OnRewardedAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded ad revenue paid. Use this callback to track user revenue.
        Debug.Log("Rewarded ad revenue paid");

        // Ad revenue
        double revenue = adInfo.Revenue;

        // Miscellaneous data
        string countryCode = MaxSdk.GetSdkConfiguration().CountryCode; // "US" for the United States, etc - Note: Do not confuse this with currency code which is "USD" in most cases!
        string networkName = adInfo.NetworkName; // Display name of the network that showed the ad (e.g. "AdColony")
        string adUnitIdentifier = adInfo.AdUnitIdentifier; // The MAX Ad Unit ID
        string placement = adInfo.Placement; // The placement this ad's postbacks are tied to

        //TrackAdRevenue(adInfo);
    }

    #endregion

    #region Rewarded Interstitial Ad Methods

    private void InitializeRewardedInterstitialAds()
    {
        // Attach callbacks
        MaxSdkCallbacks.RewardedInterstitial.OnAdLoadedEvent += OnRewardedInterstitialAdLoadedEvent;
        MaxSdkCallbacks.RewardedInterstitial.OnAdLoadFailedEvent += OnRewardedInterstitialAdFailedEvent;
        MaxSdkCallbacks.RewardedInterstitial.OnAdDisplayFailedEvent += OnRewardedInterstitialAdFailedToDisplayEvent;
        MaxSdkCallbacks.RewardedInterstitial.OnAdDisplayedEvent += OnRewardedInterstitialAdDisplayedEvent;
        MaxSdkCallbacks.RewardedInterstitial.OnAdClickedEvent += OnRewardedInterstitialAdClickedEvent;
        MaxSdkCallbacks.RewardedInterstitial.OnAdHiddenEvent += OnRewardedInterstitialAdDismissedEvent;
        MaxSdkCallbacks.RewardedInterstitial.OnAdReceivedRewardEvent += OnRewardedInterstitialAdReceivedRewardEvent;
        MaxSdkCallbacks.RewardedInterstitial.OnAdRevenuePaidEvent += OnRewardedInterstitialAdRevenuePaidEvent;

        // Load the first RewardedInterstitialAd
        LoadRewardedInterstitialAd();
    }

    private void LoadRewardedInterstitialAd()
    {
        //rewardedInterstitialStatusText.text = "Loading...";
        MaxSdk.LoadRewardedInterstitialAd(RewardedInterstitialAdUnitId);
    }

    public void ShowRewardedInterstitialAd()
    {
        if (MaxSdk.IsRewardedInterstitialAdReady(RewardedInterstitialAdUnitId))
        {
            MaxSdk.ShowRewardedInterstitialAd(RewardedInterstitialAdUnitId);
            // send ad event
            FirebaseAnalytics.LogEvent("Applovin" + "_RewardedInterstitial" + "_success");

        }
        else
        {
            Debug.Log("Ad not ready");
            // send ad event
            FirebaseAnalytics.LogEvent("Applovin" + "_RewardedInterstitial" + "_failed");
        }
    }

    private void OnRewardedInterstitialAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded interstitial ad is ready to be shown. MaxSdk.IsRewardedInterstitialAdReady(rewardedInterstitialAdUnitId) will now return 'true'
        //rewardedInterstitialStatusText.text = "Loaded";
        Debug.Log("Rewarded interstitial ad loaded");

        // Reset retry attempt
        rewardedInterstitialRetryAttempt = 0;
    }

    private void OnRewardedInterstitialAdFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        // Rewarded interstitial ad failed to load. We recommend retrying with exponentially higher delays up to a maximum delay (in this case 64 seconds).
        rewardedInterstitialRetryAttempt++;
        double retryDelay = Math.Pow(2, Math.Min(6, rewardedInterstitialRetryAttempt));

        //rewardedInterstitialStatusText.text = "Load failed: " + errorInfo.Code + "\nRetrying in " + retryDelay + "s...";
        Debug.Log("Rewarded interstitial ad failed to load with error code: " + errorInfo.Code);

        Invoke("LoadRewardedInterstitialAd", (float)retryDelay);
    }

    private void OnRewardedInterstitialAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded interstitial ad failed to display. We recommend loading the next ad
        Debug.Log("Rewarded interstitial ad failed to display with error code: " + errorInfo.Code);
        LoadRewardedInterstitialAd();
    }

    private void OnRewardedInterstitialAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("Rewarded interstitial ad displayed");
    }

    private void OnRewardedInterstitialAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("Rewarded interstitial ad clicked");
    }

    private void OnRewardedInterstitialAdDismissedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded interstitial ad is hidden. Pre-load the next ad
        Debug.Log("Rewarded interstitial ad dismissed");
        LoadRewardedInterstitialAd();
    }

    private void OnRewardedInterstitialAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded interstitial ad was displayed and user should receive the reward
        Debug.Log("Rewarded interstitial ad received reward");
    }

    private void OnRewardedInterstitialAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded interstitial ad revenue paid. Use this callback to track user revenue.
        Debug.Log("Rewarded interstitial ad revenue paid");

        // Ad revenue
        double revenue = adInfo.Revenue;

        // Miscellaneous data
        string countryCode = MaxSdk.GetSdkConfiguration().CountryCode; // "US" for the United States, etc - Note: Do not confuse this with currency code which is "USD" in most cases!
        string networkName = adInfo.NetworkName; // Display name of the network that showed the ad (e.g. "AdColony")
        string adUnitIdentifier = adInfo.AdUnitIdentifier; // The MAX Ad Unit ID
        string placement = adInfo.Placement; // The placement this ad's postbacks are tied to

        //TrackAdRevenue(adInfo);
    }

    #endregion

    #region Banner Ad Methods

    private void InitializeBannerAds()
    {
        // Attach Callbacks
        MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerAdLoadedEvent;
        MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerAdFailedEvent;
        MaxSdkCallbacks.Banner.OnAdClickedEvent += OnBannerAdClickedEvent;
        MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaidEvent;

        // Banners are automatically sized to 320x50 on phones and 728x90 on tablets.
        // You may use the utility method `MaxSdkUtils.isTablet()` to help with view sizing adjustments.
        //Customize banner ad size here
        if (AdsManager.Instance.bannerPosition == AdsManager.BannerPosition.Top)
        {
            //Customize banner ad size here
            MaxSdk.CreateBanner(BannerAdUnitId, MaxSdkBase.BannerPosition.TopCenter);
        }
        else if (AdsManager.Instance.bannerPosition == AdsManager.BannerPosition.Bottom)
        {
            //Customize banner ad size here
            MaxSdk.CreateBanner(BannerAdUnitId, MaxSdkBase.BannerPosition.BottomCenter);
        }

        // Set background or background color for banners to be fully functional.
        MaxSdk.SetBannerBackgroundColor(BannerAdUnitId, Color.black);
        // MaxSdk.SetBannerBackgroundColor(BannerAdUnitId, "#000000");
        MaxSdk.SetBannerWidth(BannerAdUnitId, 320);
    }

    public void ShowBannerMax()
    {
        MaxSdk.ShowBanner(BannerAdUnitId);
        // send ad event
        FirebaseAnalytics.LogEvent("Applovin" + "_Banner" + "_success");
    }
    public void HideBannerMax()
    {
        MaxSdk.HideBanner(BannerAdUnitId);
    }
    //private void ToggleBannerVisibility()
    //{
    //    if (!isBannerShowing)
    //    {
    //        MaxSdk.ShowBanner(BannerAdUnitId);
    //        //showBannerButton.GetComponentInChildren<Text>().text = "Hide Banner";
    //    }
    //    else
    //    {
    //        MaxSdk.HideBanner(BannerAdUnitId);
    //        //showBannerButton.GetComponentInChildren<Text>().text = "Show Banner";
    //    }

    //    isBannerShowing = !isBannerShowing;
    //}

    private void OnBannerAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Banner ad is ready to be shown.
        // If you have already called MaxSdk.ShowBanner(BannerAdUnitId) it will automatically be shown on the next ad refresh.
        Debug.Log("Banner ad loaded");
    }

    private void OnBannerAdFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        // Banner ad failed to load. MAX will automatically try loading a new ad internally.
        Debug.Log("Banner ad failed to load with error code: " + errorInfo.Code);
    }

    private void OnBannerAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("Banner ad clicked");
    }

    private void OnBannerAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Banner ad revenue paid. Use this callback to track user revenue.
        Debug.Log("Banner ad revenue paid");

        // Ad revenue
        double revenue = adInfo.Revenue;

        // Miscellaneous data
        string countryCode = MaxSdk.GetSdkConfiguration().CountryCode; // "US" for the United States, etc - Note: Do not confuse this with currency code which is "USD" in most cases!
        string networkName = adInfo.NetworkName; // Display name of the network that showed the ad (e.g. "AdColony")
        string adUnitIdentifier = adInfo.AdUnitIdentifier; // The MAX Ad Unit ID
        string placement = adInfo.Placement; // The placement this ad's postbacks are tied to

        //TrackAdRevenue(adInfo);
    }

    #endregion

    #region MREC Ad Methods

    private void InitializeMRecAds()
    {
        // MRECs are sized to 300x250 on phones and tablets
        MaxSdk.CreateMRec(MRecAdUnitId, 0,0);

        MaxSdkCallbacks.MRec.OnAdLoadedEvent += OnMRecAdLoadedEvent;
        MaxSdkCallbacks.MRec.OnAdClickedEvent += OnMRecAdClickedEvent;
        MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent += OnMRecAdRevenuePaidEvent;
    }

    public void ShowMRectangleMax()
    {
        // Attach Callbacks
        MaxSdkCallbacks.MRec.OnAdLoadedEvent += OnMRecAdLoadedEvent;
        MaxSdkCallbacks.MRec.OnAdLoadFailedEvent += OnMRecAdFailedEvent;
        MaxSdkCallbacks.MRec.OnAdClickedEvent += OnMRecAdClickedEvent;
        MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent += OnMRecAdRevenuePaidEvent;

        // MRECs are automatically sized to 300x250.
        MaxSdk.CreateMRec(MRecAdUnitId, AdsManager.Instance.MRecPosition);
        // send ad event
        FirebaseAnalytics.LogEvent("Applovin" + "_MREC" + "_success");
    }
    public void HideMRectangleMax()
    {
        MaxSdk.HideMRec(MRecAdUnitId);
    }
    //private void ToggleMRecVisibility()
    //{
    //    if (!isMRecShowing)
    //    {
    //        MaxSdk.ShowMRec(MRecAdUnitId);
    //        showMRecButton.GetComponentInChildren<Text>().text = "Hide MREC";
    //    }
    //    else
    //    {
    //        MaxSdk.HideMRec(MRecAdUnitId);
    //        showMRecButton.GetComponentInChildren<Text>().text = "Show MREC";
    //    }

    //    isMRecShowing = !isMRecShowing;
    //}

    private void OnMRecAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // MRec ad is ready to be shown.
        // If you have already called MaxSdk.ShowMRec(MRecAdUnitId) it will automatically be shown on the next MRec refresh.
        Debug.Log("MRec ad loaded");
    }

    private void OnMRecAdFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        // MRec ad failed to load. MAX will automatically try loading a new ad internally.
        Debug.Log("MRec ad failed to load with error code: " + errorInfo.Code);
    }

    private void OnMRecAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log("MRec ad clicked");
    }

    private void OnMRecAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // MRec ad revenue paid. Use this callback to track user revenue.
        Debug.Log("MRec ad revenue paid");

        // Ad revenue
        double revenue = adInfo.Revenue;

        // Miscellaneous data
        string countryCode = MaxSdk.GetSdkConfiguration().CountryCode; // "US" for the United States, etc - Note: Do not confuse this with currency code which is "USD"!
        string networkName = adInfo.NetworkName; // Display name of the network that showed the ad (e.g. "AdColony")
        string adUnitIdentifier = adInfo.AdUnitIdentifier; // The MAX Ad Unit ID
        string placement = adInfo.Placement; // The placement this ad's postbacks are tied to

        //TrackAdRevenue(adInfo);
    }

    #endregion

    //private void TrackAdRevenue(MaxSdkBase.AdInfo adInfo)
    //{
    //    AdjustAdRevenue adjustAdRevenue = new AdjustAdRevenue(AdjustConfig.AdjustAdRevenueSourceAppLovinMAX);

    //    adjustAdRevenue.setRevenue(adInfo.Revenue, "USD");
    //    adjustAdRevenue.setAdRevenueNetwork(adInfo.NetworkName);
    //    adjustAdRevenue.setAdRevenueUnit(adInfo.AdUnitIdentifier);
    //    adjustAdRevenue.setAdRevenuePlacement(adInfo.Placement);

    //    Adjust.trackAdRevenue(adjustAdRevenue);
    //}
}
*/