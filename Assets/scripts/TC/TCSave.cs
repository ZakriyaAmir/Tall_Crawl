using System;
using UnityEngine;

/// Persistent save (PlayerPrefs). Migrates the old "coins"/"highscore" keys.
public static class TCSave
{
    public static event Action Changed;
    static void Fire() { PlayerPrefs.Save(); Changed?.Invoke(); }

    static TCSave()
    {
        if (!PlayerPrefs.HasKey("tc_init"))
        {
            PlayerPrefs.SetInt("tc_init", 1);
            PlayerPrefs.SetInt("tc_coins", PlayerPrefs.GetInt("coins", 0) + TCConfig.I.startingCoins);
            PlayerPrefs.SetInt("tc_best", PlayerPrefs.GetInt("highscore", 0));
            PlayerPrefs.SetInt("skin_own_0", 1);
            PlayerPrefs.Save();
        }
    }

    public static int Coins { get => PlayerPrefs.GetInt("tc_coins"); set { PlayerPrefs.SetInt("tc_coins", Mathf.Max(0, value)); Fire(); } }
    public static int Gems { get => PlayerPrefs.GetInt("tc_gems", 3); set { PlayerPrefs.SetInt("tc_gems", Mathf.Max(0, value)); Fire(); } }
    public static int Best { get => PlayerPrefs.GetInt("tc_best"); set { PlayerPrefs.SetInt("tc_best", value); Fire(); } }
    public static int Runs { get => PlayerPrefs.GetInt("tc_runs"); set => PlayerPrefs.SetInt("tc_runs", value); }
    public static int LifetimeCoins { get => PlayerPrefs.GetInt("tc_life_coins"); set => PlayerPrefs.SetInt("tc_life_coins", value); }
    public static bool NoAds { get => PlayerPrefs.GetInt("tc_noads") == 1; set { PlayerPrefs.SetInt("tc_noads", value ? 1 : 0); Fire(); } }
    public static bool TutorialDone { get => PlayerPrefs.GetInt("tc_tut") == 1; set => PlayerPrefs.SetInt("tc_tut", value ? 1 : 0); }

    public static bool Sound { get => PlayerPrefs.GetInt("DisableSound") == 0; set { PlayerPrefs.SetInt("DisableSound", value ? 0 : 1); Apply(); } }
    public static bool Music { get => PlayerPrefs.GetInt("tc_music_off") == 0; set { PlayerPrefs.SetInt("tc_music_off", value ? 0 : 1); Apply(); } }
    public static bool Vibration { get => PlayerPrefs.GetInt("DisableHaptic") == 0; set { PlayerPrefs.SetInt("DisableHaptic", value ? 0 : 1); Fire(); } }
    public static float Sensitivity { get => PlayerPrefs.GetFloat("tc_sens", TCConfig.I.defaultSensitivity); set { PlayerPrefs.SetFloat("tc_sens", Mathf.Clamp(value, 0.4f, 2.2f)); Fire(); } }

    public static int SelectedSkin { get => PlayerPrefs.GetInt("skin_sel", 0); set { PlayerPrefs.SetInt("skin_sel", value); Fire(); } }
    public static bool SkinOwned(int i) => i == 0 || PlayerPrefs.GetInt("skin_own_" + i) == 1;
    public static void UnlockSkin(int i) { PlayerPrefs.SetInt("skin_own_" + i, 1); Fire(); }
    public static int SkinAdsWatched(int i) => PlayerPrefs.GetInt("skin_ads_" + i);
    public static void AddSkinAd(int i) { PlayerPrefs.SetInt("skin_ads_" + i, SkinAdsWatched(i) + 1); Fire(); }

    // Upgrades: 0 = Magnet time, 1 = Boost time, 2 = Extra heart, 3 = Coin value
    public static int Upgrade(int id) => PlayerPrefs.GetInt("upg_" + id);
    public static void SetUpgrade(int id, int lvl) { PlayerPrefs.SetInt("upg_" + id, lvl); Fire(); }

    public static void Apply()
    {
        AudioListener.volume = Sound ? 1 : 0;
        Fire();
    }

    // Daily reward: 7-day streak, resets if a day is missed.
    public static bool DailyAvailable(out int day)
    {
        var last = PlayerPrefs.GetString("tc_daily_last", "");
        int streak = PlayerPrefs.GetInt("tc_daily_streak");
        var today = DateTime.Now.Date;
        if (DateTime.TryParse(last, out var d))
        {
            if (d.Date == today) { day = streak % 7; return false; }
            if (d.Date != today.AddDays(-1)) streak = 0;
        }
        day = streak % 7;
        return true;
    }
    public static void ClaimDaily(int day)
    {
        PlayerPrefs.SetString("tc_daily_last", DateTime.Now.Date.ToString("yyyy-MM-dd"));
        PlayerPrefs.SetInt("tc_daily_streak", day + 1);
        Fire();
    }
}
