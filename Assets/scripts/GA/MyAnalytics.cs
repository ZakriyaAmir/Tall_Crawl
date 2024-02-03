using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameAnalyticsSDK;

public class MyAnalytics : MonoBehaviour
{
    public static MyAnalytics instance;
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        GameAnalytics.Initialize();
    }

    public void LevelPassEvent(string LevelNumber)
    {
        GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete,"Level"+LevelNumber+"Completed");
    }

    public void LevelFailedEvent(string LevelNumber) {

        GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, "Level" + LevelNumber + "Failed");

    }

    public void RewardedFailedAd(string AdID) {

        GameAnalytics.NewAdEvent(GAAdAction.FailedShow, GAAdType.RewardedVideo, "admob", AdID);
    }

    public void RewardCollected(string AdID) {
        GameAnalytics.NewAdEvent(GAAdAction.RewardReceived, GAAdType.RewardedVideo, "admob", AdID);
    }

    public void BuyEvent(string ItemName) {

        GameAnalytics.NewDesignEvent(ItemName+" Buyed");

    }
    public void ScreenEvent(string ScreenName) {
        GameAnalytics.NewDesignEvent(ScreenName + " Opened");
    }
}
