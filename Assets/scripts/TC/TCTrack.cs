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
    int rowIndex;
    TCConfig C;

    const float MinCoinSpacing = 0.55f;

    void Start()
    {
        C = TCConfig.I;
        trunkPool = new TCPool(trunkPrefab, stemParent, C.chunksAhead + 4);
        branchPool = new TCPool(branchPrefab, hazardParent, 24);
        beePool = new TCPool(beesPrefab, hazardParent, 3);
        coinPool = new TCPool(coinPrefab, bonusParent, 48);
        heartPool = new TCPool(heartPrefab, bonusParent, 2);
        boostPool = new TCPool(boostPrefab, bonusParent, 2);

        float py = player.position.y;
        float chunkH = C.trunkChunkHeight;
        nextTrunkY = Mathf.Floor((py - 2f * chunkH) / chunkH) * chunkH;
        nextRowY = py + C.safeStartHeight;
        safeAngle = TCPlayerAngle();
        lastCoinY = -999f;

        // Safe start: welcome coin trail straight up with zero overlap
        SpawnCoinTrail(py + 1.2f, py + 4.2f, safeAngle, safeAngle);
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
        float metres = Mathf.Max(0, py * C.metresPerUnit);
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
            if (!p || !p.gameObject.activeSelf) { live.RemoveAt(i); continue; }
            if (p.y < cut) { p.Release(); live.RemoveAt(i); }
        }
    }

    void GenerateRow(float metres)
    {
        float d = C.Difficulty(metres);
        float y = nextRowY;
        rowIndex++;
        float gap = Mathf.Lerp(C.rowGapEasy, C.rowGapHard, d) * Random.Range(0.9f, 1.15f);
        bool breather = C.breatherEvery > 0 && rowIndex % C.breatherEvery == 0;

        if (breather)
        {
            // Breather: coin spiral and maybe a power-up with guaranteed spacing
            float spiralStartY = y + 0.2f;
            float spiralEndY = y + gap * 1.1f;
            SpawnCoinTrail(spiralStartY, spiralEndY, safeAngle, safeAngle + 90f);
            safeAngle += 90f;
            if (Random.value < C.heartChance * 4) Spawn(heartPool, "heart", y + gap * 0.9f, safeAngle);
            nextRowY += gap * 1.3f;
            return;
        }

        if (metres > C.beeMinMetres && Random.value < C.beeChance * d + 0.04f)
        {
            // Bee swarm row: no branches, needs a jump. Extra spacing before & after.
            Spawn(beePool, "bees", y + gap * 0.3f, 0);
            nextRowY += gap * 1.6f;
            return;
        }

        float turn = Mathf.Lerp(C.maxTurnEasy, C.maxTurnHard, d);
        float newSafe = safeAngle + Random.Range(-turn, turn);
        float window = Mathf.Lerp(C.openWindowEasy, C.openWindowHard, d);

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
        }

        // Coins along the safe path between this row and the previous one
        if (Random.value < C.coinTrailChance)
        {
            float trailStartY = y - gap + 0.35f;
            float trailEndY = y - 0.25f;
            SpawnCoinTrail(trailStartY, trailEndY, safeAngle, newSafe);
        }
        else if (Random.value < C.heartChance) Spawn(heartPool, "heart", y + gap * 0.5f, newSafe);
        else if (Random.value < C.boostChance) Spawn(boostPool, "boost", y + gap * 0.5f, newSafe);

        safeAngle = newSafe;
        nextRowY += gap;
    }

    void SpawnCoinTrail(float startY, float endY, float fromAngle, float toAngle)
    {
        float minY = Mathf.Max(startY, lastCoinY + MinCoinSpacing);
        float span = endY - minY;
        if (span < MinCoinSpacing * 0.7f) return;

        int count = Mathf.Max(1, Mathf.RoundToInt(span / MinCoinSpacing));
        float step = span / count;

        for (int i = 0; i < count; i++)
        {
            float cy = minY + (i + 0.5f) * step;
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
