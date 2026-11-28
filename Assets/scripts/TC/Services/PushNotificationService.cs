using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections;
using Firebase;
using Firebase.Extensions;
using Firebase.Messaging;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Firebase Cloud Messaging for Android player builds.
/// Does not call CheckAndFixDependenciesAsync and does not touch any Firebase API until
/// FirebaseBootstrap.ReadyTask completes with DependencyStatus.Available.
/// The FCM token is logged with Android tag TallCrawlPush (adb logcat | grep -iE "TallCrawlPush|\[Push\]"). Development builds also copy it to the clipboard.
/// </summary>
public sealed class PushNotificationService : MonoBehaviour
{
    public const string TopicAll = "all";
    public const string ChannelId = "tallcrawl_alerts"; // must match tallcrawl_default_notification_channel_id in TallCrawlNotifications.androidlib/res/values/strings.xml
    public const string LogTag = "TallCrawlPush";

    static bool _booted;

#if UNITY_ANDROID && !UNITY_EDITOR
    const string PermissionAskedKey = "tc_fcm_post_notifications_asked";
    const string PermissionAskCountKey = "tc_fcm_post_notifications_ask_count";
    const int MaxPermissionAsks = 3;
    const string PostNotificationsPermission = "android.permission.POST_NOTIFICATIONS";

    int _foregroundNotifyId = 41000;
    int _tokenAttempts;
    bool _messagingStarted;
    bool _tokenWatching;
    string _lastToken;
    static string _lastTokenStatic;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (_booted) return;
        _booted = true;
#if UNITY_ANDROID && !UNITY_EDITOR
        var go = new GameObject(nameof(PushNotificationService));
        DontDestroyOnLoad(go);
        go.AddComponent<PushNotificationService>();
#else
        Debug.Log("[Push] Firebase Messaging is enabled on Android player builds. This run leaves push idle.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void Start()
    {
        Log("PushNotificationService started");
        EnsureChannel();
        LogNotificationState("start");
        StartCoroutine(AskPermissionAfterMenu());
        WaitForFirebase();
    }

    IEnumerator AskPermissionAfterMenu()
    {
        // The SillyCon splash plays inside MainMenu (TCSplashScreen); ask once the menu is showing.
        while (SceneManager.GetActiveScene().name != "MainMenu")
            yield return null;
        yield return new WaitForSecondsRealtime(6f); // splash is ~4 s
        if (AutomatedRun()) yield break;
        RequestPostNotificationsOnce();
    }

    static bool AutomatedRun() => TCAutomation.IsAutomatedRun();

    void OnApplicationFocus(bool focus)
    {
        // Returning from the system permission dialog or from Settings: show the new state.
        if (focus && _messagingStarted) LogNotificationState("focus");
    }

    /// <summary>
    /// Asks for POST_NOTIFICATIONS (Android 13+) on each cold start until it is granted, at most
    /// MaxPermissionAsks times. The old code asked once and remembered the ask even when the dialog
    /// never showed or was dismissed, so notifications stayed blocked forever.
    /// </summary>
    static void RequestPostNotificationsOnce()
    {
        try
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            int sdk = version.GetStatic<int>("SDK_INT");
            if (sdk < 33)
            {
                Log("POST_NOTIFICATIONS not needed (SDK " + sdk + ").");
                return;
            }
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(PostNotificationsPermission))
            {
                Log("POST_NOTIFICATIONS already granted.");
                return;
            }
            int asks = PlayerPrefs.GetInt(PermissionAskCountKey, 0);
            if (asks >= MaxPermissionAsks)
            {
                Log("POST_NOTIFICATIONS is NOT granted and was asked " + asks + " times. Enable it in Settings > Apps > Tall Crawl > Notifications.");
                if (Debug.isDebugBuild) OpenNotificationSettings();
                return;
            }
            PlayerPrefs.SetInt(PermissionAskCountKey, asks + 1);
            PlayerPrefs.SetInt(PermissionAskedKey, 1);
            PlayerPrefs.Save();

            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += p => { Log("POST_NOTIFICATIONS GRANTED."); LogNotificationState("granted"); };
            callbacks.PermissionDenied += p => Log("POST_NOTIFICATIONS DENIED.");
            callbacks.PermissionDeniedAndDontAskAgain += p =>
            {
                Log("POST_NOTIFICATIONS DENIED (don't ask again). Enable it in Settings > Apps > Tall Crawl > Notifications.");
                if (Debug.isDebugBuild) OpenNotificationSettings();
            };
            UnityEngine.Android.Permission.RequestUserPermission(PostNotificationsPermission, callbacks);
            Log("Requested POST_NOTIFICATIONS (ask " + (asks + 1) + " of " + MaxPermissionAsks + ").");
        }
        catch (Exception e)
        {
            Log("Notification permission request failed (" + e.Message + ").");
        }
    }

    public static void OpenNotificationSettings()
    {
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null) return;
            using var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APP_NOTIFICATION_SETTINGS");
            intent.Call<AndroidJavaObject>("putExtra", "android.provider.extra.APP_PACKAGE", activity.Call<string>("getPackageName"));
            activity.Call("startActivity", intent);
        }
        catch (Exception e)
        {
            Log("Could not open notification settings (" + e.Message + ").");
        }
    }

    /// <summary>Logs everything that decides whether a notification can be shown.</summary>
    static void LogNotificationState(string reason)
    {
        try
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            int sdk = version.GetStatic<int>("SDK_INT");
            bool permission = sdk < 33 || UnityEngine.Android.Permission.HasUserAuthorizedPermission(PostNotificationsPermission);
            string enabled = "?";
            string channel = "n/a";
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity != null)
            {
                using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using var manager = context.Call<AndroidJavaObject>("getSystemService", "notification");
                enabled = manager.Call<bool>("areNotificationsEnabled").ToString();
                if (sdk >= 26)
                {
                    using var ch = manager.Call<AndroidJavaObject>("getNotificationChannel", ChannelId);
                    channel = ch == null ? "MISSING" : "importance=" + ch.Call<int>("getImportance") + " (0 = blocked, 3 = default, 4 = high)";
                }
            }
            Log("State [" + reason + "]: sdk=" + sdk + " POST_NOTIFICATIONS=" + permission + " areNotificationsEnabled=" + enabled
                + " channel " + ChannelId + ": " + channel + " fcmToken=" + (string.IsNullOrEmpty(_lastTokenStatic) ? "none yet" : "present"));
        }
        catch (Exception e)
        {
            Log("State log failed (" + e.Message + ").");
        }
    }

    void WaitForFirebase()
    {
        // Analytics already started the single dependency check. Do not call it again, and do not
        // touch Firebase.Messaging until that task reports Available.
        FirebaseBootstrap.ReadyTask.ContinueWithOnMainThread(task =>
        {
            try
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    var why = task.Exception != null ? task.Exception.GetBaseException().Message : "canceled";
                    Fail(why);
                    return;
                }
                if (task.Result != DependencyStatus.Available)
                {
                    Fail("dependencies: " + task.Result);
                    return;
                }
                StartMessaging();
            }
            catch (Exception e) { Fail(e.Message); }
        });
    }

    void StartMessaging()
    {
        if (_messagingStarted) return;
        _messagingStarted = true;
        var app = FirebaseApp.DefaultInstance;
        if (app == null)
        {
            Fail("no default FirebaseApp");
            return;
        }
        FirebaseMessaging.TokenRegistrationOnInitEnabled = true;
        FirebaseMessaging.TokenReceived += OnTokenReceived;
        FirebaseMessaging.MessageReceived += OnMessageReceived;
        Log("Firebase Messaging ready. Topic " + TopicAll + ".");
        RequestToken();
        FirebaseMessaging.SubscribeAsync(TopicAll).ContinueWithOnMainThread(OnSubscribe);
    }

    void RequestToken()
    {
        _tokenAttempts++;
        Log("GetTokenAsync attempt " + _tokenAttempts);
        FirebaseMessaging.GetTokenAsync().ContinueWithOnMainThread(OnTokenTask);
        if (_tokenWatching) return;
        _tokenWatching = true;
        StartCoroutine(WatchForToken());
    }

    IEnumerator WatchForToken()
    {
        yield return new WaitForSecondsRealtime(12f);
        if (!string.IsNullOrEmpty(_lastToken)) yield break;
        if (_tokenAttempts >= 4)
        {
            Log("FCM token still empty after " + _tokenAttempts + " attempts.");
            yield break;
        }
        Log("FCM token still empty after " + _tokenAttempts + " attempt(s). Retrying.");
        _tokenWatching = false;
        RequestToken();
    }

    void Fail(string why)
    {
        Log("Firebase Messaging disabled (" + why + "). The game will keep running.");
    }

    void OnTokenReceived(object sender, TokenReceivedEventArgs args)
    {
        PublishToken(args != null ? args.Token : null, "TokenReceived");
    }

    void OnTokenTask(System.Threading.Tasks.Task<string> task)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            var why = task.Exception != null ? task.Exception.GetBaseException().Message : "canceled";
            Log("GetToken failed (" + why + ").");
            return;
        }
        PublishToken(task.Result, "GetTokenAsync");
    }

    void PublishToken(string token, string source)
    {
        if (string.IsNullOrEmpty(token))
        {
            Log(source + " returned an empty token.");
            return;
        }
        if (token == _lastToken) return;
        _lastToken = token;
        _lastTokenStatic = token;
        Log("FCM registration token: " + token);
        if (!Debug.isDebugBuild) return;
        try
        {
            GUIUtility.systemCopyBuffer = token;
            Log("FCM token copied to the clipboard (development build).");
        }
        catch (Exception e)
        {
            Log("clipboard copy failed (" + e.Message + ").");
        }
    }

    void OnSubscribe(System.Threading.Tasks.Task task)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            var why = task.Exception != null ? task.Exception.GetBaseException().Message : "canceled";
            Log("Subscribe to '" + TopicAll + "' failed (" + why + ").");
            return;
        }
        Log("Subscribed to topic " + TopicAll + ".");
    }

    void OnMessageReceived(object sender, MessageReceivedEventArgs args)
    {
        try
        {
            var message = args != null ? args.Message : null;
            Log("MessageReceived event (app in foreground).");
            if (message == null)
            {
                Log("Foreground message (empty).");
                return;
            }
            var notification = message.Notification;
            string title = notification != null ? notification.Title : null;
            string body = notification != null ? notification.Body : null;
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
                Log("Foreground data message from " + message.From + " id=" + message.MessageId);
            else
                Log("Foreground message: " + title + " / " + body);
            ShowForegroundNotification(title, body);
        }
        catch (Exception e)
        {
            Log("Foreground message ignored (" + e.Message + ").");
        }
    }

    void ShowForegroundNotification(string title, string body)
    {
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) return;
        using var version = new AndroidJavaClass("android.os.Build$VERSION");
        int sdk = version.GetStatic<int>("SDK_INT");
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        if (activity == null) return;
        using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
        int icon = LookupIcon(context);
        AndroidJavaObject builder = sdk >= 26
            ? new AndroidJavaObject("android.app.Notification$Builder", context, ChannelId)
            : new AndroidJavaObject("android.app.Notification$Builder", context);
        try
        {
            builder.Call<AndroidJavaObject>("setSmallIcon", icon);
            builder.Call<AndroidJavaObject>("setContentTitle", title ?? "Tall Crawl");
            builder.Call<AndroidJavaObject>("setContentText", body ?? "");
            builder.Call<AndroidJavaObject>("setAutoCancel", true);
            using var notification = builder.Call<AndroidJavaObject>("build");
            using var manager = context.Call<AndroidJavaObject>("getSystemService", "notification");
            manager.Call("notify", _foregroundNotifyId++, notification);
        }
        finally
        {
            builder.Dispose();
        }
    }

    static int LookupIcon(AndroidJavaObject context)
    {
        using var resources = context.Call<AndroidJavaObject>("getResources");
        string packageName = context.Call<string>("getPackageName");
        int icon = resources.Call<int>("getIdentifier", "ic_stat_tallcrawl", "drawable", packageName);
        if (icon != 0) return icon;
        using var drawable = new AndroidJavaClass("android.R$drawable");
        return drawable.GetStatic<int>("stat_notify_chat");
    }

    static void EnsureChannel()
    {
        try
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            if (version.GetStatic<int>("SDK_INT") < 26) return;
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null) return;
            using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
            using var channel = new AndroidJavaObject(
                "android.app.NotificationChannel", ChannelId, "Tall Crawl", 4);
            channel.Call("setDescription", "Rewards, events and updates from Tall Crawl.");
            using var manager = context.Call<AndroidJavaObject>("getSystemService", "notification");
            manager.Call("createNotificationChannel", channel);
            Log("Notification channel " + ChannelId + " ensured (importance high).");
        }
        catch (Exception e)
        {
            Log("Notification channel was not created (" + e.Message + ").");
        }
    }

    static void Log(string message)
    {
        Debug.Log("[Push] " + message);
        try
        {
            using var log = new AndroidJavaClass("android.util.Log");
            log.CallStatic<int>("i", LogTag, message);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Push] android.util.Log failed: " + e.Message);
        }
    }
#endif
}
