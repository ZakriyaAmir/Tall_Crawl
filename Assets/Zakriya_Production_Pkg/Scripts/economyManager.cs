using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class economyManager : MonoBehaviour
{
    public int initialMoney;
    public GameObject ErrorPanel;
    public GameObject confirmationPanel;
    public Button acceptButton;
    public Button declineButton;
    public int money;

    private TaskCompletionSource<bool> userChoiceTaskCompletionSource;

    public static economyManager Instance;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        if (Instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void updateAmount() 
    {
        money = PlayerPrefs.GetInt("money", 0);

        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            //mainMenu.Instance.updateMoney();
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (PlayerPrefs.GetInt("firstTime", 1) == 1)
        {
            PlayerPrefs.SetInt("firstTime", 0);
            addMoney(initialMoney);
        }

        updateAmount();
    }

    public void addMoney(int amount)
    {
        PlayerPrefs.SetInt("money", PlayerPrefs.GetInt("money", 0) + amount);

        updateAmount();
    }

    public async Task<bool> DeductMoneyAsync(int amount)
    {
        if (PlayerPrefs.GetInt("money", 0) < amount)
        {
            Debug.Log("Insufficient funds. You don't have enough money.");
            ErrorPanel.SetActive(true);
            return false; // Money deduction failed
        }

        // Display UI with Yes/No buttons and listen for clicks
        confirmationPanel.SetActive(true);

        // Wait for user choice
        bool userChoice = await WaitForUserChoice();

        // Deduct money based on user choice
        if (userChoice)
        {
            DeductMoney(amount);
        }

        // Hide UI or perform other actions
        confirmationPanel.SetActive(false);
        return userChoice;
    }

    private void DeductMoney(int amount)
    {
        PlayerPrefs.SetInt("money", PlayerPrefs.GetInt("money", 0) - amount);
        updateAmount();
    }

    private async Task<bool> WaitForUserChoice()
    {
        userChoiceTaskCompletionSource = new TaskCompletionSource<bool>();

        acceptButton.onClick.AddListener(() => UserClicked(true));
        declineButton.onClick.AddListener(() => UserClicked(false));

        while (!userChoiceTaskCompletionSource.Task.IsCompleted)
        {
            await Task.Yield(); // Yield to the Unity main thread
        }

        return userChoiceTaskCompletionSource.Task.Result;
    }

    private void UserClicked(bool choice)
    {
        // Set the result of the task when the user clicks a button
        try
        {
            userChoiceTaskCompletionSource.SetResult(choice);
        }
        catch 
        {
        
        }
    }
}
