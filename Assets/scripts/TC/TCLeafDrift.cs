using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// polish3: palette-tinted leaves drifting slowly down the menu background (tan, coral, crimson, cream, brown).
/// Spawned at runtime under the background, behind every button; unscaled time; no raycasts.
public class TCLeafDrift : MonoBehaviour
{
    public int count = 10;
    readonly List<(RectTransform rt, float speed, float sway, float spin, float phase)> leaves = new List<(RectTransform, float, float, float, float)>();
    void Start()
    {
        if (!Application.isPlaying) return;
        var spr = TCTheme.Spr("ui_leaf"); if (!spr) return;
        Color[] cols = { TCUI.Tan, TCUI.Coral, TCUI.Crimson, TCUI.Cream, TCUI.Brown, TCUI.Tan, TCUI.Coral };
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Leaf" + i, typeof(RectTransform)); go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            var img = go.AddComponent<Image>(); img.sprite = spr; img.raycastTarget = false; img.preserveAspect = true;
            var c = cols[i % cols.Length]; c.a = Random.Range(0.55f, 0.85f); img.color = c;
            var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(TCUI.Ink.r, TCUI.Ink.g, TCUI.Ink.b, 0.25f); sh.effectDistance = new Vector2(3, -5);
            float s = Random.Range(34f, 70f); rt.sizeDelta = new Vector2(s, s * 1.5f);
            var a = new Vector2(Random.value, Random.Range(0f, 1.1f)); rt.anchorMin = rt.anchorMax = a;
            leaves.Add((rt, Random.Range(0.018f, 0.04f), Random.Range(20f, 60f), Random.Range(-40f, 40f), Random.value * 10f));
        }
    }
    void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        for (int i = 0; i < leaves.Count; i++)
        {
            var l = leaves[i]; if (!l.rt) continue;
            var a = l.rt.anchorMin; a.y -= l.speed * dt;
            if (a.y < -0.08f) { a.y = 1.08f; a.x = Random.value; }
            l.rt.anchorMin = l.rt.anchorMax = a;
            l.rt.anchoredPosition = new Vector2(Mathf.Sin(t * 0.8f + l.phase) * l.sway, 0);
            l.rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.6f + l.phase) * 35f + l.spin * Mathf.Sin(t * 0.25f + l.phase));
        }
    }
}
