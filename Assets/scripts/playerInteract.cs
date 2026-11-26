using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Spider collisions, hearts, boost, pickups and feedback.
public class playerInteract : MonoBehaviour
{
    public int life;
    public GameObject GameOverScreen;          // legacy (unused)
    public GameObject impactParticle;
    public GameObject BeesimpactParticle;
    public Image lifebar;                      // legacy (unused)
    public float boostDuration;

    public AudioClip woodImpactSound, beesImpactSound, deathSound, boostSound, coinSound, healthSound, jumpSound;

    public int MaxLife { get; private set; }
    public bool IsDead => initialized && life <= 0;
    public bool Invulnerable => invulnTimer > 0 || boostDuration > 0 || IsRolling;
    public bool IsJumping => jumpTimer > 0;
    public bool IsRolling => boostDuration > 0 || (p != null && p.boost);
    public static System.Action<string, Vector3> OnEvent; // "coin","hit","heart","boost","smash","death","cooldown"
    public static int CooldownInventory = 0; // Stored slow-mo cooldown power ups

    // Dual collider specification:
    // 1. Hit collider: detects obstacle/enemy hits, reduced by 40% (scale 0.60)
    // 2. Points collider: detects coin/powerup pickups, increased by 2x (scale 2.00)
    public const float BASE_COLLIDER_RADIUS = 0.4f;
    public const float BASE_COLLIDER_HEIGHT = 0.8f;
    public static readonly Vector3 BASE_COLLIDER_CENTER = new Vector3(0f, 0.39414242f, -0.25999996f);
    public const float HIT_COLLIDER_SCALE = 0.60f;     // 40% reduction -> 60% of original
    public const float POINTS_COLLIDER_SCALE = 2.00f;  // 2x increase

    public CapsuleCollider HitCollider { get; private set; }
    public CapsuleCollider PointsCollider { get; private set; }

    AudioSource audioSource; player p; Animator animator; Renderer[] renderers;
    float invulnTimer, jumpTimer, rollTimer, lastChilliHapticTime; bool blinking, initialized;
    int coinStreak; float lastCoinCollectTime;
    Vector3 lastTrackPosition; bool hasLastTrackPosition;
    TCCuteSpider cachedCuteSpider;

    public static playerInteract Instance { get; private set; }
    public bool IsChilliActive => boostDuration > 0f;
    public int ChilliComboCount { get; private set; }
    public int ChilliCoinsEarned { get; private set; }
    public float ChilliComboMultiplier => 1.0f + ChilliComboCount * 0.1f;

    public void OnCoinCollected()
    {
        if (boostDuration > 0f)
        {
            ChilliCoinsEarned++;
        }
    }

    public void AddChilliBonusCoins(int amount)
    {
        if (boostDuration > 0f && amount > 0)
        {
            ChilliCoinsEarned += amount;
        }
    }

    public void OnEnemySmashedInChilli()
    {
        if (boostDuration <= 0f) return;
        ChilliComboCount++;
        ChilliCoinsEarned += 5; // The 5 bonus coins from this enemy kill
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsAirborneClear
    {
        get
        {
            if (cachedCuteSpider == null) cachedCuteSpider = GetComponentInChildren<TCCuteSpider>();
            // If cute spider is jumping and has reached sufficient vertical clearance above trunk
            if (cachedCuteSpider != null && cachedCuteSpider.CurrentAirHeight >= 0.20f) return true;
            return false;
        }
    }

    void Awake()
    {
        Instance = this;
        p = GetComponentInParent<player>();
        SetupColliders();
    }

    public void SetupColliders()
    {
        // 1. Configure Hit Collider on this GameObject (reduced by 40%)
        HitCollider = GetComponent<CapsuleCollider>();
        if (!HitCollider) HitCollider = gameObject.AddComponent<CapsuleCollider>();
        HitCollider.isTrigger = true;
        HitCollider.direction = 1;
        HitCollider.center = BASE_COLLIDER_CENTER;
        HitCollider.radius = BASE_COLLIDER_RADIUS * HIT_COLLIDER_SCALE;   // 0.24f
        HitCollider.height = BASE_COLLIDER_HEIGHT * HIT_COLLIDER_SCALE;   // 0.48f

        // 2. Configure 2x Points/Pickup Collider on child GameObject
        Transform child = transform.Find("PickupCollector");
        GameObject pickupObj;
        if (!child)
        {
            pickupObj = new GameObject("PickupCollector");
            pickupObj.transform.SetParent(transform, false);
            pickupObj.transform.localPosition = Vector3.zero;
            pickupObj.transform.localRotation = Quaternion.identity;
            pickupObj.transform.localScale = Vector3.one;
        }
        else
        {
            pickupObj = child.gameObject;
        }

        var rb = pickupObj.GetComponent<Rigidbody>();
        if (!rb) rb = pickupObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        PointsCollider = pickupObj.GetComponent<CapsuleCollider>();
        if (!PointsCollider) PointsCollider = pickupObj.AddComponent<CapsuleCollider>();
        PointsCollider.isTrigger = true;
        PointsCollider.direction = 1;
        PointsCollider.center = BASE_COLLIDER_CENTER;
        PointsCollider.radius = BASE_COLLIDER_RADIUS * POINTS_COLLIDER_SCALE; // 0.80f
        PointsCollider.height = BASE_COLLIDER_HEIGHT * POINTS_COLLIDER_SCALE; // 1.60f

        var collector = pickupObj.GetComponent<TCPickupCollector>();
        if (!collector) collector = pickupObj.AddComponent<TCPickupCollector>();
        collector.Init(this);
    }

    void Start()
    {
        if (!p) p = GetComponentInParent<player>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        renderers = GetComponentsInChildren<Renderer>(true);
        if (GameOverScreen) GameOverScreen.SetActive(false);
        MaxLife = TCConfig.I.baseHearts + Mathf.Min(2, TCSave.Upgrade(2));
        life = MaxLife;
        initialized = true;
        CooldownInventory = 0;
        TCSkins.Apply(gameObject, TCSave.SelectedSkin);
        if (!GetComponentInChildren<TCFireEffects>()) gameObject.AddComponent<TCFireEffects>();
        if (!FindObjectOfType<TCSpeedLines>()) gameObject.AddComponent<TCSpeedLines>();
        renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.enabled && !(r is ParticleSystemRenderer) && !IsFx(r.transform)); // skip the hidden realistic mesh and the VFX
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (jumpTimer > 0) jumpTimer -= dt;
        if (rollTimer > 0) rollTimer -= dt;
        if (invulnTimer > 0)
        {
            invulnTimer -= dt;
            bool vis = invulnTimer <= 0 || Mathf.Repeat(invulnTimer, 0.2f) > 0.1f;
            SetVisible(vis);
        }
        if (boostDuration > 0)
        {
            p.boost = true;
            boostDuration -= dt;
            if (boostDuration <= 0)
            {
                OnBoostEnded();
            }
            else
            {
                // Active forward sweep detection during chilli mode:
                // Prevents tunneling through moving or static enemies when climbing at 2.2x speed
                PerformChilliProximitySmash();
            }
        }

        // Continuous swept trigger check: prevents discrete tunneling through bee swarms at high speed
        PerformContinuousSweep();
    }

    readonly Collider[] chilliOverlapBuffer = new Collider[16];
    readonly Collider[] sweepOverlapBuffer = new Collider[16];

    void PerformContinuousSweep()
    {
        if (IsDead || p == null || p.GameOver)
        {
            hasLastTrackPosition = false;
            return;
        }

        Vector3 curPos = transform.position;
        if (!hasLastTrackPosition)
        {
            lastTrackPosition = curPos;
            hasLastTrackPosition = true;
            return;
        }

        float deltaDist = Vector3.Distance(curPos, lastTrackPosition);
        // Only sweep if the spider moved a significant distance this frame (e.g. at least 0.05m)
        if (deltaDist >= 0.05f)
        {
            // Sample test spheres along the movement segment from last frame to current frame
            int steps = Mathf.Clamp(Mathf.CeilToInt(deltaDist / 0.15f), 1, 4);
            bool sweptHit = false;
            for (int s = 1; s <= steps && !sweptHit; s++)
            {
                float tStep = (float)s / steps;
                Vector3 samplePos = Vector3.Lerp(lastTrackPosition, curPos, tStep);
                Vector3 center = samplePos + transform.TransformDirection(BASE_COLLIDER_CENTER);

                int hitCount = Physics.OverlapSphereNonAlloc(center, 0.40f, sweepOverlapBuffer, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < hitCount; i++)
                {
                    var col = sweepOverlapBuffer[i];
                    if (!col || !col.enabled || !col.gameObject.activeInHierarchy || col.transform.IsChildOf(transform)) continue;

                    if (col.CompareTag("bees"))
                    {
                        // In normal mode: if spider is airborne, safely hop over
                        if (!IsAirborneClear || boostDuration > 0)
                        {
                            sweptHit = true;
                            ProcessHazardOrEnemyTrigger(col);
                            break;
                        }
                    }
                }
            }
        }

        lastTrackPosition = curPos;
    }

    void PerformChilliProximitySmash()
    {
        Vector3 center = transform.TransformPoint(BASE_COLLIDER_CENTER) + transform.forward * 0.10f;
        int hitCount = Physics.OverlapSphereNonAlloc(center, 0.50f, chilliOverlapBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            var col = chilliOverlapBuffer[i];
            if (!col || !col.enabled || !col.gameObject.activeInHierarchy || col.transform.IsChildOf(transform)) continue;

            var enemy = col.GetComponentInParent<TCEnemy>();
            if (enemy != null)
            {
                SmashEnemy(enemy, col.transform.position);
                continue;
            }

            var pooled = col.GetComponentInParent<TCPooled>();
            if (col.CompareTag("branch"))
            {
                col.enabled = false;
                Vector3 hitPoint = col.ClosestPoint(transform.position);
                if (hitPoint == Vector3.zero || (hitPoint - transform.position).sqrMagnitude > 4f)
                    hitPoint = col.bounds.ClosestPoint(transform.position);
                TCFx.SpawnWoodImpact(hitPoint, impactParticle);
                if (pooled) pooled.Release(); else (col.transform.parent ? col.transform.parent.gameObject : col.gameObject).SetActive(false);
                Play(TCSfx.Get("thrust") ?? boostSound, 0.9f);
                OnEvent?.Invoke("smash", transform.position);
                TCFx.Shake(0.1f);
            }
            else if (col.CompareTag("bees"))
            {
                col.enabled = false;
                GameObject beeObj = pooled != null ? pooled.gameObject : (col.transform.parent ? col.transform.parent.gameObject : col.gameObject);
                foreach (var c in beeObj.GetComponentsInChildren<Collider>()) c.enabled = false;
                TCFx.ScatterBeeSwarm(beeObj, transform.position, 1.0f);
                Play(TCSfx.Get("bees_kill") ?? TCSfx.Get("thrust") ?? boostSound, 1f);
                if (pooled) pooled.Release(); else beeObj.SetActive(false);
                TCFx.Shake(0.18f);
                TCSave.AddBeeKnock(1);
                TCSkins.CheckAchievements();
                OnEnemySmashedInChilli();
                OnEvent?.Invoke("chilli_kill", transform.position);
            }
        }
    }

    bool IsFx(Transform t) { for (; t && t != transform; t = t.parent) if (t.name == "Chilli_Fire_Effects") return true; return false; }

    void SetVisible(bool v) { foreach (var r in renderers) if (r && !(r is TrailRenderer)) r.enabled = v; }

    void Play(AudioClip c, float vol = 1f) { if (c && audioSource) audioSource.PlayOneShot(c, vol); }

    void PlayCoinCollect()
    {
        float now = Time.unscaledTime;
        if (now - lastCoinCollectTime > 0.55f) coinStreak = 0;
        lastCoinCollectTime = now;
        float pitch = 1.0f + Mathf.Min(coinStreak, 7) * 0.045f;
        coinStreak++;

        var clip = TCSfx.Get("coin") ?? coinSound;
        if (clip != null)
        {
            TCAudio.Play(clip, 0.38f, pitch);
        }
    }

    public bool HandlePickup(Collider other)
    {
        if (IsDead || (p != null && p.GameOver)) return false;
        if (!other || !other.gameObject.activeSelf) return false;

        var fxKind = TCPickupFX.KindFromTag(other.tag);
        // No coin-burst when the magnet is picked up — the magnet aura is the collect FX.
        if (fxKind.HasValue && fxKind.Value != TCPickupKind.Magnet)
            TCPickupFX.Burst(fxKind.Value, other.bounds.center);

        if (other.CompareTag("points"))
        {
            Vector3 coinPos = other.transform.position;
            var coinItem = other.GetComponentInParent<TCCoinItem>();
            other.gameObject.SetActive(false);
            PlayCoinCollect();
            OnCoinCollected();
            gameManager.Instance?.addCoin();
            if (coinItem != null && coinItem.stringParent != null)
            {
                coinItem.stringParent.OnCoinCollected(coinItem, coinPos);
            }
            OnEvent?.Invoke("coin", coinPos);
            return true;
        }
        else if (other.CompareTag("health"))
        {
            other.gameObject.SetActive(false);
            Play(TCSfx.Get("heart") ?? healthSound, 0.8f);
            if (life < MaxLife) life++;
            TCTrack.OnHeartCollected();
            OnEvent?.Invoke("heart", transform.position);
            return true;
        }
        else if (other.CompareTag("boost"))
        {
            other.gameObject.SetActive(false);
            Play(TCSfx.Get("chilli"), 0.8f);
            StartBoost(6f + 1.2f * Mathf.Clamp(TCSave.Upgrade(1), 0, 5));
            TCFx.Shake(0.12f);
            TCTrack.OnChilliCollected();
            return true;
        }
        else if (other.CompareTag("cooldown"))
        {
            other.gameObject.SetActive(false);
            Play(TCSfx.Get("cooldown_collect") ?? TCSfx.Get("coin"), 0.85f);
            CooldownInventory++;
            OnEvent?.Invoke("cooldown", transform.position);
            TCFx.Shake(0.08f);
            return true;
        }
        else if (other.CompareTag("magnet"))
        {
            other.gameObject.SetActive(false);
            if (TCCoinMagnet.Instance != null)
                TCCoinMagnet.Instance.Activate();
            else
                TCSfx.Play("magnet_activate", 0.85f);
            TCServices.Log("magnet_activated", "metres", p != null ? (long)p.currentScore : 0L);
            OnEvent?.Invoke("magnet", transform.position);
            TCFx.Shake(0.08f);
            return true;
        }

        var letter = other.GetComponentInParent<TCWebLetter>() ?? other.GetComponent<TCWebLetter>();
        if (letter)
        {
            letter.Collect(other.transform.position);
            return true;
        }

        return false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsDead || p == null || p.GameOver) return;
        if (!other) return;

        // Collect coins/powerups if they overlap the main hit collider
        if (HandlePickup(other)) return;
        if (TCWebBonus.Instance && TCWebBonus.Instance.IsInvincible) return;

        ProcessHazardOrEnemyTrigger(other);
    }

    public void ProcessHazardOrEnemyTrigger(Collider other)
    {
        if (IsDead || p == null || p.GameOver || !other) return;

        // In chilli mode: play a very little vibration when colliding with any object/obstacle
        if (boostDuration > 0 && Time.unscaledTime - lastChilliHapticTime > 0.04f)
        {
            lastChilliHapticTime = Time.unscaledTime;
            TCServices.LightHaptic();
        }

        var pooled = other.GetComponentInParent<TCPooled>();
        var enemy = other.GetComponentInParent<TCEnemy>();

        // Sonic Roll Attack: smashes through branches, bees, and enemies!
        if (IsRolling)
        {
            if (other.CompareTag("branch"))
            {
                // Disappear instantly upon collision with wood particles spawning at the collision point
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                if (hitPoint == Vector3.zero || (hitPoint - transform.position).sqrMagnitude > 4f)
                    hitPoint = other.bounds.ClosestPoint(transform.position);
                TCFx.SpawnWoodImpact(hitPoint, impactParticle);
                if (pooled) pooled.Release(); else (other.transform.parent ? other.transform.parent.gameObject : other.gameObject).SetActive(false);
                Play(TCSfx.Get("thrust") ?? boostSound, 1f);
                OnEvent?.Invoke("smash", transform.position);
                TCFx.Shake(0.18f);
                gameManager.Instance?.addCoin();
                return;
            }
            if (other.CompareTag("bees"))
            {
                other.enabled = false;
                GameObject beeObj = pooled != null ? pooled.gameObject : (other.transform.parent ? other.transform.parent.gameObject : other.gameObject);
                foreach (var c in beeObj.GetComponentsInChildren<Collider>()) c.enabled = false;

                if (boostDuration > 0)
                {
                    TCFx.ScatterBeeSwarm(beeObj, transform.position, 1.0f);
                    Play(TCSfx.Get("bees_kill") ?? TCSfx.Get("thrust") ?? boostSound, 1f);
                }
                else
                {
                    TCFx.Spawn(BeesimpactParticle, transform.position);
                    Play(TCSfx.Get("thrust") ?? boostSound, 1f);
                }
                if (pooled) pooled.Release(); else beeObj.SetActive(false);
                TCFx.Shake(0.18f);
                TCSave.AddBeeKnock(1);
                TCSkins.CheckAchievements();
                if (boostDuration > 0)
                {
                    OnEnemySmashedInChilli();
                    OnEvent?.Invoke("chilli_kill", transform.position);
                }
                else
                {
                    OnEvent?.Invoke("smash", transform.position);
                    gameManager.Instance?.addCoin();
                }
                return;
            }
            if (enemy != null)
            {
                SmashEnemy(enemy, other.transform.position);
                return;
            }
        }

        // Enemy collision handling (when not in roll attack mode)
        if (enemy != null)
        {
            if (boostDuration > 0)
            {
                SmashEnemy(enemy, other.transform.position);
                return;
            }
            if (IsAirborneClear && enemy.CanJumpOver) return; // jumped over crawling enemy
            if (invulnTimer > 0) return;

            if (enemy.enemyType == TCEnemyType.Wasp)
            {
                var waspComp = enemy as TCWasp;
                bool isBat = waspComp != null ? waspComp.isBat : (gameManager.Instance != null && gameManager.Instance.IsBatPhase);
                Play(isBat ? (TCSfx.Get("bat") ?? woodImpactSound) : (TCSfx.Get("wasp") ?? woodImpactSound), 0.95f);
            }
            else if (enemy.enemyType == TCEnemyType.Snake)
            {
                // angry strike hiss layered over the usual damage thud
                Play(TCSfx.Get("hit") ?? woodImpactSound, 0.8f);
                Play(TCSfx.Get("snake_hit") ?? woodImpactSound, 0.9f);
            }
            else
            {
                Play(TCSfx.Get("hit") ?? woodImpactSound, 0.85f);
            }
            TCFx.Spawn(impactParticle, transform.position);
            Damage();
            return;
        }

        if (other.CompareTag("branch"))
        {
            // Disappear instantly upon collision with wood particles spawning at the collision point
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            if (hitPoint == Vector3.zero || (hitPoint - transform.position).sqrMagnitude > 4f)
                hitPoint = other.bounds.ClosestPoint(transform.position);
            TCFx.SpawnWoodImpact(hitPoint, impactParticle);
            if (pooled) pooled.Release(); else (other.transform.parent ? other.transform.parent.gameObject : other.gameObject).SetActive(false);
            if (boostDuration > 0)
            {
                // In chilli mode: smash through with the boost thrust sound
                Play(TCSfx.Get("thrust") ?? boostSound, 0.9f);
                OnEvent?.Invoke("smash", transform.position);
                TCFx.Shake(0.1f);
            }
            else if (invulnTimer > 0)
            {
                Play(TCSfx.Get("thrust") ?? woodImpactSound, 0.7f);
                OnEvent?.Invoke("smash", transform.position);
                TCFx.Shake(0.08f);
            }
            else
            {
                // Spider collides with branch: play original chilli smash thrust sound together with impact
                Play(TCSfx.Get("thrust") ?? boostSound, 0.85f);
                Play(TCSfx.Get("hit") ?? woodImpactSound, 0.8f);
                Damage();
            }
        }
        else if (other.CompareTag("bees"))
        {
            if (IsAirborneClear) return; // jumped over with sufficient air height
            other.enabled = false;
            GameObject beeObj = pooled != null ? pooled.gameObject : (other.transform.parent ? other.transform.parent.gameObject : other.gameObject);
            foreach (var c in beeObj.GetComponentsInChildren<Collider>()) c.enabled = false;

            if (boostDuration > 0)
            {
                // In chilli mode: active bees fly away scattering off the tree trunk with bee swarm kill sound
                TCFx.ScatterBeeSwarm(beeObj, transform.position, 1.0f);
                Play(TCSfx.Get("bees_kill") ?? TCSfx.Get("thrust") ?? boostSound, 1f);
                if (pooled) pooled.Release(); else beeObj.SetActive(false);
                TCSave.AddBeeKnock(1);
                TCSkins.CheckAchievements();
                OnEnemySmashedInChilli();
                OnEvent?.Invoke("chilli_kill", transform.position);
                TCFx.Shake(0.1f);
                return;
            }
            if (invulnTimer > 0) return; // post-hit invulnerability

            TCFx.Spawn(BeesimpactParticle, transform.position);
            Play(TCSfx.Get("bees") ?? beesImpactSound, 0.8f);
            if (pooled) pooled.Release(); else beeObj.SetActive(false);
            Damage();
        }
    }

    public void SmashEnemy(TCEnemy enemy, Vector3 hitWorldPos)
    {
        if (enemy == null) return;
        enemy.Smash(transform.position);

        if (boostDuration > 0)
        {
            if (enemy.enemyType == TCEnemyType.Wasp)
            {
                var waspComp = enemy as TCWasp;
                bool isBat = waspComp != null ? waspComp.isBat : (gameManager.Instance != null && gameManager.Instance.IsBatPhase);
                Play(isBat ? (TCSfx.Get("bat_kill") ?? TCSfx.Get("thrust") ?? boostSound)
                           : (TCSfx.Get("wasp_kill") ?? TCSfx.Get("thrust") ?? boostSound), 1f);
            }
            else if (enemy.enemyType == TCEnemyType.Squirrel) Play(TCSfx.Get("squirrel_kill") ?? TCSfx.Get("thrust") ?? boostSound, 1f);
            else if (enemy.enemyType == TCEnemyType.Snake) Play(TCSfx.Get("snake_kill") ?? TCSfx.Get("thrust") ?? boostSound, 1f);
            else Play(TCSfx.Get("thrust") ?? boostSound, 1f);
        }
        else
        {
            Play(TCSfx.Get("thrust") ?? boostSound, 1f);
        }

        TCFx.Spawn(impactParticle, hitWorldPos);
        TCFx.Shake(boostDuration > 0 ? 0.2f : 0.12f);

        if (enemy.enemyType == TCEnemyType.Wasp)
        {
            var waspComp = enemy as TCWasp;
            bool isBat = waspComp != null ? waspComp.isBat : (gameManager.Instance != null && gameManager.Instance.IsBatPhase);
            if (isBat) TCSave.AddBatKnock(1);
            else TCSave.AddWaspKnock(1);
        }
        else if (enemy.enemyType == TCEnemyType.Squirrel) TCSave.AddSquirrelKnock(1);
        else if (enemy.enemyType == TCEnemyType.Snake) TCSave.AddSnakeKnock(1);
        TCSkins.CheckAchievements();

        if (boostDuration > 0)
        {
            OnEnemySmashedInChilli();
            OnEvent?.Invoke("chilli_kill", hitWorldPos);
        }
        else
        {
            OnEvent?.Invoke("smash", transform.position);
            gameManager.Instance?.addCoin();
        }
    }

    void Damage()
    {
        if (TCGameUI.Instance != null ? TCGameUI.Instance.IsTutorialRun : !TCSave.TutorialDone)
        {
            invulnTimer = 0.8f;
            return;
        }
        if (TCWebBonus.Instance && TCWebBonus.Instance.IsInvincible)
        {
            invulnTimer = 0.4f;
            return;
        }
        // A hit during the coin magnet breaks the magnet and costs no health.
        if (TCCoinMagnet.Instance && TCCoinMagnet.Instance.IsActive)
        {
            TCCoinMagnet.Instance.Break();
            invulnTimer = 0.8f;
            TCServices.Haptic();
            TCFx.Shake(0.18f);
            OnEvent?.Invoke("magnet_break", transform.position);
            return;
        }
        TCServices.Haptic();
        TCFx.Shake(0.25f);
        life--;
        OnEvent?.Invoke("hit", transform.position);
        if (life > 0) invulnTimer = 1.4f; else death();
    }

    public void GrantGrace(float seconds)
    {
        invulnTimer = Mathf.Max(invulnTimer, seconds);
    }

    public void increaseHealth() { life = Mathf.Min(MaxLife, life + 1); }

    public void Revive()
    {
        life = 1; // revive (ad or gems) gives exactly one life
        if (animator)
        {
            animator.SetBool("death", false);
            animator.ResetTrigger("death");
            animator.SetTrigger("respawn");
            animator.Play("SpiderRun", 0, 0f);
            animator.Update(0f);
        }
        var spiderModel = transform.Find("Spider");
        if (spiderModel)
        {
            spiderModel.localPosition = Vector3.zero;
            spiderModel.localRotation = Quaternion.identity;
        }
        if (cachedCuteSpider == null) cachedCuteSpider = GetComponentInChildren<TCCuteSpider>();
        if (cachedCuteSpider != null) cachedCuteSpider.OnRevive();
        invulnTimer = 2.5f;
        SetVisible(true);
        Play(TCSfx.Get("revive"), 0.8f);
    }

    public IEnumerator Invincible() { invulnTimer = 1.2f; yield break; }

    public void death()
    {
        initialized = true;
        life = 0;
        jumpTimer = 0f;
        if (animator)
        {
            animator.ResetTrigger("jump");
            animator.SetTrigger("death");
            animator.SetBool("death", true);
        }
        Play(TCSfx.Get("death") ?? deathSound, 0.85f);
        if (!p) p = GetComponentInParent<player>();
        if (p)
        {
            p.GameOver = true;
            p.boost = false;
            p.ResetInputState();
        }
        boostDuration = 0;
        SetVisible(true);
        TCServices.Log("player_death", "metres", p != null ? (long)p.currentScore : 0L);
        OnEvent?.Invoke("death", transform.position);
        gameManager.Instance?.OnPlayerDied();
    }

    public void StartBoost(float secs)
    {
        if (IsDead || (p != null && p.GameOver)) return;
        if (TCWebBonus.Instance && TCWebBonus.Instance.IsSwinging) return;
        if (boostDuration <= 0)
        {
            TCPickupFX.Burst(TCPickupKind.BoostStart, transform.position + transform.up * 0.2f);
            ChilliComboCount = 0;
            ChilliCoinsEarned = 0;
            TCGameUI.Instance?.OnChilliBoostStarted();
            TCServices.Log("chilli_boost_start", "metres", p != null ? (long)p.currentScore : 0L);
        }
        boostDuration = Mathf.Max(boostDuration, secs);
        OnEvent?.Invoke("boost", transform.position);
    }

    void OnBoostEnded()
    {
        boostDuration = 0f;
        if (p) p.boost = false;
        invulnTimer = 1.0f;
        TriggerChilliComboPayout();
    }

    void TriggerChilliComboPayout()
    {
        int combo = ChilliComboCount;
        int coins = ChilliCoinsEarned;
        ChilliComboCount = 0;
        ChilliCoinsEarned = 0;

        if (TCGameUI.Instance != null)
        {
            TCGameUI.Instance.ProcessChilliComboEnd(combo, coins);
        }
    }

    /// <summary>Ends active chilli boost cleanly without triggering post-boost invulnerability blink.</summary>
    public void EndBoost()
    {
        if (boostDuration > 0f)
        {
            boostDuration = 0f;
            if (!p) p = GetComponentInParent<player>();
            if (p != null) p.boost = false;
            TriggerChilliComboPayout();
        }
    }

    public void jump(float duration = 0.6f)
    {
        if (IsDead || (p != null && p.IsInputBlocked)) return;
        jumpTimer = duration;
        Play(TCSfx.Get("jump") ?? jumpSound, 0.7f);
    }

    public void roll(float duration = 1.2f)
    {
        // Rolling is now automatic during chilli pepper boost mode.
    }

    public void doubleJump(float duration = 0.85f)
    {
        if (IsDead || (p != null && p.IsInputBlocked)) return;
        jumpTimer = duration;
        Play(TCSfx.Get("menu_flip") ?? TCSfx.Get("jump") ?? jumpSound, 0.85f);
        var cute = GetComponentInChildren<TCCuteSpider>();
        if (cute) cute.TriggerBackflip(duration * (0.75f / 0.85f));
    }

    void OnDrawGizmosSelected()
    {
        // Draw hit collider (Orange/Red) - 40% smaller (r=0.24, h=0.48)
        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.85f);
        Vector3 c = transform.TransformPoint(BASE_COLLIDER_CENTER);
        Gizmos.DrawWireSphere(c, BASE_COLLIDER_RADIUS * HIT_COLLIDER_SCALE);

        // Draw points/pickup collector (Vibrant Gold/Green) - 2x larger (r=0.80, h=1.60)
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.75f);
        Gizmos.DrawWireSphere(c, BASE_COLLIDER_RADIUS * POINTS_COLLIDER_SCALE);
    }
}

/// <summary>
/// Dedicated trigger collector for coins and powerups with 2x pickup range.
/// Hazard tags (branch, bees, enemies) are ignored so damage is never taken through this collider.
/// </summary>
public class TCPickupCollector : MonoBehaviour
{
    [SerializeField] playerInteract pi;

    public void Init(playerInteract owner)
    {
        pi = owner;
    }

    void Awake()
    {
        if (!pi) pi = GetComponentInParent<playerInteract>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!pi) pi = GetComponentInParent<playerInteract>();
        if (pi != null)
        {
            pi.HandlePickup(other);
        }
    }
}
