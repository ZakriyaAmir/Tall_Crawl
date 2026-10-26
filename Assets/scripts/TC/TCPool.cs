using System.Collections.Generic;
using UnityEngine;

/// Minimal prefab pool, avoids Instantiate/Destroy (GC + spikes) during runs.
public class TCPool
{
    readonly GameObject prefab; readonly Transform parent;
    readonly Stack<GameObject> free = new Stack<GameObject>();
    public int Created { get; private set; }
    public TCPool(GameObject prefab, Transform parent, int prewarm)
    {
        this.prefab = prefab; this.parent = parent;
        for (int i = 0; i < prewarm; i++) { var g = Object.Instantiate(prefab, parent); Created++; g.SetActive(false); free.Push(g); }
    }
    public GameObject Get(Vector3 pos, Quaternion rot)
    {
        GameObject g; if (free.Count > 0) g = free.Pop(); else { g = Object.Instantiate(prefab, parent); Created++; }
        var tag = g.GetComponent<TCPooled>(); if (tag) tag.inPool = false;
        g.transform.SetPositionAndRotation(pos, rot);
        g.SetActive(true);
        return g;
    }
    public void Release(GameObject g)
    {
        if (!g) return;
        var tag = g.GetComponent<TCPooled>();
        if (tag) { if (tag.inPool) return; tag.inPool = true; }
        g.SetActive(false); free.Push(g);
    }
    public int FreeCount => free.Count;
}

/// Tag for pooled objects so collisions can return them to their pool.
public class TCPooled : MonoBehaviour
{
    public TCPool pool; public string kind; public float y; public bool inPool;
    public void Release() { if (pool != null) pool.Release(gameObject); else gameObject.SetActive(false); }
}
