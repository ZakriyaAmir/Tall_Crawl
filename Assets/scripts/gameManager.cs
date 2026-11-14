using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Run flow: countdown, pause, game over, revive, rewards. UI lives in TCGameUI.
public class gameManager : MonoBehaviour
{
    public Material[] bgImage;
    public AudioClip[] BGM;
    public Camera camera;
    public GameObject Sun;

    [Header("WEB Letter Pickups")]
    [Range(0.1f, 1.0f)]
    [Tooltip("Spawn probability for W E B letter pickups (0.1 - 1.0)")]
    public float webPickupSpawnProbability = 0.25f;

    public static gameManager Instance;
    public int coinsCollected;
    public bool revivedThisRun, runEnded;
    public bool IsPaused { get; private set; }
    public int RunReward { get; private set; }
    public int RunGems { get; private set; }
    bool doubledClaimed;
    player p; AudioSource music; playerInteract spider;

    [System.Serializable]
    public struct DiurnalPhase
    {
        public string name;
        public int bgIndex;
        public bool isNight;
        public Color sunColor;
        public float sunIntensity;
        public Color ambientSky;
        public Color ambientEquator;
        public Color ambientGround;
    }

    DiurnalPhase[] diurnalPhases;
    int currentPhaseIndex;
    int nextPhaseIndex;
    bool isTransitioning;
    float transitionTimer;
    float phaseTimer;
    const float PhaseStayDuration = 20.0f;       // Daytime changes every 20 seconds
    const float PhaseTransitionDuration = 5.5f;  // Smooth crossfade duration between times of day

    Material blendedSkyboxMat;
    Skybox cameraSkybox;
    Light sunLight;
    float skyboxRotation;

    AudioSource musicPrimary;
    AudioSource musicSecondary;
    AudioClip dayMusicClip;
    AudioClip nightMusicClip;
    AudioClip currentBgmClip;
    bool isAudioCrossfading;
    float audioFadeTimer;
    float audioFadeDuration;

    void Awake()
    {
        Instance = this;
        TCServices.ApplyMaxDeviceRefreshRate();
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        TCSave.Apply();
    }

    void Start()
    {
        p = FindObjectOfType<player>();
        spider = FindObjectOfType<playerInteract>();
        if (p) p.paused = true;

        SetupDiurnalPhases();
        SetupSkyboxAndLighting();
        SetupMusic();

        TCServices.Banner(true);
        TCServices.Log("run_start");
    }

    void OnDestroy()
    {
        if (blendedSkyboxMat) Destroy(blendedSkyboxMat);
    }

    void SetupDiurnalPhases()
    {
        // Resolve 5 skybox materials in exact sequence: Evening -> Midday -> Daybreak -> Sunset -> Midnight
        int idxEvening = FindMaterialIndex("Evening");
        if (idxEvening < 0) idxEvening = 1;
        int idxMidday = FindMaterialIndex("Midday");
        if (idxMidday < 0) idxMidday = 0;
        int idxDaybreak = FindMaterialIndex("Daybreak");
        if (idxDaybreak < 0) idxDaybreak = 4;
        int idxSunset = FindMaterialIndex("Sunset");
        if (idxSunset < 0) idxSunset = 3;
        int idxMidnight = FindMaterialIndex("Midnight");
        if (idxMidnight < 0) idxMidnight = 2;

        diurnalPhases = new DiurnalPhase[]
        {
            new DiurnalPhase
            {
                name = "CloudyCrown_Evening",
                bgIndex = idxEvening,
                isNight = false,
                sunColor = new Color(1f, 0.95f, 0.88f),
                sunIntensity = 1.22f,
                ambientSky = new Color(0.60f, 0.68f, 0.74f),
                ambientEquator = new Color(0.46f, 0.50f, 0.54f),
                ambientGround = new Color(0.35f, 0.35f, 0.38f)
            },
            new DiurnalPhase
            {
                name = "CloudyCrown_Midday",
                bgIndex = idxMidday,
                isNight = false,
                sunColor = new Color(1f, 0.98f, 0.92f),
                sunIntensity = 1.30f,
                ambientSky = new Color(0.65f, 0.70f, 0.78f),
                ambientEquator = new Color(0.48f, 0.52f, 0.56f),
                ambientGround = new Color(0.35f, 0.35f, 0.38f)
            },
            new DiurnalPhase
            {
                name = "CloudyCrown_Daybreak",
                bgIndex = idxDaybreak,
                isNight = false,
                sunColor = new Color(1.0f, 0.78f, 0.55f),
                sunIntensity = 1.25f,
                ambientSky = new Color(0.72f, 0.54f, 0.46f),
                ambientEquator = new Color(0.54f, 0.42f, 0.40f),
                ambientGround = new Color(0.36f, 0.30f, 0.32f)
            },
            new DiurnalPhase
            {
                name = "CloudyCrown_Sunset",
                bgIndex = idxSunset,
                isNight = true,
                sunColor = new Color(0.85f, 0.70f, 0.92f),
                sunIntensity = 1.05f,
                ambientSky = new Color(0.54f, 0.46f, 0.62f),
                ambientEquator = new Color(0.40f, 0.36f, 0.48f),
                ambientGround = new Color(0.28f, 0.26f, 0.32f)
            },
            new DiurnalPhase
            {
                name = "CloudyCrown_Midnight",
                bgIndex = idxMidnight,
                isNight = true,
                sunColor = new Color(0.70f, 0.82f, 1.0f),
                sunIntensity = 0.92f,
                ambientSky = new Color(0.38f, 0.42f, 0.55f),
                ambientEquator = new Color(0.30f, 0.33f, 0.42f),
                ambientGround = new Color(0.22f, 0.24f, 0.28f)
            }
        };

        currentPhaseIndex = Random.Range(0, diurnalPhases.Length); // Start at any random time of the day
        phaseTimer = 0f;
        isTransitioning = false;
    }

    int FindMaterialIndex(string keyword)
    {
        if (bgImage == null) return -1;
        for (int i = 0; i < bgImage.Length; i++)
        {
            if (bgImage[i] != null && bgImage[i].name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return i;
        }
        return -1;
    }

    Material GetMaterialForPhase(DiurnalPhase phase)
    {
        if (bgImage != null && phase.bgIndex >= 0 && phase.bgIndex < bgImage.Length)
        {
            return bgImage[phase.bgIndex];
        }
        return (bgImage != null && bgImage.Length > 0) ? bgImage[0] : null;
    }

    void SetupSkyboxAndLighting()
    {
        if (!camera) camera = Camera.main;
        if (camera) cameraSkybox = camera.GetComponent<Skybox>();
        if (!cameraSkybox && Camera.main) cameraSkybox = Camera.main.GetComponent<Skybox>();

        if (Sun)
        {
            Sun.SetActive(true);
            var anim = Sun.GetComponent<Animator>();
            if (anim) anim.enabled = false;
            Sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sunLight = Sun.GetComponent<Light>();
            if (sunLight)
            {
                sunLight.enabled = true;
                sunLight.cullingMask &= ~(1 << 9);
            }
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientIntensity = 1.15f;

        var proto = Resources.Load<Material>("SkyboxBlended");
        if (proto != null)
        {
            blendedSkyboxMat = new Material(proto) { name = "Skybox_Blended_Runtime" };
        }
        else
        {
            Shader blendShader = Shader.Find("Skybox/Blended 6 Sided");
            if (blendShader)
            {
                blendedSkyboxMat = new Material(blendShader) { name = "Skybox_Blended_Runtime" };
            }
        }

        phaseTimer = 0f;
        ApplyPhaseImmediate(currentPhaseIndex);
    }

    void SetupMusic()
    {
        musicPrimary = GetComponent<AudioSource>();
        if (!musicPrimary) musicPrimary = gameObject.AddComponent<AudioSource>();
        musicPrimary.loop = true;
        musicPrimary.playOnAwake = false;

        musicSecondary = gameObject.AddComponent<AudioSource>();
        musicSecondary.loop = true;
        musicSecondary.playOnAwake = false;

        music = musicPrimary;

        // Resolve Day and Night music clips
        dayMusicClip = null;
        nightMusicClip = null;
        if (BGM != null)
        {
            for (int i = 0; i < BGM.Length; i++)
            {
                if (BGM[i] == null) continue;
                if (BGM[i].name.IndexOf("night", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    nightMusicClip = BGM[i];
                else if (BGM[i].name.IndexOf("day", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    dayMusicClip = BGM[i];
            }
        }
        if (!dayMusicClip && BGM != null && BGM.Length > 0) dayMusicClip = BGM[0];
        if (!nightMusicClip && BGM != null && BGM.Length > 1) nightMusicClip = BGM[1];
        if (!nightMusicClip) nightMusicClip = dayMusicClip;

        var curPhase = diurnalPhases[currentPhaseIndex];
        currentBgmClip = curPhase.isNight ? nightMusicClip : dayMusicClip;

        if (currentBgmClip != null)
        {
            musicPrimary.clip = currentBgmClip;
            musicPrimary.volume = 0.5f;
            musicPrimary.mute = !TCSave.Music;
            musicPrimary.Play();
        }
    }

    void ApplyPhaseImmediate(int phaseIdx)
    {
        if (diurnalPhases == null || diurnalPhases.Length == 0) return;
        var p = diurnalPhases[phaseIdx];
        Material mat = GetMaterialForPhase(p);

        if (blendedSkyboxMat != null && mat != null)
        {
            ApplySkyboxFaceTextures(blendedSkyboxMat, mat, false);
            ApplySkyboxFaceTextures(blendedSkyboxMat, mat, true);
            blendedSkyboxMat.SetFloat("_Blend", 0f);
            blendedSkyboxMat.SetFloat("_Rotation", skyboxRotation);

            if (cameraSkybox) cameraSkybox.material = blendedSkyboxMat;
            RenderSettings.skybox = blendedSkyboxMat;
        }
        else if (mat != null)
        {
            if (cameraSkybox) cameraSkybox.material = mat;
            RenderSettings.skybox = mat;
        }

        if (sunLight)
        {
            sunLight.color = p.sunColor;
            sunLight.intensity = p.sunIntensity;
        }
        RenderSettings.ambientSkyColor = p.ambientSky;
        RenderSettings.ambientEquatorColor = p.ambientEquator;
        RenderSettings.ambientGroundColor = p.ambientGround;
    }

    void ApplySkyboxFaceTextures(Material targetMat, Material srcMat, bool isSlot2)
    {
        if (!targetMat || !srcMat) return;
        string suffix = isSlot2 ? "2" : "";
        string[] texProps = { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex" };
        foreach (var prop in texProps)
        {
            var tex = srcMat.GetTexture(prop);
            if (tex) targetMat.SetTexture(prop + suffix, tex);
        }
        if (srcMat.HasProperty("_Tint"))
            targetMat.SetColor("_Tint" + suffix, srcMat.GetColor("_Tint"));
        else
            targetMat.SetColor("_Tint" + suffix, new Color(0.5f, 0.5f, 0.5f, 0.5f));

        if (srcMat.HasProperty("_Exposure"))
            targetMat.SetFloat("_Exposure" + suffix, srcMat.GetFloat("_Exposure"));
        else
            targetMat.SetFloat("_Exposure" + suffix, 1f);
    }

    void StartTransitionToNextPhase()
    {
        if (diurnalPhases == null || diurnalPhases.Length == 0) return;
        nextPhaseIndex = (currentPhaseIndex + 1) % diurnalPhases.Length;
        var curPhase = diurnalPhases[currentPhaseIndex];
        var nxtPhase = diurnalPhases[nextPhaseIndex];

        Material curMat = GetMaterialForPhase(curPhase);
        Material nxtMat = GetMaterialForPhase(nxtPhase);

        if (blendedSkyboxMat != null && curMat != null && nxtMat != null)
        {
            ApplySkyboxFaceTextures(blendedSkyboxMat, curMat, false);
            ApplySkyboxFaceTextures(blendedSkyboxMat, nxtMat, true);
            blendedSkyboxMat.SetFloat("_Blend", 0f);

            if (cameraSkybox) cameraSkybox.material = blendedSkyboxMat;
            RenderSettings.skybox = blendedSkyboxMat;
        }

        AudioClip targetClip = nxtPhase.isNight ? nightMusicClip : dayMusicClip;
        if (targetClip != null && targetClip != currentBgmClip)
        {
            StartAudioCrossfade(targetClip, PhaseTransitionDuration);
        }

        isTransitioning = true;
        transitionTimer = 0f;
    }

    void StartAudioCrossfade(AudioClip targetClip, float duration)
    {
        if (!musicSecondary || targetClip == null) return;
        musicSecondary.clip = targetClip;
        musicSecondary.time = 0f;
        musicSecondary.volume = 0f;
        musicSecondary.mute = !TCSave.Music;
        if (TCSave.Music && !IsPaused && (!p || !p.GameOver))
        {
            musicSecondary.Play();
        }

        isAudioCrossfading = true;
        audioFadeTimer = 0f;
        audioFadeDuration = Mathf.Max(0.5f, duration);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Gentle cloud drift across the sky dome
        skyboxRotation += dt * 0.8f;
        if (blendedSkyboxMat) blendedSkyboxMat.SetFloat("_Rotation", skyboxRotation);

        // Advance diurnal cycle when gameplay is running
        if (IsPaused || (p && (p.paused || p.GameOver))) return;

        if (!isTransitioning)
        {
            phaseTimer += dt;
            if (phaseTimer >= PhaseStayDuration)
            {
                phaseTimer = 0f;
                StartTransitionToNextPhase();
            }
        }
        else
        {
            transitionTimer += dt;
            float progress = Mathf.Clamp01(transitionTimer / PhaseTransitionDuration);
            float blendK = Mathf.SmoothStep(0f, 1f, progress);

            var curP = diurnalPhases[currentPhaseIndex];
            var nxtP = diurnalPhases[nextPhaseIndex];

            // 1. Smooth Skybox crossfade
            if (blendedSkyboxMat)
            {
                blendedSkyboxMat.SetFloat("_Blend", blendK);
            }

            // 2. Smooth Sunlight color & intensity lerp
            if (sunLight)
            {
                sunLight.color = Color.Lerp(curP.sunColor, nxtP.sunColor, blendK);
                sunLight.intensity = Mathf.Lerp(curP.sunIntensity, nxtP.sunIntensity, blendK);
            }

            // 3. Smooth Ambient light lerp
            RenderSettings.ambientSkyColor = Color.Lerp(curP.ambientSky, nxtP.ambientSky, blendK);
            RenderSettings.ambientEquatorColor = Color.Lerp(curP.ambientEquator, nxtP.ambientEquator, blendK);
            RenderSettings.ambientGroundColor = Color.Lerp(curP.ambientGround, nxtP.ambientGround, blendK);

            // 4. Smooth BGM crossfade
            if (isAudioCrossfading)
            {
                audioFadeTimer += dt;
                float audioK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(audioFadeTimer / audioFadeDuration));
                float maxVol = 0.5f;
                if (musicPrimary) musicPrimary.volume = Mathf.Lerp(maxVol, 0f, audioK);
                if (musicSecondary) musicSecondary.volume = Mathf.Lerp(0f, maxVol, audioK);

                if (audioFadeTimer >= audioFadeDuration)
                {
                    isAudioCrossfading = false;
                    if (musicPrimary)
                    {
                        musicPrimary.Stop();
                        musicPrimary.volume = 0f;
                    }
                    var tmp = musicPrimary;
                    musicPrimary = musicSecondary;
                    musicSecondary = tmp;
                    music = musicPrimary;
                    currentBgmClip = musicPrimary.clip;
                }
            }

            // Transition completion: advance phase and reset blend seamlessly
            if (transitionTimer >= PhaseTransitionDuration)
            {
                isTransitioning = false;
                currentPhaseIndex = nextPhaseIndex;
                phaseTimer = 0f;
                ApplyPhaseImmediate(currentPhaseIndex);
            }
        }
    }

    /// Called by the UI after the "tap to start"/tutorial.
    public void BeginRun()
    {
        p.paused = false;
        if (p.anim) p.anim.SetBool("started", true);
    }

    /// One collected coin = exactly one coin in the wallet (no multipliers, no doubling).
    public void addCoin()
    {
        coinsCollected += CoinValue;
    }

    public const int CoinValue = 1;
    /// Distance bonus per 100 m: base + "Climb Bonus" upgrade level.
    public static int BonusPerHundred => TCConfig.I.coinsPerHundredMetres + TCSave.Upgrade(3);
    public void SetMusic(bool on)
    {
        if (musicPrimary) musicPrimary.mute = !on;
        if (musicSecondary) musicSecondary.mute = !on;
    }

    public void Pause(bool on)
    {
        if (runEnded) return;
        IsPaused = on;
        Time.timeScale = on ? 0 : 1;
        if (p) p.ResetInputState();
        if (musicPrimary) { if (on) musicPrimary.Pause(); else musicPrimary.UnPause(); }
        if (musicSecondary && musicSecondary.isPlaying) { if (on) musicSecondary.Pause(); else musicSecondary.UnPause(); }
    }

    void OnApplicationPause(bool paused) { if (paused && p && !p.paused && !p.GameOver) TCGameUI.Instance?.ShowPause(); }

    public void OnPlayerDied()
    {
        TCTimeDilation.Instance?.CancelOnDeath();
        StartCoroutine(DeathRoutine());
    }
    IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(1.2f);
        TCGameUI.Instance?.ShowGameOver(!revivedThisRun);
    }

    public void Revive()
    {
        if (revivedThisRun || runEnded || !p.GameOver) return; // guard: ad callback + timer race could revive twice / after results
        revivedThisRun = true;
        IsPaused = false;
        p.GameOver = false;
        p.paused = true;
        p.ResetInputState();
        spider.Revive();
        StartCoroutine(ReviveRoutine());
        TCServices.Log("player_revived");
    }

    IEnumerator ReviveRoutine()
    {
        // Don't move the spider until the getting up animation is completed
        yield return new WaitForSeconds(1.25f);
        if (!runEnded) p.paused = false;
    }

    /// Banks the run: coins + distance bonus, best score, achievements, interstitial cadence.
    public int EndRun(bool doubled)
    {
        if (runEnded) return RunReward;
        runEnded = true;
        int metres = (int)p.currentScore;
        int bonus = metres / 100 * BonusPerHundred;
        // Collected coins are banked 1:1. The ad doubler only ever doubles the distance bonus.
        RunReward = coinsCollected + bonus * (doubled ? 2 : 1);
        RunGems = (TCConfig.I != null && TCConfig.I.gemEveryMetres > 0) ? metres / TCConfig.I.gemEveryMetres : metres / 10000;
        if (RunGems > 0) TCSave.Gems += RunGems;
        TCSave.Coins += RunReward;
        TCSave.LifetimeCoins += RunReward;
        TCSave.Runs++;
        if (metres > TCSave.Best) TCSave.Best = metres;
        TCSkins.CheckAchievements();
        TCServices.Log("run_end", "metres", metres);
        return RunReward;
    }

    /// x2 bonus reward: doubles the entire banked run reward (collected coins + distance bonus), once per run.
    public bool ClaimDouble()
    {
        if (!runEnded || doubledClaimed) return false;
        int extra = RunReward;
        if (extra <= 0) return false;
        doubledClaimed = true;
        RunReward += extra;
        TCSave.Coins += extra;
        TCSave.LifetimeCoins += extra;
        return true;
    }
    public bool DoubleClaimed => doubledClaimed;

    public int DistanceBonus => (int)p.currentScore / 100 * BonusPerHundred;
    public int Metres => p ? (int)p.currentScore : 0;

    public void Restart() { MaybeInterstitial(); Time.timeScale = 1; SceneManager.LoadScene("gameplay"); }
    public void Home() { MaybeInterstitial(); Time.timeScale = 1; SceneManager.LoadScene("MainMenu"); }
    void MaybeInterstitial()
    {
        bool isTest = (AdsManager.Instance != null && AdsManager.Instance.testMode);
        int n = Mathf.Max(1, TCConfig.I != null ? TCConfig.I.interstitialEveryNRuns : 3);
        bool shouldShow = isTest || (TCSave.Runs > 0 && TCSave.Runs % n == 0);
        if (shouldShow) TCServices.Interstitial();
    }
}
