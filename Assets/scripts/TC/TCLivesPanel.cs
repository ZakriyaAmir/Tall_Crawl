using UnityEngine;
using UnityEngine.UI;

/// HUD lives panel (polish2). Baked into gameplay.unity under TC_GameUI/Safe/Top/Hearts by
/// Tools > Tall Crawl > Polish2 > Bake Lives Panel, so layout/colours are editable in the scene.
/// Runtime only animates: staggered heartbeat, danger pulse + red glow on the last heart,
/// heart-break (ghost flies off, slot squashes, panel shakes) and a bouncy refill pop.
public class TCLivesPanel : MonoBehaviour
{
    public Image background;
    public RectTransform[] slots;   // Slot{i}
    public Image[] hearts;          // Slot{i}/Heart
    public Image[] glows;           // Slot{i}/Glow
    public Image[] ghosts;          // Slot{i}/Ghost
    public Sprite full, empty;
    [Tooltip("Resize the background to the number of heart slots in use.")] public bool autoWidth = true;
    public float slotSpacing = 62f, padding = 22f;
    public Color glowDanger = new Color(1f, 0.25f, 0.3f, 0.85f), glowGain = new Color(0.55f, 1f, 0.7f, 0.9f);

    int life = -1, max = -1; float shake, dangerT;
    float[] popT, breakT; Vector2 bgBasePos; Color bgBaseColor; bool inited;

    void Init()
    {
        if (inited) return; inited = true;
        int n = slots != null ? slots.Length : 0;
        popT = new float[n]; breakT = new float[n];
        for (int i = 0; i < n; i++) { popT[i] = breakT[i] = 99f; }
        if (background) { bgBasePos = background.rectTransform.anchoredPosition; bgBaseColor = background.color; }
    }

    public void SetLives(int newLife, int newMax)
    {
        Init();
        newMax = Mathf.Clamp(newMax, 0, slots.Length);
        bool first = life < 0;
        if (!first)
        {
            for (int i = 0; i < newMax; i++)
            {
                bool was = i < life, now = i < newLife;
                if (was && !now) { breakT[i] = 0f; shake = 0.35f; }
                if (!was && now) popT[i] = 0f;
            }
        }
        life = newLife; max = newMax;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i]) slots[i].gameObject.SetActive(i < max);
            if (hearts[i]) hearts[i].sprite = i < life ? full : empty;
        }
        if (autoWidth && background && max > 0)
        {
            var r = background.rectTransform;
            r.sizeDelta = new Vector2(padding * 2 + slotSpacing * max, r.sizeDelta.y);
        }
    }

    static float Beat(float t, float period)
    {
        float p = Mathf.Repeat(t, period) / period;          // lub-dub
        return Mathf.Exp(-Mathf.Pow((p - 0.08f) / 0.05f, 2)) + 0.6f * Mathf.Exp(-Mathf.Pow((p - 0.24f) / 0.05f, 2));
    }

    void Update()
    {
        if (!inited || slots == null) return;
        float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
        bool danger = life == 1 && max > 1;
        dangerT = danger ? dangerT + dt : 0f;
        for (int i = 0; i < slots.Length && i < max; i++)
        {
            var h = hearts[i]; if (!h) continue;
            if (breakT[i] < 1f) breakT[i] += dt;
            if (popT[i] < 1f) popT[i] += dt;
            float s = 1f;
            bool isFull = i < life;
            if (isFull) s += danger ? 0.2f * Beat(t, 0.62f) : 0.07f * Beat(t - i * 0.12f, 1.35f);
            // refill pop
            if (popT[i] < 0.5f) { float k = Mathf.Clamp01(popT[i] / 0.45f); s *= Mathf.LerpUnclamped(0f, 1f, TCUI.EaseOutBack(k)) * (1 + 0.25f * Mathf.Sin(k * Mathf.PI)); }
            // break squash
            float rotZ = 0;
            if (breakT[i] < 0.45f) { float k = Mathf.Clamp01(breakT[i] / 0.4f); s *= 1f + 0.35f * Mathf.Sin(k * Mathf.PI) * (1 - k); rotZ = Mathf.Sin(k * 30f) * 18f * (1 - k); }
            h.rectTransform.localScale = new Vector3(s, s, 1);
            h.rectTransform.localRotation = Quaternion.Euler(0, 0, rotZ);

            // ghost: full heart flying off and fading when one is lost
            var g = ghosts != null && i < ghosts.Length ? ghosts[i] : null;
            if (g)
            {
                bool on = breakT[i] < 0.6f;
                if (g.enabled != on) g.enabled = on;
                if (on)
                {
                    float k = Mathf.Clamp01(breakT[i] / 0.6f);
                    g.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(i * 2.3f + 1) * 30f * k, 70f * k - 90f * k * k);
                    g.rectTransform.localScale = Vector3.one * (1f + 0.8f * k);
                    g.rectTransform.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 1 : -1) * 60f * k);
                    g.color = new Color(1, 1, 1, 1 - k);
                }
            }
            // glow: red danger pulse on the last heart, green flash on refill
            var gl = glows != null && i < glows.Length ? glows[i] : null;
            if (gl)
            {
                Color c = Color.clear;
                if (danger && i == 0) { c = glowDanger; c.a *= 0.35f + 0.65f * Beat(t, 0.62f); }
                if (popT[i] < 0.5f) { var gg = glowGain; gg.a *= 1 - popT[i] / 0.5f; c = gg; }
                gl.color = c; gl.enabled = c.a > 0.01f;
                gl.rectTransform.localScale = Vector3.one * (1.1f + 0.3f * c.a);
            }
        }
        if (background)
        {
            var r = background.rectTransform;
            if (shake > 0) { shake -= dt; r.anchoredPosition = bgBasePos + new Vector2(Mathf.Sin(t * 70f) * 9f, Mathf.Cos(t * 55f) * 4f) * Mathf.Clamp01(shake / 0.35f); }
            else if (r.anchoredPosition != bgBasePos) r.anchoredPosition = bgBasePos;
            background.color = danger ? Color.Lerp(bgBaseColor, new Color(1f, 0.82f, 0.84f, bgBaseColor.a), 0.5f + 0.5f * Beat(t, 0.62f)) : bgBaseColor;
        }
    }
}
