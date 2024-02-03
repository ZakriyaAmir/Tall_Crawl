using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using GoogleMobileAds.Api;
using System.Linq;
using System.Data;

public class adsplugin : MonoBehaviour
{
    public static adsplugin instance;
    private BannerView bannerView;
    private InterstitialAd interstitial;
    private RewardedAd rewardedAd;


    public UnityEvent OnwatchvideoAd;

    private const string banner_Id = "ca-app-pub-4987175712306793/3348628581";
    private const string Init_adId = "ca-app-pub-4987175712306793/9597523925";
    private const string rewarded_AdId = "ca-app-pub-4987175712306793/5974791927";

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }
    void Start()
    {
        // Initialize the Google Mobile Ads SDK.
        MobileAds.Initialize(initStatus => { });
    }
    #region Banner Ad
    public void RequestBanner()
    {

#if UNITY_ANDROID
        string adUnitId = banner_Id;
#elif UNITY_IPHONE
            string adUnitId = "ca-app-pub-3940256099942544/2934735716";
#else
            string adUnitId = "unexpected_platform";
#endif

        this.bannerView = new BannerView(adUnitId, AdSize.Banner, AdPosition.Top);

        // Called when an ad request has successfully loaded.
        this.bannerView.OnAdLoaded += this.HandleOnAdLoaded;
        // Called when an ad request failed to load.
        //this.bannerView.OnAdFailedToLoad += this.HandleOnAdFailedToLoad;
        // Called when an ad is clicked.
        this.bannerView.OnAdOpening += this.HandleOnAdOpened;
        // Called when the user returned from the app after an ad click.
        this.bannerView.OnAdClosed += this.HandleOnAdClosed;
        // Called when the ad click caused the user to leave the application.
        //this.bannerView.OnAdLeavingApplication += this.HandleOnAdLeavingApplication;

        // Create an empty ad request.
        AdRequest request = new AdRequest.Builder().Build();

        // Load the banner with the request.
        this.bannerView.LoadAd(request);
    }

    public void HandleOnAdLoaded(object sender, EventArgs args)
    {
        MonoBehaviour.print("HandleAdLoaded event received");
    }

    public void HandleOnAdFailedToLoad(object sender, AdFailedToLoadEventArgs args)
    {
        //MonoBehaviour.print("HandleFailedToReceiveAd event received with message: "
        //+ args.Message);
    }

    public void HandleOnAdOpened(object sender, EventArgs args)
    {
        MonoBehaviour.print("HandleAdOpened event received");
    }

    public void HandleOnAdClosed(object sender, EventArgs args)
    {
        MonoBehaviour.print("HandleAdClosed event received");
    }

    public void HandleOnAdLeavingApplication(object sender, EventArgs args)
    {
        MonoBehaviour.print("HandleAdLeavingApplication event received");
    }
    //public void HandleOnAdFailedToLoad(object sender, AdFailedToLoadEventArgs args)
    //{
    //    //MonoBehaviour.print("Banner failed to load: " + args.Message);
    //    // Handle the ad failed to load event.
    //}
    #endregion
    #region Init Ads
    public void Showinterstial()
    {
        if (interstitial.IsLoaded())
        {
            interstitial.Show();
        }
    }
    public void RequestInterstitial()
    {
#if UNITY_ANDROID
        string adUnitId = Init_adId;
#elif UNITY_IPHONE
        string adUnitId = "ca-app-pub-3940256099942544/4411468910";
#else
        string adUnitId = "unexpected_platform";
#endif

        // Initialize an InterstitialAd.
        this.interstitial = new InterstitialAd(adUnitId);

        // Called when an ad request has successfully loaded.
        this.interstitial.OnAdLoaded += HandleOnAdLoadedInterstitial;
        // Called when an ad request failed to load.
        this.interstitial.OnAdFailedToLoad += HandleOnAdFailedToLoadInterstitial;
        // Called when an ad is shown.
        this.interstitial.OnAdOpening += HandleOnAdOpenedInterstitial;
        // Called when the ad is closed.
        this.interstitial.OnAdClosed += HandleOnAdClosedInterstitial;
        // Called when the ad click caused the user to leave the application.
        //this.interstitial.OnAdLeavingApplication += HandleOnAdLeavingApplication;

        // Create an empty ad request.
        AdRequest request = new AdRequest.Builder().Build();
        // Load the interstitial with the request.
        this.interstitial.LoadAd(request);
    }

    public void HandleOnAdLoadedInterstitial(object sender, EventArgs args)
    {
        MonoBehaviour.print("HandleAdLoaded event received");
    }

    public void HandleOnAdFailedToLoadInterstitial(object sender, AdFailedToLoadEventArgs args)
    {
        //MonoBehaviour.print("HandleFailedToReceiveAd event received with message: "
        //+ args.Message);
    }

    public void HandleOnAdOpenedInterstitial(object sender, EventArgs args)
    {
        MonoBehaviour.print("HandleAdOpened event received");
    }

    public void HandleOnAdClosedInterstitial(object sender, EventArgs args)
    {
        interstitial.Destroy();
        RequestInterstitial();
        MonoBehaviour.print("HandleAdClosed event received");
    }

    public void HandleOnAdLeavingApplicationInterstitial(object sender, EventArgs args)
    {
        MonoBehaviour.print("HandleAdLeavingApplication event received");
    }
    // The OnAdFailedToLoad event contains special event arguments.It passes an instance of HandleAdFailedToLoadEventArgs with a Message describing the error:


    //public void HandleOnAdFailedToLoadInterstitial(object sender, AdFailedToLoadEventArgs args)
    //    {
    //        print("Interstitial failed to load: " + args.Message);
    //        // Handle the ad failed to load event.
    //    }
    #endregion
    #region Rewarded ad
    public void ShowRewardedAd()
    {
        if (this.rewardedAd.IsLoaded())
        {
            this.rewardedAd.Show();
        }
        
    }
    public void CreateAndLoadRewardedAd()
    {
#if UNITY_ANDROID
        string adUnitId = rewarded_AdId;
#elif UNITY_IPHONE
            string adUnitId = "ca-app-pub-3940256099942544/1712485313";
#else
            string adUnitId = "unexpected_platform";
#endif

        this.rewardedAd = new RewardedAd(adUnitId);

        this.rewardedAd.OnAdLoaded += HandleRewardedAdLoaded;
        this.rewardedAd.OnUserEarnedReward += HandleUserEarnedReward;
        this.rewardedAd.OnAdClosed += HandleRewardedAdClosed;

        // Create an empty ad request.
        AdRequest request = new AdRequest.Builder().Build();
        // Load the rewarded ad with the request.
        this.rewardedAd.LoadAd(request);
    }

    public void HandleRewardedAdLoaded(object sender, EventArgs args)
    {
        Debug.Log("Zakriya LOADED");

        MonoBehaviour.print("HandleRewardedAdLoaded event received");
    }

    public void HandleRewardedAdFailedToLoad(object sender, AdFailedToLoadEventArgs args)
    {
        Debug.Log("Zakriya LOAD FAILED");

        //MonoBehaviour.print(
        //    "HandleRewardedAdFailedToLoad event received with message: "
        //                     + args.Message);
        //GA Event
        MyAnalytics.instance.RewardedFailedAd(adsplugin.rewarded_AdId);
    }

    public void HandleRewardedAdOpening(object sender, EventArgs args)
    {
        Debug.Log("Zakriya STARTED");
        MonoBehaviour.print("HandleRewardedAdOpening event received");
    }

    public void HandleRewardedAdFailedToShow(object sender, AdErrorEventArgs args)
    {
        Debug.Log("Zakriya FAILED");
        MonoBehaviour.print(
            "HandleRewardedAdFailedToShow event received with message: "
                             + args.Message);
    }

    public void HandleRewardedAdClosed(object sender, EventArgs args)
    {
        Debug.Log("Zakriya CLOSED");

        this.CreateAndLoadRewardedAd();
        MonoBehaviour.print("HandleRewardedAdClosed event received");
    }

    public void HandleUserEarnedReward(object sender, Reward args)
    {
        OnwatchvideoAd.Invoke();
        string type = args.Type;
        double amount = args.Amount;
        Debug.Log("Zakriya GIVEN");

        //Give reward on watching video
        GameObject.FindObjectOfType<menu>().RespawnPlayer();

        //GA Event
        MyAnalytics.instance.RewardCollected("Respawned");

        MonoBehaviour.print(
            "HandleRewardedAdRewarded event received for "
                        + amount.ToString() + " " + type);
    }
}
#endregion