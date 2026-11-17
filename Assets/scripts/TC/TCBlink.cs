using UnityEngine;

/// Spider-style blink for every child named "Eye*" that has a "Ball": quick squash every 2-4.5 s (sometimes a double blink).
public class TCBlink : MonoBehaviour
{
    Transform[] balls; Vector3[] rest; float next, t0 = -1f; bool dbl;
    void Awake()
    {
        var list = new System.Collections.Generic.List<Transform>();
        foreach (var tr in GetComponentsInChildren<Transform>(true)) if (tr.name == "Ball" && tr.parent && tr.parent.name.StartsWith("Eye")) list.Add(tr);
        balls = list.ToArray(); rest = new Vector3[balls.Length]; for (int i = 0; i < balls.Length; i++) rest[i] = balls[i].localScale;
        next = Time.time + Random.Range(0.6f, 3f);
    }
    void LateUpdate()
    {
        float t = Time.time;
        if (t0 < 0 && t >= next) { t0 = t; dbl = Random.value < 0.25f; }
        float k = 1f;
        if (t0 >= 0)
        {
            float e = t - t0, len = dbl ? 0.36f : 0.15f;
            float ph = Mathf.Repeat(e, 0.15f) / 0.15f; k = e < len ? Mathf.Lerp(1f, 0.08f, Mathf.Sin(ph * Mathf.PI)) : 1f;
            if (e >= len) { t0 = -1f; next = t + Random.Range(2f, 4.5f); }
        }
        for (int i = 0; i < balls.Length; i++) if (balls[i]) balls[i].localScale = new Vector3(rest[i].x, rest[i].y * k, rest[i].z);
    }
}
