using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class showads : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        ////adsplugin.instance.RequestBanner();
        adsplugin.instance.RequestInterstitial();
        //adsplugin.instance.Showinterstial();
        adsplugin.instance.CreateAndLoadRewardedAd();
        //adsplugin.instance.ShowRewardedAd();
    }

    public void Banner() 
    {
        adsplugin.instance.RequestBanner();
    }
    public void Init()
    {
        adsplugin.instance.Showinterstial();
    }
    public void Rewarded() 
    {
        adsplugin.instance.ShowRewardedAd();
    }
}
