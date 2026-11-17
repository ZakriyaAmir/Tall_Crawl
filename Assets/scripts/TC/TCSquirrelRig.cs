using UnityEngine;

/// Fluid bounding gallop for the squirrel (runs in LateUpdate, after TCSquirrel's own pose): long leg swings with knee
/// bend, spine stretch/squash, head nod, ear flicks and a two-piece tail that sways with follow-through.
public class TCSquirrelRig : MonoBehaviour
{
    public Transform torso, head, tailBase, tailTip; public Transform[] legs;
    Transform[] knees; Transform[] ears; float phase; Vector3 torsoScale = Vector3.one;
    void Awake()
    {
        if (legs != null) { knees = new Transform[legs.Length]; for (int i = 0; i < legs.Length; i++) knees[i] = legs[i] ? legs[i].Find("Knee") : null; }
        ears = head ? new[] { head.Find("EarL"), head.Find("EarR") } : new Transform[0];
        if (torso) torsoScale = torso.localScale; phase = Random.value * 6f;
    }
    void LateUpdate()
    {
        bool fast = player.Instance && player.Instance.boost;
        float p = Time.time * (fast ? 19f : 14f) + phase;
        // bound: fronts reach together (slight split), hinds push together half a cycle later
        float[] off = { 0f, 0.45f, Mathf.PI, Mathf.PI + 0.45f };
        for (int i = 0; i < 4 && legs != null && i < legs.Length; i++)
        {
            if (!legs[i]) continue;
            bool hind = i >= 2; float s = Mathf.Sin(p + off[i]);
            float swing = hind ? -s * 52f + 6f : s * 58f - 4f;
            legs[i].localRotation = Quaternion.Euler(swing, 0, 0);
            if (knees[i]) knees[i].localRotation = Quaternion.Euler(hind ? Mathf.Max(0f, Mathf.Cos(p + off[i])) * -62f : Mathf.Max(0f, -Mathf.Cos(p + off[i])) * 70f, 0, 0);
        }
        if (torso)
        {
            float st = Mathf.Sin(p * 1f + 0.6f);
            torso.localScale = new Vector3(torsoScale.x * (1f - 0.03f * st), torsoScale.y * (1f - 0.05f * st), torsoScale.z * (1f + 0.08f * st));
            torso.localRotation = Quaternion.Euler(Mathf.Sin(p + 1.2f) * 5f, 0, 0);
        }
        if (head) head.localRotation = Quaternion.Euler(8f + Mathf.Sin(p - 0.7f) * 9f, Mathf.Sin(p * 0.37f) * 7f, Mathf.Sin(p * 0.5f) * 4f);
        for (int i = 0; i < ears.Length; i++) if (ears[i]) { float sg = i == 0 ? -1 : 1; ears[i].localRotation = Quaternion.Euler(-20f + Mathf.Sin(p - 1.4f) * 10f, sg * 10f, sg * (-24f + Mathf.Sin(p * 0.6f + i) * 8f)); }
        if (tailBase) tailBase.localRotation = Quaternion.Euler(Mathf.Sin(p - 1.0f) * 16f, Mathf.Sin(p * 0.5f) * 20f, Mathf.Sin(p * 0.5f - 0.4f) * 6f);
        if (tailTip) tailTip.localRotation = Quaternion.Euler(Mathf.Sin(p - 2.1f) * 24f, Mathf.Sin(p * 0.5f - 1.1f) * 30f, 0);
    }
}
