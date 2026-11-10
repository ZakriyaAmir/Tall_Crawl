using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns a high-score milestone tag & ribbon on the tree trunk at the exact height
/// where the previous high score was set in the gameplay.
/// When the player passes it, it plays a triumphant victory sound ("celebrate.wav"),
/// bursts in-game 3D fluttering confetti + UI screen confetti, and flashes celebratory feedback.
/// </summary>
public class TCHighScoreMarker : MonoBehaviour
{
    public static TCHighScoreMarker Instance { get; private set; }

    Transform playerTransform;
    player playerScript;
    int previousHighScore;
    float targetHeight;
    bool passed = false;

    GameObject ribbonObj;
    Material ribbonMat;
    readonly List<TextMeshProUGUI> tagHeaders = new List<TextMeshProUGUI>();
    readonly List<TextMeshProUGUI> tagScores = new List<TextMeshProUGUI>();
    readonly List<Transform> tagRoots = new List<Transform>();

    public static TCHighScoreMarker Spawn(Transform parent, Transform playerTr, int bestScore, float metresPerUnit)
    {
        if (bestScore <= 0 || playerTr == null) return null;

        var go = new GameObject("HighScore_Milestone_Marker");
        if (parent) go.transform.SetParent(parent, false);

        var marker = go.AddComponent<TCHighScoreMarker>();
        marker.Init(playerTr, bestScore, metresPerUnit);
        return marker;
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (ribbonMat != null) Destroy(ribbonMat);
    }

    public void Init(Transform playerTr, int bestScore, float metresPerUnit)
    {
        playerTransform = playerTr;
        playerScript = playerTr ? playerTr.GetComponent<player>() : null;
        previousHighScore = bestScore;

        float py = playerTr ? playerTr.position.y : 0.543f;
        targetHeight = py + (float)bestScore / Mathf.Max(1f, metresPerUnit);
        transform.position = new Vector3(0f, targetHeight, 0f);

        BuildRibbonAndTags();
    }

    void Update()
    {
        if (passed) return;

        if (playerTransform == null)
        {
            var pl = FindObjectOfType<player>();
            if (pl != null)
            {
                playerTransform = pl.transform;
                playerScript = pl;
            }
        }

        if (playerTransform != null)
        {
            bool crossed = (playerTransform.position.y >= targetHeight)
                || (playerScript != null && playerScript.currentScore >= previousHighScore);

            if (crossed)
            {
                OnPassed();
            }
        }
    }

    void OnPassed()
    {
        passed = true;

        // 1. Victory Fanfare Sound & Haptics
        TCSfx.Play("celebrate", 1.0f);
        TCServices.Haptic();
        TCFx.Shake(0.20f);

        // 2. In-game 3D Fluttering Confetti around the trunk & spider
        StartCoroutine(Spawn3DConfetti(playerTransform != null ? playerTransform.position : transform.position));

        // 3. UI Celebration (Screen Confetti & Header Punch)
        if (TCGameUI.Instance != null)
        {
            TCGameUI.Instance.TriggerHighScoreCelebration();
            Vector3 popPos = playerTransform != null ? playerTransform.position + Vector3.up * 0.45f : transform.position;
            TCGameUI.Instance.Pop("NEW BEST SCORE!", popPos, TCUI.Peach);
        }

        // 4. Milestone Ribbon & Tag Visual Celebration Reaction
        StartCoroutine(RibbonCelebrationRoutine());
    }

    void BuildRibbonAndTags()
    {
        const float ribbonRadius = 0.613f;
        const float ribbonHeight = 0.22f;

        // 1. Checkered Milestone Finish Ribbon around the trunk
        ribbonObj = new GameObject("Milestone_Ribbon");
        ribbonObj.transform.SetParent(transform, false);

        var mf = ribbonObj.AddComponent<MeshFilter>();
        mf.sharedMesh = CreateRibbonCylinderMesh(ribbonRadius, ribbonHeight, 48);

        var mr = ribbonObj.AddComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Mobile/Diffuse")
            ?? Shader.Find("Standard");

        ribbonMat = new Material(shader) { name = "HighScore_Ribbon_Mat" };
        ribbonMat.mainTexture = CreateCheckeredRibbonTexture();
        if (ribbonMat.HasProperty("_Color")) ribbonMat.SetColor("_Color", Color.white);
        if (ribbonMat.HasProperty("_BaseColor")) ribbonMat.SetColor("_BaseColor", Color.white);
        mr.sharedMaterial = ribbonMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Glowing golden trim rings at top and bottom of the ribbon
        CreateTrimRing(ribbonRadius + 0.003f, ribbonHeight * 0.5f);
        CreateTrimRing(ribbonRadius + 0.003f, -ribbonHeight * 0.5f);

        // 2. 3D Milestone Tags at 3 cardinal angles around the tree (0, 120, 240 deg)
        float[] tagAngles = { 0f, 120f, 240f };
        for (int i = 0; i < tagAngles.Length; i++)
        {
            CreateTagPlate(tagAngles[i], ribbonRadius + 0.018f);
        }
    }

    void CreateTrimRing(float radius, float yPos)
    {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var col = ring.GetComponent<Collider>();
        if (col) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = new Vector3(0, yPos, 0);
        ring.transform.localScale = new Vector3(radius * 2f, 0.014f, radius * 2f);

        var r = ring.GetComponent<MeshRenderer>();
        if (r)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Standard"));
            mat.color = new Color(1.0f, 0.85f, 0.25f, 1f);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    void CreateTagPlate(float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 pos = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
        Quaternion rot = Quaternion.Euler(0f, angleDeg, 0f);

        var tagRoot = new GameObject("TagPlate_" + (int)angleDeg);
        tagRoot.transform.SetParent(transform, false);
        tagRoot.transform.localPosition = pos;
        tagRoot.transform.localRotation = rot;
        tagRoots.Add(tagRoot.transform);

        // 3D Backplate: deep navy body with golden rim
        var backMat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse"));
        backMat.color = new Color(0.12f, 0.15f, 0.20f, 1f);

        var goldMat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse"));
        goldMat.color = new Color(1.0f, 0.82f, 0.22f, 1f);

        // Gold outer frame
        var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var fCol = frame.GetComponent<Collider>();
        if (fCol) { if (Application.isPlaying) Destroy(fCol); else DestroyImmediate(fCol); }
        frame.transform.SetParent(tagRoot.transform, false);
        frame.transform.localPosition = Vector3.zero;
        frame.transform.localScale = new Vector3(0.62f, 0.25f, 0.035f);
        var fr = frame.GetComponent<MeshRenderer>();
        if (fr) { fr.sharedMaterial = goldMat; fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }

        // Navy inner card
        var card = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cCol = card.GetComponent<Collider>();
        if (cCol) { if (Application.isPlaying) Destroy(cCol); else DestroyImmediate(cCol); }
        card.transform.SetParent(tagRoot.transform, false);
        card.transform.localPosition = new Vector3(0, 0, 0.008f);
        card.transform.localScale = new Vector3(0.58f, 0.21f, 0.032f);
        var cr = card.GetComponent<MeshRenderer>();
        if (cr) { cr.sharedMaterial = backMat; cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }

        // WorldSpace Canvas on the face of the tag (facing outward towards camera)
        var canvasGo = new GameObject("TagCanvas");
        canvasGo.transform.SetParent(tagRoot.transform, false);
        canvasGo.transform.localPosition = new Vector3(0, 0, 0.026f);
        canvasGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360f, 150f);
        rt.localScale = Vector3.one * 0.00165f; // scale canvas to match ~0.58m plate

        // Header: "★ HIGH SCORE ★"
        var hdr = TCUI.Label("Header", canvasGo.transform, "★ HIGH SCORE ★", 28, new Color(1.0f, 0.84f, 0.25f, 1f));
        TCUI.Place(hdr.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 36), new Vector2(340, 40));
        hdr.fontStyle = FontStyles.Bold;
        hdr.raycastTarget = false;
        tagHeaders.Add(hdr);

        // Score: "{score} M"
        var val = TCUI.Label("ScoreVal", canvasGo.transform, previousHighScore.ToString("N0") + " M", 56, Color.white);
        TCUI.Place(val.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(340, 68));
        val.fontStyle = FontStyles.Bold;
        val.raycastTarget = false;
        TCUI.Outline(val, 0.25f);
        tagScores.Add(val);
    }

    IEnumerator RibbonCelebrationRoutine()
    {
        // Flash tags and ribbon
        for (int i = 0; i < tagHeaders.Count; i++)
        {
            if (tagHeaders[i] != null)
            {
                tagHeaders[i].text = "★ NEW RECORD! ★";
                tagHeaders[i].color = new Color(0.2f, 1.0f, 0.6f, 1f);
            }
        }

        // Punch tags
        for (int i = 0; i < tagRoots.Count; i++)
        {
            if (tagRoots[i] != null) StartCoroutine(PunchTransform(tagRoots[i], 0.35f, 1.25f));
        }

        // Pulse ribbon glow
        float duration = 1.8f;
        Color baseCol = Color.white;
        Color flashCol = new Color(1.5f, 1.4f, 0.6f, 1f);

        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t * 8f) * 0.5f + 0.5f;
            Color c = Color.Lerp(baseCol, flashCol, k * (1f - t / duration));
            if (ribbonMat != null)
            {
                if (ribbonMat.HasProperty("_Color")) ribbonMat.SetColor("_Color", c);
                if (ribbonMat.HasProperty("_BaseColor")) ribbonMat.SetColor("_BaseColor", c);
            }
            yield return null;
        }
    }

    IEnumerator PunchTransform(Transform t, float dur, float scaleMult)
    {
        if (t == null) yield break;
        Vector3 orig = t.localScale;
        for (float elapsed = 0; elapsed < dur; elapsed += Time.deltaTime)
        {
            if (t == null) yield break;
            float p = elapsed / dur;
            float s = 1f + Mathf.Sin(p * Mathf.PI) * (scaleMult - 1f);
            t.localScale = orig * s;
            yield return null;
        }
        if (t != null) t.localScale = orig;
    }

    /// <summary>
    /// Spawns ~60 colorful 3D fluttering confetti quads bursting outward around the spider and trunk.
    /// </summary>
    IEnumerator Spawn3DConfetti(Vector3 origin)
    {
        var container = new GameObject("3D_Confetti_Container");
        container.transform.position = origin;

        Color[] confettiPalette = new Color[]
        {
            new Color(1.0f, 0.82f, 0.22f, 1f), // Bright Gold
            new Color(0.12f, 0.86f, 0.62f, 1f), // Mint Green
            new Color(0.15f, 0.68f, 0.95f, 1f), // Vivid Azure
            new Color(0.96f, 0.28f, 0.45f, 1f), // Coral Pink
            new Color(1.0f, 0.55f, 0.20f, 1f),  // Warm Orange
            new Color(0.65f, 0.32f, 0.95f, 1f), // Royal Purple
            new Color(1.0f, 1.0f, 1.0f, 1f)     // Pure White
        };

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Mobile/Particles/Alpha Blended")
            ?? Shader.Find("Sprites/Default");

        // Cache materials for palette
        var mats = new Material[confettiPalette.Length];
        for (int c = 0; c < mats.Length; c++)
        {
            mats[c] = new Material(shader) { name = "Confetti_Mat_" + c, color = confettiPalette[c] };
            if (mats[c].HasProperty("_Color")) mats[c].SetColor("_Color", confettiPalette[c]);
            if (mats[c].HasProperty("_BaseColor")) mats[c].SetColor("_BaseColor", confettiPalette[c]);
        }

        int count = 60;
        var pieces = new Transform[count];
        var velocities = new Vector3[count];
        var rotSpeeds = new Vector3[count];
        var pieceMats = new Material[count];

        for (int i = 0; i < count; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var col = quad.GetComponent<Collider>();
            if (col) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }

            quad.name = "Confetti_" + i;
            quad.transform.SetParent(container.transform, false);

            float rAngle = Random.Range(0f, Mathf.PI * 2f);
            float rDist = Random.Range(0.60f, 0.85f);
            quad.transform.position = new Vector3(Mathf.Sin(rAngle) * rDist, targetHeight + Random.Range(-0.1f, 0.25f), Mathf.Cos(rAngle) * rDist);

            float w = Random.Range(0.065f, 0.11f);
            float h = Random.Range(0.035f, 0.065f);
            quad.transform.localScale = new Vector3(w, h, 1f);
            quad.transform.rotation = Random.rotation;

            int matIdx = Random.Range(0, mats.Length);
            var mr = quad.GetComponent<MeshRenderer>();
            if (mr)
            {
                mr.sharedMaterial = mats[matIdx];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // Upward pop + radial spread
            float burstUp = Random.Range(2.4f, 5.2f);
            float burstOut = Random.Range(1.2f, 3.4f);
            Vector3 radDir = new Vector3(Mathf.Sin(rAngle), 0, Mathf.Cos(rAngle));
            velocities[i] = Vector3.up * burstUp + radDir * burstOut;

            rotSpeeds[i] = new Vector3(Random.Range(-480f, 480f), Random.Range(-480f, 480f), Random.Range(-480f, 480f));
            pieces[i] = quad.transform;
            pieceMats[i] = mr ? mr.material : null; // instance copy for alpha fade
        }

        float lifetime = 2.6f;
        for (float elapsed = 0; elapsed < lifetime; elapsed += Time.deltaTime)
        {
            float dt = Time.deltaTime;
            float fadeProgress = Mathf.Clamp01((elapsed - 1.8f) / 0.8f);

            for (int i = 0; i < count; i++)
            {
                if (pieces[i] == null) continue;

                // Gravity & Drag
                velocities[i] += Vector3.down * (9.8f * dt);
                velocities[i] *= (1f - 1.4f * dt);

                pieces[i].position += velocities[i] * dt;
                pieces[i].Rotate(rotSpeeds[i] * dt);

                // Fade out near end of life
                if (fadeProgress > 0f && pieceMats[i] != null)
                {
                    Color c = pieceMats[i].color;
                    c.a = 1f - fadeProgress;
                    if (pieceMats[i].HasProperty("_Color")) pieceMats[i].SetColor("_Color", c);
                    if (pieceMats[i].HasProperty("_BaseColor")) pieceMats[i].SetColor("_BaseColor", c);
                }
            }
            yield return null;
        }

        Destroy(container);
    }

    /// <summary>
    /// Generates an open cylinder (tube) mesh around the tree trunk for the milestone ribbon.
    /// </summary>
    static Mesh CreateRibbonCylinderMesh(float radius, float height, int segments)
    {
        var mesh = new Mesh { name = "Ribbon_Tube_Mesh" };

        int vertCount = (segments + 1) * 2;
        var vertices = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        float halfH = height * 0.5f;

        for (int s = 0; s <= segments; s++)
        {
            float a = (s / (float)segments) * Mathf.PI * 2f;
            float cos = Mathf.Cos(a);
            float sin = Mathf.Sin(a);

            int topIdx = s;
            int botIdx = s + (segments + 1);

            vertices[topIdx] = new Vector3(cos * radius, halfH, sin * radius);
            vertices[botIdx] = new Vector3(cos * radius, -halfH, sin * radius);

            Vector3 norm = new Vector3(cos, 0f, sin);
            normals[topIdx] = norm;
            normals[botIdx] = norm;

            float u = (s / (float)segments) * 12f; // repeats 12 times around the trunk
            uvs[topIdx] = new Vector2(u, 1f);
            uvs[botIdx] = new Vector2(u, 0f);
        }

        var triangles = new int[segments * 12]; // double-sided (6 front + 6 back)
        int tIdx = 0;

        for (int s = 0; s < segments; s++)
        {
            int t0 = s;
            int t1 = s + 1;
            int b0 = s + (segments + 1);
            int b1 = s + 1 + (segments + 1);

            // Front (outward)
            triangles[tIdx++] = t0;
            triangles[tIdx++] = t1;
            triangles[tIdx++] = b0;

            triangles[tIdx++] = b0;
            triangles[tIdx++] = t1;
            triangles[tIdx++] = b1;

            // Back (inward)
            triangles[tIdx++] = t0;
            triangles[tIdx++] = b0;
            triangles[tIdx++] = t1;

            triangles[tIdx++] = b0;
            triangles[tIdx++] = b1;
            triangles[tIdx++] = t1;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Generates a procedural checkered milestone ribbon texture (golden amber & dark navy checks with glowing gold trims).
    /// </summary>
    static Texture2D CreateCheckeredRibbonTexture()
    {
        int w = 128, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Procedural_Checkered_Ribbon",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color gold = new Color(1.0f, 0.78f, 0.18f, 1f);
        Color navy = new Color(0.12f, 0.16f, 0.22f, 1f);
        Color trimGold = new Color(1.0f, 0.92f, 0.45f, 1f);

        for (int y = 0; y < h; y++)
        {
            bool isTrim = (y < 8) || (y >= h - 8);
            for (int x = 0; x < w; x++)
            {
                if (isTrim)
                {
                    tex.SetPixel(x, y, trimGold);
                }
                else
                {
                    int checkX = (x / 16) % 2;
                    int checkY = (y / 16) % 2;
                    tex.SetPixel(x, y, (checkX == checkY) ? gold : navy);
                }
            }
        }

        tex.Apply();
        return tex;
    }
}
