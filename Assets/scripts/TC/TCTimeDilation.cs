using System.Collections;
using UnityEngine;

/// <summary>
/// Time Slow-Down (Cooldown) Controller.
/// Slows down gameplay time to 0.5x when activated, persists for 3 seconds,
/// and smoothly lerps / eases timeScale in and out.
/// </summary>
public class TCTimeDilation : MonoBehaviour
{
    public static TCTimeDilation Instance { get; private set; }

    public const float SlowScale = 0.5f;
    public const float SlowDuration = 3.0f;
    public const float EaseInDuration = 0.25f;
    public const float EaseOutDuration = 0.45f;

    public bool IsActive => activeTimeRemaining > 0f;
    public float ActiveTimeRemaining => Mathf.Max(0f, activeTimeRemaining);

    float activeTimeRemaining = 0f;
    Coroutine dilationRoutine;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            if (activeTimeRemaining > 0f) Time.timeScale = 1f;
            Instance = null;
        }
    }

    public static bool TriggerCooldown()
    {
        if (playerInteract.CooldownInventory <= 0) return false;
        playerInteract.CooldownInventory--;

        if (Instance != null)
        {
            Instance.Activate();
        }
        return true;
    }

    public void Activate()
    {
        TCSfx.Play("cooldown_activate", 0.9f);
        TCFx.Shake(0.15f);
        if (dilationRoutine != null) StopCoroutine(dilationRoutine);
        dilationRoutine = StartCoroutine(DilationSequence());
    }

    IEnumerator DilationSequence()
    {
        activeTimeRemaining = SlowDuration;

        // Ease in from current timeScale to 0.5x
        float startScale = Time.timeScale;
        for (float t = 0; t < EaseInDuration; t += Time.unscaledDeltaTime)
        {
            // Smooth hermite interpolation
            float frac = Mathf.SmoothStep(0f, 1f, t / EaseInDuration);
            Time.timeScale = Mathf.Lerp(startScale, SlowScale, frac);
            activeTimeRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }
        Time.timeScale = SlowScale;

        // Hold at 0.5x for remainder of the 3-second duration
        while (activeTimeRemaining > EaseOutDuration)
        {
            activeTimeRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        // Smoothly ease / fade back to normal speed (1.0x)
        float fromScale = Time.timeScale;
        for (float t = 0; t < EaseOutDuration; t += Time.unscaledDeltaTime)
        {
            float frac = Mathf.SmoothStep(0f, 1f, t / EaseOutDuration);
            Time.timeScale = Mathf.Lerp(fromScale, 1.0f, frac);
            activeTimeRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = 1.0f;
        activeTimeRemaining = 0f;
        dilationRoutine = null;
    }

    /// <summary>
    /// Procedurally creates a 3D Cooldown Pickup GameObject to be pooled and spawned on the trunk.
    /// Features an animated glowing cyan stopwatch/hourglass crystal with rotating outer orbital rings.
    /// </summary>
    public static GameObject CreateCooldownPickupPrefab(Transform parent)
    {
        var root = new GameObject("Cooldown_Pickup_Root");
        if (parent) root.transform.SetParent(parent, false);

        // Child container placed at trunk radius (0.603f)
        var item = new GameObject("CooldownItem");
        item.transform.SetParent(root.transform, false);
        item.transform.localPosition = new Vector3(0, 0, 0.603f);
        item.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        item.transform.localScale = Vector3.one * 0.45f;
        item.tag = "cooldown";

        // PhysX components: kinematic rigidbody and trigger collider
        var rb = item.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.mass = 0.0000001f;

        var col = item.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.7f, 0.7f, 0.7f);

        // Continuous rotation for visual juice (rotates the visual model without dirtying PhysX bounds)
        var rot = item.AddComponent<continuousRotation>();
        rot.y = true;
        rot.speed = 120f;

        // Visual Model
        var model = new GameObject("VisualModel");
        model.transform.SetParent(item.transform, false);

        var shader = Shader.Find("TallCrawl/SpiderPreBaked") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        
        // Cyan crystal glowing material
        var crystalMat = new Material(shader);
        crystalMat.color = new Color(0.25f, 0.85f, 1.0f, 1.0f);
        if (crystalMat.HasProperty("_EmissionColor"))
        {
            crystalMat.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 0.95f) * 1.5f);
            crystalMat.EnableKeyword("_EMISSION");
        }
        if (crystalMat.HasProperty("_OutlineColor")) crystalMat.SetColor("_OutlineColor", new Color(0.05f, 0.25f, 0.45f, 1f));
        if (crystalMat.HasProperty("_OutlineWidth")) crystalMat.SetFloat("_OutlineWidth", 1.5f);

        // White core accent material
        var coreMat = new Material(shader);
        coreMat.color = Color.white;
        if (coreMat.HasProperty("_EmissionColor"))
        {
            coreMat.SetColor("_EmissionColor", Color.white * 1.2f);
            coreMat.EnableKeyword("_EMISSION");
        }

        // Gold/Brass metal rim material
        var rimMat = new Material(shader);
        rimMat.color = new Color(1.0f, 0.82f, 0.35f, 1.0f);
        if (rimMat.HasProperty("_OutlineColor")) rimMat.SetColor("_OutlineColor", new Color(0.35f, 0.22f, 0.05f, 1f));
        if (rimMat.HasProperty("_OutlineWidth")) rimMat.SetFloat("_OutlineWidth", 1.2f);

        // Central diamond/crystal (octahedron / sphere flattened into jewel)
        CreateVisualPart(PrimitiveType.Sphere, model.transform, Vector3.zero, new Vector3(0.38f, 0.38f, 0.22f), crystalMat);
        CreateVisualPart(PrimitiveType.Sphere, model.transform, Vector3.zero, new Vector3(0.20f, 0.20f, 0.28f), coreMat);

        // Surrounding orbital clock ring
        CreateVisualPart(PrimitiveType.Cylinder, model.transform, Vector3.zero, new Vector3(0.55f, 0.035f, 0.55f), rimMat, Quaternion.Euler(90f, 0, 0));

        // Top button stem
        CreateVisualPart(PrimitiveType.Cylinder, model.transform, new Vector3(0, 0.34f, 0), new Vector3(0.09f, 0.08f, 0.09f), rimMat);

        // Clock hands inside
        CreateVisualPart(PrimitiveType.Cube, model.transform, new Vector3(-0.06f, 0.06f, 0), new Vector3(0.045f, 0.16f, 0.03f), coreMat, Quaternion.Euler(0, 0, 35f));
        CreateVisualPart(PrimitiveType.Cube, model.transform, new Vector3(0.07f, 0.04f, 0), new Vector3(0.045f, 0.14f, 0.03f), coreMat, Quaternion.Euler(0, 0, -50f));

        return root;
    }

    static GameObject CreateVisualPart(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(type);
        var col = g.GetComponent<Collider>();
        if (col) Object.DestroyImmediate(col);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        if (rot.HasValue) g.transform.localRotation = rot.Value;
        var r = g.GetComponent<MeshRenderer>();
        if (r && mat) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }
}
