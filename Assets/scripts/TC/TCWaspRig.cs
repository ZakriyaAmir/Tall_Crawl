using UnityEngine;

/// Wasp extras on top of TCWasp's wing buzz / abdomen pump / head twitch: springy antennae, dangling legs, body hum.
public class TCWaspRig : MonoBehaviour
{
    Transform al, ar; Transform[] legs; Quaternion[] legRest; Quaternion alR, arR; float phase;
    void Awake()
    {
        var h = transform.Find("Head"); al = h ? h.Find("AntennaL") : null; ar = h ? h.Find("AntennaR") : null;
        if (al) alR = al.localRotation; if (ar) arR = ar.localRotation;
        var l = new System.Collections.Generic.List<Transform>(); foreach (Transform c in transform) if (c.name.StartsWith("Leg")) l.Add(c);
        legs = l.ToArray(); legRest = new Quaternion[legs.Length]; for (int i = 0; i < legs.Length; i++) legRest[i] = legs[i].localRotation;
        phase = Random.value * 10f;
    }
    void LateUpdate()
    {
        float t = Time.time + phase;
        if (al) al.localRotation = alR * Quaternion.Euler(Mathf.Sin(t * 7f) * 9f, Mathf.Sin(t * 4.3f) * 6f, 0);
        if (ar) ar.localRotation = arR * Quaternion.Euler(Mathf.Sin(t * 7f + 1.3f) * 9f, Mathf.Sin(t * 4.3f + 0.8f) * 6f, 0);
        for (int i = 0; i < legs.Length; i++) legs[i].localRotation = legRest[i] * Quaternion.Euler(Mathf.Sin(t * 5f + i * 0.9f) * 10f, 0, 0);
        transform.localPosition = new Vector3(0, Mathf.Sin(t * 26f) * 0.004f, 0);
    }
}
