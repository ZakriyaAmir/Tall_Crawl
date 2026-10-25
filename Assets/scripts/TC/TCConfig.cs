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
    [Tooltip("Every N rows a breather (no hurdles, coin trail) is inserted")] public int breatherEvery = 7;
    [Range(0, 1)] public float beeChance = 0.18f;
    public float beeMinMetres = 250f;
    [Range(0, 1)] public float coinTrailChance = 0.55f;
    [Range(0, 1)] public float heartChance = 0.06f;
    [Range(0, 1)] public float boostChance = 0.05f;

    [Header("Player")]
    public float defaultSensitivity = 1f;
    public float dragDegreesPerScreenWidth = 300f;
    public float deadZonePixels = 6f;
    public float rotationSmoothing = 18f;
    public float keyboardTurnSpeed = 220f;
    public float swipeUpThreshold = 0.06f; // fraction of screen height
    public int baseHearts = 3;

    [Header("Economy")]
    public int coinValue = 1;
    public int coinsPerHundredMetres = 2;
    public int reviveCostGems = 5;
    public int startingCoins = 150;
    public int[] dailyRewards = { 50, 75, 100, 150, 200, 300, 2 /*gems*/ };
    public int interstitialEveryNRuns = 3;
    public int[] upgradeCosts = { 150, 400, 900, 1800, 3500 };

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
