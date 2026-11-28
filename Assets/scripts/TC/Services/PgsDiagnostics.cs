using System;
using UnityEngine;

/// <summary>
/// Diagnostics for Google Play Games sign-in. Everything is logged under the tag "[PGS]".
/// Filter on a device with:  adb logcat | grep -E "PGS|GamesUnitySDK|PlayGamesServices|Games"
/// </summary>
public static class PgsDiagnostics
{
    public const string Tag = "[PGS]";
    static bool _envLogged;

    public static void Log(string msg) { Debug.Log(Tag + " " + msg); }
    public static void Warn(string msg) { Debug.LogWarning(Tag + " " + msg); }

    /// <summary>Turns on the plugin's verbose logging. Call before PlayGamesPlatform.Activate().</summary>
    public static void EnableDebugLogging()
    {
#if UNITY_ANDROID
        try { GooglePlayGames.PlayGamesPlatform.DebugLogEnabled = true; }
        catch (Exception e) { Warn("DebugLogEnabled failed: " + e.Message); }
#endif
    }

    /// <summary>Logs package, APP_ID meta-data, installed signing SHA-1, installer, GMS and Play Games app info (once per session).</summary>
    public static void LogEnvironment(bool force = false)
    {
        KeepPlayGamesApi();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_envLogged && !force) return;
        _envLogged = true;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
            {
                string pkg = activity.Call<string>("getPackageName");
                Log("package=" + pkg + " unity=" + Application.unityVersion + " device=" + SystemInfo.deviceModel
                    + " os=" + SystemInfo.operatingSystem + " dev=" + Debug.isDebugBuild);

                // APP_ID meta-data baked into the merged manifest (what the Games SDK reads).
                try
                {
                    using (var ai = pm.Call<AndroidJavaObject>("getApplicationInfo", pkg, 128 /* GET_META_DATA */))
                    using (var meta = ai.Get<AndroidJavaObject>("metaData"))
                    {
                        if (meta == null) Warn("manifest meta-data bundle is NULL");
                        else
                        {
                            Log("manifest com.google.android.gms.games.APP_ID=" + meta.Call<string>("getString", "com.google.android.gms.games.APP_ID")
                                + " (expected " + TCGameServicesConfig.PlayGamesAppId + ")");
                            Log("manifest games.unityVersion=" + meta.Call<string>("getString", "com.google.android.gms.games.unityVersion"));
                        }
                    }
                }
                catch (Exception e) { Warn("APP_ID read failed: " + e.Message); }

                // SHA-1 of the certificate that signed the installed APK. This must match a Play Games credential.
                try
                {
                    using (var pi = pm.Call<AndroidJavaObject>("getPackageInfo", pkg, 0x08000000 /* GET_SIGNING_CERTIFICATES */))
                    using (var si = pi.Get<AndroidJavaObject>("signingInfo"))
                    using (var signers = si.Call<AndroidJavaObject>("getApkContentsSigners"))
                    {
                        var arr = AndroidJNIHelper.ConvertFromJNIArray<AndroidJavaObject[]>(signers.GetRawObject());
                        foreach (var sig in arr)
                        {
                            byte[] cert = sig.Call<byte[]>("toByteArray");
                            using (var sha1 = System.Security.Cryptography.SHA1.Create())
                                Log("installed APK signing SHA-1=" + BitConverter.ToString(sha1.ComputeHash(cert)));
                        }
                    }
                }
                catch (Exception e) { Warn("signing SHA-1 read failed: " + e.Message); }

                try { Log("installer=" + pm.Call<string>("getInstallerPackageName", pkg)); } catch { }

                foreach (var other in new[] { "com.google.android.play.games", "com.google.android.gms", "com.android.vending" })
                {
                    try
                    {
                        using (var opi = pm.Call<AndroidJavaObject>("getPackageInfo", other, 0))
                            Log(other + " version=" + opi.Get<string>("versionName"));
                    }
                    catch (Exception) { Warn(other + " NOT installed"); }
                }

                try
                {
                    using (var gaa = new AndroidJavaClass("com.google.android.gms.common.GoogleApiAvailability"))
                    using (var inst = gaa.CallStatic<AndroidJavaObject>("getInstance"))
                        Log("GoogleApiAvailability.isGooglePlayServicesAvailable=" + inst.Call<int>("isGooglePlayServicesAvailable", activity) + " (0 = SUCCESS)");
                }
                catch (Exception e) { Warn("GMS availability check failed: " + e.Message); }

                foreach (var cls in new[] { "com.google.android.gms.games.PlayGames", "com.google.android.gms.games.PlayGamesSdk", "com.google.games.bridge.NativeBridgeActivity" })
                {
                    try { using (new AndroidJavaClass(cls)) Log("class present: " + cls); }
                    catch (Exception e) { Warn("class MISSING (stripped?): " + cls + " - " + e.Message); }
                }
            }
        }
        catch (Exception e)
        {
            Warn("LogEnvironment exception: " + e);
        }
#endif
    }

    /// <summary>
    /// Direct (non-reflection) references so the IL2CPP linker can never strip the sign-in API that
    /// PlayerProfile looks up by name. Never actually executes.
    /// </summary>
    static void KeepPlayGamesApi()
    {
#if UNITY_ANDROID
        if (Time.frameCount < 0)
        {
            var p = GooglePlayGames.PlayGamesPlatform.Instance;
            p.ManuallyAuthenticate((Action<GooglePlayGames.BasicApi.SignInStatus>)null);
            p.Authenticate((Action<GooglePlayGames.BasicApi.SignInStatus>)null);
            p.GetUserDisplayName();
            p.GetUserImageUrl();
        }
#endif
    }

    public static string IsAuthenticatedText()
    {
#if UNITY_ANDROID
        try { return GooglePlayGames.PlayGamesPlatform.Instance.IsAuthenticated().ToString(); }
        catch (Exception e) { return "EXC(" + e.GetType().Name + ": " + e.Message + ")"; }
#else
        return "n/a";
#endif
    }
}
