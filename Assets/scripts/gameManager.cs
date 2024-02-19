using Firebase.Analytics;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class gameManager : MonoBehaviour
{
    public AudioClip[] bgMusic;
    public Material[] bgImage;
    public AudioClip[] BGM;
    public Sprite[] stagetoload;
    public GameObject LoadingScreen;
    public GameObject TutorialScreen;
    public GameObject PauseScreen;
    public GameObject loseScreen;
    public GameObject confirmScreen;
    public Camera camera;
    public bool cantplay;
    public int coinsCollected;
    public int totalCoins;
    public Text CollectedCoinsText;
    public Text TotalcoinsText;

    public AudioClip music;
    public int musicIndex;
    private AudioSource audioSource1;
    private player p;
    public Button pauseBtn;
    public Button respawnBtn;
    public static gameManager Instance;
    public GameObject Sun;

    private void Awake()
    {
        Application.targetFrameRate = 120;

        totalCoins = PlayerPrefs.GetInt("coins", 0);

        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        //DontDestroyOnLoad(gameObject);

        /*PauseScreen.SetActive(false);
        confirmScreen.SetActive(false);
        LoadingScreen.GetComponent<Image>().sprite = stagetoload[PlayerPrefs.GetInt("Difficulty")];
        LoadingScreen.SetActive(true);
        //Show Tutorial
        if (PlayerPrefs.GetInt("notnew") == 0)
        {
            TutorialScreen.SetActive(true);
            cantplay = true;
            Time.timeScale = 0;
        }*/
    }

    public void addCoin() 
    {
        coinsCollected++;
        PlayerPrefs.SetInt("coins", PlayerPrefs.GetInt("coins", 0) + 1);
        CollectedCoinsText.text = coinsCollected.ToString();
    }

    // Use this for initialization
    void Start()
    {
        p = GameObject.Find("player").GetComponent<player>();

        pauseBtn.enabled = false;
        PauseScreen.SetActive(false);
        audioSource1 = transform.GetComponent<AudioSource>();
        p.paused = true;
        StartCoroutine(startGame());
        int RandomStage = Random.Range(0, 5);

        camera.GetComponent<Skybox>().material = bgImage[RandomStage];

        AudioClip muse;
        switch (RandomStage)
        {
            // Select and play BGM
            case 2:
                muse = BGM[1];
                //Hide sun in daytime
                Sun.SetActive(false);
                break;

            default:
                muse = BGM[0];
                //Show sun in daytime
                Sun.SetActive(true);
                break;
        }

        //Play Taunt only if the game not runs for the first time
        playMusic(muse);
    }

    IEnumerator startGame()
    {
        yield return new WaitForSeconds(2);
        p.paused = false;
        pauseBtn.enabled = true;
        p.anim.SetBool("started", true);
    }

    public void playMusic(AudioClip muse)
    {
        music = muse;
        audioSource1.clip = muse;
        audioSource1.Play();
    }

    public void stopMusic()
    {
        audioSource1.Stop();
    }

    public void restartgamebtn()
    {
        SceneManager.LoadScene("gameplay");
        Time.timeScale = 1;
    }

    public void MainMenubtn()
    {
        SceneManager.LoadScene(0);
        Time.timeScale = 1;
    }

    public void pausebtn()
    {
        pauseBtn.enabled = false;
        PauseScreen.SetActive(true);
        //Time.timeScale = 0;
        player p = GameObject.Find("player").GetComponent<player>();
        p.paused = true;
    }

    public void resumebtn()
    {
        pauseBtn.enabled = true;
        PauseScreen.SetActive(false);
        Time.timeScale = 1;
        player p = GameObject.Find("player").GetComponent<player>();
        p.paused = false;
    }

    private void Update()
    {
        if (!audioSource1.isPlaying)
        {
        }
    }

    public void notfirstitme()
    {
        PlayerPrefs.SetInt("notnew", 1);
        closeTutorial();
    }

    public void closeTutorial()
    {
        cantplay = false;
        TutorialScreen.SetActive(false);
    }

    public void OpenConfirm()
    {
        confirmScreen.SetActive(true);
    }

    public void CancelConfirm()
    {
        confirmScreen.SetActive(false);
    }

    public void PlayRewardedAd() 
    {
        //Play Reward Ad here
        //Initialize Admob reward callback in the script in which you are required to use rewarded ad for admob
        if (AdsManager.Instance.RunRewardedAd(() => grantReward()))
        {
            Debug.Log("Reward Ad Available");
        }

        else
        {
            Debug.Log("Reward Ad Unavailable");
            grantReward();
        }

        //Initialize Max reward callback in the script in which you are required to use rewarded ad for applovin
        //MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;
    }

    public void getRewardInterstitial()
    {
        //Initialize Admob reward callback in the script in which you are required to use rewarded ad for admob
        AdsManager.Instance.RunRewardedInterstitialAd(() => grantReward());
        //Initialize Max reward callback in the script in which you are required to use rewarded ad for applovin
        //MaxSdkCallbacks.RewardedInterstitial.OnAdReceivedRewardEvent += OnRewardedInterstitialAdReceivedRewardEvent;
    }

    //Rewarded sample callback methods for Applovin Max
    /*private void OnRewardedAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
    {
        grantReward();
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnRewardedAdReceivedRewardEvent;
    }
    //Rewarded sample callback methods for Applovin Max
    private void OnRewardedInterstitialAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
    {
        grantReward();
        MaxSdkCallbacks.RewardedInterstitial.OnAdReceivedRewardEvent -= OnRewardedInterstitialAdReceivedRewardEvent;
    }*/

    public void grantReward()
    {
        //Give reward on watching video
        gameManager.Instance.RespawnPlayer();

        //GA Event
        FirebaseAnalytics.LogEvent("Player_Respawned");
    }

    public void RespawnPlayer() 
    {
        respawnBtn.gameObject.SetActive(false);
        FindObjectOfType<playerInteract>().increaseHealth();
        p.anim.SetTrigger("respawn");
        p.anim.SetBool("death",false);
        loseScreen.SetActive(false);

        Invoke("RestartMoving",1);
    }

    public void RestartMoving() 
    {
        p.GameOver = false;
        //Blink spider
        StartCoroutine(FindObjectOfType<playerInteract>().Invincible());
    }
}