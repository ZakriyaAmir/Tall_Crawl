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
    float invulnTimer, jumpTimer, rollTimer; bool blinking, initialized;

    void Awake()
    {
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
            if (boostDuration <= 0) { p.boost = false; invulnTimer = 1.0f; }
        }
    }

    bool IsFx(Transform t) { for (; t && t != transform; t = t.parent) if (t.name == "Chilli_Fire_Effects") return true; return false; }

    void SetVisible(bool v) { foreach (var r in renderers) if (r && !(r is TrailRenderer)) r.enabled = v; }

    void Play(AudioClip c, float vol = 1f) { if (c && audioSource) audioSource.PlayOneShot(c, vol); }

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
            other.gameObject.SetActive(false);
            Play(TCSfx.Get("coin") ?? coinSound, 0.55f);
            gameManager.Instance?.addCoin();
            OnEvent?.Invoke("coin", other.transform.position);
            return true;
        }
        else if (other.CompareTag("health"))
        {
            other.gameObject.SetActive(false);
            Play(TCSfx.Get("heart") ?? healthSound, 0.8f);
            if (life < MaxLife) life++;
            OnEvent?.Invoke("heart", transform.position);
            return true;
        }
        else if (other.CompareTag("boost"))
        {
            other.gameObject.SetActive(false);
            Play(TCSfx.Get("chilli"), 0.8f);
            StartBoost(3f + 0.75f * TCSave.Upgrade(1));
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
            Play(TCSfx.Get("cooldown_collect") ?? TCSfx.Get("coin"), 0.85f);
            TCCoinMagnet.Instance?.Activate();
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

        var pooled = other.GetComponentInParent<TCPooled>();
        var enemy = other.GetComponentInParent<TCEnemy>();

        // Sonic Roll Attack: smashes through branches, bees, and enemies!
        if (IsRolling)
        {
            if (other.CompareTag("branch"))
            {
                TCFx.Spawn(impactParticle, other.transform.position);
                if (pooled) pooled.Release(); else other.transform.parent.gameObject.SetActive(false);
                Play(TCSfx.Get("thrust") ?? boostSound, 1f);
                OnEvent?.Invoke("smash", transform.position);
                TCFx.Shake(0.18f);
                gameManager.Instance?.addCoin();
                return;
            }
            if (other.CompareTag("bees"))
            {
                TCFx.Spawn(BeesimpactParticle, transform.position);
                Play(TCSfx.Get("thrust") ?? boostSound, 1f);
                if (pooled) pooled.Release();
                TCFx.Shake(0.18f);
                TCSave.AddBeeKill(1);
                TCSkins.CheckAchievements();
                if (boostDuration > 0)
                {
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
                enemy.Smash();
                Play(TCSfx.Get("thrust") ?? boostSound, 1f);
                TCFx.Spawn(impactParticle, other.transform.position);
                TCFx.Shake(0.2f);
                if (enemy.enemyType == TCEnemyType.Wasp) TCSave.AddWaspKill(1);
                else if (enemy.enemyType == TCEnemyType.Squirrel) TCSave.AddSquirrelKill(1);
                TCSkins.CheckAchievements();

                if (boostDuration > 0)
                {
                    OnEvent?.Invoke("chilli_kill", other.transform.position);
                }
                else
                {
                    OnEvent?.Invoke("smash", transform.position);
                    gameManager.Instance?.addCoin();
                }
                return;
            }
        }

        // Enemy collision handling (when not in roll attack mode)
        if (enemy != null)
        {
            if (boostDuration > 0)
            {
                enemy.Smash();
                Play(TCSfx.Get("thrust") ?? boostSound, 0.9f);
                TCFx.Spawn(impactParticle, other.transform.position);
                TCFx.Shake(0.12f);
                if (enemy.enemyType == TCEnemyType.Wasp) TCSave.AddWaspKill(1);
                else if (enemy.enemyType == TCEnemyType.Squirrel) TCSave.AddSquirrelKill(1);
                TCSkins.CheckAchievements();
                OnEvent?.Invoke("chilli_kill", other.transform.position);
                return;
            }
            if (jumpTimer > 0 && enemy.CanJumpOver) return; // jumped over crawling enemy
            if (invulnTimer > 0) return;

            if (enemy.enemyType == TCEnemyType.Wasp)
            {
                Play(TCSfx.Get("wasp") ?? woodImpactSound, 0.95f);
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
            TCFx.Spawn(impactParticle, other.transform.position);
            if (pooled) pooled.Release(); else other.transform.parent.gameObject.SetActive(false);
            if (boostDuration > 0)
            {
                // In chilli mode: smash through with a powerful thrust sound (no sad/hurt sound)
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
                Play(TCSfx.Get("hit") ?? woodImpactSound, 0.8f);
                Damage();
            }
        }
        else if (other.CompareTag("bees"))
        {
            if (jumpTimer > 0) return; // jumped over
            if (boostDuration > 0)
            {
                // In chilli mode: thrust through bee swarms with punchy thrust sound
                TCFx.Spawn(BeesimpactParticle, transform.position);
                Play(TCSfx.Get("thrust") ?? boostSound, 0.9f);
                if (pooled) pooled.Release();
                TCSave.AddBeeKill(1);
                TCSkins.CheckAchievements();
                OnEvent?.Invoke("chilli_kill", transform.position);
                TCFx.Shake(0.1f);
                return;
            }
            if (invulnTimer > 0) return; // post-hit invulnerability

            TCFx.Spawn(BeesimpactParticle, transform.position);
            Play(TCSfx.Get("bees") ?? beesImpactSound, 0.8f);
            if (pooled) pooled.Release();
            Damage();
        }
    }

    void Damage()
    {
        if (!TCSave.TutorialDone)
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
        if (animator) { animator.SetBool("death", false); animator.SetTrigger("respawn"); }
        invulnTimer = 2.5f;
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
        OnEvent?.Invoke("death", transform.position);
        gameManager.Instance?.OnPlayerDied();
    }

    public void StartBoost(float secs)
    {
        if (IsDead || (p != null && p.GameOver)) return;
        if (boostDuration <= 0) TCPickupFX.Burst(TCPickupKind.BoostStart, transform.position + transform.up * 0.2f);
        boostDuration = Mathf.Max(boostDuration, secs);
        OnEvent?.Invoke("boost", transform.position);
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
