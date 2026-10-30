using UnityEngine;

/// Every gameplay / economy number lives here. Edit Assets/Resources/TCConfig.asset in the Inspector.
[CreateAssetMenu(menuName = "Tall Crawl/Config", fileName = "TCConfig")]
public class TCConfig : ScriptableObject
{
    [Header("Climb speed (units/sec)")]
    public float startSpeed = 1.6f;
    public float maxSpeed = 4.2f;
    [Tooltip("Metres climbed until max speed is reached")] public float metresToMaxSpeed = 3000f;
    public float boostMultiplier = 2.2f;
    public float metresPerUnit = 20f;

    [Header("Track generation")]
    public float trunkChunkHeight = 2f;
    public int chunksAhead = 8;
    public float safeStartHeight = 5f;
    [Tooltip("Vertical gap between hurdle rows at start / at max difficulty")] public float rowGapEasy = 2.2f;
    public float rowGapHard = 1.15f;
    [Tooltip("Free angular window (deg) always left open in a row, easy -> hard")] public float openWindowEasy = 170f;
    public float openWindowHard = 95f;
    [Tooltip("Max yaw change (deg) required between consecutive rows, easy -> hard")] public float maxTurnEasy = 80f;
    public float maxTurnHard = 150f;
    [Tooltip("Fastest steering (deg/sec) the generator may ever demand between two rows")] public float maxTurnRate = 240f;
    [Tooltip("Every N rows a breather (no hurdles, coin trail) is inserted")] public int breatherEvery = 7;
    [Range(0, 1)] [Tooltip("Probability of spawning branches in a hazard row (0.5 = 50% fewer branch rows)")] public float branchChance = 0.5f;
    [Range(0, 1)] public float beeChance = 0.09f;
    public float beeMinMetres = 250f;
    [Range(0, 1)] public float coinTrailChance = 0.55f;
    [Range(0, 1)] public float heartChance = 0.06f;
    [Range(0, 1)] public float boostChance = 0.12f;

    [Header("Player")]
    public float defaultSensitivity = 1f;
    public float dragDegreesPerScreenWidth = 300f;
    public float deadZonePixels = 6f;
    public float rotationSmoothing = 18f;
    public float keyboardTurnSpeed = 220f;
    public float swipeUpThreshold = 0.06f; // fraction of screen height
    [Tooltip("Max seconds between two taps for a double-tap jump")] public float doubleTapWindow = 0.3f;
    [Tooltip("A tap must be released within this many seconds")] public float doubleTapMaxHold = 0.22f;
    [Tooltip("Max distance between the two taps, fraction of screen height")] public float doubleTapMaxDistance = 0.12f;
    public int baseHearts = 3;

    [Header("Economy")]
    // Pacing target (see docs/ECONOMY.md, verified by TCSelfTest): first upgrade after ~8 runs,
    // everything bought with coins only after ~4-5 weeks of a 20-runs-a-day player.
    [Tooltip("Legacy, ignored: one collected coin always adds exactly 1 coin")] public int coinValue = 1;
    [Tooltip("Distance bonus coins per 100 m (Climb Bonus upgrade adds +1 per level)")] public int coinsPerHundredMetres = 1;
    [Tooltip("Gems awarded per this many metres in one run (0 = off)")] public int gemEveryMetres = 2000;
    public int reviveCostGems = 5;
    public int startingCoins = 50;
    public int[] dailyRewards = { 15, 20, 25, 30, 40, 60, 1 /*gems*/ };
    public int interstitialEveryNRuns = 3;
    public int[] upgradeCosts = { 250, 600, 1200, 2200, 3500 };
    [Tooltip("Coins from the shop's free rewarded ad")] public int adCoinReward = 20;

    static TCConfig _inst;
    public static TCConfig I
    {
        get
        {
            if (_inst == null) _inst = Resources.Load<TCConfig>("TCConfig");
            if (_inst == null) _inst = CreateInstance<TCConfig>();
            return _inst;
        }
    }

    public float Difficulty(float metres) => Mathf.Clamp01(metres / metresToMaxSpeed);
}
