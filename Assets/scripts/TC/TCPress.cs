using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TCPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    Vector3 initialScale = Vector3.one;
    Vector3 target = Vector3.one;
    bool hasInitial;

    void Awake()
    {
        initialScale = transform.localScale;
        target = initialScale;
        hasInitial = true;
    }

    void OnEnable()
    {
        if (!hasInitial)
        {
            initialScale = transform.localScale;
            hasInitial = true;
        }
        target = initialScale;
    }

    public void OnPointerDown(PointerEventData e) => target = initialScale * 0.92f;
    public void OnPointerUp(PointerEventData e) => target = initialScale;
    public void OnPointerExit(PointerEventData e) => target = initialScale;
    void OnDisable() { if (hasInitial) transform.localScale = initialScale; target = initialScale; }
    void Update() { transform.localScale = Vector3.Lerp(transform.localScale, target, 1 - Mathf.Exp(-25 * Time.unscaledDeltaTime)); }
}
