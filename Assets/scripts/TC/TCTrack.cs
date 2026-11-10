using System.Collections.Generic;
using UnityEngine;

/// Endless, fair track generator with pooling. Replaces the old levelManager/hurdlesSpawner/bonusSpawner.
/// Rows of branches always leave an open angular window; the window follows a "safe path" whose
/// turn rate and row spacing ramp with difficulty. Bee swarms (jump hazards) only on empty rows.
public class TCTrack : MonoBehaviour
{
    public GameObject trunkPrefab, branchPrefab, beesPrefab, coinPrefab, heartPrefab, boostPrefab;
    public GameObject pineconePrefab, waspPrefab, squirrelPrefab, cooldownPrefab;
    public Transform stemParent, hazardParent, bonusParent;
    public Transform player;

    TCPool trunkPool, branchPool, beePool, coinPool, heartPool, boostPool, cooldownPool;
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
    public TCPool[] Pools => new[] { trunkPool, branchPool, beePool, coinPool, heartPool, boostPool, cooldownPool, pineconePool, waspPool, squirrelPool };
    public static TCTrack Instance;
    TCConfig C;

    const float UniformCoinSpacing = 0.38f; // Constant uniform distance between consecutive coins in any string
    const float MinHazardSpacing = 1.0f;
    const float MinBonusSpacing = 1.5f;
    const float BonusCoinMargin = 0.8f; // absolute clearance between any bonus pickup and any coin

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        C = TCConfig.I;
        pl = player.GetComponentInParent<global::player>() ?? FindObjectOfType<global::player>();
        trunkPool = new TCPool(trunkPrefab, stemParent, C.chunksAhead + 4);
        branchPool = new TCPool(branchPrefab, hazardParent, 24);
        beePool = new TCPool(beesPrefab, hazardParent, 3);
        coinPool = new TCPool(coinPrefab, bonusParent, 120);
        heartPool = new TCPool(heartPrefab, bonusParent, 2);
        boostPool = new TCPool(boostPrefab, bonusParent, 2);
        var cProto = cooldownPrefab ? cooldownPrefab : TCTimeDilation.CreateCooldownPickupPrefab(bonusParent);
        cooldownPool = new TCPool(cProto, bonusParent, 3);

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

            if (canSpawnBreatherBonus && heartEligible)
            {
                Spawn(heartPool, "heart", bonusY, safeAngle);
                lastBonusY = bonusY;
                heartEligible = false; // consumed
            }
            else if (canSpawnBreatherBonus && Random.value < C.boostChance * 2f)
            {
                Spawn(boostPool, "boost", bonusY, safeAngle);
                lastBonusY = bonusY;
            }
            else if (canSpawnBreatherBonus && metres >= C.cooldownMinMetres && Random.value < C.cooldownChance * 2f)
            {
                Spawn(cooldownPool, "cooldown", bonusY, safeAngle);
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
            // Bee swarm row: full 360 jump ring hazard. Never spawns coins or pickups directly inside it.
            float beeY = y + gap * 0.4f;
            Spawn(beePool, "bees", beeY, 0);
            lastHazardY = beeY + 0.3f;
            lastWasBee = true;
            OnRow?.Invoke(beeY, safeAngle, 0, 360, gap / C.ClimbSpeedAtDistance(metres), metres);
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

        if (canSpawnBonus && heartEligible)
        {
            Spawn(heartPool, "heart", bonusYPos, newSafe);
            lastBonusY = bonusYPos;
            heartEligible = false; // consumed
        }
        else if (canSpawnBonus && Random.value < C.boostChance)
        {
            Spawn(boostPool, "boost", bonusYPos, newSafe);
            lastBonusY = bonusYPos;
        }
        else if (canSpawnBonus && metres >= C.cooldownMinMetres && Random.value < C.cooldownChance)
        {
            Spawn(cooldownPool, "cooldown", bonusYPos, newSafe);
            lastBonusY = bonusYPos;
        }
        else if (Random.value < C.coinTrailChance)
        {
            // String of uniformly spaced coins (5-10 coins) along the safe climbing trajectory
            // Placed before the hazard row leading smoothly into the safe gap
            int stringCount = Random.Range(5, 11); // 5 to 10 coins
            float trailSpan = (stringCount - 1) * UniformCoinSpacing;
            float trailEndY = y - 0.35f;
            float trailStartY = Mathf.Max(trailEndY - trailSpan, Mathf.Max(prevRowY + 0.35f, lastBonusY + BonusCoinMargin));
            SpawnCoinTrail(trailStartY, trailEndY, Mathf.LerpAngle(safeAngle, newSafe, 0.70f), newSafe, stringCount);
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

    TCPooled Spawn(TCPool pool, string kind, float y, float angle)
    {
        var g = pool.Get(new Vector3(0, y, 0), Quaternion.Euler(0, angle, 0));
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
        for (int i = 0; i < g.transform.childCount; i++) g.transform.GetChild(i).gameObject.SetActive(true);
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
