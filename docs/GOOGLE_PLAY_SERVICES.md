# Google Play Games, leaderboard, cloud save and push (Nov 2026)

Ported from Wordwise Solitaire (`playableAdsWordsSolitaire`, branch `unity-full-game`), which works on device.

## What was added
| Feature | Code |
|---|---|
| Play Games v2 sign-in: `PlayGamesPlatform.Activate()` first, silent `Authenticate` at launch, `ManuallyAuthenticate` from buttons | `Assets/scripts/TC/Services/TCPlayGames.cs` |
| `[PGS]` diagnostics (package, APP_ID, installed signing SHA-1, GMS / Play Games versions, stripped classes) | `Services/PgsDiagnostics.cs` |
| Distance-only leaderboard (best metres, `TCSave.Best`), Top 20 + my worldwide rank | `Services/TCLeaderboard.cs`, UI in `TCMenuUI.Leaderboard.cs` |
| Saved Games cloud save of all progress + IAP state, max/union merge | `Services/TCCloudSave.cs`, `Services/TCIapLedger.cs` |
| FCM push: token log tag `TallCrawlPush`, topic `all`, POST_NOTIFICATIONS request, `tallcrawl_alerts` channel, foreground notifications | `Services/PushNotificationService.cs`, `Assets/Plugins/Android/TallCrawlNotifications.androidlib`, `Assets/Plugins/Android/AndroidManifest.xml` |
| Single Firebase init (`FirebaseBootstrap.ReadyTask`) used by analytics + push | `Services/FirebaseBootstrap.cs`, `Zakriya_Production_Pkg/Scripts/FireBaseInit.cs` |
| IL2CPP stripping protection | `Services/link.xml` |
| Firebase SDK 11.6.0 -> 14.2.0 (App, Analytics, Messaging), Play Games plugin 2.1.0 | `Assets/Firebase`, `Assets/GeneratedLocalRepo/Firebase`, `Assets/GooglePlayGames` |

UI: a star button right of the BEST pill opens the LEADERBOARD panel; Settings has SIGN IN WITH GOOGLE PLAY.

## IDs (configured Nov 28, 2026)
| Item | Value |
|---|---|
| Package | `com.sillycon.tallcrawl` |
| Play Games APP_ID / Firebase project number | `98077034227` |
| Firebase project | `tall-crawl-8ea97` (`Assets/google-services.json`, regenerated `FirebaseApp.androidlib/res/values/google-services.xml`) |
| Distance leaderboard (`GPGSIds.leaderboard_distance`) | `CgkI843jru0CEAIQAg` (Numeric, larger is better, metres) |
| Saved Games snapshot | `tall_crawl_save_v1` (Saved Games ON) |
| Android OAuth clients (Play Games credentials) | `98077034227-bkros7l53296nnaldov7o7o9oskrdelp`, `98077034227-7vi5uve4p11sgnb5q0d7dvpc5kkmivod` |

The APP_ID lives in `Services/TCGameServicesConfig.cs`, `Assets/GPGSIds.cs`,
`Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml`,
`Assets/GooglePlayGames/Resources/PlayGamesSettings.asset` and `ProjectSettings/GooglePlayGameSettings.txt`
(which also holds the resources XML). To change them, run *Window > Google Play Games > Setup > Android setup*
with the resources XML from Play Console.

Sign-in only works for builds whose signing SHA-1 matches one of the Android credentials above
(the `sillycon` upload key for local builds, the Play App Signing key for store installs), and, while the
OAuth consent screen is in testing, only for listed test users. The `[PGS] installed APK signing SHA-1=` log line
prints the key in use.

## Device checks
```
adb logcat | grep -E "PGS|GamesUnitySDK|PlayGamesServices|Games"      # sign-in
adb logcat | grep -iE "TallCrawlPush|\[Push\]"                          # FCM token, topic, permission
adb logcat | grep -E "CloudSave|\[IAP\]"                                # snapshot sync, purchase ledger
```
Push notifications only pop up in the system tray while the game is in the background; in the foreground the service shows them itself.
