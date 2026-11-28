/// <summary>
/// Google Play Games Services IDs for Tall Crawl (package com.sillycon.tallcrawl).
///
/// TODO(Play Console): VERIFY / REPLACE these values. They are copied from the old 2024 GPGS setup
/// (Assets/GPGSIds.cs: leaderboard_highscore = CgkImILy6-AKEAIQAA, which encodes app id 369593450776).
/// If Play Console > Grow users > Play Games Services shows a different project/leaderboard for
/// Tall Crawl, replace them here AND in:
///   - Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml (APP_ID meta-data)
///   - Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/project.properties (proj.AppId)
///   - Assets/GooglePlayGames/Resources/PlayGamesSettings.asset (mAppId)
///   - ProjectSettings/GooglePlayGameSettings.txt (proj.AppId)
/// The easiest way is Window > Google Play Games > Setup > Android setup in the Unity editor, pasting
/// the resources XML from Play Console; it rewrites all of the files above.
/// </summary>
public static class TCGameServicesConfig
{
    /// Play Games Services project / application id (numeric, the "APP_ID").
    public const string PlayGamesAppId = "369593450776"; // TODO(Play Console): VERIFY

    /// Leaderboard that ranks players by their best distance climbed, in whole metres.
    /// Create it in Play Console as: score format Numeric, ordering "Larger is better", unit "m".
    public const string DistanceLeaderboardId = "CgkImILy6-AKEAIQAA"; // TODO(Play Console): VERIFY (old "highscore" leaderboard)

    /// Saved Games snapshot that holds all progress + purchase state. Saved Games must be ON in Play Console.
    public const string SnapshotName = "tall_crawl_save_v1";
}
