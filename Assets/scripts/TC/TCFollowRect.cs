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
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        Vector2 targetPos = target.anchoredPosition + offset;
        if (me.anchoredPosition != targetPos) me.anchoredPosition = targetPos;
        if (me.sizeDelta != target.sizeDelta) me.sizeDelta = target.sizeDelta;
        if (me.localScale != target.localScale) me.localScale = target.localScale;
        if (me.pivot != target.pivot) me.pivot = target.pivot;
        if (me.anchorMin != target.anchorMin) me.anchorMin = target.anchorMin;
        if (me.anchorMax != target.anchorMax) me.anchorMax = target.anchorMax;

        var tImg = target.GetComponent<Graphic>();
        var mine = GetComponent<Graphic>();
        if (tImg && mine && mine.enabled != tImg.isActiveAndEnabled)
            mine.enabled = tImg.isActiveAndEnabled;
    }
}
