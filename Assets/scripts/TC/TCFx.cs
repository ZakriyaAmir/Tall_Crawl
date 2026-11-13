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
/// Uses an unlit white material over a temporary ghost hierarchy of the target,
/// so the original target can immediately disable/pool while a brilliant pure-white mask
/// flashes for a fraction of a second and cleanly self-destructs.
/// </summary>
public static class TCHitFlash
{
    static Material whiteMaskMat;

    public static Material WhiteMaskMat
    {
        get
        {
            if (!whiteMaskMat)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Mobile/Particles/Alpha Blended");

                whiteMaskMat = new Material(s);
                whiteMaskMat.name = "TC_HitFlash_WhiteMask";

                if (whiteMaskMat.HasProperty("_Color")) whiteMaskMat.SetColor("_Color", Color.white);
                if (whiteMaskMat.HasProperty("_BaseColor")) whiteMaskMat.SetColor("_BaseColor", Color.white);
                if (whiteMaskMat.HasProperty("_EmissionColor"))
                {
                    whiteMaskMat.SetColor("_EmissionColor", Color.white * 2f);
                    whiteMaskMat.EnableKeyword("_EMISSION");
                }
            }
            return whiteMaskMat;
        }
    }

    /// <summary>
    /// Spawns an immediate pure white flash mask for the given target GameObject.
    /// Clones renderers into a temporary flash ghost that holds pure white for duration, then destroys itself.
    /// </summary>
    public static void FlashAndDestroy(GameObject target, float duration = 0.12f)
    {
        if (!target) return;

        var renderers = target.GetComponentsInChildren<Renderer>(false);
        if (renderers == null || renderers.Length == 0) return;

        var flashHost = new GameObject("HitFlash_" + target.name);
        flashHost.transform.position = target.transform.position;
        flashHost.transform.rotation = target.transform.rotation;
        flashHost.transform.localScale = target.transform.lossyScale;

        Material wMat = WhiteMaskMat;
        var createdRenderers = new List<Renderer>();

        foreach (var r in renderers)
        {
            if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            if (r is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;

                var go = new GameObject(mr.name + "_Flash");
                go.transform.SetParent(flashHost.transform, false);
                go.transform.position = mr.transform.position;
                go.transform.rotation = mr.transform.rotation;
                go.transform.localScale = mr.transform.lossyScale;

                var nmf = go.AddComponent<MeshFilter>();
                nmf.sharedMesh = mf.sharedMesh;

                var nmr = go.AddComponent<MeshRenderer>();
                int matCount = mr.sharedMaterials != null ? Mathf.Max(1, mr.sharedMaterials.Length) : 1;
                var mats = new Material[matCount];
                for (int m = 0; m < matCount; m++) mats[m] = wMat;
                nmr.sharedMaterials = mats;
                nmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                nmr.receiveShadows = false;
                createdRenderers.Add(nmr);
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                if (!smr.sharedMesh) continue;

                var go = new GameObject(smr.name + "_Flash");
                go.transform.SetParent(flashHost.transform, false);
                go.transform.position = smr.transform.position;
                go.transform.rotation = smr.transform.rotation;
                go.transform.localScale = smr.transform.lossyScale;

                var nmf = go.AddComponent<MeshFilter>();
                Mesh bakedMesh = new Mesh();
                smr.BakeMesh(bakedMesh);
                nmf.sharedMesh = bakedMesh;

                var nmr = go.AddComponent<MeshRenderer>();
                int matCount = smr.sharedMaterials != null ? Mathf.Max(1, smr.sharedMaterials.Length) : 1;
                var mats = new Material[matCount];
                for (int m = 0; m < matCount; m++) mats[m] = wMat;
                nmr.sharedMaterials = mats;
                nmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                nmr.receiveShadows = false;
                createdRenderers.Add(nmr);
            }
        }

        if (createdRenderers.Count == 0)
        {
            Object.Destroy(flashHost);
            return;
        }

        var runner = flashHost.AddComponent<TCHitFlashRunner>();
        runner.StartFlash(flashHost, duration);
    }
}

public class TCHitFlashRunner : MonoBehaviour
{
    public void StartFlash(GameObject host, float duration)
    {
        StartCoroutine(FlashRoutine(host, duration));
    }

    IEnumerator FlashRoutine(GameObject host, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(host);
    }
}
