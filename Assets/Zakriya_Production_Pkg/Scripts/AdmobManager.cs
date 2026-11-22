using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;

/// Google Mobile Ads (AdMob) wrapper for Tall Crawl.
/// - Official Google TEST ad units only (see AdsManager.ApplyOfficialTestIds).
/// - Every SDK callback is marshalled onto the Unity main thread before touching Unity APIs
///   (Android raises GMA callbacks on a Java thread; StartCoroutine / gameObject / UI calls
///   from there throw and silently kill the ad flow).
/// - Interstitial and rewarded are preloaded after init and reloaded after every close / failure.
public class AdmobManager : MonoBehaviour
{
    public static AdmobManager Instance;

    public const string OFFICIAL_BANNER_TEST_ID = "ca-app-pub-3940256099942544/6300978111";
    public const string OFFICIAL_INTERSTITIAL_TEST_ID = "ca-app-pub-3940256099942544/1033173712";
    public const string OFFICIAL_REWARDED_TEST_ID = "ca-app-pub-3940256099942544/5224354917";

    public bool isRewardedAdReady = false;
    [NonSerialized] public bool isInitialized = false;

    BannerView _bannerView;
    InterstitialAd _interstitialAd;
    RewardedAd _rewardedAd;
    RewardedInterstitialAd _rewardedInterstitialAd;
    AppOpenAd _appOpenAd;

    string _adBannerUnitId, _adInterstitialUnitId, _adRewardedUnitId, _adRewardedInterstitialUnitId, _adAppOpenAdUnitId;
    AdsManager _AdsManager;

    bool _initStarted;
    bool _bannerRequestedVisible, _isBannerLoading, _isBannerLoaded;
    bool _isInterstitialLoading, _isRewardedLoading;
    int _bannerFails, _interstitialFails, _rewardedFails;

    Action _pendingRewardedCallback;
    bool _rewardEarned;

    /// Tracks how many full-screen ads (interstitial/rewarded) are currently active.
    static int _activeFullScreenAds = 0;
    static float _previousTimeScale = 1f;

    /// Fired on the main thread when an interstitial or rewarded ad opens.
    public static event Action OnFullScreenAdOpened;

    /// Fired on the main thread when an interstitial or rewarded ad closes.
    public static event Action OnFullScreenAdClosed;

    public static void HandleAdOpened()
    {
        _activeFullScreenAds++;
        if (_activeFullScreenAds == 1)
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Debug.Log("[AdmobManager] Full screen ad opened: paused time & muted audio.");
        }
        OnFullScreenAdOpened?.Invoke();
    }

    public static void HandleAdClosed()
    {
        if (_activeFullScreenAds > 0)
        {
            _activeFullScreenAds--;
            if (_activeFullScreenAds == 0)
            {
                AudioListener.pause = false;
                TCSave.Apply();
                // Restore timeScale only if game is not paused / game over
                var gm = gameManager.Instance;
                var pl = player.Instance;
                if ((gm != null && gm.IsPaused) || (pl != null && pl.GameOver))
                {
                    // keep paused / frozen
                }
                else
                {
                    Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
                }
                Debug.Log("[AdmobManager] Full screen ad closed: unpaused time & restored audio.");
            }
        }
        OnFullScreenAdClosed?.Invoke();
    }

    // ---------------------------------------------------------------- main-thread dispatch
    static readonly ConcurrentQueue<Action> _mainQueue = new ConcurrentQueue<Action>();
    static void OnMain(Action a) { if (a != null) _mainQueue.Enqueue(a); }

    void Update()
    {
        while (_mainQueue.TryDequeue(out var a))
        {
            try { a(); } catch (Exception e) { Debug.LogException(e); }
        }
    }

    void LogFirebaseEvent(string eventName) { TCServices.Log(eventName); }

    public void RefreshUnitIds(bool forceReset = false)
    {
        if (_AdsManager == null) _AdsManager = GetComponent<AdsManager>();
        if (!forceReset && !string.IsNullOrEmpty(_adRewardedUnitId)) return;
        if (_AdsManager != null)
        {
            _AdsManager.ApplyOfficialTestIds();
            _adBannerUnitId = _AdsManager.AdmobBannerUnitId;
            _adInterstitialUnitId = _AdsManager.AdmobInterstitialUnitId;
            _adRewardedUnitId = _AdsManager.AdmobRewardedUnitId;
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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        RefreshUnitIds(true);
    }

    void Start() { Initialize(); }

    public void Initialize()
    {
        if (_initStarted) return;
        if (!Application.isMobilePlatform && !Application.isEditor) return; // no desktop client
        _initStarted = true;

        MobileAds.SetRequestConfiguration(new RequestConfiguration
        {
            TestDeviceIds = new List<string> { AdRequest.TestDeviceSimulator, "AAF6E9CA381F9FB97840D63DB7169163" }
        });

        Debug.Log("[AdmobManager] Initializing Google Mobile Ads SDK...");
        MobileAds.Initialize(status => OnMain(() =>
        {
            isInitialized = true;
            Debug.Log("[AdmobManager] Google Mobile Ads initialized.");
            RefreshUnitIds(false);
            LoadInterstitialAd();
            LoadRewardedAd();
            if (_bannerRequestedVisible) showBannerAd();
        }));
    }

    static AdRequest NewRequest() { return new AdRequest(); }

    IEnumerator After(float delay, Action a)
    {
        yield return new WaitForSecondsRealtime(delay);
        a();
    }

    static float Backoff(int fails) { return Mathf.Min(60f, 4f * Mathf.Pow(2f, Mathf.Max(0, fails - 1))); }

    // ---------------------------------------------------------------- interstitial
    public void LoadInterstitialAd()
    {
        if (!isInitialized || _isInterstitialLoading) return;
        if (_interstitialAd != null && _interstitialAd.CanShowAd()) return;
        if (_interstitialAd != null) { _interstitialAd.Destroy(); _interstitialAd = null; }

        _isInterstitialLoading = true;
        Debug.Log("[AdmobManager] Loading interstitial: " + _adInterstitialUnitId);
        InterstitialAd.Load(_adInterstitialUnitId, NewRequest(), (ad, error) => OnMain(() =>
        {
            _isInterstitialLoading = false;
            if (error != null || ad == null)
            {
                _interstitialFails++;
                Debug.LogWarning("[AdmobManager] Interstitial failed to load: " + error);
                StartCoroutine(After(Backoff(_interstitialFails), LoadInterstitialAd));
                return;
            }
            _interstitialFails = 0;
            Debug.Log("[AdmobManager] Interstitial loaded.");
            _interstitialAd = ad;
            ad.OnAdFullScreenContentOpened += () => OnMain(() =>
            {
                Debug.Log("[AdmobManager] Interstitial opened.");
                HandleAdOpened();
            });
            ad.OnAdFullScreenContentClosed += () => OnMain(() =>
            {
                Debug.Log("[AdmobManager] Interstitial closed — preloading next.");
                if (_interstitialAd == ad) _interstitialAd = null;
                ad.Destroy();
                LoadInterstitialAd();
                HandleAdClosed();
            });
            ad.OnAdFullScreenContentFailed += err => OnMain(() =>
            {
                Debug.LogWarning("[AdmobManager] Interstitial failed to show: " + err);
                if (_interstitialAd == ad) _interstitialAd = null;
                ad.Destroy();
                LoadInterstitialAd();
                HandleAdClosed();
            });
        }));
    }

    public bool IsInterstitialReady => _interstitialAd != null && _interstitialAd.CanShowAd();

    public bool ShowInterstitialAd()
    {
        if (IsInterstitialReady)
        {
            Debug.Log("[AdmobManager] Showing interstitial.");
            _interstitialAd.Show();
            LogFirebaseEvent("Admob_Interstitial_success");
            return true;
        }
        Debug.LogWarning("[AdmobManager] Interstitial not ready — loading.");
        LoadInterstitialAd();
        LogFirebaseEvent("Admob_Interstitial_failed");
        return false;
    }

    public bool IsRewardedReady => _rewardedAd != null && _rewardedAd.CanShowAd();

    // ---------------------------------------------------------------- rewarded
    public void LoadRewardedAd()
    {
        if (!isInitialized || _isRewardedLoading) return;
        if (_rewardedAd != null && _rewardedAd.CanShowAd()) { isRewardedAdReady = true; return; }
        if (_rewardedAd != null) { _rewardedAd.Destroy(); _rewardedAd = null; }

        isRewardedAdReady = false;
        _isRewardedLoading = true;
        Debug.Log("[AdmobManager] Loading rewarded: " + _adRewardedUnitId);
        RewardedAd.Load(_adRewardedUnitId, NewRequest(), (ad, error) => OnMain(() =>
        {
            _isRewardedLoading = false;
            if (error != null || ad == null)
            {
                _rewardedFails++;
                Debug.LogWarning("[AdmobManager] Rewarded failed to load: " + error);
                StartCoroutine(After(Backoff(_rewardedFails), LoadRewardedAd));
                return;
            }
            _rewardedFails = 0;
            Debug.Log("[AdmobManager] Rewarded loaded.");
            _rewardedAd = ad;
            isRewardedAdReady = true;
            ad.OnAdFullScreenContentOpened += () => OnMain(() =>
            {
                Debug.Log("[AdmobManager] Rewarded opened.");
                HandleAdOpened();
            });
            ad.OnAdFullScreenContentClosed += () => OnMain(() =>
            {
                Debug.Log("[AdmobManager] Rewarded closed — preloading next.");
                if (_rewardedAd == ad) { _rewardedAd = null; isRewardedAdReady = false; }
                ad.Destroy();
                LoadRewardedAd();

                if (_rewardEarned)
                {
                    _rewardEarned = false;
                    var cb = _pendingRewardedCallback;
                    _pendingRewardedCallback = null;
                    cb?.Invoke();
                }
                else
                {
                    _pendingRewardedCallback = null;
                }

                HandleAdClosed();
            });
            ad.OnAdFullScreenContentFailed += err => OnMain(() =>
            {
                Debug.LogWarning("[AdmobManager] Rewarded failed to show: " + err);
                if (_rewardedAd == ad) { _rewardedAd = null; isRewardedAdReady = false; }
                _pendingRewardedCallback = null;
                _rewardEarned = false;
                ad.Destroy();
                LoadRewardedAd();
                HandleAdClosed();
            });
        }));
    }

    /// Rewarded ads always play (NoAds does not block them). The reward callback runs on the main thread after ad dismiss.
    public bool ShowRewardedAd(Action rewardCallback)
    {
        if (_rewardedAd == null || !_rewardedAd.CanShowAd())
        {
            isRewardedAdReady = false;
            Debug.LogWarning("[AdmobManager] Rewarded not ready — loading.");
            LoadRewardedAd();
            LogFirebaseEvent("Admob_Rewarded_failed");
            return false;
        }
        isRewardedAdReady = false;
        _pendingRewardedCallback = rewardCallback;
        _rewardEarned = false;
        Debug.Log("[AdmobManager] Showing rewarded.");
        _rewardedAd.Show(reward => OnMain(() =>
        {
            Debug.Log("[AdmobManager] User earned reward " + reward.Type + " x" + reward.Amount);
            _rewardEarned = true;
            LogFirebaseEvent("Admob_Rewarded_success");
        }));
        return true;
    }

    // ---------------------------------------------------------------- rewarded interstitial (unused by the game)
    public void LoadRewardedInterstitialAd()
    {
        if (!isInitialized || string.IsNullOrEmpty(_adRewardedInterstitialUnitId)) return;
        if (_rewardedInterstitialAd != null) { _rewardedInterstitialAd.Destroy(); _rewardedInterstitialAd = null; }
        RewardedInterstitialAd.Load(_adRewardedInterstitialUnitId, NewRequest(), (ad, error) => OnMain(() =>
        {
            if (error != null || ad == null) { Debug.LogWarning("[AdmobManager] Rewarded interstitial failed: " + error); return; }
            _rewardedInterstitialAd = ad;
        }));
    }

    public bool ShowRewardedInterstitialAd(Action rewardCallback)
    {
        if (_rewardedInterstitialAd == null || !_rewardedInterstitialAd.CanShowAd())
        {
            LoadRewardedInterstitialAd();
            LogFirebaseEvent("Admob_RewardedInterstitial_failed");
            return false;
        }
        var ad = _rewardedInterstitialAd;
        _rewardedInterstitialAd = null;
        ad.Show(reward => OnMain(() =>
        {
            rewardCallback?.Invoke();
            LogFirebaseEvent("Admob_RewardedInterstitial_success");
        }));
        ad.OnAdFullScreenContentClosed += () => OnMain(() => { ad.Destroy(); LoadRewardedInterstitialAd(); });
        return true;
    }

    // ---------------------------------------------------------------- app open (disabled by default)
    public bool IsAdAvailable => _appOpenAd != null && _appOpenAd.CanShowAd();

    public void LoadAppOpenAd()
    {
        if (!isInitialized || string.IsNullOrEmpty(_adAppOpenAdUnitId)) return;
        if (_appOpenAd != null) { _appOpenAd.Destroy(); _appOpenAd = null; }
        AppOpenAd.Load(_adAppOpenAdUnitId, NewRequest(), (ad, error) => OnMain(() =>
        {
            if (error != null || ad == null) { Debug.LogWarning("[AdmobManager] App open failed: " + error); return; }
            _appOpenAd = ad;
        }));
    }

    public void ShowAppOpenAd()
    {
        if (IsAdAvailable) _appOpenAd.Show();
        else LoadAppOpenAd();
    }

    // ---------------------------------------------------------------- banner
    public void CreateBannerView()
    {
        if (_bannerView != null) DestroyBannerView();
        AdPosition pos = (_AdsManager != null && _AdsManager.bannerPosition == AdsManager.BannerPosition.Top) ? AdPosition.Top : AdPosition.Bottom;
        AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
        Debug.Log("[AdmobManager] Creating banner " + _adBannerUnitId);
        var view = new BannerView(_adBannerUnitId, size, pos);
        _bannerView = view;

        view.OnBannerAdLoaded += () => OnMain(() =>
        {
            if (_bannerView != view) return;
            _isBannerLoading = false;
            _isBannerLoaded = true;
            _bannerFails = 0;
            float h = 0;
            try { h = view.GetHeightInPixels(); } catch (Exception) { }
            TCSafeArea.BannerHeightPx = h;
            Debug.Log("[AdmobManager] Banner loaded (" + h + " px).");
            if (_bannerRequestedVisible) view.Show(); else view.Hide();
        });
        view.OnBannerAdLoadFailed += error => OnMain(() =>
        {
            if (_bannerView != view) return;
            _isBannerLoading = false;
            _isBannerLoaded = false;
            _bannerFails++;
            Debug.LogWarning("[AdmobManager] Banner failed to load: " + error);
            StartCoroutine(After(Backoff(_bannerFails), () =>
            {
                if (_bannerRequestedVisible && !_isBannerLoaded && !_isBannerLoading) LoadBannerAd();
            }));
        });
    }

    public void LoadBannerAd()
    {
        if (!isInitialized) return;
        if (_bannerView == null) CreateBannerView();
        if (_isBannerLoading) return;
        _isBannerLoading = true;
        Debug.Log("[AdmobManager] Loading banner.");
        _bannerView.LoadAd(NewRequest());
    }

    public bool showBannerAd()
    {
        _bannerRequestedVisible = true;
        if (!isInitialized) { Debug.Log("[AdmobManager] Banner queued until SDK init."); return true; }
        if (_bannerView == null || (!_isBannerLoaded && !_isBannerLoading)) LoadBannerAd();
        else if (_isBannerLoaded) _bannerView.Show();
        LogFirebaseEvent("Admob_Banner_success");
        return true;
    }

    public void HideBannerAd()
    {
        _bannerRequestedVisible = false;
        if (_bannerView != null) _bannerView.Hide();
    }

    public void DestroyBannerView()
    {
        if (_bannerView == null) return;
        _bannerView.Destroy();
        _bannerView = null;
        _isBannerLoaded = false;
        _isBannerLoading = false;
    }
}
