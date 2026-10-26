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

    public static gameManager Instance;
    public int coinsCollected;
    public bool revivedThisRun, runEnded;
    public int RunReward { get; private set; }
    public int RunGems { get; private set; }
    bool doubledClaimed;
    player p; AudioSource music; playerInteract spider;

    void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        TCSave.Apply();
    }

    void Start()
    {
        p = FindObjectOfType<player>();
        spider = FindObjectOfType<playerInteract>();
        music = GetComponent<AudioSource>();
        p.paused = true;
        int stage = bgImage != null && bgImage.Length > 0 ? Random.Range(0, bgImage.Length) : 0;
        if (camera && bgImage != null && bgImage.Length > 0) { var sb = camera.GetComponent<Skybox>(); if (sb) sb.material = bgImage[stage]; }
        if (Sun)
        {
            Sun.SetActive(true);
            var anim = Sun.GetComponent<Animator>();
            if (anim) anim.enabled = false;
            Sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var sunLight = Sun.GetComponent<Light>();
            if (sunLight)
            {
                sunLight.enabled = true;
                if (stage == 2) // Midnight sky
                {
                    sunLight.color = new Color(0.72f, 0.82f, 1f);
                    sunLight.intensity = 0.95f;
                }
                else if (stage == 3 || stage == 4) // Sunset / Evening
                {
                    sunLight.color = new Color(1f, 0.88f, 0.72f);
                    sunLight.intensity = 1.25f;
                }
                else // Midday / Daybreak
                {
                    sunLight.color = new Color(1f, 0.98f, 0.92f);
                    sunLight.intensity = 1.3f;
                }
            }
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.65f, 0.68f, 0.74f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.50f, 0.54f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.35f, 0.38f);
        RenderSettings.ambientIntensity = 1.15f;
        if (music && BGM != null && BGM.Length > 0)
        {
            music.clip = BGM[stage == 2 && BGM.Length > 1 ? 1 : 0]; music.loop = true;
            music.volume = 0.5f; music.mute = !TCSave.Music; music.Play();
        }
        TCServices.Banner(true);
        TCServices.Log("run_start");
    }

    /// Called by the UI after the "tap to start"/tutorial.
    public void BeginRun()
    {
        p.paused = false;
        if (p.anim) p.anim.SetBool("started", true);
        if (TCSave.Upgrade(4) > 0 && spider) spider.StartBoost(1.5f + 1.5f * TCSave.Upgrade(4)); // Head Start (was invisible: no anim/FX)
    }

    public void addCoin()
    {
        coinsCollected += CoinValue;
    }

    public static int CoinValue => TCConfig.I.coinValue * (1 + TCSave.Upgrade(3));
    public void SetMusic(bool on) { if (music) music.mute = !on; }

    public void Pause(bool on)
    {
        if (runEnded) return;
        Time.timeScale = on ? 0 : 1;
        if (music) { if (on) music.Pause(); else music.UnPause(); }
    }

    void OnApplicationPause(bool paused) { if (paused && p && !p.paused && !p.GameOver) TCGameUI.Instance?.ShowPause(); }

    public void OnPlayerDied() { StartCoroutine(DeathRoutine()); }
    IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(1.2f);
        TCGameUI.Instance?.ShowGameOver(!revivedThisRun);
    }

    public void Revive()
    {
        if (revivedThisRun || runEnded || !p.GameOver) return; // guard: ad callback + timer race could revive twice / after results
        revivedThisRun = true;
        p.GameOver = false;
        p.paused = true;
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
        int bonus = metres / 100 * TCConfig.I.coinsPerHundredMetres;
        RunReward = (coinsCollected + bonus) * (doubled ? 2 : 1);
        RunGems = TCConfig.I.gemEveryMetres > 0 ? metres / TCConfig.I.gemEveryMetres : 0;
        if (RunGems > 0) TCSave.Gems += RunGems;
        TCSave.Coins += RunReward;
        TCSave.LifetimeCoins += RunReward;
        TCSave.Runs++;
        if (metres > TCSave.Best) TCSave.Best = metres;
        TCSkins.CheckAchievements();
        TCServices.Log("run_end", "metres", metres);
        return RunReward;
    }

    /// x2 coins reward: may only be granted once per run, after the run is banked.
    public bool ClaimDouble()
    {
        if (!runEnded || doubledClaimed || RunReward <= 0) return false;
        doubledClaimed = true; TCSave.Coins += RunReward; TCSave.LifetimeCoins += RunReward; return true;
    }

    public int DistanceBonus => (int)p.currentScore / 100 * TCConfig.I.coinsPerHundredMetres;
    public int Metres => p ? (int)p.currentScore : 0;

    public void Restart() { MaybeInterstitial(); Time.timeScale = 1; SceneManager.LoadScene("gameplay"); }
    public void Home() { MaybeInterstitial(); Time.timeScale = 1; SceneManager.LoadScene("MainMenu"); }
    void MaybeInterstitial()
    {
        int n = Mathf.Max(1, TCConfig.I.interstitialEveryNRuns);
        if (TCSave.Runs > 0 && TCSave.Runs % n == 0) TCServices.Interstitial();
    }
}
