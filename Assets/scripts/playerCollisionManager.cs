using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class playerCollisionManager : MonoBehaviour
{

    public player plyer;
    private levelManager lvlManager;

    void Start()
    {
        plyer = FindObjectOfType<player>();
        lvlManager = FindObjectOfType<levelManager>();
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("treeChunk"))
        {
            StartCoroutine(destroyChunk(other.gameObject));
        }
        if (other.CompareTag("boost"))
        {
            StartCoroutine(destroyChunk(other.gameObject));
        }
        if (other.CompareTag("health"))
        {
            StartCoroutine(destroyChunk(other.gameObject));
        }
        if (other.CompareTag("bees"))
        {
            StartCoroutine(destroyChunk(other.gameObject));
        }
    }

    IEnumerator destroyChunk(GameObject other)
    {
        yield return new WaitForSeconds(0.5f);
        if (!plyer.GameOver)
        {
            lvlManager.DeleteChunk(other);
        }
    }
}
