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
    public bool Invulnerable => invulnTimer > 0 || boostDuration > 0 || IsRolling;
    public bool IsJumping => jumpTimer > 0;
    public bool IsRolling => boostDuration > 0 || (p != null && p.boost);
    public static System.Action<string, Vector3> OnEvent; // "coin","hit","heart","boost","smash","death","cooldown"
    public static int CooldownInventory = 0; // Stored slow-mo cooldown power ups

    AudioSource audioSource; player p; Animator animator; Renderer[] renderers;
    float invulnTimer, jumpTimer, rollTimer; bool blinking;

    void Start()
    {
        p = GetComponentInParent<player>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        renderers = GetComponentsInChildren<Renderer>(true);
        if (GameOverScreen) GameOverScreen.SetActive(false);
        MaxLife = TCConfig.I.baseHearts + Mathf.Min(2, TCSave.Upgrade(2));
        life = MaxLife;
        CooldownInventory = 0;
        TCSkins.Apply(gameObject, TCSave.SelectedSkin);
        if (!GetComponentInChildren<TCFireEffects>()) gameObject.AddComponent<TCFireEffects>();
        renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.enabled); // skip the hidden realistic mesh
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

    void SetVisible(bool v) { foreach (var r in renderers) if (r && !(r is TrailRenderer)) r.enabled = v; }

    void Play(AudioClip c, float vol = 1f) { if (c && audioSource) audioSource.PlayOneShot(c, vol); }

    void OnTriggerEnter(Collider other)
    {
        if (p == null || p.GameOver) return;
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
        else if (other.CompareTag("points"))
        {
            if (!other.gameObject.activeSelf) return; // already collected this physics step (several colliders on the spider)
            other.gameObject.SetActive(false);
            Play(TCSfx.Get("coin") ?? coinSound, 0.55f);
            gameManager.Instance?.addCoin();
            OnEvent?.Invoke("coin", other.transform.position);
        }
        else if (other.CompareTag("health"))
        {
            if (!other.gameObject.activeSelf) return;
            other.gameObject.SetActive(false);
            Play(TCSfx.Get("heart") ?? healthSound, 0.8f);
            if (life < MaxLife) life++;
            OnEvent?.Invoke("heart", transform.position);
        }
        else if (other.CompareTag("boost"))
        {
            if (!other.gameObject.activeSelf) return;
            other.gameObject.SetActive(false); // short cute "nom-nom-yip" (no slow-motion whoosh)
            Play(TCSfx.Get("chilli"), 0.8f);
            StartBoost(3f + 0.75f * TCSave.Upgrade(1));
            TCFx.Shake(0.12f);
            TCTrack.OnChilliCollected();
        }
        else if (other.CompareTag("cooldown"))
        {
            if (!other.gameObject.activeSelf) return;
            other.gameObject.SetActive(false);
            Play(TCSfx.Get("cooldown_collect") ?? TCSfx.Get("coin"), 0.85f);
            CooldownInventory++;
            OnEvent?.Invoke("cooldown", transform.position);
            TCFx.Shake(0.08f);
        }
    }

    void Damage()
    {
        if (!TCSave.TutorialDone)
        {
            invulnTimer = 0.8f;
            return;
        }
        TCServices.Haptic();
        TCFx.Shake(0.25f);
        life--;
        OnEvent?.Invoke("hit", transform.position);
        if (life > 0) invulnTimer = 1.4f; else death();
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
        life = 0;
        if (animator) { animator.SetTrigger("death"); animator.SetBool("death", true); }
        Play(TCSfx.Get("death") ?? deathSound, 0.85f);
        p.GameOver = true; p.boost = false; boostDuration = 0;
        SetVisible(true);
        OnEvent?.Invoke("death", transform.position);
        gameManager.Instance?.OnPlayerDied();
    }

    public void StartBoost(float secs)
    {
        boostDuration = Mathf.Max(boostDuration, secs);
        OnEvent?.Invoke("boost", transform.position);
    }

    public void jump(float duration = 0.6f) { jumpTimer = duration; Play(TCSfx.Get("jump") ?? jumpSound, 0.7f); }

    public void roll(float duration = 1.2f)
    {
        // Rolling is now automatic during chilli pepper boost mode.
    }

    public void doubleJump(float duration = 0.85f)
    {
        jumpTimer = duration;
        Play(TCSfx.Get("menu_flip") ?? TCSfx.Get("jump") ?? jumpSound, 0.85f);
        var cute = GetComponentInChildren<TCCuteSpider>();
        if (cute) cute.TriggerBackflip(duration * (0.75f / 0.85f));
    }
}
