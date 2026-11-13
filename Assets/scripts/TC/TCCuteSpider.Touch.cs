using UnityEngine;

/// Menu / skin-preview tap actions: 12 procedural gags, never the same twice in a row.
/// Each one plays a fitting stinger plus a chattering voice (the menu "wave" clip, or the
/// in-game death voice for dizzy / flop gags only).
public partial class TCCuteSpider
{
    public enum TouchAct
    {
        Wave, Somersault, Blink, TwoLegs, Dance, Turn,
        Spin, Wink, WebTrick, Hang, Wiggle, Flop
    }

    int lastTouch = -1;
    TouchAct touchAct;
    float touchT, touchDur;
    bool touchBusy;
    int winkEye;
    LineRenderer touchSilk;
    Vector3 bodyTouchOff;
    Quaternion bodyTouchRot = Quaternion.identity;
    float eyeLidMul = 1f;
    int eyeLidOnly = -1; // -1 both, else index into eyes
    bool eyeOverrideActive;

    public void SetEyeOverride(float lidMul, int onlyIndex = -1)
    {
        eyeOverrideActive = true;
        eyeLidMul = lidMul;
        eyeLidOnly = onlyIndex;
        ApplyTouchOffsets();
    }

    public void ClearEyeOverride()
    {
        eyeOverrideActive = false;
        eyeLidMul = 1f;
        eyeLidOnly = -1;
        ApplyTouchOffsets();
    }

    public bool IsTouchActing => touchBusy;

    /// Pick a random action (no immediate repeat) and play it. Returns false if one is already running.
    public bool PlayRandomTouch()
    {
        if (touchBusy) return false;
        int n = System.Enum.GetValues(typeof(TouchAct)).Length;
        int pick;
        do { pick = Random.Range(0, n); } while (n > 1 && pick == lastTouch);
        lastTouch = pick;
        BeginTouch((TouchAct)pick);
        return true;
    }

    public void BeginTouch(TouchAct act)
    {
        touchAct = act;
        touchT = 0f;
        touchDur = DurationOf(act);
        touchBusy = true;
        winkEye = Random.value < 0.5f ? 0 : 2; // main left or right eye (skip tiny laterals)
        bodyTouchOff = Vector3.zero;
        bodyTouchRot = Quaternion.identity;
        eyeLidMul = 1f;
        eyeLidOnly = -1;
        if (act == TouchAct.Somersault) TriggerBackflip(0.85f);
        if (act == TouchAct.Spin) TriggerRoll(0.9f);
        PlayTouchAudio(act);
    }

    static float DurationOf(TouchAct a) => a switch
    {
        TouchAct.Wave => 1.15f,
        TouchAct.Somersault => 0.95f,
        TouchAct.Blink => 0.7f,
        TouchAct.TwoLegs => 1.35f,
        TouchAct.Dance => 1.55f,
        TouchAct.Turn => 1.05f,
        TouchAct.Spin => 1.15f,
        TouchAct.Wink => 0.7f,
        TouchAct.WebTrick => 1.35f,
        TouchAct.Hang => 1.7f,
        TouchAct.Wiggle => 0.95f,
        TouchAct.Flop => 1.4f,
        _ => 1f
    };

    static void PlayTouchAudio(TouchAct a)
    {
        // voicePitch is the per-action base; PlayAction adds a small random jitter.
        // Dizzy / flop gags alone use the death voice, pitched up so it reads as a gag, not a KO.
        switch (a)
        {
            case TouchAct.Wave:       TCSfx.PlayAction("chime", 0.7f, 1.00f); break;
            case TouchAct.Somersault: TCSfx.PlayAction("whoosh", 0.7f, 1.16f); break;
            case TouchAct.Blink:      TCSfx.PlayAction("boop", 0.75f, 1.24f); break;
            case TouchAct.TwoLegs:    TCSfx.PlayAction("rise", 0.65f, 0.96f); break;
            case TouchAct.Dance:      TCSfx.PlayAction("dance", 0.7f, 1.10f); break;
            case TouchAct.Turn:       TCSfx.PlayAction("whoosh", 0.5f, 0.98f); break;
            case TouchAct.Spin:       TCSfx.PlayAction("spin_whoosh", 0.7f, 1.22f, dizzy: true, voiceVol: 0.42f); break;
            case TouchAct.Wink:       TCSfx.PlayAction("wink", 0.75f, 1.28f); break;
            case TouchAct.WebTrick:   TCSfx.PlayAction("thwip", 0.75f, 1.06f); break;
            case TouchAct.Hang:       TCSfx.PlayAction("thwip", 0.7f, 1.12f, dizzy: true, voiceVol: 0.40f); break;
            case TouchAct.Wiggle:     TCSfx.PlayAction("wiggle", 0.7f, 1.14f); break;
            case TouchAct.Flop:       TCSfx.PlayAction("land", 0.65f, 1.18f, dizzy: true, voiceVol: 0.45f); break;
        }
    }

    /// Called from LateUpdate after the regular walk / idle pose is built.
    void TickTouch(float dt)
    {
        if (!touchBusy)
        {
            // Ease any leftover touch offsets back to rest
            bodyTouchOff = Vector3.Lerp(bodyTouchOff, Vector3.zero, 1f - Mathf.Exp(-10f * dt));
            bodyTouchRot = Quaternion.Slerp(bodyTouchRot, Quaternion.identity, 1f - Mathf.Exp(-10f * dt));
            if (!eyeOverrideActive) eyeLidMul = Mathf.MoveTowards(eyeLidMul, 1f, dt * 8f);
            ApplyTouchOffsets();
            return;
        }

        touchT += dt;
        float u = Mathf.Clamp01(touchT / Mathf.Max(0.01f, touchDur));
        EvaluateTouch(u);

        if (touchT >= touchDur)
        {
            touchBusy = false;
            wave = 0f;
            eyeLidMul = 1f;
            eyeLidOnly = -1;
            if (touchSilk) { Object.Destroy(touchSilk.gameObject); touchSilk = null; }
        }
        ApplyTouchOffsets();
    }

    void ApplyTouchOffsets()
    {
        if (!body) return;
        body.localPosition = bodyRest + bodyTouchOff;
        // Compose on top of whatever LateUpdate already set
        body.localRotation = bodyTouchRot * body.localRotation;
        // Eye lids
        if (eyes.Count > 0 && (eyeLidMul < 0.999f || eyeOverrideActive))
        {
            for (int i = 0; i < eyes.Count; i++)
            {
                if (!eyes[i]) continue;
                if (eyeLidOnly >= 0 && i != eyeLidOnly) continue;
                var r = eyeRest[i];
                eyes[i].localScale = new Vector3(r.x, r.y * eyeLidMul, r.z);
            }
        }
    }

    void EvaluateTouch(float u)
    {
        bodyTouchOff = Vector3.zero;
        bodyTouchRot = Quaternion.identity;
        eyeLidMul = 1f;
        eyeLidOnly = -1;
        wave = 0f;

        float ease = u * u * (3f - 2f * u);
        float bell = Mathf.Sin(u * Mathf.PI);

        switch (touchAct)
        {
            case TouchAct.Wave:
                wave = Mathf.Clamp01(bell * 1.4f);
                bodyTouchRot = Quaternion.Euler(0, Mathf.Sin(u * Mathf.PI * 2f) * 8f, 0);
                break;

            case TouchAct.Somersault:
                // Backflip is driven by TriggerBackflip; add a happy little hop
                bodyTouchOff = new Vector3(0, Mathf.Sin(u * Mathf.PI) * 0.08f, 0);
                break;

            case TouchAct.Blink:
                // Two slow cute blinks
                {
                    float w1 = Pulse(u, 0.15f, 0.28f);
                    float w2 = Pulse(u, 0.55f, 0.28f);
                    eyeLidMul = 1f - 0.9f * Mathf.Max(w1, w2);
                }
                break;

            case TouchAct.TwoLegs:
                // Lean back, rear legs plant, front legs wave a little
                {
                    float hold = Mathf.Clamp01(bell * 1.2f);
                    bodyTouchRot = Quaternion.Euler(-28f * hold, 0, 0);
                    bodyTouchOff = new Vector3(0, 0.05f * hold, -0.03f * hold);
                    wave = hold * 0.6f;
                    // Tuck mid legs in, stretch rear (applied in ApplyTouchLegs)
                }
                break;

            case TouchAct.Dance:
                {
                    float sway = Mathf.Sin(u * Mathf.PI * 6f) * 16f * bell;
                    bodyTouchRot = Quaternion.Euler(6f * Mathf.Sin(u * Mathf.PI * 8f) * bell, 0, sway);
                    bodyTouchOff = new Vector3(0, Mathf.Abs(Mathf.Sin(u * Mathf.PI * 8f)) * 0.05f * bell, 0);
                }
                break;

            case TouchAct.Turn:
                bodyTouchRot = Quaternion.Euler(0, 360f * ease, 0);
                break;

            case TouchAct.Spin:
                // Roll is driven by TriggerRoll; add a dizzy wobble at the end
                if (u > 0.65f)
                {
                    float dizz = (u - 0.65f) / 0.35f;
                    bodyTouchRot = Quaternion.Euler(0, 0, Mathf.Sin(dizz * Mathf.PI * 6f) * 14f * (1f - dizz));
                    eyeLidMul = 1f - 0.25f * Mathf.Sin(dizz * Mathf.PI);
                }
                break;

            case TouchAct.Wink:
                {
                    float w = Pulse(u, 0.18f, 0.45f);
                    eyeLidMul = 1f - 0.94f * w;
                    eyeLidOnly = winkEye;
                    bodyTouchRot = Quaternion.identity; // Comfortable happy wink without tilting body
                }
                break;

            case TouchAct.WebTrick:
                {
                    EnsureTouchSilk();
                    float shoot = Mathf.Clamp01(u / 0.25f);
                    float twirl = Mathf.Clamp01((u - 0.25f) / 0.6f);
                    if (touchSilk)
                    {
                        var origin = body.TransformPoint(new Vector3(0, 0.02f, 0.08f));
                        float len = 0.55f * shoot * (1f - 0.5f * Mathf.Clamp01((u - 0.8f) / 0.2f));
                        Vector3 dir = body.TransformDirection(Quaternion.Euler(0, twirl * 360f, 20f) * Vector3.forward);
                        touchSilk.enabled = len > 0.01f;
                        touchSilk.useWorldSpace = true;
                        touchSilk.SetPosition(0, origin);
                        touchSilk.SetPosition(1, origin + dir * len);
                    }
                    bodyTouchRot = Quaternion.Euler(0, twirl * 360f, 0);
                    wave = 0.4f * bell;
                }
                break;

            case TouchAct.Hang:
                {
                    EnsureTouchSilk();
                    float up = Mathf.Sin(Mathf.Clamp01(u / 0.35f) * Mathf.PI * 0.5f);
                    float hang = 1f;
                    if (u > 0.75f) hang = 1f - Mathf.Clamp01((u - 0.75f) / 0.25f);
                    bodyTouchOff = new Vector3(Mathf.Sin(u * 8f) * 0.03f * hang, 0.28f * up * hang, 0);
                    bodyTouchRot = Quaternion.Euler(0, 0, Mathf.Sin(u * 7f) * 10f * hang);
                    if (touchSilk)
                    {
                        var origin = body.position + Vector3.up * 0.7f;
                        touchSilk.enabled = hang > 0.05f;
                        touchSilk.useWorldSpace = true;
                        touchSilk.SetPosition(0, origin);
                        touchSilk.SetPosition(1, body.position + body.up * 0.05f);
                    }
                    if (u > 0.78f) eyeLidMul = 0.35f; // flop-eyed on the drop
                }
                break;

            case TouchAct.Wiggle:
                bodyTouchRot = Quaternion.Euler(0, Mathf.Sin(u * Mathf.PI * 8f) * 18f * bell, Mathf.Sin(u * Mathf.PI * 10f) * 10f * bell);
                bodyTouchOff = new Vector3(0, 0.02f * bell, 0);
                break;

            case TouchAct.Flop:
                {
                    // Play-dead: roll onto the back, little legs curl, then pop back up
                    float down = u < 0.55f ? ease / 0.55f * 0.55f : 0.55f; // climb to 0.55 then hold
                    if (u < 0.55f) down = Mathf.SmoothStep(0, 1, u / 0.55f);
                    else if (u < 0.8f) down = 1f;
                    else down = 1f - Mathf.SmoothStep(0, 1, (u - 0.8f) / 0.2f);
                    bodyTouchRot = Quaternion.Euler(0, 0, 165f * down);
                    bodyTouchOff = new Vector3(0, 0.04f * down, 0);
                    eyeLidMul = 1f - 0.35f * down;
                }
                break;
        }
    }

    static float Pulse(float u, float start, float dur)
    {
        if (u < start || u > start + dur) return 0f;
        return Mathf.Sin((u - start) / dur * Mathf.PI);
    }

    void EnsureTouchSilk()
    {
        if (touchSilk) return;
        var go = new GameObject("TouchSilk");
        go.transform.SetParent(transform, false);
        touchSilk = go.AddComponent<LineRenderer>();
        touchSilk.positionCount = 2;
        touchSilk.startWidth = 0.012f;
        touchSilk.endWidth = 0.006f;
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        touchSilk.material = new Material(sh) { color = new Color(1f, 1f, 1f, 0.9f) };
        touchSilk.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        touchSilk.enabled = false;
    }

    void OnDestroy()
    {
        if (touchSilk) Object.Destroy(touchSilk.gameObject);
    }
}
