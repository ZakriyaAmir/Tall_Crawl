using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TCPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    Vector3 target = Vector3.one;
    public void OnPointerDown(PointerEventData e) => target = Vector3.one * 0.92f;
    public void OnPointerUp(PointerEventData e) => target = Vector3.one;
    public void OnPointerExit(PointerEventData e) => target = Vector3.one;
    void OnDisable() { transform.localScale = Vector3.one; target = Vector3.one; }
    void Update() { transform.localScale = Vector3.Lerp(transform.localScale, target, 1 - Mathf.Exp(-25 * Time.unscaledDeltaTime)); }
}
