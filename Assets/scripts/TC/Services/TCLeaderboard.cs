using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

/// <summary>
/// Google Play Games leaderboard for Tall Crawl. The ONLY score is the best distance climbed (whole metres,
/// TCSave.Best). Ported from Wordwise Solitaire LeaderboardService (Top 20 worldwide + my worldwide rank).
/// </summary>
public static class TCLeaderboard
{
    public const int TopCount = 20;
    const string LastSubmittedKey = "tc_lb_submitted";

    public struct Entry
    {
        public int rank;
        public string playerName;
        public string userId;
        public long metres;
        public bool isLocalPlayer;
    }

    public struct Result
    {
        public bool success;
        public bool isAuthenticated;
        public string statusMessage;
        public int localPlayerRank;
        public long localPlayerMetres;
        public List<Entry> entries;
    }

    public static string LeaderboardId => TCGameServicesConfig.DistanceLeaderboardId;

    static bool Configured => !string.IsNullOrEmpty(LeaderboardId) && !LeaderboardId.StartsWith("REPLACE");

    /// Submits TCSave.Best (best distance in metres). Called after a run that set a new best and after sign-in.
    public static void SubmitBest(Action<bool> onComplete = null)
    {
        SubmitDistance(TCSave.Best, onComplete);
    }

    public static void SubmitDistance(long metres, Action<bool> onComplete = null)
    {
        if (metres <= 0 || !Configured || !TCPlayGames.IsSignedIn) { onComplete?.Invoke(false); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            PlayGamesPlatform.Instance.ReportScore(metres, LeaderboardId, ok =>
            {
                PgsDiagnostics.Log("ReportScore " + metres + "m -> " + ok);
                if (ok) { PlayerPrefs.SetInt(LastSubmittedKey, (int)Math.Min(int.MaxValue, metres)); }
                TCServices.Log("leaderboard_score_submitted", ("metres", metres), ("success", ok ? 1 : 0));
                onComplete?.Invoke(ok);
            });
        }
        catch (Exception e) { PgsDiagnostics.Warn("ReportScore exception: " + e.Message); onComplete?.Invoke(false); }
#else
        onComplete?.Invoke(false);
#endif
    }

    /// Fetches the Top 20 worldwide distances and the signed-in player's worldwide rank.
    public static void FetchTop20AndMyRank(Action<Result> callback)
    {
        long mine = TCSave.Best;
        if (!Configured)
        {
            callback?.Invoke(Fail(false, "Leaderboard ID is not configured (TCGameServicesConfig).", mine));
            return;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!TCPlayGames.IsSignedIn)
        {
            callback?.Invoke(Fail(false, "Sign in with Google Play Games to see the worldwide Top 20 and your rank.", mine));
            return;
        }
        // Push the latest best first so the rank is up to date.
        SubmitDistance(mine, _ =>
        {
            try
            {
                PlayGamesPlatform.Instance.LoadScores(LeaderboardId, LeaderboardStart.TopScores, TopCount,
                    LeaderboardCollection.Public, LeaderboardTimeSpan.AllTime, data => OnScores(data, mine, callback));
            }
            catch (Exception e) { callback?.Invoke(Fail(true, "Could not load leaderboard (" + e.Message + ").", mine)); }
        });
#else
        callback?.Invoke(Fail(false, "Google Play Games leaderboards are available on Android devices.", mine));
#endif
    }

    static Result Fail(bool authed, string msg, long mine) => new Result
    {
        success = false, isAuthenticated = authed, statusMessage = msg,
        localPlayerRank = 0, localPlayerMetres = mine, entries = new List<Entry>()
    };

#if UNITY_ANDROID && !UNITY_EDITOR
    static void OnScores(LeaderboardScoreData data, long mine, Action<Result> callback)
    {
        if (data == null || !data.Valid)
        {
            callback?.Invoke(Fail(true, "Could not load leaderboard (" + (data != null ? data.Status.ToString() : "unknown") + ").", mine));
            return;
        }
        int myRank = data.PlayerScore != null ? data.PlayerScore.rank : 0;
        long myScore = data.PlayerScore != null ? Math.Max(data.PlayerScore.value, mine) : mine;
        string me = TCPlayGames.UserId;
        IScore[] raw = data.Scores ?? Array.Empty<IScore>();
        var ids = new List<string>(raw.Length);
        foreach (var s in raw) if (s != null && !string.IsNullOrEmpty(s.userID)) ids.Add(s.userID);
        if (ids.Count == 0)
        {
            callback?.Invoke(new Result { success = true, isAuthenticated = true, statusMessage = "No distances yet. Climb to claim #1!",
                localPlayerRank = myRank, localPlayerMetres = myScore, entries = new List<Entry>() });
            return;
        }
        PlayGamesPlatform.Instance.LoadUsers(ids.ToArray(), profiles =>
        {
            var names = new Dictionary<string, string>();
            if (profiles != null)
                foreach (var p in profiles)
                    if (p != null && !string.IsNullOrEmpty(p.id) && !string.IsNullOrWhiteSpace(p.userName)) names[p.id] = p.userName;
            var list = new List<Entry>(raw.Length);
            foreach (var s in raw)
            {
                if (s == null || list.Count >= TopCount) continue;
                bool isMe = !string.IsNullOrEmpty(me) && s.userID == me;
                list.Add(new Entry
                {
                    rank = s.rank,
                    playerName = names.TryGetValue(s.userID, out var n) ? n : (isMe ? TCPlayGames.DisplayName : "Player"),
                    userId = s.userID,
                    metres = s.value,
                    isLocalPlayer = isMe
                });
            }
            TCServices.Log("leaderboard_loaded", ("entries", list.Count), ("my_rank", myRank));
            callback?.Invoke(new Result { success = true, isAuthenticated = true, statusMessage = "",
                localPlayerRank = myRank, localPlayerMetres = myScore, entries = list });
        });
    }
#endif

    public static string FormatMetres(long m) => m.ToString("N0") + " m";
}
