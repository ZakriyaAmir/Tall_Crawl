using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TCSafeArea : MonoBehaviour
{
    public float bottomReserve; Rect last; float lastReserve = -1;
    public static bool BannerVisible = true;
    void Update()
    {
        var sa = Screen.safeArea; float reserve = BannerVisible && !TCSave.NoAds ? bottomReserve : 0;
        if (sa == last && reserve == lastReserve) return; last = sa; lastReserve = reserve;
        var rt = (RectTransform)transform; var canvas = GetComponentInParent<Canvas>();
        float scale = canvas ? canvas.scaleFactor : 1;
        Vector2 min = sa.position, max = sa.position + sa.size;
        min.x /= Screen.width; max.x /= Screen.width; min.y /= Screen.height; max.y /= Screen.height;
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = new Vector2(0, reserve); rt.offsetMax = Vector2.zero;
    }
}

/// UI sounds (click etc.) + music toggle helper.
public static class TCAudio
{
    static AudioSource src;
    static AudioSource Src { get { if (!src) { var g = new GameObject("TCAudio"); UnityEngine.Object.DontDestroyOnLoad(g); src = g.AddComponent<AudioSource>(); src.playOnAwake = false; } return src; } }
    public static void Click() { if (TCAssets.I.click) Src.PlayOneShot(TCAssets.I.click, 0.8f); }
    public static void Play(AudioClip c, float v = 1) { if (c) Src.PlayOneShot(c, v); }
}
