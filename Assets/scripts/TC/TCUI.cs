using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Tiny runtime UI kit: pastel palette, generated rounded sprites & icons, buttons with press
/// feedback, and fade/slide/pop panel transitions (unscaled time, so they work while paused).
public static class TCUI
{
    public static readonly Color Cream = Hex("#FFF7EE"), Cocoa = Hex("#4A3B3F"), Mint = Hex("#8FDCC4"), Peach = Hex("#FFB494"),
        Blush = Hex("#FFC2D1"), Sky = Hex("#9FD3FF"), Butter = Hex("#FFE08A"), Lilac = Hex("#CFC6F5"), White = Color.white,
        Dim = new Color(0.16f, 0.12f, 0.14f, 0.55f), Grey = Hex("#D9D2CC");
    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static Sprite _round, _circle;
    public static Sprite Round { get { if (!_round) _round = MakeRound(64, 26); return _round; } }
    public static Sprite Circle { get { if (!_circle) _circle = MakeRound(128, 64, false); return _circle; } }

    static Sprite MakeRound(int size, int radius, bool slice = true)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect,
            slice ? new Vector4(radius, radius, radius, radius) : Vector4.zero);
    }

    // ---------- Icons (signed-distance drawn, cached) ----------
    static readonly System.Collections.Generic.Dictionary<string, Sprite> icons = new System.Collections.Generic.Dictionary<string, Sprite>();
    public static Sprite Icon(string name)
    {
        if (icons.TryGetValue(name, out var s)) return s;
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / N * 2 - 1, (y + 0.5f) / N * 2 - 1);
                px[y * N + x] = Shade(name, p);
            }
        tex.SetPixels(px); tex.Apply(false, true);
        s = Sprite.Create(tex, new Rect(0, 0, N, N), Vector2.one * 0.5f);
        icons[name] = s; return s;
    }
    static Color Fill(float d, Color c) { c.a *= Mathf.Clamp01(-d * 64f + 0.5f); return c; }
    static Color Over(Color a, Color b) { float o = b.a + a.a * (1 - b.a); if (o < 1e-4f) return Color.clear; var r = (b * b.a + a * a.a * (1 - b.a)) / o; r.a = o; return r; }
    static Color Shade(string n, Vector2 p)
    {
        Color outline = Cocoa;
        switch (n)
        {
            case "coin":
            {
                float d = p.magnitude - 0.9f;
                var c = Over(Fill(d, Hex("#E0A93B")), Fill(p.magnitude - 0.72f, Hex("#FFD45E")));
                var bar = new Vector2(Mathf.Abs(p.x) - 0.12f, Mathf.Abs(p.y) - 0.42f);
                return Over(c, Fill(Mathf.Max(bar.x, bar.y), Hex("#F2B544")));
            }
            case "gem":
            {
                float d = Mathf.Abs(p.x) * 1.0f + Mathf.Abs(p.y + 0.05f) * 0.85f - 0.8f;
                var c = Fill(d, Hex("#7FD6F5"));
                return Over(c, Fill(Mathf.Abs(p.x) + Mathf.Abs(p.y - 0.25f) * 1.6f - 0.35f, new Color(1, 1, 1, 0.7f)));
            }
            case "heart":
            {
                var q = new Vector2(Mathf.Abs(p.x), p.y + 0.05f);
                float d = Mathf.Min(Vector2.Distance(q, new Vector2(0.38f, 0.32f)) - 0.42f, Mathf.Max(q.x + q.y * 0.9f - 0.62f + 0.0f, -q.y - 0.95f) );
                d = Mathf.Min(d, (q.x * 0.85f - q.y * 0.6f) > 0 ? 9 : (q.x + -q.y * 0.9f - 0.85f));
                float h = HeartSdf(p);
                return Over(Fill(h, Hex("#FF6F8E")), Fill(Vector2.Distance(p, new Vector2(-0.35f, 0.35f)) - 0.12f, new Color(1, 1, 1, 0.8f)));
            }
            case "heartEmpty": return Fill(HeartSdf(p), new Color(0.3f, 0.25f, 0.27f, 0.35f));
            case "pause":
            {
                var q = new Vector2(Mathf.Abs(Mathf.Abs(p.x) - 0.3f) - 0.13f, Mathf.Abs(p.y) - 0.55f);
                return Fill(Mathf.Max(q.x, q.y), Cocoa);
            }
            case "gear":
            {
                float a = Mathf.Atan2(p.y, p.x); float r = p.magnitude;
                float teeth = 0.72f + 0.14f * Mathf.Clamp(Mathf.Sin(a * 8) * 3, -1, 1);
                return Over(Fill(r - teeth, Cocoa), Fill(r - 0.3f, Color.white));
            }
            case "play":
            {
                float d = Mathf.Max(-p.x - 0.45f, Mathf.Abs(p.y) * 1.3f + p.x * 0.75f - 0.55f);
                return Fill(d, Color.white);
            }
            case "lock":
            {
                var body = new Vector2(Mathf.Abs(p.x) - 0.5f, Mathf.Abs(p.y + 0.25f) - 0.4f);
                float ring = Mathf.Abs(Vector2.Distance(p, new Vector2(0, 0.2f)) - 0.32f) - 0.09f;
                if (p.y < 0.15f) ring = 9;
                return Over(Fill(ring, Cocoa), Fill(Mathf.Max(body.x, body.y), Cocoa));
            }
            case "check":
            {
                float d1 = SegDist(p, new Vector2(-0.55f, 0), new Vector2(-0.15f, -0.4f)) - 0.13f;
                float d2 = SegDist(p, new Vector2(-0.15f, -0.4f), new Vector2(0.6f, 0.45f)) - 0.13f;
                return Fill(Mathf.Min(d1, d2), Color.white);
            }
            case "hand":
            {
                float d = Mathf.Min(Vector2.Distance(p, new Vector2(0, -0.3f)) - 0.42f, SegDist(p, new Vector2(0, -0.2f), new Vector2(0, 0.6f)) - 0.16f);
                return Over(Fill(d - 0.06f, Cocoa), Fill(d, Color.white));
            }
            case "bolt":
            {
                float d = Mathf.Min(SegDist(p, new Vector2(0.25f, 0.8f), new Vector2(-0.25f, 0f)), SegDist(p, new Vector2(-0.25f, 0f), new Vector2(0.25f, 0f)));
                d = Mathf.Min(d, SegDist(p, new Vector2(0.25f, 0f), new Vector2(-0.25f, -0.8f))) - 0.14f;
                return Fill(d, Hex("#FFB13B"));
            }
            case "star":
            {
                float a = Mathf.Atan2(p.y, p.x) + Mathf.PI / 2; float r = p.magnitude;
                float k = Mathf.Cos(Mathf.Floor(0.5f + a / (2 * Mathf.PI / 5)) * (2 * Mathf.PI / 5) - a);
                return Fill(r * k * 1.0f - 0.45f - 0.0f + (1 - k) * 0.6f * 0, Hex("#FFD45E"));
            }
            case "gift":
            {
                var b = new Vector2(Mathf.Abs(p.x) - 0.7f, Mathf.Abs(p.y + 0.15f) - 0.55f);
                var c = Fill(Mathf.Max(b.x, b.y), Hex("#FF8FA8"));
                c = Over(c, Fill(Mathf.Max(Mathf.Abs(p.x) - 0.12f, b.y), Hex("#FFE08A")));
                c = Over(c, Fill(Mathf.Max(Mathf.Abs(p.y - 0.12f) - 0.1f, b.x), Hex("#FFE08A")));
                return Over(c, Fill(Mathf.Min(Vector2.Distance(p, new Vector2(-0.22f, 0.55f)), Vector2.Distance(p, new Vector2(0.22f, 0.55f))) - 0.2f, Hex("#FFE08A")));
            }
            case "bag":
            {
                var b = new Vector2(Mathf.Abs(p.x) - 0.62f, Mathf.Abs(p.y + 0.2f) - 0.55f);
                float handle = Mathf.Abs(Vector2.Distance(p, new Vector2(0, 0.38f)) - 0.3f) - 0.08f; if (p.y < 0.38f) handle = 9;
                return Over(Fill(handle, Cocoa), Fill(Mathf.Max(b.x, b.y), Hex("#FFB494")));
            }
            case "shirt":
            {
                var b = new Vector2(Mathf.Abs(p.x) - 0.45f, Mathf.Abs(p.y + 0.1f) - 0.6f);
                var s = new Vector2(Mathf.Abs(Mathf.Abs(p.x) - 0.6f) - 0.2f, Mathf.Abs(p.y - 0.25f) - 0.25f);
                return Fill(Mathf.Min(Mathf.Max(b.x, b.y), Mathf.Max(s.x, s.y)), Hex("#8FDCC4"));
            }
        }
        return Color.clear;
    }
    static float HeartSdf(Vector2 p)
    {
        p.y = -p.y * 1.1f + 0.15f; p.x = Mathf.Abs(p.x);
        p *= 1.15f;
        if (p.y + p.x > 1.0f) return Mathf.Sqrt(Dot2(p - new Vector2(0.25f, 0.75f))) - Mathf.Sqrt(2.0f) / 4.0f;
        return Mathf.Sqrt(Mathf.Min(Dot2(p - new Vector2(0.00f, 1.00f)), Dot2(p - 0.5f * Mathf.Max(p.x + p.y, 0.0f) * Vector2.one))) * Mathf.Sign(p.x - p.y);
    }
    static float Dot2(Vector2 v) => Vector2.Dot(v, v);
    static float SegDist(Vector2 p, Vector2 a, Vector2 b) { var pa = p - a; var ba = b - a; float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba)); return (pa - ba * h).magnitude; }

    // ---------- Builders ----------
    public static Canvas MakeCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
        var s = go.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1080, 1920); s.matchWidthOrHeight = 0.5f;
        if (!UnityEngine.Object.FindObjectOfType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        return c;
    }

    /// Safe-area root that also leaves room at the bottom for the banner ad.
    public static RectTransform SafeRoot(Transform canvas, float bannerReserve = 170)
    {
        var rt = Rect("Safe", canvas); Stretch(rt);
        var sa = rt.gameObject.AddComponent<TCSafeArea>(); sa.bottomReserve = bannerReserve;
        return rt;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    public static void Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t); }
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    { rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size; return rt; }

    public static Image Img(string name, Transform parent, Color c, Sprite s = null)
    {
        var rt = Rect(name, parent); var i = rt.gameObject.AddComponent<Image>(); i.sprite = s ? s : Round; i.color = c;
        if (i.sprite == Round) i.type = Image.Type.Sliced; else i.preserveAspect = true;
        i.raycastTarget = false; return i;
    }

    public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color c, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rt = Rect(name, parent); var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (TCAssets.I.font) t.font = TCAssets.I.font;
        t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.raycastTarget = false; t.enableWordWrapping = true;
        t.fontStyle = FontStyles.Bold;
        return t;
    }

    /// Chunky pastel button: drop "shadow" layer + face + label; squish on press, click sound.
    public static Button Btn(string name, Transform parent, string text, Color face, Action onClick, float fontSize = 52, Sprite icon = null)
    {
        var rt = Rect(name, parent);
        var shadow = Img("Shadow", rt, Color.Lerp(face, Cocoa, 0.35f)); Stretch(shadow.rectTransform, 0, 0, 10, -10);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = Round; img.type = Image.Type.Sliced; img.color = face;
        var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img;
        var cb = b.colors; cb.pressedColor = new Color(0.92f, 0.92f, 0.92f); cb.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.7f); b.colors = cb;
        shadow.transform.SetAsFirstSibling();
        // face is the root image; shadow sits behind via a separate child under it, so we render shadow first using a nested canvas trick-free approach:
        shadow.transform.SetParent(rt.parent, false); shadow.transform.SetSiblingIndex(rt.GetSiblingIndex());
        shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var follow = shadow.gameObject.AddComponent<TCFollowRect>(); follow.target = rt; follow.offset = new Vector2(0, -10);
        if (icon)
        {
            var ic = Img("Icon", rt, Color.white, icon);
            if (string.IsNullOrEmpty(text)) { Stretch(ic.rectTransform, 22, 22, 22, 22); }
            else { ic.rectTransform.anchorMin = new Vector2(0, 0.5f); ic.rectTransform.anchorMax = new Vector2(0, 0.5f); ic.rectTransform.sizeDelta = new Vector2(fontSize * 1.3f, fontSize * 1.3f); ic.rectTransform.anchoredPosition = new Vector2(fontSize * 1.1f, 0); }
        }
        if (!string.IsNullOrEmpty(text))
        {
            var l = Label("Text", rt, text, fontSize, Cocoa); Stretch(l.rectTransform, icon ? fontSize * 1.9f : 16, 16, 6, 6);
            l.enableAutoSizing = true; l.fontSizeMax = fontSize; l.fontSizeMin = fontSize * 0.5f;
        }
        rt.gameObject.AddComponent<TCPress>();
        b.onClick.AddListener(() => { TCAudio.Click(); onClick?.Invoke(); });
        return b;
    }

    public static CanvasGroup Panel(string name, Transform parent, out RectTransform card, Vector2 size, string title)
    {
        var root = Rect(name, parent); Stretch(root);
        var cg = root.gameObject.AddComponent<CanvasGroup>();
        var dim = Img("Dim", root, Dim); Stretch(dim.rectTransform); dim.raycastTarget = true; dim.sprite = null; dim.type = Image.Type.Simple;
        card = Img("Card", root, Cream).rectTransform; Place(card, new Vector2(0.5f, 0.5f), new Vector2(0, 0), size);
        card.GetComponent<Image>().raycastTarget = true;
        var head = Img("Header", card, Peach); var hr = head.rectTransform; hr.anchorMin = new Vector2(0.5f, 1); hr.anchorMax = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(size.x * 0.7f, 120); hr.anchoredPosition = new Vector2(0, 0);
        var ht = Label("Title", hr, title, 64, Cocoa); Stretch(ht.rectTransform);
        root.gameObject.SetActive(false);
        return cg;
    }

    // ---------- Transitions ----------
    public static void Show(MonoBehaviour host, CanvasGroup cg, bool pop = true)
    {
        cg.gameObject.SetActive(true); cg.transform.SetAsLastSibling();
        host.StartCoroutine(Anim(cg, true, pop));
    }
    public static void Outline(TMP_Text t, float w)
    {
        try { var m = t.fontMaterial; m.EnableKeyword("OUTLINE_ON"); m.SetFloat("_OutlineWidth", w); m.SetColor("_OutlineColor", Cocoa); }
        catch (Exception) { }
    }
    public static void Hide(MonoBehaviour host, CanvasGroup cg, Action done = null)
    {
        if (!cg.gameObject.activeSelf) { done?.Invoke(); return; }
        host.StartCoroutine(Anim(cg, false, true, done));
    }
    static IEnumerator Anim(CanvasGroup cg, bool show, bool pop, Action done = null)
    {
        var card = cg.transform.Find("Card");
        cg.interactable = false; cg.blocksRaycasts = true;
        float dur = show ? 0.32f : 0.18f;
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur;
            cg.alpha = show ? EaseOut(k) : 1 - k;
            if (card && pop) card.localScale = Vector3.one * (show ? Mathf.LerpUnclamped(0.8f, 1f, EaseOutBack(k)) : Mathf.Lerp(1f, 0.9f, k));
            yield return null;
        }
        cg.alpha = show ? 1 : 0; if (card) card.localScale = Vector3.one;
        cg.interactable = show; cg.blocksRaycasts = show;
        if (!show) cg.gameObject.SetActive(false);
        done?.Invoke();
    }
    public static float EaseOut(float k) => 1 - (1 - k) * (1 - k);
    public static float EaseOutBack(float k) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(k - 1, 3) + c1 * Mathf.Pow(k - 1, 2); }

    public static IEnumerator Punch(Transform t, float amount = 0.2f, float dur = 0.25f)
    {
        for (float x = 0; x < dur; x += Time.unscaledDeltaTime) { if (!t) yield break; t.localScale = Vector3.one * (1 + amount * Mathf.Sin(x / dur * Mathf.PI)); yield return null; }
        if (t) t.localScale = Vector3.one;
    }
}

public class TCPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    Vector3 target = Vector3.one;
    public void OnPointerDown(PointerEventData e) => target = Vector3.one * 0.92f;
    public void OnPointerUp(PointerEventData e) => target = Vector3.one;
    public void OnPointerExit(PointerEventData e) => target = Vector3.one;
    void OnDisable() { transform.localScale = Vector3.one; target = Vector3.one; }
    void Update() { transform.localScale = Vector3.Lerp(transform.localScale, target, 1 - Mathf.Exp(-25 * Time.unscaledDeltaTime)); }
}

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
