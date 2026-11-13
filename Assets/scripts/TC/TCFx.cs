using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Game-feel helpers: pooled one-shot particles and camera shake (applied after the camera Animator).
public class TCFx : MonoBehaviour
{
    static TCFx inst;
    readonly Dictionary<GameObject, TCPool> pools = new Dictionary<GameObject, TCPool>();
    float shake; Vector3 lastOffset;
    Transform cam;

    static TCFx I { get { if (!inst) inst = new GameObject("TCFx").AddComponent<TCFx>(); return inst; } }

    public static void Spawn(GameObject prefab, Vector3 pos)
    {
        if (!prefab) return;
        var me = I;
        if (!me.pools.TryGetValue(prefab, out var pool)) { pool = new TCPool(prefab, me.transform, 2); me.pools[prefab] = pool; }
        var g = pool.Get(pos, Quaternion.identity);
        foreach (var ps in g.GetComponentsInChildren<ParticleSystem>()) { ps.Clear(); ps.Play(); }
        me.StartCoroutine(me.Return(pool, g));
    }
    System.Collections.IEnumerator Return(TCPool p, GameObject g) { yield return new WaitForSeconds(2f); p.Release(g); }

    public static void Shake(float amount) { I.shake = Mathf.Max(I.shake, amount); }

    void LateUpdate()
    {
        if (!cam && Camera.main) cam = Camera.main.transform;
        if (!cam) return;
        cam.localPosition -= lastOffset;
        lastOffset = Vector3.zero;
        if (shake > 0.001f && Time.timeScale > 0)
        {
            lastOffset = Random.insideUnitSphere * shake * 0.25f;
            cam.localPosition += lastOffset;
            shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 1.2f);
        }
    }
}

/// <summary>
/// Arcade-style pure white flash mask when enemies or branches are smashed/destroyed.
/// <summary>
/// Spawns a smooth fade-away ghost for branches or enemies when hit/destroyed by the spider.
/// Clones the renderers, preserving their textures and colors with a transparent fade shader,
/// and smoothly animates alpha from 1.0 down to 0.0 over 0.5s before cleanly destroying itself.
/// </summary>
public static class TCHitFlash
{
    static Shader fadeShader;

    public static Shader FadeShader
    {
        get
        {
            if (!fadeShader)
            {
                fadeShader = Shader.Find("TallCrawl/TCFadeEntity")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Mobile/Particles/Alpha Blended")
                    ?? Shader.Find("Unlit/Transparent");
            }
            return fadeShader;
        }
    }

    /// <summary>
    /// Backward-compatible alias for FadeAndDestroy.
    /// </summary>
    public static void FlashAndDestroy(GameObject target, float duration = 0.5f)
    {
        FadeAndDestroy(target, duration);
    }

    /// <summary>
    /// Spawns an immediate visual ghost of the target GameObject that smoothly fades away over duration (0.5f).
    /// Preserves original mesh, baked skinned poses, textures, and color tints while fading alpha to zero.
    /// </summary>
    public static void FadeAndDestroy(GameObject target, float duration = 0.5f)
    {
        if (!target) return;

        var renderers = target.GetComponentsInChildren<Renderer>(false);
        if (renderers == null || renderers.Length == 0) return;

        var fadeHost = new GameObject("HitFade_" + target.name);
        fadeHost.transform.position = Vector3.zero;
        fadeHost.transform.rotation = Quaternion.identity;
        fadeHost.transform.localScale = Vector3.one;

        Shader shader = FadeShader;
        var createdMats = new List<Material>();
        var baseColors = new List<Color>();
        var bakedMeshes = new List<Mesh>();
        int renderersCloned = 0;

        foreach (var r in renderers)
        {
            if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            Mesh meshToRender = null;
            if (r is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                meshToRender = mf.sharedMesh;
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                if (!smr.sharedMesh) continue;
                Mesh baked = new Mesh();
                smr.BakeMesh(baked);
                bakedMeshes.Add(baked);
                meshToRender = baked;
            }

            if (!meshToRender) continue;

            var go = new GameObject(r.name + "_FadeGhost");
            go.transform.position = r.transform.position;
            go.transform.rotation = r.transform.rotation;
            go.transform.localScale = r.transform.lossyScale;
            go.transform.SetParent(fadeHost.transform, true);

            var nmf = go.AddComponent<MeshFilter>();
            nmf.sharedMesh = meshToRender;

            var nmr = go.AddComponent<MeshRenderer>();
            var sharedMats = r.sharedMaterials;
            int matCount = (sharedMats != null && sharedMats.Length > 0) ? sharedMats.Length : 1;
            var matsForR = new Material[matCount];

            for (int m = 0; m < matCount; m++)
            {
                Material orig = (sharedMats != null && m < sharedMats.Length) ? sharedMats[m] : null;
                Material fMat = (shader != null) ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
                fMat.name = "FadeMat_" + (orig ? orig.name : "Default");

                Color col = Color.white;
                Texture tex = null;
                if (orig != null)
                {
                    if (orig.HasProperty("_MainTex") && orig.mainTexture != null)
                        tex = orig.mainTexture;
                    else if (orig.HasProperty("_BaseMap") && orig.GetTexture("_BaseMap") != null)
                        tex = orig.GetTexture("_BaseMap");

                    if (orig.HasProperty("_Color"))
                        col = orig.GetColor("_Color");
                    else if (orig.HasProperty("_BaseColor"))
                        col = orig.GetColor("_BaseColor");
                }

                if (tex != null)
                {
                    fMat.mainTexture = tex;
                    if (fMat.HasProperty("_BaseMap")) fMat.SetTexture("_BaseMap", tex);
                }
                if (fMat.HasProperty("_Color")) fMat.SetColor("_Color", col);
                if (fMat.HasProperty("_BaseColor")) fMat.SetColor("_BaseColor", col);

                matsForR[m] = fMat;
                createdMats.Add(fMat);
                baseColors.Add(col);
            }

            nmr.sharedMaterials = matsForR;
            nmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            nmr.receiveShadows = false;
            renderersCloned++;
        }

        if (renderersCloned == 0)
        {
            Object.Destroy(fadeHost);
            return;
        }

        var runner = fadeHost.AddComponent<TCHitFlashRunner>();
        runner.StartFade(fadeHost, createdMats, baseColors, bakedMeshes, duration);
    }
}

public static class TCHitFade
{
    public static void FadeAndDestroy(GameObject target, float duration = 0.5f)
    {
        TCHitFlash.FadeAndDestroy(target, duration);
    }
}

public class TCHitFlashRunner : MonoBehaviour
{
    public void StartFade(GameObject host, List<Material> mats, List<Color> baseColors, List<Mesh> bakedMeshes, float duration)
    {
        StartCoroutine(FadeRoutine(host, mats, baseColors, bakedMeshes, duration));
    }

    IEnumerator FadeRoutine(GameObject host, List<Material> mats, List<Color> baseColors, List<Mesh> bakedMeshes, float duration)
    {
        if (duration <= 0.001f) duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t;

            if (mats != null && baseColors != null)
            {
                int count = Mathf.Min(mats.Count, baseColors.Count);
                for (int i = 0; i < count; i++)
                {
                    var m = mats[i];
                    if (m != null)
                    {
                        Color c = baseColors[i];
                        c.a *= alpha;
                        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    }
                }
            }
            yield return null;
        }

        // Clean up created material instances and baked meshes
        if (mats != null)
        {
            for (int i = 0; i < mats.Count; i++)
            {
                if (mats[i] != null) Destroy(mats[i]);
            }
        }
        if (bakedMeshes != null)
        {
            for (int i = 0; i < bakedMeshes.Count; i++)
            {
                if (bakedMeshes[i] != null) Destroy(bakedMeshes[i]);
            }
        }

        if (host != null)
        {
            Destroy(host);
        }
    }
}
