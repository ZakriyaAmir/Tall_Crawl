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

## Console values to confirm
- **Play Games APP_ID** `369593450776` and **leaderboard id** `CgkImILy6-AKEAIQAA` come from the old 2024 `Assets/GPGSIds.cs`.
  Confirm them in Play Console (Play Games Services > Configuration). If different, run
  *Window > Google Play Games > Setup > Android setup* with the new resources XML, and update
  `Services/TCGameServicesConfig.cs`.
- The leaderboard must be **Numeric, Larger is better** (unit metres). Turn **Saved Games ON** in the Play Games configuration.
- Add a Play Games Android credential for `com.sillycon.tallcrawl` with the SHA-1 of `extras/user.keystore.keystore`
  (alias `sillycon`) for local builds, and the Play App Signing SHA-1 for store installs. The `[PGS] installed APK signing SHA-1=` log line prints the one in use.
- OAuth consent screen in testing mode needs your tester accounts listed as test users.
- `Assets/google-services.json` (project `tall-crawl-8ea97`) is already present and is enough for FCM.

## Device checks
```
adb logcat | grep -E "PGS|GamesUnitySDK|PlayGamesServices|Games"      # sign-in
adb logcat | grep -iE "TallCrawlPush|\[Push\]"                          # FCM token, topic, permission
adb logcat | grep -E "CloudSave|\[IAP\]"                                # snapshot sync, purchase ledger
```
Push notifications only pop up in the system tray while the game is in the background; in the foreground the service shows them itself.
