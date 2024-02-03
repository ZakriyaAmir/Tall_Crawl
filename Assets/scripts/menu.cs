using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class menu : MonoBehaviour
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

    public AudioClip music;
    public int musicIndex;
    private AudioSource audioSource1;
    private player p;
    public Button pauseBtn;
    public Button respawnBtn;


    private void Awake()
    {
        p = GameObject.Find("player").GetComponent<player>();
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

    // Use this for initialization
    void Start()
    {
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
                break;

            default:
                muse = BGM[0];
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
        GameObject.Find("spider").GetComponent<Animator>().SetBool("started", true);
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
        adsplugin.instance.ShowRewardedAd();
    }

    public void RespawnPlayer() 
    {
        respawnBtn.gameObject.SetActive(false);
        FindObjectOfType<playerInteract>().increaseHealth();
        p.transform.Find("spider").GetComponent<Animator>().SetTrigger("respawn");
        p.transform.Find("spider").GetComponent<Animator>().SetBool("death",false);
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