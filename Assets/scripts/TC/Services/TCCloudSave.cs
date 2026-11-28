using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
#endif

/// <summary>
/// Google Play Games Saved Games (Snapshots) cloud save for ALL Tall Crawl progress + purchase state.
/// Ported from Wordwise Solitaire CloudSaveService.
///
/// What is saved: every PlayerPrefs key the game persists (TCSave: best distance, coins, gems, runs, lifetime coins,
/// enemy knock counters, upgrades, owned/ad-progress skins, selected skin, daily streak, tutorial, settings, consent,
/// remove-ads, legacy economyManager money) plus the IAP ledger (TCIapLedger: owned non-consumables and every
/// purchase transaction with the coins/gems it granted).
///
/// Merge (never loses progress):
///  - Monotonic values (best, runs, lifetime coins, knocks, upgrade levels, skin unlocks / ad progress, tutorial,
///    remove-ads, consent) take the MAX of both saves.
///  - Spendable balances and settings (coins, gems, selected skin, daily streak, sound...) come from the save with
///    more progress (lifetime coins, then runs, then best, then newest), so spending is not undone.
///  - Purchases: ledgers are unioned by transaction id; coins/gems from a purchase only the other save knows about
///    are added once. Owned non-consumables are unioned (remove-ads is re-applied).
///
/// When: silent sign-in at launch / manual sign-in -> SyncWithCloud. Saves are debounced after TCSave changes
/// (never mid-run), forced at the end of each run, after every purchase, and on app pause/quit.
/// </summary>
public static class TCCloudSave
{
    const string PlayTimeKey = "tc_playtime_sec";
    const string LastSyncKey = "tc_cloud_last_sync_utc";
    const float DebounceSeconds = 8f;

    // ---- key registry (PlayerPrefs cannot be enumerated at runtime on Android, so every persisted key is listed) ----
    static readonly string[] MaxIntKeys =
    {
        "tc_best", "highscore", "tc_runs", "tc_life_coins",
        "tc_kills_bee", "tc_kills_wasp", "tc_kills_squirrel", "tc_knocks_bat", "tc_knocks_snake",
        "tc_tut", "tc_init", "tc_booted_before", "consent", "tc_noads", "noAds", "levelsCompleted", "firstTime",
    };
    static readonly string[] MaxIntPrefixes = { "upg_", "skin_own_", "skin_ads_" };
    static readonly string[] PrimaryIntKeys =
    {
        "tc_coins", "tc_gems", "money", "TotalMoney", "skin_sel", "tc_daily_streak",
        "DisableSound", "DisableHaptic", "tc_music_off", "tc_menu_entrance_style",
    };
    static readonly string[] FloatKeys = { "tc_sens", "soundVolume", "musicVolume" };
    static readonly string[] StringKeys = { "tc_daily_last" };
    const int UpgradeSlots = 10;
    const int MinSkinSlots = 40;

    [Serializable] public class IntKV { public string k; public int v; }
    [Serializable] public class FloatKV { public string k; public float v; }
    [Serializable] public class StrKV { public string k; public string v; }

    [Serializable]
    public class Data
    {
        public int version = 1;
        public long savedAtUtcMs;
        public long playTimeSeconds;
        public List<IntKV> ints = new List<IntKV>();
        public List<FloatKV> floats = new List<FloatKV>();
        public List<StrKV> strings = new List<StrKV>();
        public TCIapLedger.State iap = new TCIapLedger.State();
    }

    /// (success, message) after every sync/save attempt.
    public static event Action<bool, string> SyncCompleted;

    static Runner _runner;
    static bool _booted, _suppress, _dirty, _opInFlight, _queued;
    static float _saveAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (_booted) return;
        _booted = true;
        if (TCAutomation.IsAutomatedRun()) return;
        EnsureRunner();
        TCSave.Changed += () => MarkDirty();
    }

    static void EnsureRunner()
    {
        if (_runner != null) return;
        var go = new GameObject("TCCloudSaveRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<Runner>();
    }

    public static bool IsSignedIn => TCPlayGames.IsSignedIn;
    public static bool IsBusy => _opInFlight;

    /// Debounced upload (skipped mid-run; flushed on run end / pause).
    public static void MarkDirty()
    {
        if (_suppress) return;
        _dirty = true;
        if (!IsSignedIn) return;
        _saveAt = Time.realtimeSinceStartup + DebounceSeconds;
    }

    /// Immediate upload (run end, purchase).
    public static void SaveNow()
    {
        if (_suppress) return;
        _dirty = true;
        _saveAt = -1f;
        if (IsSignedIn) SaveToCloud();
    }

    // ------------------------------------------------------------------ capture / apply

    static IEnumerable<string> AllIntKeys()
    {
        foreach (var k in MaxIntKeys) yield return k;
        foreach (var k in PrimaryIntKeys) yield return k;
        for (int i = 0; i < UpgradeSlots; i++) yield return "upg_" + i;
        int skins = MinSkinSlots;
        try { var all = TCSkins.All; if (all != null) skins = Mathf.Max(skins, all.Length + 4); } catch { }
        for (int i = 0; i < skins; i++) { yield return "skin_own_" + i; yield return "skin_ads_" + i; }
    }

    static bool IsMaxKey(string k)
    {
        if (Array.IndexOf(MaxIntKeys, k) >= 0) return true;
        foreach (var p in MaxIntPrefixes) if (k.StartsWith(p, StringComparison.Ordinal)) return true;
        return false;
    }

    public static Data CaptureLocal()
    {
        var d = new Data
        {
            savedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            playTimeSeconds = StoredPlayTime(),
            iap = TCIapLedger.Decode(TCIapLedger.Encode(TCIapLedger.Current)),
        };
        foreach (var k in AllIntKeys()) if (PlayerPrefs.HasKey(k)) d.ints.Add(new IntKV { k = k, v = PlayerPrefs.GetInt(k) });
        foreach (var k in FloatKeys) if (PlayerPrefs.HasKey(k)) d.floats.Add(new FloatKV { k = k, v = PlayerPrefs.GetFloat(k) });
        foreach (var k in StringKeys) if (PlayerPrefs.HasKey(k)) d.strings.Add(new StrKV { k = k, v = PlayerPrefs.GetString(k) });
        return d;
    }

    public static void ApplyToLocal(Data d)
    {
        if (d == null) return;
        _suppress = true;
        try
        {
            foreach (var e in d.ints) if (!string.IsNullOrEmpty(e.k)) PlayerPrefs.SetInt(e.k, e.v);
            foreach (var e in d.floats) if (!string.IsNullOrEmpty(e.k)) PlayerPrefs.SetFloat(e.k, e.v);
            foreach (var e in d.strings) if (!string.IsNullOrEmpty(e.k)) PlayerPrefs.SetString(e.k, e.v ?? "");
            TCIapLedger.Replace(d.iap);
            if (OwnsRemoveAds(d.iap)) { PlayerPrefs.SetInt("tc_noads", 1); PlayerPrefs.SetInt("noAds", 1); }
            if (d.playTimeSeconds > StoredPlayTime()) PlayerPrefs.SetString(PlayTimeKey, d.playTimeSeconds.ToString());
            PlayerPrefs.Save();
            TCSave.NotifyExternalChange();
            if (TCSave.NoAds) TCServices.Banner(false);
        }
        catch (Exception e) { Debug.LogWarning("[CloudSave] apply failed: " + e.Message); }
        finally { _suppress = false; }
    }

    static bool OwnsRemoveAds(TCIapLedger.State s)
    {
        if (s == null) return false;
        foreach (var o in s.owned) if (!string.IsNullOrEmpty(o) && (o.EndsWith(".noads") || o.EndsWith(".premium_noads"))) return true;
        return false;
    }

    // ------------------------------------------------------------------ merge

    static int GetInt(Data d, string k, int def = 0) { foreach (var e in d.ints) if (e.k == k) return e.v; return def; }
    static bool HasInt(Data d, string k) { foreach (var e in d.ints) if (e.k == k) return true; return false; }
    static void SetInt(Data d, string k, int v) { foreach (var e in d.ints) if (e.k == k) { e.v = v; return; } d.ints.Add(new IntKV { k = k, v = v }); }

    static bool IsEmpty(Data d) =>
        d == null || (GetInt(d, "tc_runs") <= 0 && GetInt(d, "tc_life_coins") <= 0 && GetInt(d, "tc_best") <= 0
                      && (d.iap == null || (d.iap.tx.Count == 0 && d.iap.owned.Count == 0)));

    /// &gt;0 when a has more progress than b.
    static int Compare(Data a, Data b)
    {
        int c = GetInt(a, "tc_life_coins").CompareTo(GetInt(b, "tc_life_coins")); if (c != 0) return c;
        c = GetInt(a, "tc_runs").CompareTo(GetInt(b, "tc_runs")); if (c != 0) return c;
        c = GetInt(a, "tc_best").CompareTo(GetInt(b, "tc_best")); if (c != 0) return c;
        return a.savedAtUtcMs.CompareTo(b.savedAtUtcMs);
    }

    public static Data Merge(Data primary, Data secondary)
    {
        var r = JsonUtility.FromJson<Data>(JsonUtility.ToJson(primary));
        if (secondary == null) return r;
        foreach (var e in secondary.ints)
        {
            if (string.IsNullOrEmpty(e.k)) continue;
            if (!HasInt(r, e.k)) SetInt(r, e.k, e.v);                 // fill gaps from the other save
            else if (IsMaxKey(e.k)) SetInt(r, e.k, Math.Max(GetInt(r, e.k), e.v));
        }
        foreach (var e in secondary.floats) if (!r.floats.Exists(x => x.k == e.k)) r.floats.Add(e);
        foreach (var e in secondary.strings) if (!r.strings.Exists(x => x.k == e.k)) r.strings.Add(e);

        // Purchases: credit coins/gems of transactions the primary save has never seen, exactly once.
        var p = primary.iap ?? new TCIapLedger.State();
        int addCoins = 0, addGems = 0;
        if (secondary.iap != null)
            foreach (var t in secondary.iap.tx)
                if (t != null && !TCIapLedger.HasTx(p, t.id)) { addCoins += t.coins; addGems += t.gems; }
        r.iap = TCIapLedger.Union(p, secondary.iap);
        if (addCoins > 0) SetInt(r, "tc_coins", GetInt(r, "tc_coins") + addCoins);
        if (addGems > 0) SetInt(r, "tc_gems", GetInt(r, "tc_gems", 3) + addGems);
        if (OwnsRemoveAds(r.iap)) { SetInt(r, "tc_noads", 1); SetInt(r, "noAds", 1); }

        r.savedAtUtcMs = Math.Max(primary.savedAtUtcMs, secondary.savedAtUtcMs);
        r.playTimeSeconds = Math.Max(primary.playTimeSeconds, secondary.playTimeSeconds);
        return r;
    }

    static bool SameState(Data a, Data b)
    {
        if (a == null || b == null) return false;
        var x = JsonUtility.FromJson<Data>(JsonUtility.ToJson(a)); var y = JsonUtility.FromJson<Data>(JsonUtility.ToJson(b));
        x.savedAtUtcMs = y.savedAtUtcMs = 0; x.playTimeSeconds = y.playTimeSeconds = 0;
        x.ints.Sort((m, n) => string.CompareOrdinal(m.k, n.k)); y.ints.Sort((m, n) => string.CompareOrdinal(m.k, n.k));
        return JsonUtility.ToJson(x) == JsonUtility.ToJson(y);
    }

    static byte[] Serialize(Data d) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(d));

    static bool TryDeserialize(byte[] bytes, out Data d)
    {
        d = null;
        if (bytes == null || bytes.Length == 0) return false;
        try
        {
            d = JsonUtility.FromJson<Data>(Encoding.UTF8.GetString(bytes));
            if (d == null) return false;
            if (d.ints == null) d.ints = new List<IntKV>();
            if (d.floats == null) d.floats = new List<FloatKV>();
            if (d.strings == null) d.strings = new List<StrKV>();
            if (d.iap == null) d.iap = new TCIapLedger.State();
            if (d.iap.tx == null) d.iap.tx = new List<TCIapLedger.Tx>();
            if (d.iap.owned == null) d.iap.owned = new List<string>();
            return true;
        }
        catch (Exception e) { Debug.LogWarning("[CloudSave] bad snapshot: " + e.Message); return false; }
    }

    // ------------------------------------------------------------------ Saved Games I/O

    /// Load + merge + apply locally + upload the merged result. Called right after sign-in.
    public static void SyncWithCloud(Action<bool, string> onComplete = null) => Run(preferRemoteOnTie: true, onComplete);

    /// Upload local progress (still merged with whatever is in the cloud so nothing is overwritten).
    public static void SaveToCloud(Action<bool, string> onComplete = null) => Run(preferRemoteOnTie: false, onComplete);

    static void Run(bool preferRemoteOnTie, Action<bool, string> onComplete)
    {
        EnsureRunner();
        if (!IsSignedIn) { onComplete?.Invoke(false, "Sign in with Google Play Games to use cloud save"); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_opInFlight) { if (!preferRemoteOnTie) _queued = true; onComplete?.Invoke(false, "Cloud save busy"); return; }
        _opInFlight = true;
        _saveAt = -1f;
        try
        {
            ISavedGameClient sg = PlayGamesPlatform.Instance.SavedGame;
            if (sg == null) { Finish(false, "Saved Games unavailable", onComplete); return; }
            sg.OpenWithManualConflictResolution(TCGameServicesConfig.SnapshotName, DataSource.ReadCacheOrNetwork, true,
                ResolveConflict, (status, meta) =>
                {
                    if (status != SavedGameRequestStatus.Success || meta == null || !meta.IsOpen)
                    { Finish(false, "Could not open cloud save (" + status + ")", onComplete); return; }
                    sg.ReadBinaryData(meta, (readStatus, bytes) =>
                    {
                        Data local = CaptureLocal();
                        Data toWrite = local;
                        Data remote = null;
                        bool hasRemote = readStatus == SavedGameRequestStatus.Success && TryDeserialize(bytes, out remote) && !IsEmpty(remote);
                        if (hasRemote)
                        {
                            int cmp = Compare(remote, local);
                            bool remotePrimary = IsEmpty(local) || cmp > 0 || (cmp == 0 && preferRemoteOnTie);
                            toWrite = remotePrimary ? Merge(remote, local) : Merge(local, remote);
                            if (!SameState(toWrite, local))
                            {
                                Debug.Log("[CloudSave] applying merged cloud progress (remotePrimary=" + remotePrimary + ")");
                                ApplyToLocal(toWrite);
                            }
                            if (SameState(toWrite, remote))
                            { Done(true, "Cloud save up to date", toWrite, onComplete); return; }
                        }
                        else if (IsEmpty(local))
                        { Done(true, "No cloud save yet", local, onComplete); return; }

                        toWrite.savedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        sg.CommitUpdate(meta, BuildMeta(toWrite), Serialize(toWrite), (commitStatus, _) =>
                        {
                            bool ok = commitStatus == SavedGameRequestStatus.Success;
                            if (ok) TCServices.Log("cloud_save_success", ("best", GetInt(toWrite, "tc_best")), ("coins", GetInt(toWrite, "tc_coins")));
                            if (ok) Done(true, "Progress saved to Google Play", toWrite, onComplete);
                            else Finish(false, "Cloud save failed (" + commitStatus + ")", onComplete);
                        });
                    });
                });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CloudSave] exception: " + e.Message);
            Finish(false, "Cloud save error", onComplete);
        }
#else
        onComplete?.Invoke(false, "Google Play cloud save works on Android devices");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static void ResolveConflict(IConflictResolver resolver, ISavedGameMetadata original, byte[] originalData,
                                ISavedGameMetadata unmerged, byte[] unmergedData)
    {
        bool a = TryDeserialize(originalData, out var o), b = TryDeserialize(unmergedData, out var u);
        if (!a && !b) { resolver.ChooseMetadata(original); return; }
        if (!a) { resolver.ChooseMetadata(unmerged); return; }
        if (!b) { resolver.ChooseMetadata(original); return; }
        bool origPrimary = Compare(o, u) >= 0;
        var merged = origPrimary ? Merge(o, u) : Merge(u, o);
        resolver.ResolveConflict(origPrimary ? original : unmerged, BuildMeta(merged), Serialize(merged));
    }

    static SavedGameMetadataUpdate BuildMeta(Data d) =>
        new SavedGameMetadataUpdate.Builder()
            .WithUpdatedDescription($"Best {GetInt(d, "tc_best")} m | {GetInt(d, "tc_coins")} coins | {GetInt(d, "tc_gems", 3)} gems")
            .WithUpdatedPlayedTime(TimeSpan.FromSeconds(Math.Max(0, d.playTimeSeconds)))
            .Build();
#endif

    static void Done(bool ok, string msg, Data written, Action<bool, string> cb)
    {
        _dirty = false;
        PlayerPrefs.SetString(LastSyncKey, written.savedAtUtcMs.ToString());
        PlayerPrefs.Save();
        Finish(ok, msg, cb);
    }

    static void Finish(bool ok, string msg, Action<bool, string> cb)
    {
        _opInFlight = false;
        Debug.Log("[CloudSave] " + (ok ? "OK: " : "FAIL: ") + msg);
        try { cb?.Invoke(ok, msg); } catch (Exception e) { Debug.LogWarning("[CloudSave] callback error: " + e.Message); }
        try { SyncCompleted?.Invoke(ok, msg); } catch (Exception e) { Debug.LogWarning("[CloudSave] event error: " + e.Message); }
        if (_queued && IsSignedIn) { _queued = false; SaveToCloud(); }
    }

    static long StoredPlayTime() => long.TryParse(PlayerPrefs.GetString(PlayTimeKey, "0"), out long v) ? Math.Max(0, v) : 0;
    static void AddPlayTime(long s) { if (s > 0) PlayerPrefs.SetString(PlayTimeKey, (StoredPlayTime() + s).ToString()); }

    class Runner : MonoBehaviour
    {
        float _acc;

        void Update()
        {
            _acc += Time.unscaledDeltaTime;
            if (_acc >= 10f) { long w = (long)_acc; _acc -= w; AddPlayTime(w); }
            if (_saveAt > 0f && Time.realtimeSinceStartup >= _saveAt)
            {
                // Never hit the network mid-run: wait until the player is back on the menu (or pauses / ends the run).
                if (SceneManager.GetActiveScene().name == "gameplay") { _saveAt = Time.realtimeSinceStartup + DebounceSeconds; return; }
                _saveAt = -1f;
                if (_dirty && IsSignedIn) SaveToCloud();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            Flush();
            if (_dirty && IsSignedIn) SaveToCloud();
        }

        void OnApplicationQuit()
        {
            Flush();
            if (_dirty && IsSignedIn) SaveToCloud();
        }

        void Flush() { long w = (long)_acc; if (w > 0) { _acc -= w; AddPlayTime(w); } }
    }
}
