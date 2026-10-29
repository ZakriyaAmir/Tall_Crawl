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
    public bool Invulnerable => invulnTimer > 0 || boostDuration > 0;
    public static System.Action<string, Vector3> OnEvent; // "coin","hit","heart","boost","smash","death"

    AudioSource audioSource; player p; Animator animator; Renderer[] renderers;
    float invulnTimer, jumpTimer; bool blinking;

    void Start()
    {
        p = GetComponentInParent<player>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        renderers = GetComponentsInChildren<Renderer>(true);
        if (GameOverScreen) GameOverScreen.SetActive(false);
        MaxLife = TCConfig.I.baseHearts + Mathf.Min(2, TCSave.Upgrade(2));
        life = MaxLife;
        TCSkins.Apply(gameObject, TCSave.SelectedSkin);
        renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.enabled); // skip the hidden realistic mesh
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (jumpTimer > 0) jumpTimer -= dt;
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
            if (boostDuration <= 0) { p.boost = false; if (animator) animator.SetBool("boost", false); invulnTimer = 1.0f; }
        }
    }

    void SetVisible(bool v) { foreach (var r in renderers) if (r && !(r is TrailRenderer)) r.enabled = v; }

    void Play(AudioClip c, float vol = 1f) { if (c && audioSource) audioSource.PlayOneShot(c, vol); }

    void OnTriggerEnter(Collider other)
    {
        if (p == null || p.GameOver) return;
        var pooled = other.GetComponentInParent<TCPooled>();
        if (other.CompareTag("branch"))
        {
            TCFx.Spawn(impactParticle, other.transform.position);
            Play(TCSfx.Get("hit") ?? woodImpactSound, 0.8f);
            if (pooled) pooled.Release(); else other.transform.parent.gameObject.SetActive(false);
            if (Invulnerable) { OnEvent?.Invoke("smash", transform.position); TCFx.Shake(0.08f); }
            else Damage();
        }
        else if (other.CompareTag("bees"))
        {
            if (jumpTimer > 0 || Invulnerable) return; // jumped over / boosting
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
        }
    }

    void Damage()
    {
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
        if (animator) animator.SetBool("boost", true);
        OnEvent?.Invoke("boost", transform.position);
    }

    public void jump() { jumpTimer = 0.6f; Play(TCSfx.Get("jump") ?? jumpSound, 0.7f); }
}
