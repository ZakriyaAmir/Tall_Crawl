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
