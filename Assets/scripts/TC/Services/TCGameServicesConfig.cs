/// <summary>
/// Google Play Games Services IDs for Tall Crawl (package com.sillycon.tallcrawl).
///
/// Play Games project 98077034227 (same number as Firebase project tall-crawl-8ea97).
/// The APP_ID is also stored in:
///   - Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml (APP_ID meta-data)
///   - Assets/GooglePlayGames/Resources/PlayGamesSettings.asset (mAppId)
///   - ProjectSettings/GooglePlayGameSettings.txt (proj.AppId + and.ResourceData)
///   - Assets/GPGSIds.cs (generated constants)
/// To change them, use Window > Google Play Games > Setup > Android setup with the resources XML
/// from Play Console, then update this file.
/// </summary>
public static class TCGameServicesConfig
{
    /// Play Games Services project / application id (numeric, the "APP_ID").
    public const string PlayGamesAppId = "98077034227";

    /// "Distance" leaderboard: best distance climbed in whole metres (Numeric, larger is better).
    public const string DistanceLeaderboardId = GPGSIds.leaderboard_distance; // CgkI843jru0CEAIQAg

    /// Saved Games snapshot that holds all progress + purchase state. Saved Games must be ON in Play Console.
    public const string SnapshotName = "tall_crawl_save_v1";
}
