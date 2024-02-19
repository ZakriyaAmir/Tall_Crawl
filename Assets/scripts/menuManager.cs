using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class menuManager : MonoBehaviour
{
	
	public bool paused;
	public GameObject OptionsScreen;
	public Transform soundOn;
	public Transform soundOff;
	public Transform hapticOff;
	public Transform hapticOn;
	public Text topScore;
    public GameObject consentPanel;
    public int totalCoins;
    public Text TotalcoinsText;

    void Awake()
	{
		Application.targetFrameRate = 120;

        totalCoins = PlayerPrefs.GetInt("coins", 0);
        TotalcoinsText.text = totalCoins.ToString();
        /*if (PlayerPrefs.GetInt("notfirsttime", 0) == 0)
        {
            TutorialPanel.SetActive(true);
            Time.timeScale = 0;
        }*/
        paused = false;
		//pauseScreen.SetActive(false);

		//Disable stuff on start keep the tagg stuff in the end of this function to avoid execution stop when error occured
		if (PlayerPrefs.GetInt("DisableSound") == 1)
		{
			AudioListener.volume = 0;
		}

		if (SceneManager.GetActiveScene().name == "MainMenu")
		{
			topScore.text = PlayerPrefs.GetInt("highscore").ToString();
		}
	}

    private void Start()
    {
        if (PlayerPrefs.GetInt("consent", 0) == 0)
        {
            consentPanel.SetActive(true);
        }
        else
        {
            AdsManager.Instance.Invoke("RunBannerAd", 4f);
        }
    }

    public void acceptConsent()
    {
        PlayerPrefs.SetInt("consent", 1);
        consentPanel.SetActive(false);
        AdsManager.Instance.Invoke("RunBannerAd", 4f);
    }

    public void rejectConsent()
    {
        PlayerPrefs.SetInt("consent", 0);
        Application.Quit();
    }
    public void OpenURL(string url)
    {
        Application.OpenURL(url);
    }

    public void exitgamebtn() {
		Application.Quit();
	}
	
	public void startgameBtn() {
		StartCoroutine(DelayStart());
	}
	
	IEnumerator DelayStart()
	{
		yield return  new WaitForSeconds(1.2f);
		SceneManager.LoadScene("gameplay");
		Time.timeScale = 1;
	}

	public void backmenubtn() {
		SceneManager.LoadScene(0);
	}
	
	/*public void restartgamebtn()
	{
		SceneManager.LoadScene("gameplay");
		Time.timeScale = 1;
	}*/

	public void OptionsScreenOn()
	{
		OptionsScreen.SetActive(true);
		SoundCheck();
		HapticCheck();
	}
	
	public void OptionsScreenOff()
	{
		OptionsScreen.SetActive(false);
	}
	
	public void SoundCheck()
	{
		if (PlayerPrefs.GetInt("DisableSound") == 1)
		{
			soundOff.GetComponent<Image>().color = Color.green;
			//soundOff.GetChild(0).GetComponent<Text>().color = Color.green;
			soundOn.GetComponent<Image>().color = Color.red;
			//soundOn.GetChild(0).GetComponent<Text>().color = Color.red;
		}
		else
		{
			soundOn.GetComponent<Image>().color = Color.green;
			//soundOn.GetChild(0).GetComponent<Text>().color = Color.green;
			soundOff.GetComponent<Image>().color = Color.red;
			//soundOff.GetChild(0).GetComponent<Text>().color = Color.red;
		}
	}
	
	public void EnableSound()
	{
		PlayerPrefs.SetInt("DisableSound", 0);
		SoundCheck();
		AudioListener.volume = 1;
	}
	
	public void DisableSound()
	{
		PlayerPrefs.SetInt("DisableSound", 1);
		SoundCheck();
		AudioListener.volume = 0;

	}
	
	public void HapticCheck()
	{
		if (PlayerPrefs.GetInt("DisableHaptic",1) == 1)
		{
			hapticOff.GetComponent<Image>().color = Color.green;
			//hapticOff.GetChild(0).GetComponent<Text>().color = Color.green;
			hapticOn.GetComponent<Image>().color = Color.red;
			//hapticOn.GetChild(0).GetComponent<Text>().color = Color.red;
		}
		else
		{
			hapticOn.GetComponent<Image>().color = Color.green;
			//hapticOn.GetChild(0).GetComponent<Text>().color = Color.green;
			hapticOff.GetComponent<Image>().color = Color.red;
			//hapticOff.GetChild(0).GetComponent<Text>().color = Color.red;
		}
	}
	
	public void EnableHaptic()
	{
		PlayerPrefs.SetInt("DisableHaptic", 0);
		HapticCheck();
	}
	
	public void DisableHaptic()
	{
		PlayerPrefs.SetInt("DisableHaptic", 1);
		HapticCheck();
	}
}
