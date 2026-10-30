using System.Collections.Generic;
using UnityEngine;

/// Endless, fair track generator with pooling. Replaces the old levelManager/hurdlesSpawner/bonusSpawner.
/// Rows of branches always leave an open angular window; the window follows a "safe path" whose
/// turn rate and row spacing ramp with difficulty. Bee swarms (jump hazards) only on empty rows.
public class TCTrack : MonoBehaviour
{
    public GameObject trunkPrefab, branchPrefab, beesPrefab, coinPrefab, heartPrefab, boostPrefab;
    public Transform stemParent, hazardParent, bonusParent;
    public Transform player;

    TCPool trunkPool, branchPool, beePool, coinPool, heartPool, boostPool;
    readonly List<TCPooled> live = new List<TCPooled>(256);
    readonly List<float> angles = new List<float>(8);
    float nextTrunkY, nextRowY, safeAngle, lastCoinY = -999f;
    float lastHazardY = -999f, lastBonusY = -999f;
    float nextHeartEligibleTime;
    int rowIndex; bool lastWasBee; player pl;
    public static System.Action<float, float, float, float, float, float> OnRow; // y, safe angle, required turn deg, window deg (360 = bees), seconds available, metres
    public int LiveCount => live.Count;
    public TCPool[] Pools => new[] { trunkPool, branchPool, beePool, coinPool, heartPool, boostPool };
    TCConfig C;

    const float MinCoinSpacing = 0.22f; // tighter spacing for dense long coin strings
    const float MinHazardSpacing = 1.0f;
    const float MinBonusSpacing = 1.5f;
    const float BonusCoinMargin = 0.8f; // absolute clearance between any bonus pickup and any coin

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

        float py = player.position.y;
        float chunkH = C.trunkChunkHeight;
        nextTrunkY = Mathf.Floor((py - 2f * chunkH) / chunkH) * chunkH;
        nextRowY = py + C.safeStartHeight;
        safeAngle = TCPlayerAngle();
        lastCoinY = -999f;
        lastHazardY = -999f;
        lastBonusY = -999f;
        nextHeartEligibleTime = Time.time + 10f; // heart check every 10 seconds

        // Safe start: welcome string of coins straight up
        SpawnCoinTrail(py + 1.2f, py + 1.2f + 6 * MinCoinSpacing, safeAngle, safeAngle, 7);
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
        float py = player.position.y;
        float metres = pl ? pl.currentScore : 0f; // same distance the HUD/difficulty uses
        while (nextTrunkY < py + C.chunksAhead * C.trunkChunkHeight)
        {
            Spawn(trunkPool, "trunk", nextTrunkY, 0);
            nextTrunkY += C.trunkChunkHeight;
        }
        while (nextRowY < py + 12f) GenerateRow(metres + (nextRowY - py) * C.metresPerUnit);

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
            else
            {
                // Coin spiral that guarantees no overlap with hazards or powerups
                float spiralStartY = Mathf.Max(y + 0.3f, Mathf.Max(lastCoinY + MinCoinSpacing, lastBonusY + BonusCoinMargin));
                float spiralEndY = y + gap - 0.3f;
                SpawnCoinTrail(spiralStartY, spiralEndY, from, safeAngle);
            }

            nextRowY = y + gap * 1.3f;
            return;
        }

        if (!lastWasBee && metres > C.beeMinMetres && Random.value < C.beeChance * d + 0.02f)
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

                // Apply flower/foliage variant:
                // 4 variants: None (0), Red (1), Yellow (2), Pink (3)
                // Each branch row or individual branch gets its own variant
                var bv = b.GetComponent<TCBranchVariant>();
                if (!bv) bv = b.gameObject.AddComponent<TCBranchVariant>();
                var flowerType = (TCBranchVariant.FlowerType)Random.Range(0, 4);
                bv.SetVariant(flowerType);
            }
        }
        float prevRowY = lastHazardY;
        if (spawnBranches) lastHazardY = y;
        OnRow?.Invoke(y, newSafe, Mathf.Abs(Mathf.DeltaAngle(safeAngle, newSafe)), window, secs, metres);

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
        else if (Random.value < C.coinTrailChance)
        {
            // Long shape string of coins (5-10 coins) along the safe climbing trajectory
            // Placed before the hazard row leading smoothly into the safe gap
            int stringCount = Random.Range(5, 11); // 5 to 10 coins
            float trailSpan = (stringCount - 1) * MinCoinSpacing;
            float trailEndY = y - 0.35f;
            float trailStartY = Mathf.Max(trailEndY - trailSpan, Mathf.Max(prevRowY + 0.35f, lastBonusY + BonusCoinMargin));
            // Recalculate actual count if space between rows was slightly constrained
            int actualCount = Mathf.Clamp(Mathf.RoundToInt((trailEndY - trailStartY) / MinCoinSpacing) + 1, 5, stringCount);
            SpawnCoinTrail(trailStartY, trailEndY, Mathf.LerpAngle(safeAngle, newSafe, 0.70f), newSafe, actualCount);
        }

        safeAngle = newSafe;
        nextRowY = y + gap;
    }

    void SpawnCoinTrail(float startY, float endY, float fromAngle, float toAngle, int preferredCount = 0)
    {
        float minY = Mathf.Max(startY, Mathf.Max(lastCoinY + MinCoinSpacing, lastBonusY + BonusCoinMargin));
        float span = endY - minY;
        if (span < MinCoinSpacing * 0.7f) return;

        int count = preferredCount > 0 ? preferredCount : Mathf.Max(1, Mathf.RoundToInt(span / MinCoinSpacing));
        float step = count > 1 ? span / (count - 1) : span;

        for (int i = 0; i < count; i++)
        {
            float cy = count > 1 ? minY + i * step : minY + 0.5f * span;
            float t = (count <= 1) ? 0.5f : (float)i / (count - 1);
            float ca = Mathf.LerpAngle(fromAngle, toAngle, t);
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
        // re-enable children that were hidden on pickup
        for (int i = 0; i < g.transform.childCount; i++) g.transform.GetChild(i).gameObject.SetActive(true);
        live.Add(p);
        return p;
    }
}
