using System.Collections.Generic;
using UnityEngine;

/// Endless, fair track generator with pooling. Replaces the old levelManager/hurdlesSpawner/bonusSpawner.
/// Rows of branches always leave an open angular window; the window follows a "safe path" whose
/// turn rate and row spacing ramp with difficulty. Bee swarms (jump hazards) only on empty rows.
public class TCTrack : MonoBehaviour
{
    public GameObject trunkPrefab, branchPrefab, beesPrefab, coinPrefab, heartPrefab, boostPrefab;
    public GameObject pineconePrefab, waspPrefab, squirrelPrefab, cooldownPrefab, magnetPrefab;
    public Transform stemParent, hazardParent, bonusParent;
    public Transform player;

    TCPool trunkPool, branchPool, beePool, coinPool, heartPool, boostPool, cooldownPool, magnetPool;
    TCPool pineconePool, waspPool, squirrelPool;
    readonly List<TCPooled> live = new List<TCPooled>(256);
    readonly List<float> angles = new List<float>(8);
    float nextTrunkY, nextRowY, safeAngle, lastCoinY = -999f;
    float lastHazardY = -999f, lastBonusY = -999f;
    float nextHeartEligibleTime;
    float runActiveTimer;
    int rowIndex; bool lastWasBee; player pl;
    public static System.Action<float, float, float, float, float, float> OnRow; // y, safe angle, required turn deg, window deg (360 = bees), seconds available, metres
    public int LiveCount => live.Count;
    public TCPool[] Pools => new[] { trunkPool, branchPool, beePool, coinPool, heartPool, boostPool, cooldownPool, magnetPool, pineconePool, waspPool, squirrelPool };
    public static TCTrack Instance;
    TCConfig C;

    const float UniformCoinSpacing = 0.38f; // Constant uniform distance between consecutive coins in any string
    const float MinHazardSpacing = 1.0f;
    const float MinBonusSpacing = 1.5f;
    const float BonusCoinMargin = 0.8f; // absolute clearance between any bonus pickup and any coin

    void Awake()
    {
        Instance = this;
        ResetChilliRegulation();
    }

    // Chilli Regulation: if player collects >= 3 chillies under 10 seconds, suppress new chilli spawns for 10 seconds
    static readonly List<float> chilliPickupTimestamps = new List<float>();
    static float chilliSpawnBlockedUntil = 0f;

    public static bool CanSpawnChilli => Time.time >= chilliSpawnBlockedUntil;

    public static void ResetChilliRegulation()
    {
        chilliPickupTimestamps.Clear();
        chilliSpawnBlockedUntil = 0f;
    }

    public static void OnChilliCollected()
    {
        float now = Time.time;

        // Purge timestamps older than 10 seconds
        for (int i = chilliPickupTimestamps.Count - 1; i >= 0; i--)
        {
            if (now - chilliPickupTimestamps[i] > 10f)
                chilliPickupTimestamps.RemoveAt(i);
        }

        // Record this collection
        chilliPickupTimestamps.Add(now);

        // Check if 3 or more chillies have been collected in the sliding 10-second window
        if (chilliPickupTimestamps.Count >= 3)
        {
            chilliSpawnBlockedUntil = Mathf.Max(chilliSpawnBlockedUntil, now + 10f);
            chilliPickupTimestamps.Clear();

            if (Instance != null)
            {
                Instance.ClearActiveChilliesAhead();
            }
        }
    }

    public void ClearActiveChilliesAhead()
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var item = live[i];
            if (item != null && item.kind == "boost" && item.gameObject.activeSelf)
            {
                item.Release();
                live.RemoveAt(i);
            }
        }
    }

    void Start()
    {
        C = TCConfig.I;
        pl = player.GetComponentInParent<global::player>() ?? FindObjectOfType<global::player>();
        trunkPool = new TCPool(trunkPrefab, stemParent, C.chunksAhead + 4);
        branchPool = new TCPool(branchPrefab, hazardParent, 24);
        beePool = new TCPool(beesPrefab, hazardParent, 3);
        coinPool = new TCPool(coinPrefab, bonusParent, 120);
        var hProto = heartPrefab;
        if (!hProto) hProto = Resources.Load<GameObject>("PowerUps/Health_Pickup_Root");
        if (hProto != null) hProto.SetActive(false);
        heartPool = new TCPool(hProto, bonusParent, 2);

        var bProto = boostPrefab;
        if (!bProto) bProto = Resources.Load<GameObject>("PowerUps/Chilli_Pickup_Root");
        if (bProto != null) bProto.SetActive(false);
        boostPool = new TCPool(bProto, bonusParent, 2);
        var cProto = cooldownPrefab;
        if (!cProto) cProto = Resources.Load<GameObject>("PowerUps/Cooldown_Pickup_Root");
        if (!cProto) cProto = TCTimeDilation.CreateCooldownPickupPrefab(bonusParent);
        if (cProto != null) cProto.SetActive(false);
        cooldownPool = new TCPool(cProto, bonusParent, 3);

        var mProto = magnetPrefab;
        if (!mProto) mProto = Resources.Load<GameObject>("PowerUps/Magnet_Pickup_Root");
        if (!mProto) mProto = TCCoinMagnet.CreateMagnetPickupPrefab(bonusParent);
        if (mProto != null) mProto.SetActive(false);
        magnetPool = new TCPool(mProto, bonusParent, 3);

        var pProto = pineconePrefab ? pineconePrefab : TCEnemy.CreatePineconePrefab(hazardParent);
        pineconePool = new TCPool(pProto, hazardParent, 5);

        var sProto = squirrelPrefab ? squirrelPrefab : TCEnemy.CreateSquirrelPrefab(hazardParent);
        squirrelPool = new TCPool(sProto, hazardParent, 5);

        var wProto = waspPrefab ? waspPrefab : TCEnemy.CreateWaspPrefab(hazardParent);
        waspPool = new TCPool(wProto, hazardParent, 5);

        float py = player.position.y;
        float chunkH = C.trunkChunkHeight;
        nextTrunkY = Mathf.Floor((py - 2f * chunkH) / chunkH) * chunkH;
        nextRowY = py + (TCSave.TutorialDone ? C.safeStartHeight : 100f);
        safeAngle = TCPlayerAngle();
        lastCoinY = -999f;
        lastHazardY = -999f;
        lastBonusY = -999f;
        runActiveTimer = 0f;
        nextHeartEligibleTime = Time.time + 10f; // heart check every 10 seconds

        // Spawn High Score Milestone Tag & Ribbon on trunk if previous high score exists
        if (TCSave.Best >= 10 && player != null)
        {
            TCHighScoreMarker.Spawn(stemParent, player, TCSave.Best, C.metresPerUnit);
        }

        // Safe start: welcome string of coins straight up
        SpawnCoinTrail(py + 1.2f, py + 1.2f + 6 * UniformCoinSpacing, safeAngle, safeAngle, 7);
        Tick();
    }

    float TCPlayerAngle()
    {
        var sp = FindObjectOfType<playerInteract>();
        if (!sp) return 0;
        var p = sp.transform.position; return Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
    }

    void Update() => Tick();

    void Tick()
    {
        if (trunkPool == null || !player) return;
        if (pl && !pl.paused && !pl.GameOver)
        {
            runActiveTimer += Time.deltaTime;
        }

        float py = player.position.y;
        float metres = pl ? pl.currentScore : 0f; // same distance the HUD/difficulty uses
        while (nextTrunkY < py + C.chunksAhead * C.trunkChunkHeight)
        {
            Spawn(trunkPool, "trunk", nextTrunkY, 0);
            nextTrunkY += C.trunkChunkHeight;
        }
        if (TCSave.TutorialDone)
        {
            while (nextRowY < py + 12f) GenerateRow(metres + (nextRowY - py) * C.metresPerUnit);
        }

        // Recycle everything far below the player (iterate backwards, no allocations)
        float cut = py - 3.5f;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var p = live[i];
            if (!p) { live.RemoveAt(i); continue; }
            // collected / smashed items are hidden by gameplay: return them to their pool (was leaking)
            if (!p.gameObject.activeSelf) { p.Release(); live.RemoveAt(i); continue; }
            if (p.y < cut) { p.Release(); live.RemoveAt(i); }
        }
    }

    void GenerateRow(float metres)
    {
        float d = C.Difficulty(metres);
        float y = Mathf.Max(nextRowY, lastHazardY + MinHazardSpacing);
        rowIndex++;
        float gap = Mathf.Lerp(C.rowGapEasy, C.rowGapHard, d) * Random.Range(0.95f, 1.15f);
        bool breather = C.breatherEvery > 0 && rowIndex % C.breatherEvery == 0;

        // Check 10-second timer for heart eligibility (2 out of 10 probability, i.e. 0.20)
        bool heartEligible = false;
        if (Time.time >= nextHeartEligibleTime)
        {
            nextHeartEligibleTime = Time.time + 10f;
            heartEligible = (Random.value < 0.20f);
        }

        if (breather)
        {
            // Breather: clear row with no hazards.
            float bonusY = y + gap * 0.5f;
            float from = safeAngle;
            safeAngle += Random.value < 0.5f ? -60f : 60f;
            lastWasBee = false;

            bool canSpawnBreatherBonus = bonusY > lastBonusY + MinBonusSpacing && Mathf.Abs(bonusY - lastCoinY) > BonusCoinMargin;

            float playerTraveledMetres = pl ? pl.currentScore : 0f;
            bool canSpawnCooldown = playerTraveledMetres >= C.cooldownMinMetres && metres >= C.cooldownMinMetres;
            bool canSpawnMagnet = playerTraveledMetres >= C.magnetMinMetres && metres >= C.magnetMinMetres;

            if (canSpawnBreatherBonus && heartEligible)
            {
                Spawn(heartPool, "heart", bonusY, safeAngle);
                lastBonusY = bonusY;
                heartEligible = false; // consumed
            }
            else if (canSpawnBreatherBonus && CanSpawnChilli && Random.value < C.boostChance * 2f)
            {
                Spawn(boostPool, "boost", bonusY, safeAngle);
                lastBonusY = bonusY;
            }
            else if (canSpawnBreatherBonus && canSpawnCooldown && Random.value < C.cooldownChance * 2f)
            {
                Spawn(cooldownPool, "cooldown", bonusY, safeAngle);
                lastBonusY = bonusY;
            }
            else if (canSpawnBreatherBonus && canSpawnMagnet && Random.value < C.magnetChance * 2f)
            {
                Spawn(magnetPool, "magnet", bonusY, safeAngle);
                lastBonusY = bonusY;
            }
            else
            {
                // Coin spiral that guarantees no overlap with hazards or powerups
                float spiralStartY = Mathf.Max(y + 0.3f, Mathf.Max(lastCoinY + UniformCoinSpacing, lastBonusY + BonusCoinMargin));
                float spiralEndY = y + gap - 0.3f;
                SpawnCoinTrail(spiralStartY, spiralEndY, from, safeAngle);
            }

            nextRowY = y + gap * 1.3f;
            return;
        }

        // Bee swarms only spawn after crossing 2000 metres
        if (!lastWasBee && metres >= 2000f && Random.value < C.beeChance * d + 0.02f)
        {
            // Bee swarm row: full 360 jump ring hazard. Spawns an arching coin string over the bees!
            float beeY = y + gap * 0.4f;
            Spawn(beePool, "bees", beeY, 0);
            lastHazardY = beeY + 0.3f;
            lastWasBee = true;
            OnRow?.Invoke(beeY, safeAngle, 0, 360, gap / C.ClimbSpeedAtDistance(metres), metres);

            // Vertical arc string over bees so spider can jump and collect coins over the swarm
            // In classic endless runners (like Subway Surfers), jump coin arcs are canonical:
            // exactly 8 coins, fixed uniform angle along the track, farthest uniform flight & landing span (3.5m).
            const float jumpSpan = LongestArcSpan;
            // Parabolic symmetric apex is at t = 0.5 (dead center); place apex directly over the bee swarm centre (beeY)
            float arcStartY = Mathf.Max(beeY - jumpSpan * 0.5f, Mathf.Max(lastCoinY + UniformCoinSpacing, lastBonusY + BonusCoinMargin));
            float arcEndY = arcStartY + jumpSpan;
            SpawnCoinArc(arcStartY, arcEndY, safeAngle, SpiderJumpHeight, 8);

            nextRowY = beeY + gap * 1.5f;
            return;
        }

        lastWasBee = false;
        // Fairness: the turn between rows can never exceed what a player can steer in the time available.
        float secs = Mathf.Max(0.1f, Mathf.Min(y - lastHazardY, gap * 3f) / C.ClimbSpeedAtDistance(metres));
        float turn = Mathf.Min(Mathf.Lerp(C.maxTurnEasy, C.maxTurnHard, d), C.maxTurnRate * secs);
        float newSafe = safeAngle + Random.Range(-turn, turn);
        float window = Mathf.Lerp(C.openWindowEasy, C.openWindowHard, d);

        // Branch row: spawn branches according to branchChance (default 0.5 = 50% reduced branch probability)
        bool spawnBranches = Random.value < C.branchChance;
        if (spawnBranches)
        {
            // Branches fill the rest of the circle with spacing; count ramps with difficulty.
            int maxBranches = Mathf.RoundToInt(Mathf.Lerp(1.5f, 4.2f, d));
            int count = Random.Range(1, maxBranches + 1);
            angles.Clear();
            float blocked = 360 - window;
            for (int k = 0; k < count; k++)
            {
                float a = newSafe + window * 0.5f + 20 + (blocked - 40) * ((k + Random.Range(0.2f, 0.8f)) / count);
                angles.Add(a);
                var b = Spawn(branchPool, "branch", y, a);
                b.transform.rotation = Quaternion.Euler(0, a, 0);

                // Apply flower/foliage variant (None, Red, Yellow, Pink, Thorn):
                var bv = b.GetComponent<TCBranchVariant>();
                if (!bv) bv = b.gameObject.AddComponent<TCBranchVariant>();
                var flowerType = (TCBranchVariant.FlowerType)Random.Range(0, 5);
                bv.SetVariant(flowerType);
            }
        }
        float prevRowY = lastHazardY;
        if (spawnBranches) lastHazardY = y;
        OnRow?.Invoke(y, newSafe, Mathf.Abs(Mathf.DeltaAngle(safeAngle, newSafe)), window, secs, metres);

        // Enemy / Hurdle Spawns based on distance:
        // - First 2000 meters: only wasps and branches
        // - After 2000 meters: bee swarms can spawn (handled above)
        // - After 3000 meters: squirrels can spawn
        if (metres >= 3000f && Random.value < 0.32f)
        {
            float squirrelY = y + gap * 0.52f;
            float squirrelAngle = newSafe + (Random.value < 0.5f ? -35f : 35f);
            Spawn(squirrelPool, "squirrel", squirrelY, squirrelAngle);
        }

        // Wasps spawn from the beginning (alongside branches)
        if (metres > 30f && Random.value < 0.22f)
        {
            float waspY = y + gap * 0.72f;
            float waspAngle = newSafe + (Random.value < 0.5f ? -42f : 42f);
            Spawn(waspPool, "wasp", waspY, waspAngle);
        }

        // In the safe gap between rows, spawn strictly ONE item type (heart, chilli boost, or coins) with proper margins
        float bonusYPos = y + gap * 0.5f;
        bool canSpawnBonus = bonusYPos > lastBonusY + MinBonusSpacing && Mathf.Abs(bonusYPos - lastCoinY) > BonusCoinMargin;

        float playerDistance = pl ? pl.currentScore : 0f;
        bool canSpawnCooldownGap = playerDistance >= C.cooldownMinMetres && metres >= C.cooldownMinMetres;
        bool canSpawnMagnetGap = playerDistance >= C.magnetMinMetres && metres >= C.magnetMinMetres;

        if (canSpawnBonus && heartEligible)
        {
            Spawn(heartPool, "heart", bonusYPos, newSafe);
            lastBonusY = bonusYPos;
            heartEligible = false; // consumed
        }
        else if (canSpawnBonus && CanSpawnChilli && Random.value < C.boostChance)
        {
            Spawn(boostPool, "boost", bonusYPos, newSafe);
            lastBonusY = bonusYPos;
        }
        else if (canSpawnBonus && canSpawnCooldownGap && Random.value < C.cooldownChance)
        {
            Spawn(cooldownPool, "cooldown", bonusYPos, newSafe);
            lastBonusY = bonusYPos;
        }
        else if (canSpawnBonus && canSpawnMagnetGap && Random.value < C.magnetChance)
        {
            Spawn(magnetPool, "magnet", bonusYPos, newSafe);
            lastBonusY = bonusYPos;
        }
        else if (Random.value < C.coinTrailChance)
        {
            // Spawn one of three coin string patterns randomly:
            // 0: Standard smooth climbing trail (leads smoothly into safe gap)
            // 1: Zigzag string (player weaves left and right across climbing path)
            // 2: Vertical jump arc string (spider jumps to collect the arced coins in the air)
            int pattern = Random.Range(0, 3);
            int stringCount = Random.Range(6, 11); // 6 to 10 coins
            float trailSpan = (stringCount - 1) * UniformCoinSpacing;
            float trailEndY = y - 0.35f;
            float trailStartY = Mathf.Max(trailEndY - trailSpan, Mathf.Max(prevRowY + 0.35f, lastBonusY + BonusCoinMargin));

            if (pattern == 1)
            {
                // Zigzag pattern
                float centerAngle = Mathf.LerpAngle(safeAngle, newSafe, 0.5f);
                float angleAmp = Random.Range(14f, 22f);
                SpawnCoinZigzag(trailStartY, trailEndY, centerAngle, angleAmp, stringCount);
            }
            else if (pattern == 2)
            {
                // Vertical arc pattern straight upward (constant angle, Subway Surfers style canonical 8-coin jump arc, farthest landing span)
                const float jumpSpan = LongestArcSpan;
                float arcEndY = y - 0.35f;
                float arcStartY = Mathf.Max(arcEndY - jumpSpan, Mathf.Max(prevRowY + 0.35f, lastBonusY + BonusCoinMargin));
                SpawnCoinArc(arcStartY, arcEndY, newSafe, SpiderJumpHeight, 8);
            }
            else
            {
                // Classic smooth trail
                SpawnCoinTrail(trailStartY, trailEndY, Mathf.LerpAngle(safeAngle, newSafe, 0.70f), newSafe, stringCount);
            }
        }

        safeAngle = newSafe;
        nextRowY = y + gap;
    }

    void SpawnCoinTrail(float startY, float endY, float fromAngle, float toAngle, int preferredCount = 0)
    {
        float minY = Mathf.Max(startY, Mathf.Max(lastCoinY + UniformCoinSpacing, lastBonusY + BonusCoinMargin));
        float spanY = endY - minY;
        if (spanY <= 0.05f) return;

        // Trunk cylinder surface metric: distance ds^2 = dy^2 + (R * da_radians)^2
        const float trunkRadius = 0.603f;
        float deltaAngleDeg = Mathf.DeltaAngle(fromAngle, toAngle);
        float deltaAngleRad = deltaAngleDeg * Mathf.Deg2Rad;
        float arcDistance = trunkRadius * deltaAngleRad;
        float totalPathLength = Mathf.Sqrt(spanY * spanY + arcDistance * arcDistance);

        if (totalPathLength < UniformCoinSpacing * 0.7f) return;

        // Calculate exact count of coins so that the spacing between consecutive coins is exactly UniformCoinSpacing
        int steps = Mathf.Max(1, Mathf.RoundToInt(totalPathLength / UniformCoinSpacing));
        int count = steps + 1;
        if (preferredCount > 0)
        {
            count = Mathf.Min(count, preferredCount);
            steps = count - 1;
        }

        if (count < 2)
        {
            Spawn(coinPool, "coin", minY + spanY * 0.5f, Mathf.LerpAngle(fromAngle, toAngle, 0.5f));
            lastCoinY = minY + spanY * 0.5f;
            return;
        }

        // Advance along the path in strictly uniform surface arc/linear distance steps
        float actualTotalDist = steps * UniformCoinSpacing;
        float frac = Mathf.Clamp01(actualTotalDist / totalPathLength);
        float actualSpanY = spanY * frac;
        float actualDeltaAngle = deltaAngleDeg * frac;

        float stepY = actualSpanY / steps;
        float stepAngle = actualDeltaAngle / steps;

        for (int i = 0; i <= steps; i++)
        {
            float cy = minY + i * stepY;
            float ca = fromAngle + i * stepAngle;
            Spawn(coinPool, "coin", cy, ca);
            lastCoinY = cy;
        }
    }

    void SpawnCoinZigzag(float startY, float endY, float centerAngle, float amplitudeDeg = 18f, int count = 8)
    {
        float minY = Mathf.Max(startY, Mathf.Max(lastCoinY + UniformCoinSpacing, lastBonusY + BonusCoinMargin));
        float spanY = endY - minY;
        if (spanY <= 0.05f || count < 3) return;

        const float trunkRadius = 0.603f;
        // Start zigzag direction randomly (-1 or +1)
        float dir = Random.value < 0.5f ? -1f : 1f;
        float freq = Mathf.PI * 2.5f;

        // Discretize the continuous zigzag path at high resolution to measure true arc-length on the trunk cylinder
        const int Samples = 240;
        float[] sampleY = new float[Samples + 1];
        float[] sampleAngle = new float[Samples + 1];
        float[] cumDist = new float[Samples + 1];
        cumDist[0] = 0f;

        float prevY = minY;
        float prevAngle = centerAngle;
        sampleY[0] = prevY;
        sampleAngle[0] = prevAngle;

        for (int s = 1; s <= Samples; s++)
        {
            float t = (float)s / Samples;
            float cy = minY + t * spanY;
            float ca = centerAngle + Mathf.Sin(t * freq) * amplitudeDeg * dir;

            sampleY[s] = cy;
            sampleAngle[s] = ca;

            float dy = cy - prevY;
            float dAngleRad = Mathf.DeltaAngle(prevAngle, ca) * Mathf.Deg2Rad;
            float dArc = trunkRadius * dAngleRad;
            float ds = Mathf.Sqrt(dy * dy + dArc * dArc);

            cumDist[s] = cumDist[s - 1] + ds;
            prevY = cy;
            prevAngle = ca;
        }

        float totalPathLen = cumDist[Samples];
        if (totalPathLen < UniformCoinSpacing * 0.7f) return;

        // Determine number of steps matching UniformCoinSpacing
        int steps = Mathf.Max(2, Mathf.RoundToInt(totalPathLen / UniformCoinSpacing));
        if (count > 0)
        {
            steps = Mathf.Min(steps, count - 1);
        }

        float targetSpacing = totalPathLen / steps;

        // Spawn coins along the zigzag trajectory with strictly uniform surface distance between consecutive coins
        int sampleIdx = 0;
        for (int i = 0; i <= steps; i++)
        {
            float targetDist = i * targetSpacing;
            while (sampleIdx < Samples && cumDist[sampleIdx + 1] < targetDist)
            {
                sampleIdx++;
            }

            float d0 = cumDist[sampleIdx];
            float d1 = (sampleIdx < Samples) ? cumDist[sampleIdx + 1] : d0;
            float segFrac = (d1 > d0) ? Mathf.Clamp01((targetDist - d0) / (d1 - d0)) : 0f;

            float coinY = Mathf.Lerp(sampleY[sampleIdx], sampleY[Mathf.Min(sampleIdx + 1, Samples)], segFrac);
            float coinAngle = Mathf.LerpAngle(sampleAngle[sampleIdx], sampleAngle[Mathf.Min(sampleIdx + 1, Samples)], segFrac);

            Spawn(coinPool, "coin", coinY, coinAngle);
            lastCoinY = coinY;
        }
    }

    public const float SpiderJumpHeight = 0.785f;
    public const float LongestArcSpan = 1.75f; // Canonical landing span reduced by 50% from 3.50m (matches single jump climb distance)

    /// <summary>
    /// Computes normalized jump trajectory height [0..1] at normalized flight progress t [0..1].
    /// In Subway Surfers and canonical platformers, the jump arc is a symmetric parabola:
    /// h(t) = 4 * t * (1 - t)
    /// - Launches from bark at t = 0 (h = 0)
    /// - Smoothly peaks at apex at t = 0.5 (h = 1.0)
    /// - Lands back on tree bark at t = 1.0 (h = 0)
    /// </summary>
    public static float EvaluateJumpTrajectory(float t)
    {
        t = Mathf.Clamp01(t);
        return 4.0f * t * (1.0f - t);
    }

    void SpawnCoinArc(float startY, float endY, float angle, float peakArcHeight = SpiderJumpHeight, int count = 8)
    {
        float minY = Mathf.Max(startY, Mathf.Max(lastCoinY + UniformCoinSpacing, lastBonusY + BonusCoinMargin));
        // Use the uniform canonical span (LongestArcSpan = 1.75m, 50% reduced) consistently across all arc strings
        float spanY = LongestArcSpan;
        if (count < 3) return;

        const float trunkRadius = 0.603f;
        float stepY = spanY / (count - 1);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float cy = minY + i * stepY;
            // Straight vertical line: angle is constant across all coins in the arc
            float ca = angle;

            // Canonical Subway Surfers symmetric parabolic jump arc:
            // - t = 0: height = 0 (on tree bark)
            // - t = 0.5: apex height = peakArcHeight (exact peak centered)
            // - t = 1.0: height = 0 (landing back on tree bark at farthest point)
            float normHeight = EvaluateJumpTrajectory(t);
            float r = trunkRadius + normHeight * peakArcHeight;

            SpawnCoinAtRadius(cy, ca, r);
            lastCoinY = cy;
        }
    }

    TCPooled SpawnCoinAtRadius(float y, float angle, float radius)
    {
        var p = Spawn(coinPool, "coin", y, angle);
        for (int i = 0; i < p.transform.childCount; i++)
        {
            var ch = p.transform.GetChild(i);
            ch.localPosition = new Vector3(0f, 0f, radius);
        }
        return p;
    }

    public void AttractCoins(Vector3 spiderPos, float maxMetresDiff, float maxMove)
    {
        float spiderY = spiderPos.y;
        float spiderAngle = Mathf.Atan2(spiderPos.x, spiderPos.z) * Mathf.Rad2Deg;
        float spiderRadius = Mathf.Sqrt(spiderPos.x * spiderPos.x + spiderPos.z * spiderPos.z);
        const float trunkRadius = 0.603f;
        float mpu = (C != null && C.metresPerUnit > 0f) ? C.metresPerUnit : 20f;

        for (int i = live.Count - 1; i >= 0; i--)
        {
            var p = live[i];
            if (p == null || !p.gameObject.activeSelf || p.kind != "coin") continue;

            Transform coinChild = (p.transform.childCount > 0) ? p.transform.GetChild(0) : p.transform;
            if (coinChild == null || !coinChild.gameObject.activeSelf)
            {
                p.Release();
                live.RemoveAt(i);
                continue;
            }

            float coinY = p.transform.position.y;
            float dy = spiderY - coinY;
            // Height distance converted to true game metres (1 world unit = 20m)
            float heightDiffMetres = Mathf.Abs(dy) * mpu;

            // Only attract coins when height distance is within maxMetresDiff (40m)
            if (heightDiffMetres > maxMetresDiff) continue;

            float coinAngle = p.transform.eulerAngles.y;
            float dAngle = Mathf.DeltaAngle(coinAngle, spiderAngle);
            float arc = trunkRadius * dAngle * Mathf.Deg2Rad;
            float dist = Mathf.Sqrt(dy * dy + arc * arc);

            // Check if coin has reached the spider -> collect it immediately!
            if (dist <= 0.45f)
            {
                coinChild.gameObject.SetActive(false);
                coinChild.localPosition = new Vector3(0f, 0f, 0.603f);
                coinChild.localRotation = Quaternion.Euler(0f, 0f, 90f);

                p.gameObject.SetActive(false);
                p.Release();
                live.RemoveAt(i);

                TCSfx.Play("coin", 0.55f);
                gameManager.Instance?.addCoin();
                playerInteract.OnEvent?.Invoke("coin", spiderPos);
                continue;
            }

            // Fly coin smoothly with curved acceleration: gentle drift from far, swooping curve closer in
            float tNear = Mathf.Clamp01(1f - (heightDiffMetres / maxMetresDiff)); // 0 when at 40m, 1 when at spider height
            float smoothSpeed = Mathf.Lerp(8.0f, 26.0f, tNear * tNear);
            float step = smoothSpeed * Time.deltaTime;

            // Smooth interpolation along height (Y)
            float moveRatio = Mathf.Clamp01(step / Mathf.Max(0.001f, dist));
            float newY = Mathf.MoveTowards(coinY, spiderY, step * Mathf.Clamp01(Mathf.Abs(dy) / Mathf.Max(0.001f, dist)));

            // Smooth angular tracking around cylinder trunk
            float angleSpeed = Mathf.Lerp(90f, 360f, tNear);
            float newAngle = Mathf.MoveTowardsAngle(coinAngle, spiderAngle, angleSpeed * Time.deltaTime);

            // Lift coin gently off bark along an arc as it glides in
            float targetR = Mathf.Max(trunkRadius, spiderRadius);
            float arcLift = Mathf.Sin(tNear * Mathf.PI) * 0.12f;
            float currentR = Mathf.Lerp(trunkRadius, targetR, tNear) + arcLift;

            p.transform.position = new Vector3(0f, newY, 0f);
            p.transform.rotation = Quaternion.Euler(0f, newAngle, 0f);
            p.y = newY;
            coinChild.localPosition = new Vector3(0f, 0f, currentR);
        }
    }

    TCPooled Spawn(TCPool pool, string kind, float y, float angle)
    {
        if (pool == null) return null;
        var g = pool.Get(new Vector3(0, y, 0), Quaternion.Euler(0, angle, 0));
        if (!g) return null;
        var p = g.GetComponent<TCPooled>();
        if (!p) p = g.AddComponent<TCPooled>();
        p.pool = pool; p.kind = kind; p.y = y;

        var enemy = g.GetComponent<TCEnemy>();
        if (enemy)
        {
            enemy.pooled = p;
            enemy.Init(y, angle, Random.Range(35f, 60f), Random.Range(1.8f, 2.8f));
        }

        // re-enable children that were hidden on pickup
        for (int i = 0; i < g.transform.childCount; i++)
        {
            var ch = g.transform.GetChild(i);
            ch.gameObject.SetActive(true);
            if (kind == "coin")
            {
                ch.localPosition = new Vector3(0f, 0f, 0.603f);
                ch.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else if (kind == "heart")
            {
                if (ch.CompareTag("health") || ch.name.IndexOf("Heart", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ch.localPosition = new Vector3(0f, -0.4105f, 0.6253f);
                    ch.localRotation = Quaternion.Euler(0f, 119.2f, 0f);
                    ch.localScale = new Vector3(3f, 3f, 3f);

                    var anim = ch.GetComponent<Animator>();
                    if (anim) anim.enabled = false;
                    var rot = ch.GetComponent<continuousRotation>();
                    if (rot) rot.enabled = false;
                }
                else if (ch.name.IndexOf("CircularGlow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         ch.name.IndexOf("Circle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ch.localPosition = new Vector3(0f, -0.105f, 0.5464f);
                    ch.localRotation = Quaternion.identity;
                    ch.localScale = new Vector3(0.8f, 0.8f, 0.8f);

                    var glow = ch.GetComponent<TCPickupGlow>();
                    if (glow)
                    {
                        glow.pulseSpeed = 0f;
                        glow.pulseMagnitude = 0f;
                    }
                }
            }
        }
        live.Add(p);
        return p;
    }

    public void OnTutorialFinished()
    {
        float py = player ? player.position.y : 0f;
        nextRowY = py + C.safeStartHeight;
        lastHazardY = py;
        lastBonusY = py;
    }

    public TCPooled SpawnTutorialBranch(float y, float angle)
    {
        var b = Spawn(branchPool, "branch", y, angle);
        b.transform.rotation = Quaternion.Euler(0, angle, 0);
        var bv = b.GetComponent<TCBranchVariant>();
        if (!bv) bv = b.gameObject.AddComponent<TCBranchVariant>();
        bv.SetVariant(TCBranchVariant.FlowerType.Red);
        return b;
    }

    public TCWasp SpawnTutorialWasp(float y, float angle)
    {
        var p = Spawn(waspPool, "wasp", y, angle);
        var wasp = p.GetComponent<TCWasp>();
        if (wasp) wasp.Init(y, angle, 0f, 0f);
        return wasp;
    }

    public TCPooled SpawnTutorialBees(float y)
    {
        return Spawn(beePool, "bees", y, 0);
    }

    public TCPooled SpawnTutorialChilli(float y, float angle)
    {
        return Spawn(boostPool, "boost", y, angle);
    }

    public TCPooled SpawnTutorialCooldown(float y, float angle)
    {
        return Spawn(cooldownPool, "cooldown", y, angle);
    }
}
