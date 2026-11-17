using UnityEngine;

/// polish3: tiny sparkle on ribbon headers - slow twinkle (scale + spin), unscaled time so it runs while paused.
public class TCTwinkle : MonoBehaviour
{
    public float phase, speed = 2.2f;
    void Update()
    {
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed + phase);
        transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.1f, k * k);
        transform.localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 40f + phase * 50f);
    }
}
