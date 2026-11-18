using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Darting Wasp / Hornet: hovering aerial hazard.
/// Fully animated with 70Hz fluttering wings, rhythmic stinger abdomen pumping, and 3D figure-8 hover!
/// </summary>
public class TCWasp : TCEnemy
{
    public Transform wingL, wingR;
    public Transform abdomen, head;
    public AudioSource audioSrc;

    [Header("Reskin Variants")]
    public GameObject waspVisual;
    public GameObject batVisual;
    public Transform waspWingL, waspWingR, waspAbdomen, waspHead;
    public Transform batWingL, batWingR, batTail, batHead;
    public SphereCollider bodyCollider;
    public bool isBat;

    bool hasPlayedBuzz;
    bool pendingReskinShift;
    static player cachedPlayer;
    static Camera cachedCamera;

    void Awake()
    {
        if (!audioSrc) audioSrc = GetComponent<AudioSource>();
        if (audioSrc) audioSrc.loop = false;
    }

    void OnEnable()
    {
        if (!audioSrc) audioSrc = GetComponent<AudioSource>();
        if (audioSrc) audioSrc.loop = false;
        gameManager.OnBatPhaseChanged += OnBatPhaseChanged;
        UpdateReskin();
    }

    public void UpdateReskin()
    {
        bool shouldBeBat = gameManager.Instance != null && gameManager.Instance.IsBatPhase;
        isBat = shouldBeBat;

        if (waspVisual) waspVisual.SetActive(!isBat);
        if (batVisual) batVisual.SetActive(isBat);

        if (!bodyCollider && model) bodyCollider = model.GetComponent<SphereCollider>();

        if (isBat)
        {
            radius = 0.68f;
            if (model) model.localPosition = new Vector3(model.localPosition.x, model.localPosition.y, 0.68f);
            wingL = batWingL;
            wingR = batWingR;
            head = batHead;
            abdomen = batTail;

            if (bodyCollider)
            {
                bodyCollider.radius = 0.20f;
                bodyCollider.center = new Vector3(0, 0.04f, 0);
            }

            if (batVisual)
            {
                var anim = batVisual.GetComponent<Animator>();
                if (anim && anim.runtimeAnimatorController)
                {
                    anim.speed = UnityEngine.Random.Range(0.92f, 1.08f);
                    anim.Play("Bat_Fly", 0, UnityEngine.Random.value);
                }
            }
        }
        else
        {
            radius = 0.95f;
            if (model) model.localPosition = new Vector3(model.localPosition.x, model.localPosition.y, 0.95f);
            wingL = waspWingL;
            wingR = waspWingR;
            head = waspHead;
            abdomen = waspAbdomen;

            if (bodyCollider)
            {
                bodyCollider.radius = 0.28f;
                bodyCollider.center = new Vector3(0, 0.04f, -0.21f);
            }
        }
    }

    public override void Init(float startY, float startAngle, float sweep = 40f, float spd = 2.2f)
    {
        base.Init(startY, startAngle, sweep, spd);
        hasPlayedBuzz = false;
        pendingReskinShift = false;
        UpdateReskin();
        if (!audioSrc) audioSrc = GetComponent<AudioSource>();
        if (audioSrc)
        {
            var clip = isBat ? (TCSfx.Get("bat") ?? Resources.Load<AudioClip>("Sfx/bat"))
                             : (TCSfx.Get("wasp") ?? Resources.Load<AudioClip>("Sfx/wasp"));
            if (clip && audioSrc.clip != clip) audioSrc.clip = clip;
            audioSrc.loop = false;
            audioSrc.volume = 0.85f;
        }
    }

    void OnDisable()
    {
        gameManager.OnBatPhaseChanged -= OnBatPhaseChanged;
        pendingReskinShift = false;
        if (audioSrc && audioSrc.isPlaying) audioSrc.Stop();
    }

    void OnBatPhaseChanged(bool batPhase)
    {
        bool targetIsBat = gameManager.Instance != null && gameManager.Instance.IsBatPhase;
        if (isBat == targetIsBat) return;

        // If the enemy is in camera view or close to the spider, defer the visual shift until off-camera
        if (IsInCameraOrCloseToSpider())
        {
            pendingReskinShift = true;
        }
        else
        {
            pendingReskinShift = false;
            UpdateReskin();
        }
    }

    bool IsInCameraOrCloseToSpider()
    {
        if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
        if (cachedPlayer != null)
        {
            float dy = Mathf.Abs(y - cachedPlayer.transform.position.y);
            if (dy < 9.0f) return true; // Close proximity to spider (vertical view range)
        }

        if (!cachedCamera)
        {
            if (cachedPlayer && cachedPlayer.maincam) cachedCamera = cachedPlayer.maincam;
            else cachedCamera = Camera.main;
        }

        if (cachedCamera != null)
        {
            Vector3 vp = cachedCamera.WorldToViewportPoint(transform.position);
            // In front of camera with comfortable margin outside the screen frustum
            if (vp.z > 0f && vp.x >= -0.15f && vp.x <= 1.15f && vp.y >= -0.15f && vp.y <= 1.15f)
            {
                return true;
            }
        }
        return false;
    }

    public override void Smash(Vector3? hitSourcePos = null)
    {
        if (audioSrc && audioSrc.isPlaying) audioSrc.Stop();
        base.Smash(hitSourcePos);
    }

    void PlayBuzz()
    {
        var clip = isBat ? (TCSfx.Get("bat") ?? Resources.Load<AudioClip>("Sfx/bat"))
                         : (TCSfx.Get("wasp") ?? Resources.Load<AudioClip>("Sfx/wasp"));
        if (clip)
        {
            float playVol = isBat ? 0.85f : 0.62f; // Soothing, gentle volume for wasp chime
            if (audioSrc)
            {
                audioSrc.loop = false;
                audioSrc.volume = playVol;
                audioSrc.PlayOneShot(clip, playVol);
            }
            else
            {
                TCAudio.Play(clip, playVol);
            }
        }
    }

    void Update()
    {
        if (isSmashed) return;
        bool isTutorial = TCGameUI.Instance != null && TCGameUI.Instance.IsTutorialRun;
        if (!isTutorial)
        {
            if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
            if (cachedPlayer && (cachedPlayer.paused || cachedPlayer.currentScore < 200f)) return;
            if (TCGameUI.Instance && !TCGameUI.Instance.Started) return;
        }

        float t = Time.time;

        // If a reskin shift was deferred because the enemy was on-screen/close to spider,
        // execute the shift as soon as it moves completely off-camera and far from spider.
        if (pendingReskinShift)
        {
            if (!IsInCameraOrCloseToSpider())
            {
                pendingReskinShift = false;
                UpdateReskin();
            }
        }

        // Proximity buzz warning sound when spider approaches within 4.5m - plays ONCE, never in a loop
        if (!hasPlayedBuzz)
        {
            if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
            if (cachedPlayer != null)
            {
                float dy = y - cachedPlayer.transform.position.y;
                if (dy > -0.5f && dy < 4.5f)
                {
                    hasPlayedBuzz = true;
                    PlayBuzz();
                }
            }
        }

        // 1. Wing flutter: 70Hz for wasp, authentic deep flapping stroke for bat
        if (isBat)
        {
            var anim = batVisual != null ? batVisual.GetComponent<Animator>() : null;
            if (anim == null || !anim.enabled || anim.runtimeAnimatorController == null)
            {
                // Procedural bat wing flapping fallback: 5.5Hz fluid flapping stroke cycle
                float wingPhase = t * 11f * Mathf.PI;
                float flap = Mathf.Sin(wingPhase) * 44f;
                float fold = Mathf.Cos(wingPhase) * 16f;
                if (wingL) wingL.localRotation = Quaternion.Euler(fold * 0.4f, -fold * 0.35f, flap + 4f);
                if (wingR) wingR.localRotation = Quaternion.Euler(fold * 0.4f, fold * 0.35f, -flap - 4f);
            }
        }
        else
        {
            // High-frequency 70Hz wing flutter with pitch stroke angling for wasp
            float flap = Mathf.Sin(t * 70f) * 44f;
            float strokePitch = Mathf.Cos(t * 70f) * 16f;
            if (wingL) wingL.localRotation = Quaternion.Euler(strokePitch, 0, flap + 12f);
            if (wingR) wingR.localRotation = Quaternion.Euler(strokePitch, 0, -flap - 12f);

            // Realistic wasp stinger abdomen pumping / curling
            if (abdomen)
            {
                float pump = Mathf.Sin(t * 7.5f + phase) * 20f;
                abdomen.localRotation = Quaternion.Euler(-26f + pump, 0, 0);
            }

            // Head twitching scan
            if (head)
            {
                float twitch = Mathf.Sin(t * 3.2f) * 6f;
                head.localRotation = Quaternion.Euler(twitch, Mathf.Sin(t * 5.4f) * 8f, 0);
            }
        }

        // Natural 3D Figure-8 Hover & banking motion (shared identical motion across both wasp and bat)
        float hoverY = y + Mathf.Sin(t * 3.8f + phase) * 0.18f;
        float swayAngle = baseAngle + Mathf.Sin(t * 2.0f + phase) * (sweepAngle * 0.45f);
        float bankRoll = Mathf.Cos(t * 2.0f + phase) * -14f;
        float pitchBob = Mathf.Cos(t * 3.8f + phase) * 8f;

        transform.position = new Vector3(0, hoverY, 0);
        transform.rotation = Quaternion.Euler(pitchBob, swayAngle, bankRoll);
    }
}
