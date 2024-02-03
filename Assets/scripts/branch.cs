using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class branch : MonoBehaviour
{
    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("playerReflection"))
        {
            StartCoroutine(destroyBranch());
        }
    }
    
    IEnumerator destroyBranch()
    {
        yield return new WaitForSeconds(0.6f);
        Destroy(gameObject);
    }
}
