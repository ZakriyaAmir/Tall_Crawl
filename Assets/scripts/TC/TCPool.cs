using System.Collections.Generic;
using UnityEngine;

/// Minimal prefab pool, avoids Instantiate/Destroy (GC + spikes) during runs.
public class TCPool
{
    readonly GameObject prefab; readonly Transform parent;
    readonly Stack<GameObject> free = new Stack<GameObject>();
    public TCPool(GameObject prefab, Transform parent, int prewarm)
    {
        this.prefab = prefab; this.parent = parent;
        for (int i = 0; i < prewarm; i++) { var g = Object.Instantiate(prefab, parent); g.SetActive(false); free.Push(g); }
    }
    public GameObject Get(Vector3 pos, Quaternion rot)
    {
        var g = free.Count > 0 ? free.Pop() : Object.Instantiate(prefab, parent);
        g.transform.SetPositionAndRotation(pos, rot);
        g.SetActive(true);
        return g;
    }
    public void Release(GameObject g) { if (!g) return; g.SetActive(false); free.Push(g); }
}

/// Tag for pooled objects so collisions can return them to their pool.
public class TCPooled : MonoBehaviour
{
    public TCPool pool; public string kind; public float y;
    public void Release() { if (pool != null) pool.Release(gameObject); else gameObject.SetActive(false); }
}
