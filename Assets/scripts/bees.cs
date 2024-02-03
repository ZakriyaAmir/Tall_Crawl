using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bees : MonoBehaviour
{
    private void OnBecameVisible()
    {
        gameObject.GetComponent<AudioSource>().Play();
    }
}
