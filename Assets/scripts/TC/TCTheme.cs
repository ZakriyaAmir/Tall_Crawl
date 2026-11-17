using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// polish3 palette theme. Five brand colours (Crimson, Cream, Brown, Tan, Coral) plus tints/shades of them:
///   Crimson  - primary CTAs (PLAY, x2, REVIVE, CLAIM ...), ribbon headers, ON states, accents
///   Coral    - secondary buttons (SKINS, SHOP, DAILY, HOME, BUY ...)
///   Tan      - tertiary buttons (BACK, LATER, RESTORE), chips, OFF toggles, inner card borders
///   Cream    - panel cards, HUD chips, text on dark faces
///   Brown    - text (deep shade "Ink"), outlines, borders, drop shadows
/// Every styled button gets: rim outline in a darker shade of its face, a deep-brown drop shadow, a bevel/gloss
/// child (vertical gradient highlight + bottom shade), readable text (auto-contrast), squash press (TCPress).
/// Primary buttons also get the shine sweep. Cards get a gradient, an inner tan border and a crimson ribbon.
/// Applied by TCUI builders to new objects and, in place, by TCPolish3Bake / runtime ThemeTree to baked canvases.
public static class TCTheme
{
    public const int Version = 3;

    public static Color Crimson => TCUI.Crimson;
    public static Color Cream => TCUI.Cream;
    public static Color Brown => TCUI.Brown;
    public static Color Tan => TCUI.Tan;
    public static Color Coral => TCUI.Coral;
    public static Color Ink => TCUI.Ink;

    /// Darker shade of a colour toward deep brown (keeps it in the palette family).
    public static Color Shade(Color c, float k) { var r = Color.Lerp(c, TCUI.Ink, k); r.a = c.a; return r; }
    public static Color Tint(Color c, float k) { var r = Color.Lerp(c, TCUI.CreamLight, k); r.a = c.a; return r; }
    public static Color A(Color c, float a) { c.a = a; return c; }

    // ---------- contrast ----------
    static float Lin(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
    public static float Lum(Color c) => 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
    public static float Contrast(Color a, Color b) { float x = Lum(a), y = Lum(b); return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f); }
    /// Cream text on dark faces (crimson, brown), deep-brown ink on light faces (cream, tan, coral) - whichever reads better.
    public static Color TextOn(Color face)
    {
        if (face.a < 0.25f) return TCUI.Ink; // transparent button sits on a cream card
        return Contrast(face, TCUI.Cream) >= Contrast(face, TCUI.Ink) ? TCUI.Cream : TCUI.Ink;
    }
    public static bool LightText(Color face) => TextOn(face) == TCUI.Cream;

    // ---------- sprites ----------
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    public static Sprite Spr(string n)
    {
        if (sprites.TryGetValue(n, out var s) && s) return s;
        s = Resources.Load<Sprite>("TCUI/" + n);
        if (!s) { var t = Resources.Load<Texture2D>("TCUI/" + n); if (t) s = UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, Border(n)); }
        sprites[n] = s; return s;
    }
    static Vector4 Border(string n) => n == "ui_ribbon" ? new Vector4(90, 0, 90, 0) : n == "ui_bevel" || n == "ui_ring" || n == "ui_cardgrad" ? new Vector4(26, 26, 26, 26) : Vector4.zero;
    public static void ClearCache() => sprites.Clear();

    // ---------- text ----------
    /// Editor bake hook: returns a shared outline material asset (so baked scenes do not hold instance materials).
    public static Func<TMP_FontAsset, float, Material> MatFor;
    public static void StyleText(TMP_Text t, Color col, float outline, bool lightOnDark)
    {
        if (!t) return;
        t.color = col;
        Material m = null;
        if (MatFor != null) m = t.font ? MatFor(t.font, outline) : null;
        else if (TCUI.Baking && TCUI.BakeOutlineMat != null && t.font && t.font == TCAssets.I.font) m = TCUI.BakeOutlineMat(outline);
        if (m) t.fontSharedMaterial = m;
        else if (Application.isPlaying)
        {
            try
            {
                var fm = t.fontMaterial; fm.EnableKeyword("OUTLINE_ON");
                fm.SetFloat(ShaderUtilities.ID_OutlineWidth, outline); fm.SetColor(ShaderUtilities.ID_OutlineColor, TCUI.Ink);
            }
            catch (Exception) { }
        }
        var sh = t.GetComponent<Shadow>();
        if (!sh) sh = t.gameObject.AddComponent<Shadow>();
        // dark drop under light text; soft cream "letterpress" under dark ink
        sh.effectColor = lightOnDark ? A(TCUI.Ink, 0.55f) : A(TCUI.CreamLight, 0.35f);
        sh.effectDistance = lightOnDark ? new Vector2(0, -4) : new Vector2(0, -2);
    }
    public static void TextOnFace(Transform root, Color face, Transform skipUnder = null)
    {
        if (!root) return;
        bool light = LightText(face); var col = TextOn(face);
        foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (skipUnder && t.transform.IsChildOf(skipUnder)) continue;
            var pb = t.GetComponentInParent<Button>(true); // a nested real button keeps its own face's text colour
            if (pb && pb.transform != root && pb.transform.IsChildOf(root) && pb.GetComponent<Image>()) continue;
            StyleText(t, col, light ? 0.26f : 0.12f, light);
        }
    }

    // ---------- helpers ----------
    static T Ensure<T>(GameObject g) where T : Component { var c = g.GetComponent<T>(); return c ? c : g.AddComponent<T>(); }
    static RectTransform Child(Transform parent, string name, int sibling = -1)
    {
        var t = parent.Find(name);
        if (!t)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false); t = go.transform;
            if (TCUI.Baking) go.AddComponent<TCBaked>();
        }
        if (sibling >= 0) t.SetSiblingIndex(Mathf.Min(sibling, parent.childCount - 1));
        return (RectTransform)t;
    }
    static Image Deco(Transform parent, string name, Sprite s, Color c, int sibling, float inset = 0)
    {
        var rt = Child(parent, name, sibling);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset); rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
        var i = Ensure<Image>(rt.gameObject); i.sprite = s; i.type = s && s.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple; i.color = c; i.raycastTarget = false; i.enabled = s != null; // never a flat white box if a sprite is missing
        var le = Ensure<LayoutElement>(rt.gameObject); le.ignoreLayout = true;
        return i;
    }
    public static void Mark(Component c, string role) { if (!c) return; var m = Ensure<TCThemed>(c.gameObject); m.version = Version; m.role = role; }
    public static bool Marked(Component c) { if (!c) return true; var m = c.GetComponent<TCThemed>(); return m && m.version >= Version; }
    static void Effects(Graphic g, Color rim, float rimW, Color drop, float dropY)
    {
        Outline ol = null; Shadow sh = null;
        foreach (var s in g.GetComponents<Shadow>()) { if (s is Outline o) { if (!ol) ol = o; } else if (!sh) sh = s; }
        if (rimW > 0) { if (!ol) ol = g.gameObject.AddComponent<Outline>(); ol.enabled = true; ol.effectColor = rim; ol.effectDistance = new Vector2(rimW, -rimW); ol.useGraphicAlpha = true; }
        else if (ol) ol.enabled = false;
        if (dropY != 0) { if (!sh) sh = g.gameObject.AddComponent<Shadow>(); sh.enabled = true; sh.effectColor = drop; sh.effectDistance = new Vector2(0, -dropY); sh.useGraphicAlpha = true; }
        else if (sh) sh.enabled = false;
    }

    // ---------- buttons ----------
    public static readonly HashSet<string> PrimaryNames = new HashSet<string> { "Play", "Ad", "Double", "Claim", "Accept", "Resume", "Again", "ConfirmBtn", "FreeCoins" };

    /// Full chunky palette button. Safe to call again (idempotent) - also used to re-face toggles.
    public static void StyleButton(Button b, Color face, bool primary)
    {
        if (!b) return;
        var img = b.GetComponent<Image>(); if (!img) return;
        img.color = face;
        if (!img.sprite) { img.sprite = TCUI.Round; img.type = Image.Type.Sliced; }
        bool clear = face.a < 0.05f;
        // drop shadow: an active "Shadow" sibling (TCFollowRect) when the builder made one, otherwise a Shadow effect
        Image sib = null;
        var p = b.transform.parent;
        if (p) foreach (var f in p.GetComponentsInChildren<TCFollowRect>(true)) if (f && f.target == b.transform) { sib = f.GetComponent<Image>(); break; }
        bool solidBase = false;
        int si = b.transform.GetSiblingIndex();
        if ((!sib || !sib.gameObject.activeSelf) && p && si > 0)
        {
            // a hand-made base right behind the button (e.g. "BG (1)" under the menu Settings button) acts as its 3D slab
            var prev = p.GetChild(si - 1).GetComponent<Image>();
            if (prev && prev.gameObject.activeSelf && (prev.name.StartsWith("BG") || prev.name == "Shadow")) { sib = prev; solidBase = prev.name.StartsWith("BG"); }
        }
        bool sibOn = sib && sib.gameObject.activeSelf;
        if (sibOn) { sib.color = clear ? Color.clear : solidBase ? Shade(face, 0.5f) : A(TCUI.Ink, 0.55f); Mark(sib, solidBase ? "base" : "shadow"); }
        Effects(img, Shade(face, 0.5f), clear ? 0 : 4.5f, A(TCUI.Ink, 0.5f), clear || sibOn ? 0 : 11f);
        if (!clear)
        {
            var bev = Deco(b.transform, "Bevel", Spr("ui_bevel"), Color.white, 0);
            bev.enabled = true;
            if (img.sprite && img.sprite.name != TCUI.Round.name && !img.sprite.name.StartsWith("round")) bev.enabled = false; // custom art face (e.g. his own sprites)
        }
        var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.86f, 0.84f, 0.82f); cb.selectedColor = Color.white; cb.disabledColor = new Color(0.78f, 0.74f, 0.7f, 0.75f); b.colors = cb;
        if (!b.GetComponent<TCPress>()) b.gameObject.AddComponent<TCPress>();
        // icon glyphs: cream on dark faces, ink on light faces (colourful icons keep their art)
        foreach (var ic in b.GetComponentsInChildren<Image>(true))
        {
            if (ic == img || ic.transform.parent != b.transform || ic.name != "Icon" || !ic.sprite) continue;
            string sn = ic.sprite.name;
            bool glyph = sn.Contains("play") || sn.Contains("home") || sn.Contains("restart") || sn.Contains("check") || sn.Contains("help") || sn.Contains("pause") || sn.Contains("gear");
            ic.color = glyph ? (LightText(face) ? TCUI.Cream : (sn.Contains("pause") || sn.Contains("gear") ? Color.white : TCUI.Ink)) : Color.white;
        }
        TextOnFace(b.transform, face);
        if (primary && !clear) Shine(b);
        Mark(b, primary ? "primary" : "button");
    }
    /// Recolour a live button face (toggles, daily cells) keeping rim and text readable.
    public static void SetFace(Button b, Color face)
    {
        if (!b) return; var img = b.GetComponent<Image>(); if (img) img.color = face;
        foreach (var o in b.GetComponents<Outline>()) o.effectColor = Shade(face, 0.5f);
        TextOnFace(b.transform, face);
    }
    public static void Shine(Button b)
    {
        var rt = (RectTransform)b.transform;
        var oldMask = b.GetComponent<Mask>(); if (oldMask) UnityEngine.Object.DestroyImmediate(oldMask);
        int idx = rt.Find("Bevel") ? rt.Find("Bevel").GetSiblingIndex() + 1 : 0;
        var clip = Deco(rt, "ShineClip", TCUI.Round, Color.white, idx);
        Ensure<Mask>(clip.gameObject).showMaskGraphic = false;
        var band = rt.Find("Shine") as RectTransform;
        if (!band) band = clip.transform.Find("Shine") as RectTransform;
        if (!band) { band = Child(clip.transform, "Shine"); }
        band.SetParent(clip.transform, false);
        var bi = Ensure<Image>(band.gameObject); bi.sprite = Resources.Load<Sprite>("TCUI/ui_shine"); bi.color = A(TCUI.CreamLight, 0.6f); bi.raycastTarget = false;
        band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f); band.pivot = new Vector2(0.5f, 0.5f);
        band.sizeDelta = new Vector2(Mathf.Max(60, rt.rect.height * 0.5f), Mathf.Max(rt.rect.height, 100) * 1.9f); band.localRotation = Quaternion.Euler(0, 0, -18);
        var s = Ensure<TCShine>(b.gameObject); s.band = band;
    }

    // ---------- panels ----------
    public static void StylePanel(Transform root)
    {
        if (!root) return;
        var card = root.Find("Card") as RectTransform; if (!card) return;
        var dim = root.Find("Dim")?.GetComponent<Image>(); if (dim) dim.color = A(TCUI.Ink, 0.62f);
        var ci = card.GetComponent<Image>();
        if (ci && !Marked(card))
        {
            ci.color = TCUI.Cream;
            Effects(ci, TCUI.Brown, 6f, A(TCUI.Ink, 0.42f), 22f);
            Deco(card, "DecoGrad", Spr("ui_cardgrad"), Color.white, 0);
            Deco(card, "DecoRing", Spr("ui_ring"), A(TCUI.Tan, 0.95f), 1, 16f);
            Mark(card, "card");
        }
        var head = card.Find("Header") as RectTransform;
        if (head && !Marked(head)) Ribbon(head);
        // section headings & body copy
        foreach (var t in card.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (Marked(t) || t.GetComponentInParent<Button>(true) || (head && t.transform.IsChildOf(head))) continue;
            if (Near(t.color, Legacy.Peach) || Near(t.color, Legacy.Mint) || Near(t.color, Legacy.Total)) StyleText(t, TCUI.Crimson, 0.1f, false);
            else if (Near(t.color, Legacy.Cocoa) || t.color.a < 0.8f && t.color.r < 0.5f) StyleText(t, t.color.a < 0.8f ? TCUI.Brown : TCUI.Ink, 0.08f, false);
            else continue;
            Mark(t, "text");
        }
        Mark(root, "panel");
    }
    public static void Ribbon(RectTransform head)
    {
        var hi = head.GetComponent<Image>(); if (!hi) return;
        var rib = Spr("ui_ribbon");
        if (rib && hi.sprite != rib)
        {
            hi.sprite = rib; hi.type = Image.Type.Sliced; hi.preserveAspect = false;
            head.sizeDelta += new Vector2(80, 30); head.anchoredPosition += new Vector2(0, -15);
        }
        hi.color = TCUI.Crimson;
        Effects(hi, A(TCUI.CrimsonDark, 1f), 3f, A(TCUI.Ink, 0.45f), 9f);
        var bev = Deco(head, "Bevel", Spr("ui_bevel"), A(Color.white, 0.9f), 0);
        bev.rectTransform.offsetMin = new Vector2(44, 32); bev.rectTransform.offsetMax = new Vector2(-44, -2);
        var title = head.Find("Title") as RectTransform;
        if (title)
        {
            title.offsetMin = new Vector2(64, 30); title.offsetMax = new Vector2(-64, 0);
            var tt = title.GetComponent<TextMeshProUGUI>(); if (tt) { StyleText(tt, TCUI.Cream, 0.3f, true); tt.characterSpacing = 2; }
        }
        // two small sparkles riding the ribbon ends
        for (int i = 0; i < 2; i++)
        {
            var sp = Child(head, "Sparkle" + i); var si = Ensure<Image>(sp.gameObject);
            si.sprite = Spr("ui_sparkle"); si.color = A(TCUI.CreamLight, 0.95f); si.raycastTarget = false;
            sp.anchorMin = sp.anchorMax = new Vector2(i == 0 ? 0 : 1, 1); sp.pivot = new Vector2(0.5f, 0.5f);
            sp.anchoredPosition = new Vector2(i == 0 ? 60 : -60, -14); sp.sizeDelta = Vector2.one * (i == 0 ? 30 : 22);
            Ensure<TCTwinkle>(sp.gameObject).phase = i * 1.7f;
        }
        Mark(head, "ribbon");
    }

    // ---------- chips (HUD pills, currency, rows) ----------
    public static void StyleChip(Image img, Color face, Color rim, float rimW = 4f, float drop = 6f, bool bevel = true, bool text = true)
    {
        if (!img || Marked(img)) return;
        img.color = face;
        Effects(img, rim, rimW, A(TCUI.Ink, 0.38f), drop);
        if (bevel) { var b = Deco(img.transform, "Bevel", Spr("ui_bevel"), A(Color.white, 0.75f), 0); }
        if (text) TextOnFace(img.transform, face);
        Mark(img, "chip");
    }

    // ---------- legacy (pre-polish3 pastel) colours found in baked scenes ----------
    public static class Legacy
    {
        public static readonly Color Mint = TCUI.Hex("#5EE0A8"), Peach = TCUI.Hex("#FF9F7A"), Blush = TCUI.Hex("#FF9EC0"), Sky = TCUI.Hex("#6FC3FF"),
            Butter = TCUI.Hex("#FFD54F"), Lilac = TCUI.Hex("#CFC6F5"), Grey = TCUI.Hex("#D9D2CC"), Cocoa = TCUI.Hex("#4A3B3F"), Cream = TCUI.Hex("#FFF7EE"),
            Total = TCUI.Hex("#E07A5F"), Badge = TCUI.Hex("#FF6F8E");
    }
    public static bool Near(Color a, Color b, float tol = 0.06f) => Mathf.Abs(a.r - b.r) < tol && Mathf.Abs(a.g - b.g) < tol && Mathf.Abs(a.b - b.b) < tol;
    /// Palette role for an old pastel face colour.
    public static Color MapFace(Color old)
    {
        if (old.a < 0.05f) return Color.clear;
        if (Near(old, Legacy.Mint)) return TCUI.Crimson;
        if (Near(old, Legacy.Sky) || Near(old, Legacy.Peach) || Near(old, Legacy.Blush)) return TCUI.Coral;
        if (Near(old, Legacy.Butter) || Near(old, Legacy.Lilac) || Near(old, Legacy.Grey)) return TCUI.Tan;
        if (old.r > 0.9f && old.g > 0.9f && old.b > 0.9f) return TCUI.Cream;
        return old;
    }

    // ---------- whole-canvas pass ----------
    /// Styles every not-yet-themed object under a canvas: buttons by role, panels, HUD chips, toasts, sliders.
    /// Objects already marked TCThemed (baked) are left exactly as they are in the scene.
    public static void ThemeTree(Transform canvas)
    {
        if (!canvas) return;
        // panels first (cards, ribbons, headings)
        foreach (var cg in canvas.GetComponentsInChildren<CanvasGroup>(true))
            if (cg.transform.Find("Card") && !Marked(cg)) StylePanel(cg.transform);

        foreach (var b in canvas.GetComponentsInChildren<Button>(true))
        {
            if (Marked(b) || SkipButton(b)) continue;
            var img = b.GetComponent<Image>(); if (!img) continue;
            Color face = FaceFor(b, img.color);
            StyleButton(b, face, PrimaryNames.Contains(b.name) && face == TCUI.Crimson);
        }

        foreach (var img in canvas.GetComponentsInChildren<Image>(true))
        {
            if (Marked(img)) continue;
            string n = img.name; var par = img.transform.parent ? img.transform.parent.name : "";
            switch (n)
            {
                case "Coins": case "Gems": if (par == "Safe" || par == "Top") StyleChip(img, A(TCUI.Cream, 0.96f), TCUI.Brown); break;
                case "ScorePill": StyleChip(img, TCUI.Crimson, TCUI.CrimsonDark, 4.5f, 7f); break;
                case "Best": if (par == "Safe") StyleChip(img, A(TCUI.Tan, 0.97f), TCUI.Brown); break;
                case "LivesBg": StyleChip(img, A(TCUI.Cream, 0.95f), TCUI.Brown); break;
                case "SpeedChilliPanel":
                    StyleChip(img, A(TCUI.Cream, 0.95f), TCUI.Brown, 4f, 6f, false, false);
                    // power-up timers: speed = ink, chilli & magnet = crimson, slow-mo = brown (set live by TCGameUI)
                    foreach (var t in img.GetComponentsInChildren<TextMeshProUGUI>(true))
                        StyleText(t, t.name == "SpeedVal" ? TCUI.Ink : t.name == "SlowmoTime" || t.name == "StockText" ? TCUI.Brown : TCUI.Crimson, 0.1f, false);
                    break;
                case "BestBadge": StyleChip(img, TCUI.Tan, TCUI.Brown, 3f, 4f); break;
                case "TimerBg": StyleChip(img, TCUI.Cream, TCUI.Crimson, 6f, 8f, false); foreach (var t in img.GetComponentsInChildren<TextMeshProUGUI>(true)) StyleText(t, TCUI.Crimson, 0.14f, false); break;
                case "Coins Gathered": case "Distance Bonus": StyleChip(img, TCUI.CreamLight, A(TCUI.Tan, 1f), 3f, 3f, false); break;
                case "Total Coins Earned":
                    StyleChip(img, TCUI.TanLight, TCUI.Brown, 3.5f, 4f, false);
                    var v = img.transform.Find("V")?.GetComponent<TextMeshProUGUI>(); if (v) StyleText(v, TCUI.Crimson, 0.14f, false); break;
                case "Badge": img.color = TCUI.Crimson; Effects(img, TCUI.Cream, 4f, A(TCUI.Ink, 0.4f), 4f); Mark(img, "badge"); break;
                case "Shadow": if (img.GetComponent<TCFollowRect>()) { if (img.color.a > 0.05f) img.color = A(TCUI.Ink, 0.55f); Mark(img, "shadow"); } break;
                case "Toast": img.color = A(TCUI.Ink, 0.95f); Effects(img, TCUI.Tan, 4f, A(TCUI.Ink, 0.4f), 8f); Deco(img.transform, "Bevel", Spr("ui_bevel"), A(Color.white, 0.5f), 0); TextOnFace(img.transform, TCUI.Ink); Mark(img, "toast"); break;
                case "Flash": img.color = A(TCUI.Crimson, 0.35f); Mark(img, "flash"); break;
                case "Handle":
                    if (par == "HandleArea") { img.color = TCUI.Cream; Effects(img, TCUI.Crimson, 5f, A(TCUI.Ink, 0.45f), 5f); }
                    else img.color = TCUI.Coral; Mark(img, "handle"); break;
                case "Fill": if (par == "FillArea") { img.color = TCUI.Crimson; Effects(img, TCUI.CrimsonDark, 2f, Color.clear, 0); Mark(img, "fill"); } break;
                case "BG":
                    if (par == "Slider") { img.color = TCUI.TanLight; Effects(img, TCUI.Brown, 3f, Color.clear, 0); Mark(img, "track"); }
                    else if (par == "Safe") { img.color = Shade(TCUI.Tan, 0.45f); Mark(img, "base"); } // custom base under menu top buttons
                    break;
                case "BG (1)": if (par == "Safe") { img.color = Shade(TCUI.Tan, 0.45f); Mark(img, "base"); } break;
                case "Scrollbar Vertical": img.color = A(TCUI.Ink, 0.15f); Mark(img, "scroll"); break;
                default:
                    if (n.StartsWith("Upg") && par == "Card") { StyleChip(img, TCUI.CreamLight, TCUI.Tan, 3f, 4f, false); foreach (var t in img.GetComponentsInChildren<TextMeshProUGUI>(true)) if (!t.GetComponentInParent<Button>(true)) StyleText(t, t.name == "P" ? TCUI.Crimson : TCUI.Ink, 0.08f, false); }
                    else if (n.StartsWith("Day") && par == "Card") StyleChip(img, TCUI.CreamLight, TCUI.Tan, 3.5f, 5f);
                    break;
            }
        }
        foreach (var t in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (Marked(t) || t.GetComponentInParent<Button>(true) && t.name != "Hint") continue;
            if (t.name == "Hint") StyleText(t, TCUI.Cream, 0.28f, true);
            else if (t.name == "Boost") StyleText(t, TCUI.Cream, 0.32f, true);
            else if (Near(t.color, Legacy.Cocoa) || Near(t.color, TCUI.Hex("#4A3B40"))) StyleText(t, TCUI.Ink, 0.08f, false);
            else continue;
            Mark(t, "text");
        }
        Mark(canvas, "canvas");
    }
    static bool SkipButton(Button b)
    {
        string n = b.name;
        if (n == "Start" || n == "HTPLabel") return true;
        if (n.StartsWith("Skin") && n.Length > 4 && char.IsDigit(n[4])) return true; // skin colour swatches
        if (b.GetComponentInParent<ScrollRect>(true)) return true;                  // skin list items (their own prefab art)
        if (b.GetComponentInParent<SpiderSkinListObj>(true)) return true;
        return false;
    }
    static Color FaceFor(Button b, Color old)
    {
        string n = b.name; string par = b.transform.parent ? b.transform.parent.name : "";
        var card = b.transform.parent; string panel = card && card.parent ? card.parent.name : "";
        if (old.a < 0.05f) return Color.clear;
        if (PrimaryNames.Contains(n)) return TCUI.Crimson;
        if (n == "Pause" && par == "Top") return TCUI.Cream;
        if (panel == "Pause" && n == "Restart") return TCUI.Coral;
        if (panel == "Pause" && n == "Home") return TCUI.Tan;
        if (n == "Buy") return TCUI.Coral;
        if (n.StartsWith("Gems") && panel == "Shop" || n.StartsWith("Pack")) return TCUI.Tan;
        if (n == "Settings" || n == "HowToPlay") return TCUI.Tan;
        return MapFace(old);
    }

    // ---------- menu background ----------
    public static void MenuBackground(Transform bg)
    {
        if (!bg || bg.Find("Wash")) return;
        var w = Deco(bg, "Wash", Spr("ui_wash"), Color.white, bg.childCount);
        Ensure<TCLeafDrift>(bg.gameObject);
        Mark(w, "wash");
    }
}
