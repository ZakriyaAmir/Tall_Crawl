using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// WEB letter collectible + 5000 m invincible web-swing bonus.
/// Letters W, E, B spawn sparsely along the trunk. Collecting all three fires a
/// cinematic upward swing: the spider shoots a web, is immune to hits, and is
/// carried 5000 m in a few seconds. Play then resumes with a short grace period
/// and a clear stretch of trunk ahead.
public class TCWebBonus : MonoBehaviour
{
    public static TCWebBonus Instance { get; private set; }

    public const float DistanceBonusMetres = 5000f;
    public const float SwingSeconds = 2.6f;
    public const float GraceSeconds = 1.8f;
    public const float FirstLetterMetres = 220f;
    public const float MinLetterSpacingMetres = 70f;
    public const float LetterChance = 0.085f;

    public bool HasW { get; private set; }
    public bool HasE { get; private set; }
    public bool HasB { get; private set; }
    public bool AllCollected => HasW && HasE && HasB;
    public bool IsSwinging { get; private set; }
    public bool IsInvincible => IsSwinging || graceLeft > 0f;
    public float ClimbSpeed { get; private set; } // world units / sec during the swing

    public static System.Action OnLettersChanged, OnSwingStart, OnSwingEnd;

    float graceLeft;
    float lastLetterMetres = -999f;
    LineRenderer webLine;
    Transform webAnchor;
    Coroutine swingCo;
    GameObject letterProto;
    AudioSource whoosh;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (graceLeft > 0f) graceLeft = Mathf.Max(0f, graceLeft - Time.deltaTime);
        if (IsSwinging && webLine && webAnchor)
        {
            var sp = ResolveSpider();
            if (sp)
            {
                webLine.positionCount = 2;
                webLine.SetPosition(0, webAnchor.position);
                webLine.SetPosition(1, sp.position + sp.up * 0.15f);
            }
        }
    }

    Transform ResolveSpider()
    {
        var p = player.Instance;
        if (p && p.p1) return p.GetComponentInChildren<playerInteract>() ? p.GetComponentInChildren<playerInteract>().transform : p.p1;
        var sp = FindObjectOfType<playerInteract>();
        return sp ? sp.transform : null;
    }

    public bool HasLetter(char c)
    {
        c = char.ToUpperInvariant(c);
        return c == 'W' ? HasW : c == 'E' ? HasE : c == 'B' && HasB;
    }

    public bool CanSpawnLetter(float metres)
    {
        if (IsSwinging || AllCollected) return false;
        if (metres < FirstLetterMetres) return false;
        if (metres - lastLetterMetres < MinLetterSpacingMetres) return false;
        return true;
    }

    public char NextLetterToSpawn()
    {
        // Prefer a letter the player does not already have, in W-E-B order with a little shuffle.
        var missing = new List<char>(3);
        if (!HasW) missing.Add('W');
        if (!HasE) missing.Add('E');
        if (!HasB) missing.Add('B');
        if (missing.Count == 0) return 'W';
        return missing[Random.Range(0, missing.Count)];
    }

    public void MarkLetterSpawned(float metres) { lastLetterMetres = metres; }

    public void CollectLetter(char c, Vector3 pos)
    {
        c = char.ToUpperInvariant(c);
        bool changed = false;
        if (c == 'W' && !HasW) { HasW = true; changed = true; }
        else if (c == 'E' && !HasE) { HasE = true; changed = true; }
        else if (c == 'B' && !HasB) { HasB = true; changed = true; }
        if (!changed) return;
        TCSfx.Play("coin", 0.9f);
        TCFx.Shake(0.08f);
        OnLettersChanged?.Invoke();
        if (AllCollected && !IsSwinging) BeginSwing();
    }

    public void ResetLetters()
    {
        HasW = HasE = HasB = false;
        lastLetterMetres = -999f;
        OnLettersChanged?.Invoke();
    }

    public void BeginSwing()
    {
        if (IsSwinging) return;
        if (swingCo != null) StopCoroutine(swingCo);
        swingCo = StartCoroutine(SwingRoutine());
    }

    IEnumerator SwingRoutine()
    {
        IsSwinging = true;
        OnSwingStart?.Invoke();
        var pl = player.Instance;
        var sp = ResolveSpider();
        if (pl) pl.ResetInputState();

        // Clear hazards ahead so the climb is a pure celebration.
        TCTrack.Instance?.ClearHazardsAhead(80f);
        TCTrack.Instance?.ReserveClearRun(DistanceBonusMetres);

        // Web strand from a point high above the spider.
        EnsureWeb();
        if (sp && webAnchor)
        {
            webAnchor.position = sp.position + Vector3.up * 14f + sp.up * 0.2f;
            webLine.enabled = true;
        }
        TCFx.Shake(0.22f);
        TCSfx.Play("celebrate", 1f);
        if (whoosh && TCAssets.I && TCAssets.I.whoosh) whoosh.PlayOneShot(TCAssets.I.whoosh, 0.9f);

        // Camera punch.
        var cam = Camera.main;
        Vector3 camLocal = cam ? cam.transform.localPosition : Vector3.zero;
        if (cam) StartCoroutine(CamPunch(cam, camLocal, 0.18f, 0.55f));

        // Convert 5000 m into world units (metresPerUnit) and climb it in SwingSeconds.
        float mpu = TCConfig.I != null ? Mathf.Max(0.01f, TCConfig.I.metresPerUnit) : 20f;
        float worldUnits = DistanceBonusMetres / mpu;
        ClimbSpeed = worldUnits / Mathf.Max(0.2f, SwingSeconds);

        float t = 0f;
        while (t < SwingSeconds)
        {
            t += Time.deltaTime;
            if (webAnchor && sp) webAnchor.position = sp.position + Vector3.up * 14f + sp.up * 0.2f;
            yield return null;
        }

        ClimbSpeed = 0f;
        IsSwinging = false;
        if (webLine) webLine.enabled = false;

        // Land safely: another clear stretch + grace invulnerability.
        TCTrack.Instance?.ClearHazardsAhead(12f);
        TCTrack.Instance?.ReserveClearRun(400f);
        graceLeft = GraceSeconds;
        if (pl) pl.ResetInputState();

        var pi = FindObjectOfType<playerInteract>();
        if (pi) pi.GrantGrace(GraceSeconds);

        ResetLetters();
        OnSwingEnd?.Invoke();
        swingCo = null;
    }

    IEnumerator CamPunch(Camera cam, Vector3 rest, float amount, float dur)
    {
        float t = 0f;
        while (t < dur && cam)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / dur);
            k = k * k;
            cam.transform.localPosition = rest + Random.insideUnitSphere * (amount * k);
            yield return null;
        }
        if (cam) cam.transform.localPosition = rest;
    }

    void EnsureWeb()
    {
        if (webLine) return;
        var go = new GameObject("TCWebStrand");
        go.transform.SetParent(transform, false);
        webLine = go.AddComponent<LineRenderer>();
        webLine.positionCount = 2;
        webLine.startWidth = 0.045f;
        webLine.endWidth = 0.018f;
        webLine.useWorldSpace = true;
        webLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        webLine.receiveShadows = false;
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        var mat = new Material(sh) { color = new Color(0.95f, 0.97f, 1f, 0.92f) };
        webLine.material = mat;
        webLine.textureMode = LineTextureMode.Stretch;
        webLine.enabled = false;
        var anchor = new GameObject("TCWebAnchor");
        webAnchor = anchor.transform;
        webAnchor.SetParent(transform, false);
        whoosh = gameObject.GetComponent<AudioSource>();
        if (!whoosh) whoosh = gameObject.AddComponent<AudioSource>();
        whoosh.playOnAwake = false;
    }

    /// Procedural W/E/B pickup. Parent is the track bonus root; caller pools or destroys after collect.
    public static GameObject CreateLetterPrefab(Transform parent, char letter)
    {
        letter = char.ToUpperInvariant(letter);
        var root = new GameObject("WebLetter_" + letter);
        if (parent) root.transform.SetParent(parent, false);
        root.tag = "Untagged";

        var item = new GameObject("LetterItem");
        item.transform.SetParent(root.transform, false);
        var col = item.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.28f;
        var letterComp = item.AddComponent<TCWebLetter>();
        letterComp.letter = letter;

        // Billboard letter card
        var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(card.GetComponent<Collider>());
        card.name = "Glyph";
        card.transform.SetParent(item.transform, false);
        card.transform.localScale = Vector3.one * 0.42f;
        var mr = card.GetComponent<MeshRenderer>();
        mr.sharedMaterial = LetterMaterial(letter);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(glow.GetComponent<Collider>());
        glow.name = "Glow";
        glow.transform.SetParent(item.transform, false);
        glow.transform.localPosition = new Vector3(0, 0, 0.02f);
        glow.transform.localScale = Vector3.one * 0.7f;
        var gr = glow.GetComponent<MeshRenderer>();
        var gmat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
        gmat.color = new Color(0.85f, 0.95f, 1f, 0.35f);
        gr.sharedMaterial = gmat;
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var bill = item.AddComponent<TCWebLetterBillboard>();
        bill.visual = item.transform;
        return root;
    }

    static readonly Dictionary<char, Material> letterMats = new Dictionary<char, Material>();
    public static Material LetterMaterial(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        if (letterMats.TryGetValue(letter, out var m) && m) return m;
        var tex = LetterTexture(letter);
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Texture");
        m = new Material(sh) { mainTexture = tex, color = Color.white, name = "WEB_" + letter };
        if (m.HasProperty("_Mode")) { /* keep default */ }
        letterMats[letter] = m;
        return m;
    }

    static Texture2D LetterTexture(char letter)
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        // Soft cream card with cocoa letter
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2 - 1, v = (y + 0.5f) / N * 2 - 1;
                // rounded rect
                float ax = Mathf.Abs(u) - 0.72f, ay = Mathf.Abs(v) - 0.72f;
                float d = Mathf.Min(Mathf.Max(ax, ay), 0) + new Vector2(Mathf.Max(ax, 0), Mathf.Max(ay, 0)).magnitude - 0.18f;
                float a = Mathf.Clamp01(-d * 32f);
                px[y * N + x] = new Color(1f, 0.97f, 0.92f, a);
            }
        tex.SetPixels(px);
        // Stamp a chunky letter
        StampLetter(tex, letter);
        tex.Apply(false, false);
        return tex;
    }

    static void StampLetter(Texture2D tex, char letter)
    {
        int N = tex.width;
        // Very small bitmap-ish strokes for W, E, B so we don't depend on a font at runtime.
        void Ink(int x, int y, float a = 1)
        {
            if ((uint)x >= (uint)N || (uint)y >= (uint)N) return;
            var c = tex.GetPixel(x, y);
            var ink = new Color(0.29f, 0.23f, 0.25f, 1);
            c = Color.Lerp(c, ink, a * 0.95f);
            c.a = Mathf.Max(c.a, a);
            tex.SetPixel(x, y, c);
        }
        void Disc(float cx, float cy, float r)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a > 0) Ink(x, y, a);
                }
        }
        void Capsule(float x0, float y0, float x1, float y1, float r)
        {
            Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1);
            int minx = Mathf.FloorToInt(Mathf.Min(x0, x1) - r - 1), maxx = Mathf.CeilToInt(Mathf.Max(x0, x1) + r + 1);
            int miny = Mathf.FloorToInt(Mathf.Min(y0, y1) - r - 1), maxy = Mathf.CeilToInt(Mathf.Max(y0, y1) + r + 1);
            Vector2 ba = b - a; float den = Vector2.Dot(ba, ba);
            for (int y = miny; y <= maxy; y++)
                for (int x = minx; x <= maxx; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = den > 1e-4f ? Mathf.Clamp01(Vector2.Dot(p - a, ba) / den) : 0f;
                    float d = (p - (a + ba * t)).magnitude;
                    float al = Mathf.Clamp01(r - d + 0.5f);
                    if (al > 0) Ink(x, y, al);
                }
        }
        float s = N / 128f;
        float C(float v) => v * s;
        float r = 5.2f * s;
        if (letter == 'W')
        {
            Capsule(C(36), C(96), C(46), C(36), r);
            Capsule(C(46), C(36), C(64), C(70), r);
            Capsule(C(64), C(70), C(82), C(36), r);
            Capsule(C(82), C(36), C(92), C(96), r);
        }
        else if (letter == 'E')
        {
            Capsule(C(40), C(34), C(40), C(96), r);
            Capsule(C(40), C(96), C(92), C(96), r);
            Capsule(C(40), C(65), C(82), C(65), r);
            Capsule(C(40), C(34), C(92), C(34), r);
        }
        else // B
        {
            Capsule(C(40), C(34), C(40), C(96), r);
            Capsule(C(40), C(96), C(78), C(96), r);
            Capsule(C(40), C(65), C(76), C(65), r);
            Capsule(C(40), C(34), C(78), C(34), r);
            Capsule(C(78), C(96), C(88), C(80), r);
            Capsule(C(88), C(80), C(78), C(65), r);
            Capsule(C(76), C(65), C(90), C(50), r);
            Capsule(C(90), C(50), C(78), C(34), r);
        }
    }
}

/// Trigger volume on a W/E/B pickup.
public class TCWebLetter : MonoBehaviour
{
    public char letter = 'W';
    bool taken;
    void OnEnable() { taken = false; }
    public void Collect(Vector3 pos)
    {
        if (taken) return;
        taken = true;
        TCWebBonus.Instance?.CollectLetter(letter, pos);
        var pooled = GetComponentInParent<TCPooled>();
        if (pooled) pooled.Release();
        else
        {
            var root = transform.parent ? transform.parent.gameObject : gameObject;
            root.SetActive(false);
        }
    }
}

/// Keeps the letter card facing the camera and gives it a gentle bob.
public class TCWebLetterBillboard : MonoBehaviour
{
    public Transform visual;
    Vector3 basePos;
    void OnEnable() { if (visual) basePos = visual.localPosition; }
    void LateUpdate()
    {
        if (!visual) return;
        var cam = Camera.main;
        if (cam) visual.rotation = Quaternion.LookRotation(visual.position - cam.transform.position, Vector3.up);
        visual.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.6f) * 0.04f);
    }
}
