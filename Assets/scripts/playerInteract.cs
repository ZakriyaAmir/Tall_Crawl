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
        renderers = GetComponentsInChildren<Renderer>(true);
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
            Play(woodImpactSound);
            if (pooled) pooled.Release(); else other.transform.parent.gameObject.SetActive(false);
            if (Invulnerable) { OnEvent?.Invoke("smash", transform.position); TCFx.Shake(0.08f); }
            else Damage();
        }
        else if (other.CompareTag("bees"))
        {
            if (jumpTimer > 0 || Invulnerable) return; // jumped over / boosting
            TCFx.Spawn(BeesimpactParticle, transform.position);
            Play(beesImpactSound);
            if (pooled) pooled.Release();
            Damage();
        }
        else if (other.CompareTag("points"))
        {
            other.gameObject.SetActive(false);
            Play(coinSound, 0.7f);
            gameManager.Instance?.addCoin();
            OnEvent?.Invoke("coin", other.transform.position);
        }
        else if (other.CompareTag("health"))
        {
            other.gameObject.SetActive(false);
            Play(healthSound);
            if (life < MaxLife) life++;
            OnEvent?.Invoke("heart", transform.position);
        }
        else if (other.CompareTag("boost"))
        {
            other.gameObject.SetActive(false);
            Play(boostSound);
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
        life = Mathf.Max(1, MaxLife - 1);
        if (animator) { animator.SetBool("death", false); animator.SetTrigger("respawn"); }
        invulnTimer = 2.5f;
    }

    public IEnumerator Invincible() { invulnTimer = 1.2f; yield break; }

    public void death()
    {
        life = 0;
        if (animator) { animator.SetTrigger("death"); animator.SetBool("death", true); }
        Play(deathSound);
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

    public void jump() { jumpTimer = 0.6f; Play(jumpSound); }
}
