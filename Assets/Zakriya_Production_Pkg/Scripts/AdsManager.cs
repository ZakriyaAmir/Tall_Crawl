using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AdsManager : MonoBehaviour
{
    [Header("Note: Admob ads are first priority,\nApplovinMax ads will load after checking Admob availability")]

    public bool testMode = true;

    [Header("Admob Keys")]
    //public string AndroidGoogleAppID;
    public string AdmobBannerUnitId;
    public string AdmobInterstitialUnitId;
    public string AdmobRewardedUnitId;
    public string AdmobRewardedInterstitialUnitId;
    public string AdmobAppOpenAdUnitId;
    public bool admobInterstitial = true;
    public bool admobBanner = true;
    public bool admobRewarded = true;
    public bool admobRewardedInterstitial = false;
    public bool admobOpenAd = false;

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

    public BannerPosition bannerPosition = BannerPosition.Bottom;
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

    private static AdsManager _instance;
    public static AdsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AdsManager>();
                if (_instance == null)
                {
                    var prefab = Resources.Load<GameObject>("Ads+FirebaseManager");
                    if (prefab != null)
                    {
                        var go = Instantiate(prefab);
                        go.name = "Ads+FirebaseManager";
                        _instance = go.GetComponent<AdsManager>();
                    }
                    else
                    {
                        var go = new GameObject("AdsManager");
                        _instance = go.AddComponent<AdsManager>();
                    }
                }
            }
            return _instance;
        }
        set { _instance = value; }
    }
    private bool maxBan;
    private bool admobBan;

    //Test variables
    public int money = 0;
    public Text moneytext;

    public void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        testMode = true;
        admobInterstitial = true;
        admobBanner = true;
        admobRewarded = true;
        ApplyOfficialTestIds();

        if (GetComponent<AdmobManager>() == null)
        {
            gameObject.AddComponent<AdmobManager>();
        }
        if (useMaxAds) 
        {
            //gameObject.AddComponent<MediationManager>();
        }
    }

    public void RunInterstitialAd()
    {
        if (PlayerPrefs.GetInt("noAds", 0) == 1 || TCSave.NoAds) 
        {
            Debug.Log("[AdsManager] No ads active — skipping interstitial");
            return;
        }

        if (admobInterstitial && AdmobManager.Instance != null)
        {
            if (AdmobManager.Instance.ShowInterstitialAd())
            {
                Debug.Log("[AdsManager] Admob Interstitial Ran");
            }
            else
            {
                Debug.LogWarning("[AdsManager] Admob Interstitial was not ready yet — preloading");
            }
        }
        else if (maxInterstitial)
        {
            Debug.Log("[AdsManager] Max Interstitial Ran");
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
        if (PlayerPrefs.GetInt("noAds", 0) == 1 || TCSave.NoAds)
        {
            Debug.Log("[AdsManager] No ads active — skipping banner");
            return;
        }

        if (admobBanner && AdmobManager.Instance != null)
        {
            AdmobManager.Instance.showBannerAd();
            admobBan = true;
        }
        else if (maxBanner)
        {
            Debug.Log("Max Banner Ran");
            maxBan = true;
        }
    }

    public void HideBanner()
    {
        if (admobBan && AdmobManager.Instance != null)
        {
            AdmobManager.Instance.HideBannerAd();
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
        if (admobRewarded && AdmobManager.Instance != null)
        {
            if (AdmobManager.Instance.ShowRewardedAd(rewardCallback))
            {
                Debug.Log("[AdsManager] Admob Rewarded Ran");
                available = true;
            }
            else
            {
                Debug.LogWarning("[AdsManager] Admob Rewarded was not ready yet — preloading");
            }
        }
        else if (maxRewarded)
        {
            Debug.Log("[AdsManager] Max Rewarded Ran");
            available = false; // MAX not integrated: report unavailable so caller can fall back
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

    /// Google's documented test units for Android & iOS: https://developers.google.com/admob/unity/test-ads
    public void ApplyOfficialTestIds()
    {
#if UNITY_IOS || UNITY_IPHONE
        AdmobBannerUnitId = "ca-app-pub-3940256099942544/2934735716";
        AdmobInterstitialUnitId = "ca-app-pub-3940256099942544/4411468910";
        AdmobRewardedUnitId = "ca-app-pub-3940256099942544/1712485313";
        AdmobRewardedInterstitialUnitId = "ca-app-pub-3940256099942544/6978759866";
        AdmobAppOpenAdUnitId = "ca-app-pub-3940256099942544/5662855259";
#else
        AdmobBannerUnitId = "ca-app-pub-3940256099942544/6300978111";
        AdmobInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";
        AdmobRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        AdmobRewardedInterstitialUnitId = "ca-app-pub-3940256099942544/5354046379";
        AdmobAppOpenAdUnitId = "ca-app-pub-3940256099942544/3419835294";
#endif
    }
}