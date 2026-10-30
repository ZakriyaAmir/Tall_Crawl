using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Keeps a shadow image glued under a button (so the button's own press-scale doesn't move it).
public class TCFollowRect : MonoBehaviour
{
    public RectTransform target; public Vector2 offset; RectTransform me;
    void LateUpdate()
    {
        if (!target) { Destroy(gameObject); return; }
        if (!me) me = (RectTransform)transform;
        gameObject.SetActive(true);
        me.anchorMin = target.anchorMin; me.anchorMax = target.anchorMax; me.pivot = target.pivot;
        me.sizeDelta = target.sizeDelta; me.anchoredPosition = target.anchoredPosition + offset;
        me.localScale = target.localScale;
        var tImg = target.GetComponent<Graphic>(); var mine = GetComponent<Graphic>();
        if (tImg && mine) mine.enabled = tImg.isActiveAndEnabled;
    }
}
