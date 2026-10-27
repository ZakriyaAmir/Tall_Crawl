# Tall Crawl

Hyper-casual endless climber: steer a spider around a giant trunk, dodge branches, jump bee swarms, grab coins, hearts and chilli boosts.

## Unity
Unity **6000.3.26f1**, built-in render pipeline. Open the project, then play `Assets/Scenes/MainMenu.unity`.

## Controls
- Drag left/right (touch or mouse) or A/D / arrows – circle the trunk
- Swipe up, double-tap, Space or W – jump over bees
- Sensitivity slider in Settings

## Features
- Pooled, fair endless track (`TCTrack`), tunable in `Assets/TC/Resources/TCConfig.asset`
- Economy: coins, gems, 4 upgrades (Boost Time, Extra Heart, Coin Value, Head Start), 7-day daily reward, high score, revive (ad or 5 gems), x2 coins (ad)
- 10 spider skins with patterns, trails and hats; Skins screen with live preview
- Shop with IAP hooks, Settings (sound, music, vibration, sensitivity, privacy, replay tutorial), consent, pause, tutorial, juice (particles, shake, haptics), banner safe area
- Save: `TCSave` (PlayerPrefs, migrates old `coins`/`highscore`)

## Tools
- `Tools > Tall Crawl > Setup` (`TCSetup.All`) – re-applies texture/material/scene/quality/player settings
- `TCSetup.BuildAndroid` – IL2CPP ARM64 APK, targetSdk 35, minSdk 23
- `TCSetup.BuildCapture` + `-tccapture <dir>` – automated screenshot tour
- `TCSetup.BuildCapture` + `-tcplaytest <dir> [-tcruns N]` – automated playtest bot (menus, economy, revive/x2, pause, fairness, pool/FPS stats, screenshots) → `<dir>/playtest.txt`
- `-executeMethod TCSelfTest.Run` – batchmode regression self-tests (economy, save clamp, 5000-row track fairness)
- Economy tuning: 5 coins/100 m, +1 gem/1000 m, upgrades 100/250/600/1200/2400

## Before release – replace test IDs
- AdMob app ID (`GoogleMobileAdsSettings.asset`) and ad unit IDs in `AdsManager` (currently Google test IDs, testMode on)
- IAP product IDs: `com.zakriya.tallcrawl.coins_1000`, `.noads`, `.premium` (MainMenu `UnityIAP`)
- Firebase `google-services.json`
- Privacy policy URL (`TCAssets.asset`)
- Release keystore (debug-signed now)

## v3 (Oct 2026)
- Economy rebalanced, see `docs/ECONOMY.md` (pacing checked by `TCSelfTest.Run`): 1 collected coin = 1 coin, full coin unlock ~25 days.
- Round, cute hypercasual spider (`TCCuteSpider`) used by every skin; skins differ only by colour/pattern, back spikes and body shape (no hats/props).
- Standalone gem packs (dummy IDs, 10 gems per $1): 10/$1, 50/$5, 100/$10, 200/$20.
- New app icon + menu logo (`Assets/TC/Icon`, applied by `TCSetup.Icons`).
- No game-start whoosh, no boost/slow-motion sound; revive (gems or ad) gives exactly one life.
