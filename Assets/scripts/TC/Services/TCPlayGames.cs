using System;
using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

/// <summary>
/// Google Play Games Services v2 sign-in for Tall Crawl (ported from Wordwise Solitaire PlayerProfile).
///  - PlayGamesPlatform.Activate() runs first, once.
///  - Silent Authenticate() on app start (RuntimeInitializeOnLoad), never shows UI.
///  - ManuallyAuthenticate() when the player taps a "Sign in with Google Play" button.
/// On success: pushes the best distance to the leaderboard and syncs the Saved Games snapshot.
/// Everything is logged under [PGS]:  adb logcat | grep -E "PGS|GamesUnitySDK|PlayGamesServices|Games"
/// </summary>
public static class TCPlayGames
{
    const string LinkedKey = "tc_gpgs_linked";
    const string NameKey = "tc_gpgs_name";

    /// Fired on the main thread whenever sign-in state or the display name changes.
    public static event Action Changed;

    static bool _activated, _silentTried, _busy, _signedOut;

    public static bool WasLinked => PlayerPrefs.GetInt(LinkedKey, 0) == 1;
    public static string DisplayName
    {
        get
        {
            string n = PlayerPrefs.GetString(NameKey, "");
            return string.IsNullOrWhiteSpace(n) ? "Player" : n;
        }
    }

    public static bool IsSignedIn
    {
        get
        {
            if (_signedOut) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return _activated && PlayGamesPlatform.Instance != null && PlayGamesPlatform.Instance.IsAuthenticated(); }
            catch { return false; }
#else
            return false;
#endif
        }
    }

    public static string UserId
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return IsSignedIn ? PlayGamesPlatform.Instance.GetUserId() : null; } catch { return null; }
#else
            return null;
#endif
        }
    }

    public static string AvatarUrl
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return IsSignedIn ? PlayGamesPlatform.Instance.GetUserImageUrl() : null; } catch { return null; }
#else
            return null;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (TCAutomation.IsAutomatedRun()) return;
        // Do not auto-sign-in on a fresh install; only silent-sign-in if the player previously signed in manually.
        if (WasLinked) TrySilentSignIn();
    }

    static bool EnsureActivated()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_activated) return PlayGamesPlatform.Instance != null;
        try
        {
            PgsDiagnostics.EnableDebugLogging();
            PgsDiagnostics.LogEnvironment();
            PlayGamesPlatform.Activate();
            _activated = true;
            PgsDiagnostics.Log("Activate done; IsAuthenticated=" + PgsDiagnostics.IsAuthenticatedText());
            return PlayGamesPlatform.Instance != null;
        }
        catch (Exception e)
        {
            PgsDiagnostics.Warn("Activate failed: " + e);
            return false;
        }
#else
        return false;
#endif
    }

    /// Silent sign-in (no UI). Safe to call any time; only runs once per session.
    public static void TrySilentSignIn()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_silentTried) return;
        _silentTried = true;
        if (!EnsureActivated()) return;
        try
        {
            if (PlayGamesPlatform.Instance.IsAuthenticated()) { OnSignedIn("startup (already authenticated)"); return; }
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                PgsDiagnostics.Log("startup Authenticate callback SignInStatus=" + status + "; IsAuthenticated=" + PgsDiagnostics.IsAuthenticatedText());
                if (status == SignInStatus.Success) OnSignedIn("startup silent");
                else Changed?.Invoke();
            });
        }
        catch (Exception e) { PgsDiagnostics.Warn("silent Authenticate exception: " + e); }
#else
        Debug.Log("[PGS] Google Play Games sign-in only runs on Android devices.");
#endif
    }

    /// Interactive sign-in from a button. onComplete(success, message) runs on the main thread.
    public static void SignInInteractive(Action<bool, string> onComplete)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (IsSignedIn) { onComplete?.Invoke(true, DisplayName); return; }
        if (_busy) { onComplete?.Invoke(false, "Signing in..."); return; }
        if (!EnsureActivated()) { onComplete?.Invoke(false, "Google Play Games is unavailable"); return; }
        _busy = true;
        PgsDiagnostics.Log("sign-in tapped; calling ManuallyAuthenticate; IsAuthenticated=" + PgsDiagnostics.IsAuthenticatedText());
        TCServices.Log("gpgs_sign_in_tap");
        try
        {
            PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
            {
                PgsDiagnostics.Log("ManuallyAuthenticate callback SignInStatus=" + status + "; IsAuthenticated=" + PgsDiagnostics.IsAuthenticatedText());
                if (status == SignInStatus.Success)
                {
                    _busy = false;
                    OnSignedIn("manual");
                    onComplete?.Invoke(true, DisplayName);
                    return;
                }
                // Diagnostics only: ask the silent API too so the log shows both statuses.
                PlayGamesPlatform.Instance.Authenticate(s2 =>
                {
                    _busy = false;
                    PgsDiagnostics.Log("fallback Authenticate callback SignInStatus=" + s2);
                    if (s2 == SignInStatus.Success) { OnSignedIn("fallback silent"); onComplete?.Invoke(true, DisplayName); }
                    else
                    {
                        TCServices.Log("gpgs_sign_in", ("success", 0), ("status", status.ToString()));
                        Changed?.Invoke();
                        onComplete?.Invoke(false, status == SignInStatus.Canceled ? "Sign-in cancelled" : "Google Play sign-in failed");
                    }
                });
            });
        }
        catch (Exception e)
        {
            _busy = false;
            PgsDiagnostics.Warn("ManuallyAuthenticate exception: " + e);
            onComplete?.Invoke(false, "Google Play sign-in failed");
        }
#else
        onComplete?.Invoke(false, "Google Play Games works on Android devices");
#endif
    }

    public static void SignOut()
    {
        _signedOut = true;
        PlayerPrefs.SetInt(LinkedKey, 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    static void OnSignedIn(string how)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _signedOut = false;
        string name = null;
        try { name = PlayGamesPlatform.Instance.GetUserDisplayName(); } catch { }
        if (!string.IsNullOrWhiteSpace(name)) PlayerPrefs.SetString(NameKey, name.Trim());
        PlayerPrefs.SetInt(LinkedKey, 1);
        PlayerPrefs.Save();
        PgsDiagnostics.Log("signed in (" + how + ") as " + DisplayName);
        TCServices.Log("gpgs_sign_in", ("success", 1), ("how", how));
        Changed?.Invoke();
        // Cloud save first (it may raise the local best), then push the best distance.
        TCCloudSave.SyncWithCloud((ok, msg) => TCLeaderboard.SubmitBest());
#endif
    }
}

/// Batch-mode / capture / playtest runs must never touch Play Games, cloud saves or notification prompts.
public static class TCAutomation
{
    public static bool IsAutomatedRun()
    {
        if (Application.isBatchMode) return true;
        try
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == "-tccapture" || a == "-tcplaytest" || a == "-tcselftest") return true;
        }
        catch { }
        return false;
    }
}
