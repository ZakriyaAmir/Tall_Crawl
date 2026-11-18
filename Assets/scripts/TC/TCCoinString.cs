using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to each coin instance to link it to its parent coin string / sequence.
/// Handles visual scaling, pulsing between 1.3x - 1.4x scale, and true orange color via dedicated orange material.
/// </summary>
public class TCCoinItem : MonoBehaviour
{
    public TCCoinString stringParent;
    public int indexInString = -1;
    public bool isHighlighted = false;
    public bool isSingleBonusLeader = false; // Only true for the dedicated bonus coin entity

    // Cache transforms and renderers
    Transform childTransform;   // Child with collider and tag "points"
    Transform visualTransform;  // Grandchild with mesh
    MeshRenderer meshRenderer;

    static Material sharedNormalMaterial;
    static Material sharedOrangeMaterial;

    const float BaseChildScale = 0.016f;
    const float MinPulseMultiplier = 1.30f; // Pulses between 1.3x
    const float MaxPulseMultiplier = 1.40f; // and 1.4x scale

    void Awake()
    {
        CacheComponents();
    }

    void OnEnable()
    {
        CacheComponents();
    }

    void Update()
    {
        // Smoothly and elegantly pulse between 1.3x and 1.4x scale when highlighted
        if (isHighlighted && childTransform != null)
        {
            float pulseWave = 0.5f + 0.5f * Mathf.Sin(Time.time * 7.5f);
            float currentMultiplier = Mathf.Lerp(MinPulseMultiplier, MaxPulseMultiplier, pulseWave);
            float s = BaseChildScale * currentMultiplier;
            childTransform.localScale = new Vector3(s, s, s);
        }
    }

    void CacheComponents()
    {
        if (childTransform == null && transform.childCount > 0)
        {
            childTransform = transform.GetChild(0);
        }
        if (childTransform != null)
        {
            if (visualTransform == null && childTransform.childCount > 0)
            {
                visualTransform = childTransform.GetChild(0);
            }
            if (meshRenderer == null)
            {
                meshRenderer = visualTransform != null 
                    ? visualTransform.GetComponent<MeshRenderer>() 
                    : childTransform.GetComponent<MeshRenderer>();
            }
        }

        if (sharedNormalMaterial == null && meshRenderer != null && meshRenderer.sharedMaterial != null)
        {
            sharedNormalMaterial = meshRenderer.sharedMaterial;
        }

        if (sharedOrangeMaterial == null)
        {
            sharedOrangeMaterial = Resources.Load<Material>("Materials/images_orange");
            if (sharedOrangeMaterial == null && sharedNormalMaterial != null)
            {
                sharedOrangeMaterial = new Material(sharedNormalMaterial)
                {
                    name = "Coin_Orange_Material"
                };
            }
            if (sharedOrangeMaterial != null)
            {
                Color trueOrange = new Color(1.0f, 0.55f, 0.05f, 1f);
                Color trueOrangeEmission = new Color(1.0f, 0.52f, 0.04f, 1f) * 1.8f;
                if (sharedOrangeMaterial.HasProperty("_Color")) sharedOrangeMaterial.SetColor("_Color", trueOrange);
                if (sharedOrangeMaterial.HasProperty("_BaseColor")) sharedOrangeMaterial.SetColor("_BaseColor", trueOrange);
                if (sharedOrangeMaterial.HasProperty("_EmissionColor"))
                {
                    sharedOrangeMaterial.SetColor("_EmissionColor", trueOrangeEmission);
                    sharedOrangeMaterial.EnableKeyword("_EMISSION");
                }
            }
        }
    }

    /// <summary>
    /// Configures visual presentation for normal or highlighted (pulsing 1.3-1.4x, pure orange colored) state.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        CacheComponents();
        isHighlighted = highlighted;

        if (childTransform != null)
        {
            float s = highlighted ? BaseChildScale * 1.35f : BaseChildScale;
            childTransform.localScale = new Vector3(s, s, s);
        }

        if (meshRenderer != null)
        {
            if (highlighted && sharedOrangeMaterial != null)
            {
                meshRenderer.sharedMaterial = sharedOrangeMaterial;
            }
            else if (sharedNormalMaterial != null)
            {
                meshRenderer.sharedMaterial = sharedNormalMaterial;
            }
        }
    }

    /// <summary>
    /// Resets all visual states when recycled to pool.
    /// </summary>
    public void ResetToNormal()
    {
        stringParent = null;
        indexInString = -1;
        isSingleBonusLeader = false;
        SetHighlighted(false);
    }
}

/// <summary>
/// Controller for a single spawned string/formation of coins (Trail, Spiral, Zigzag, Arc).
/// Optimized: Exactly ONE bonus coin entity exists per string.
/// When the spider collects it, that single bonus coin object jumps forward to the next coin position!
/// Other coins never swap materials or scale up, saving CPU/GPU memory & drawcalls.
/// </summary>
public class TCCoinString
{
    struct CoinSlot
    {
        public Vector3 position;
        public Quaternion rotation;
        public float y;
        public Vector3 childLocalPosition;
    }

    readonly List<CoinSlot> slots = new List<CoinSlot>();
    readonly List<TCPooled> normalCoinNodes = new List<TCPooled>();
    TCCoinItem bonusCoinItem;
    int expectedIndex = 0;
    bool isChainBroken = false;
    readonly int totalCoins;

    public TCCoinString(List<TCPooled> coinNodes)
    {
        if (coinNodes == null || coinNodes.Count == 0) return;
        totalCoins = coinNodes.Count;

        for (int i = 0; i < coinNodes.Count; i++)
        {
            var node = coinNodes[i];
            if (!node) continue;

            Transform child = node.transform.childCount > 0 ? node.transform.GetChild(0) : node.transform;
            slots.Add(new CoinSlot
            {
                position = node.transform.position,
                rotation = node.transform.rotation,
                y = node.y,
                childLocalPosition = child.localPosition
            });

            var item = node.GetComponent<TCCoinItem>();
            if (!item) item = node.gameObject.AddComponent<TCCoinItem>();
            item.stringParent = this;
            item.indexInString = i;

            if (i == 0)
            {
                // Slot 0 is the single dedicated bonus coin entity!
                bonusCoinItem = item;
                bonusCoinItem.isSingleBonusLeader = true;
                bonusCoinItem.SetHighlighted(true);
            }
            else
            {
                // All other coins remain normal (1.0x scale, standard yellow texture)
                item.isSingleBonusLeader = false;
                item.SetHighlighted(false);
                normalCoinNodes.Add(node);
            }
        }
    }

    /// <summary>
    /// Called when any coin belonging to this string is collected.
    /// </summary>
    public void OnCoinCollected(TCCoinItem item, Vector3 pos)
    {
        if (item == null) return;
        int idx = item.indexInString;

        // If the chain was already broken, award normal coin
        if (isChainBroken)
        {
            return;
        }

        // Check if the collected coin is the active bonus coin at expectedIndex
        if (idx == expectedIndex && (item == bonusCoinItem || item.isSingleBonusLeader || idx == 0))
        {
            expectedIndex++;

            if (expectedIndex < slots.Count)
            {
                // Move the bonus coin entity forward to the next slot position!
                // The underlying normal coin at this next slot is retired/hidden so they don't overlap.
                RelocateBonusCoinToSlot(expectedIndex);
            }
            else
            {
                // Full string completed from first coin to last coin!
                AwardStringBonus(pos);
            }
        }
        else
        {
            // Player missed / skipped ahead or collected a normal coin before collecting the bonus coin!
            BreakChain();
        }
    }

    void RelocateBonusCoinToSlot(int slotIndex)
    {
        if (slotIndex >= slots.Count || bonusCoinItem == null) return;

        // 1. If there's an existing normal coin at this slot, safely disable it
        int normalIndex = slotIndex - 1; // normalCoinNodes is offset by 1 because slot 0 is bonus coin
        if (normalIndex >= 0 && normalIndex < normalCoinNodes.Count)
        {
            var existingNormal = normalCoinNodes[normalIndex];
            if (existingNormal != null && existingNormal.gameObject.activeSelf)
            {
                // Hide and release the duplicate normal coin
                existingNormal.gameObject.SetActive(false);
            }
        }

        // 2. Reposition the single bonus coin entity directly to the new slot
        var slot = slots[slotIndex];
        var bonusGo = bonusCoinItem.gameObject;
        bonusGo.transform.SetPositionAndRotation(slot.position, slot.rotation);
        var pooled = bonusCoinItem.GetComponent<TCPooled>();
        if (pooled != null) pooled.y = slot.y;

        Transform child = bonusGo.transform.childCount > 0 ? bonusGo.transform.GetChild(0) : bonusGo.transform;
        child.localPosition = slot.childLocalPosition;
        child.gameObject.SetActive(true);

        bonusCoinItem.indexInString = slotIndex;
        bonusCoinItem.SetHighlighted(true);
        bonusGo.SetActive(true);
    }

    void AwardStringBonus(Vector3 pos)
    {
        // 1.5x total string bonus!
        int bonusCoins = Mathf.Max(1, Mathf.RoundToInt(totalCoins * 0.5f));

        if (gameManager.Instance != null)
        {
            gameManager.Instance.addCoin(bonusCoins);
        }

        // Also track bonus coins in Chilli boost combo if active
        if (playerInteract.Instance != null && playerInteract.Instance.boostDuration > 0f)
        {
            playerInteract.Instance.AddChilliBonusCoins(bonusCoins);
        }

        // Celebratory UI feedback directly below the coins panel
        if (TCGameUI.Instance != null)
        {
            TCGameUI.Instance.ShowStringBonusCalculation(totalCoins, bonusCoins, pos);
        }

        // Distinct golden double-ring coin collection sound for completing the full string run
        TCSfx.Play("coin_bonus", 0.70f);
        TCFx.Shake(0.06f);
    }

    void BreakChain()
    {
        isChainBroken = true;

        // When the chain breaks, disable highlight on the bonus coin
        if (bonusCoinItem != null && bonusCoinItem.gameObject.activeSelf)
        {
            bonusCoinItem.SetHighlighted(false);
        }
    }
}
