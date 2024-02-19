using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AdsManager : MonoBehaviour
{
    [Header("Note: Admob ads are first priority,\nApplovinMax ads will load after checking Admob availability")]

    public bool testMode;

    [Header("Admob Keys")]
    //public string AndroidGoogleAppID;
    public string AdmobBannerUnitId;
    public string AdmobInterstitialUnitId;
    public string AdmobRewardedUnitId;
    public string AdmobRewardedInterstitialUnitId;
    public string AdmobAppOpenAdUnitId;
    public bool admobInterstitial;
    public bool admobBanner;
    public bool admobRewarded;
    public bool admobRewardedInterstitial;
    public bool admobOpenAd;

    // These ad units are configured to always serve test ads.
    [Header("Max Keys")]
    public string MaxSdkKey;
    public string MaxInterstitialAdUnitId;
    public string MaxRewardedAdUnitId;
    public string MaxRewardedInterstitialAdUnitId;
    public string MaxBannerAdUnitId;
    public string MaxMRecAdUnitId;
    public bool useMaxAds;
    public bool maxInterstitial;
    public bool maxBanner;
    public bool maxRewarded;
    public bool maxRewardedInterstitial;
    public bool maxMRec;

    public BannerPosition bannerPosition;
    public enum BannerPosition
    {
        Top,
        Bottom
    }

    public enum _MRecPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        Centered,
        CenterLeft,
        CenterRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    public _MRecPosition MRecPos = _MRecPosition.BottomCenter;
    [HideInInspector]
    //public MaxSdkBase.AdViewPosition MRecPosition;

    public static AdsManager Instance;
    private bool maxBan;
    private bool admobBan;

    //Test variables
    public int money = 0;
    public Text moneytext;

    public void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (testMode)
        {
            //AndroidGoogleAppID = "ca-app-pub-3940256099942544~3347511713";
            AdmobBannerUnitId = "ca-app-pub-3940256099942544/6300978111";
            AdmobInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";
            AdmobRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
            AdmobRewardedInterstitialUnitId = "ca-app-pub-3940256099942544/5354046379";
            AdmobAppOpenAdUnitId = "ca-app-pub-3940256099942544/3419835294";

            MaxSdkKey = "sVWGuOQVG4gzyhb-2Qb6sRTv8qavlPzA-5V-1DcTfCWvHWTNRTTB12ENHdoQyLpX5rp1LVcPGq9Nol8469q4z7";
            MaxInterstitialAdUnitId = "f7794de01adb3bac";
            MaxRewardedAdUnitId = "2ac7a59f9db948ee";
            MaxRewardedInterstitialAdUnitId = "2ac7a59f9db948ee";
            MaxBannerAdUnitId = "a7b90d54cef49c8a";
            MaxMRecAdUnitId = "bcb5a19fc4a78113";
        }
        gameObject.AddComponent<AdmobManager>();
        if (useMaxAds) 
        {
            //gameObject.AddComponent<MediationManager>();
        }
    }

    public void RunInterstitialAd()
    {
        if (PlayerPrefs.GetInt("noAds", 0) == 1) 
        {
            Debug.Log("No ads bundle bought");
            return;
        }

        if (admobInterstitial)
        {
            if (AdmobManager.Instance.ShowInterstitialAd() == true)
            {
                Debug.Log("Admob Interstitial Ran");
            }

            else if (maxInterstitial)
            {
                Debug.Log("Max Interstitial Ran");
                //MediationManager.instance.ShowInterstitialMax();
            }
        }
        else if (maxInterstitial)
        {
            Debug.Log("Max Interstitial Ran");
            //MediationManager.instance.ShowInterstitialMax();
        }
    }

    public void RunAdmobOpenAd()
    {
        if (PlayerPrefs.GetInt("noAds", 0) == 1)
        {
            Debug.Log("No ads bundle bought");
            return;
        }

        if (admobOpenAd)
        {
            AdmobManager.Instance.ShowAppOpenAd();
        }
    }

    public void RunBannerAd()
    {
        if (PlayerPrefs.GetInt("noAds", 0) == 1)
        {
            Debug.Log("No ads bundle bought");
            return;
        }

        if (!admobBan && !maxBan)
        {
            if (admobBanner)
            {
                if (AdmobManager.Instance.showBannerAd() == true)
                {
                    Debug.Log("Admob Banner Ran");
                    admobBan = true;
                }
                else if (maxBanner)
                {
                    Debug.Log("Max Banner Ran");
                    //MediationManager.instance.ShowBannerMax();
                    maxBan = true;
                }
            }
            else if (maxBanner)
            {
                Debug.Log("Max Banner Ran");
                //MediationManager.instance.ShowBannerMax();
                maxBan = true;
            }
        }
    }

    public void HideBanner()
    {
        if (admobBan)
        {
            AdmobManager.Instance.DestroyBannerView();
            AdmobManager.Instance.LoadBannerAd();
            admobBan = false;
        }
        if (maxBan) 
        {
            //MediationManager.instance.HideBannerMax();
            maxBan = false;
        }
    }

    public bool RunRewardedAd(Action rewardCallback)
    {
        bool available = false;
        if (admobRewarded)
        {
            if (AdmobManager.Instance.ShowRewardedAd(rewardCallback) == true)
            {
                Debug.Log("Admob Rewarded Ran");
                available = true;
            }
            else if (maxRewarded)
            {
                Debug.Log("Max Rewarded Ran");
                available = true;
                //MediationManager.instance.ShowRewardedAdMax();
            }
        }
        else if (maxRewarded)
        {
            Debug.Log("Max Rewarded Ran");
            available = true;
            //MediationManager.instance.ShowRewardedAdMax();
        }
        return available;
    }

    public void RunRewardedInterstitialAd(Action rewardCallback)
    {
        if (admobRewardedInterstitial)
        {
            if (AdmobManager.Instance.ShowRewardedInterstitialAd(rewardCallback) == true)
            {
                Debug.Log("Admob Rewarded Ran");
            }
            else if (maxRewardedInterstitial)
            {
                Debug.Log("Max Rewarded Ran");
                //MediationManager.instance.ShowRewardedInterstitialAd();
            }
        }
        else if (maxRewardedInterstitial)
        {
            Debug.Log("Max Rewarded Ran");
            //MediationManager.instance.ShowRewardedInterstitialAd();
        }
    }

    public void ReceiveMaxReward() 
    {
        Debug.Log("Received Max Reward");
    }
    
    public void RunMRecAd()
    {
        if (PlayerPrefs.GetInt("noAds", 0) == 1)
        {
            Debug.Log("No ads bundle bought");
            return;
        }

        if (maxMRec)
        {
            Debug.Log("Max MRec Ran");
            //MediationManager.instance.ShowMRectangleMax();
        }
    }

    public void HideMRecAd()
    {
        if (maxMRec)
        {
            Debug.Log("Max MRec Hidden");
            //MediationManager.instance.HideMRectangleMax();
        }
    }

    //It is an example function to add money externally after successfully watching rewarded ad
    //run such functions instead of direcly using RunRewardedAd() 
    public void getReward()
    {
        //Initialize Admob reward callback in the script in which you are required to use rewarded ad for admob
        RunRewardedAd(() => grantReward());
        //Initialize Max reward callback in the script in which you are required to use rewarded ad for applovin
        //MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;
    }

    public void getRewardInterstitial()
    {
        //Initialize Admob reward callback in the script in which you are required to use rewarded ad for admob
        RunRewardedInterstitialAd(() => grantReward());
        //Initialize Max reward callback in the script in which you are required to use rewarded ad for applovin
        //MaxSdkCallbacks.RewardedInterstitial.OnAdReceivedRewardEvent += OnRewardedInterstitialAdReceivedRewardEvent;
    }

    //Rewarded sample callback methods for Applovin Max
    /*private void OnRewardedAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
    {
        grantReward();
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnRewardedAdReceivedRewardEvent;
    }
    //Rewarded sample callback methods for Applovin Max
    private void OnRewardedInterstitialAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
    {
        grantReward();
        MaxSdkCallbacks.RewardedInterstitial.OnAdReceivedRewardEvent -= OnRewardedInterstitialAdReceivedRewardEvent;
    }*/
    public void grantReward()
    {
        Debug.Log("Reward granted");
        money += 10;
        moneytext.text = "Total Reward: " + money.ToString();
    }
}