using UnityEngine;

/// Bat wing flap: fast powerful downstroke, slower recovery, wrist fold that lags the shoulder, body bob,
/// head bob and ear wiggle. LateUpdate so it overrides TCWasp's generic wing rotation.
public class TCBatFlap : MonoBehaviour
{
    Transform wl, wr, tl, tr, body, head, el, er; float phase;
    void Awake()
    {
        wl = transform.Find("BatWingPivot_L"); wr = transform.Find("BatWingPivot_R");
        tl = wl ? wl.Find("WingTip") : null; tr = wr ? wr.Find("WingTip") : null;
        body = transform.Find("Body"); head = body ? body.Find("Head") : null;
        el = head ? head.Find("EarL") : null; er = head ? head.Find("EarR") : null; phase = Random.value * 10f;
    }
    void LateUpdate()
    {
        float c = Mathf.Repeat(Time.time * 4.6f + phase, 1f);
        // asymmetric stroke: 0..0.42 down (fast), 0.42..1 up (slow), eased
        float stroke = c < 0.42f ? Mathf.Lerp(1f, -1f, Mathf.SmoothStep(0, 1, c / 0.42f)) : Mathf.Lerp(-1f, 1f, Mathf.SmoothStep(0, 1, (c - 0.42f) / 0.58f));
        float flap = stroke * 50f + 6f;
        float fold = (c < 0.42f ? 4f : Mathf.Sin((c - 0.42f) / 0.58f * Mathf.PI) * 52f) + 6f; // folds on the upstroke
        float sweep = (c < 0.42f ? -1f : 1f) * 10f * Mathf.Sin(c * Mathf.PI * 2f);
        if (wl) wl.localRotation = Quaternion.Euler(sweep * 0.5f, -sweep, -flap);
        if (wr) wr.localRotation = Quaternion.Euler(sweep * 0.5f, sweep, flap);
        if (tl) tl.localRotation = Quaternion.Euler(0, -fold * 0.4f, -fold);
        if (tr) tr.localRotation = Quaternion.Euler(0, fold * 0.4f, fold);
        float bob = -stroke; // body rises on the downstroke
        if (body) body.localPosition = new Vector3(0, bob * 0.014f, 0);
        float t = Time.time + phase;
        if (head) head.localRotation = Quaternion.Euler(bob * 3f + Mathf.Sin(t * 1.3f) * 5f, Mathf.Sin(t * 0.8f) * 12f, Mathf.Sin(t * 1.1f) * 5f);
        if (el) el.localRotation = Quaternion.Euler(Mathf.Sin(t * 9f) * 4f, 0, -(-24f + Mathf.Sin(t * 3.1f) * 9f + bob * 4f));
        if (er) er.localRotation = Quaternion.Euler(Mathf.Sin(t * 9f + 1f) * 4f, 0, (-24f + Mathf.Sin(t * 3.1f + 1.7f) * 9f + bob * 4f));
    }
}
