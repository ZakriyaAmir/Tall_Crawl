using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Local record of every in-app purchase (product id, store transaction id, and exactly how many coins/gems it
/// granted) plus the set of owned non-consumables (remove-ads). Persisted in PlayerPrefs and carried inside the
/// Google Play Saved Games snapshot so purchases survive reinstalls / new devices and are never double-granted:
/// when two saves are merged, a transaction that only one side knows about has its coins/gems added once.
/// </summary>
public static class TCIapLedger
{
    const string Key = "tc_iap_ledger";
    const int MaxTx = 300;

    [Serializable]
    public class Tx
    {
        public string id;       // store transaction id (unique)
        public string product;  // product id
        public bool nonConsumable;
        public int coins;       // coins actually granted by this purchase
        public int gems;        // gems actually granted by this purchase
        public long utcMs;
    }

    [Serializable]
    public class State
    {
        public List<Tx> tx = new List<Tx>();
        public List<string> owned = new List<string>();
    }

    static State _cache;

    public static State Current
    {
        get
        {
            if (_cache != null) return _cache;
            _cache = Decode(PlayerPrefs.GetString(Key, ""));
            return _cache;
        }
    }

    public static State Decode(string json)
    {
        if (string.IsNullOrEmpty(json)) return new State();
        try
        {
            var s = JsonUtility.FromJson<State>(json) ?? new State();
            if (s.tx == null) s.tx = new List<Tx>();
            if (s.owned == null) s.owned = new List<string>();
            return s;
        }
        catch { return new State(); }
    }

    public static string Encode(State s) => JsonUtility.ToJson(s ?? new State());

    public static void Replace(State s)
    {
        _cache = s ?? new State();
        PlayerPrefs.SetString(Key, Encode(_cache));
    }

    public static bool Owns(string productId) => !string.IsNullOrEmpty(productId) && Current.owned.Contains(productId);

    public static bool HasTx(State s, string txId)
    {
        if (s == null || string.IsNullOrEmpty(txId)) return false;
        foreach (var t in s.tx) if (t != null && t.id == txId) return true;
        return false;
    }

    /// Called from UnityIAP.ProcessPurchase after the grant ran. coins/gems are the measured balance deltas.
    public static void Record(string productId, string transactionId, bool nonConsumable, int coins, int gems)
    {
        if (string.IsNullOrEmpty(productId)) return;
        var s = Current;
        if (nonConsumable && !s.owned.Contains(productId)) s.owned.Add(productId);
        string id = string.IsNullOrEmpty(transactionId) ? productId + "@" + DateTime.UtcNow.Ticks : transactionId;
        if (!HasTx(s, id))
        {
            s.tx.Add(new Tx
            {
                id = id, product = productId, nonConsumable = nonConsumable,
                coins = Mathf.Max(0, coins), gems = Mathf.Max(0, gems),
                utcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            while (s.tx.Count > MaxTx) s.tx.RemoveAt(0);
        }
        Replace(s);
        PlayerPrefs.Save();
        Debug.Log("[IAP] ledger recorded " + productId + " tx=" + id + " coins+" + coins + " gems+" + gems);
        TCCloudSave.SaveNow(); // purchases are pushed to Saved Games right away
    }

    /// Union of two ledgers (transactions by id, owned by product id).
    public static State Union(State a, State b)
    {
        var r = new State();
        foreach (var src in new[] { a, b })
        {
            if (src == null) continue;
            foreach (var t in src.tx) if (t != null && !HasTx(r, t.id)) r.tx.Add(t);
            foreach (var o in src.owned) if (!string.IsNullOrEmpty(o) && !r.owned.Contains(o)) r.owned.Add(o);
        }
        r.tx.Sort((x, y) => x.utcMs.CompareTo(y.utcMs));
        while (r.tx.Count > MaxTx) r.tx.RemoveAt(0);
        return r;
    }
}
