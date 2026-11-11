using UnityEngine;

/// polish2: glossy light sweep across a chunky button every few seconds (the "tap me" hint of hypercasual
/// CTAs). The Shine child + Mask are baked into the scene; this only moves the band.
public class TCShine : MonoBehaviour
{
    public RectTransform band;
    public float interval = 2.6f, sweep = 0.55f, delay = 0.8f;
    float t;
    void OnEnable() { t = -delay; Place(-1); }
    void Place(float k)
    {
        if (!band) return;
        var r = (RectTransform)transform; float w = r.rect.width * 0.5f + band.rect.width;
        band.anchoredPosition = new Vector2(Mathf.Lerp(-w, w, k), 0);
    }
    void Update()
    {
        if (!band) return;
        t += Time.unscaledDeltaTime;
        if (t > interval) t -= interval;
        float k = t < 0 ? 0 : Mathf.Clamp01(t / sweep);
        Place(t < 0 || t > sweep ? (t > sweep ? 1 : 0) : TCUI.EaseOut(k));
        band.gameObject.SetActive(t >= 0 && t <= sweep);
    }
}
