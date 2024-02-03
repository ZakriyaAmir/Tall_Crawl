using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class continuousRotation : MonoBehaviour
{
    public bool x;
    public bool y;

    public bool z;
    public float speed;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (x)
        {
            transform.Rotate(speed * Time.deltaTime, 0, 0);
        }

        if (y)
        {
            transform.Rotate(0, speed * Time.deltaTime, 0);
        }

        if (z)
        {
            transform.Rotate(0, 0, - speed * Time.deltaTime);
        }
    }
}