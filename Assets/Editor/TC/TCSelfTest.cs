using System.Reflection;
using UnityEditor;
using UnityEngine;

/// Batch regression checks: Unity -batchmode -executeMethod TCSelfTest.Run -quit
/// Economy config sanity, save round-trip/clamping, and track fairness over 5,000 generated rows.
public static class TCSelfTest
{
    static int fails;
    static void Check(bool ok, string what) { if (!ok) fails++; Debug.Log((ok ? "SELFTEST ok   " : "SELFTEST FAIL ") + what); }

    [MenuItem("Tall Crawl/Run Self Test")]
    public static void Run()
    {
        fails = 0;
        var C = TCConfig.I;
        // Economy
        for (int i = 1; i < C.upgradeCosts.Length; i++) Check(C.upgradeCosts[i] > C.upgradeCosts[i - 1], "upgrade cost rises " + i);
        Check(C.dailyRewards.Length == 7, "7 daily rewards");
        Check(C.coinsPerHundredMetres > 0 && C.reviveCostGems > 0, "economy values positive");
        int avgRun = 600 / 100 * C.coinsPerHundredMetres + 30; // ~600 m run with ~30 coins
        Check(C.upgradeCosts[0] / (float)avgRun <= 4f, $"first upgrade within 4 runs ({C.upgradeCosts[0] / (float)avgRun:0.0})");
        // Save round trip
        var keep = PlayerPrefs.GetInt("tc_coins");
        TCSave.Coins = 1234; Check(TCSave.Coins == 1234, "coins round trip");
        TCSave.Coins = -5; Check(TCSave.Coins == 0, "coins clamp at 0");
        TCSave.Coins = int.MaxValue; Check(TCSave.Coins == int.MaxValue, "coins max");
        TCSave.Sensitivity = 9; Check(Mathf.Approximately(TCSave.Sensitivity, 2.2f), "sensitivity clamp");
        PlayerPrefs.SetInt("tc_coins", keep); PlayerPrefs.DeleteKey("tc_sens");
        // Track fairness
        var root = new GameObject("selftest");
        GameObject Proto(string n) { var g = new GameObject(n); g.transform.SetParent(root.transform); g.SetActive(false); return g; }
        var plT = new GameObject("p").transform; plT.SetParent(root.transform);
        var tr = root.AddComponent<TCTrack>();
        tr.trunkPrefab = Proto("t"); tr.branchPrefab = Proto("b"); tr.beesPrefab = Proto("e"); tr.coinPrefab = Proto("c"); tr.heartPrefab = Proto("h"); tr.boostPrefab = Proto("o");
        tr.stemParent = tr.hazardParent = tr.bonusParent = root.transform; tr.player = plT;
        int rows = 0, unfair = 0, bees = 0, beeChain = 0; float worst = 0, lastBee = -99;
        System.Action<float, float, float, float, float, float> h = (y, a, turn, win, secs, m) =>
        {
            rows++;
            if (win >= 360) { bees++; if (y - lastBee < 2.5f) beeChain++; lastBee = y; return; }
            float need = Mathf.Max(0, turn - win * 0.5f) / Mathf.Max(0.05f, secs);
            worst = Mathf.Max(worst, need); if (need > 300) unfair++;
            if (win < 60) unfair++;
        };
        TCTrack.OnRow += h;
        var gen = typeof(TCTrack).GetMethod("GenerateRow", BindingFlags.NonPublic | BindingFlags.Instance);
        var start = typeof(TCTrack).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        start.Invoke(tr, null);
        var nextRow = typeof(TCTrack).GetField("nextRowY", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < 5000; i++)
        {
            float y = (float)nextRow.GetValue(tr);
            gen.Invoke(tr, new object[] { y * C.metresPerUnit });
        }
        TCTrack.OnRow -= h;
        Check(rows >= 4000, "generated hazard rows " + rows + " (breathers excluded)");
        Check(unfair == 0, $"no unfair rows (worst steer {worst:0} deg/s, unfair {unfair})");
        Check(beeChain == 0, $"no back-to-back bee rows ({bees} bee rows)");
        Object.DestroyImmediate(root);
        Debug.Log($"SELFTEST DONE fails={fails}");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 2);
    }
}
