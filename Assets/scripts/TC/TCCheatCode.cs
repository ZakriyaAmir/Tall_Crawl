using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Hidden settings Easter-egg. Sequence (all within 6 s of the first tap):
/// Sound, Sound, Vibration, Vibration, slider far-left, far-right, far-left, far-right.
/// Intermediate slider values during the drag phase are ignored, so the L-R-L-R
/// half can be done as one continuous finger-down sweep or as separate drags.
/// Any other control, a wrong extreme, a wrong button, or a timeout resets the
/// sequence. Sound and Vibration still toggle their settings; the slider still
/// writes sensitivity.
public class TCCheatCode
{
    public const float Timeout = 6f;
    public const float ExtremeEps = 0.08f; // fraction of slider range (forgiving on a fat handle)
    public const float DuplicateWindow = 0.05f; // ignore duplicate feeds within 50 ms

    public enum Kind { Sound, Vibration, Music, SliderMin, SliderMax, SliderOther, Other }

    public int Progress { get; private set; } // 0 = idle, 1..8 in progress, 8 just completed before reset
    public bool Active => Progress > 0;
    public float StartTime { get; private set; }

    Kind lastFeedKind;
    float lastFeedTime = -999f;

    static readonly Kind[] Expected =
    {
        Kind.Sound, Kind.Sound, Kind.Vibration, Kind.Vibration,
        Kind.SliderMin, Kind.SliderMax, Kind.SliderMin, Kind.SliderMax
    };

    /// Feed an input at time <paramref name="now"/> (unscaled seconds).
    /// Returns true only when the full sequence just completed successfully.
    public bool Feed(Kind kind, float now)
    {
        if (Active && now - StartTime > Timeout)
            Reset();

        // One physical tap can raise two UI events on device (touch + mouse, or a
        // double-bound listener). Collapse those so a single Sound tap is one step.
        if (now - lastFeedTime < DuplicateWindow && kind == lastFeedKind)
        {
            Debug.Log("[TCCheat] Feed " + kind + " ignored duplicate at progress " + Progress);
            return false;
        }
        lastFeedTime = now;
        lastFeedKind = kind;

        int before = Progress;
        bool completed = false;

        if (!Active)
        {
            if (kind == Kind.Sound)
            {
                Progress = 1;
                StartTime = now;
            }
            Debug.Log("[TCCheat] Feed " + kind + " progress " + before + "->" + Progress);
            return false;
        }

        Kind expect = Expected[Progress];
        // During the slider half, in-between values are part of the drag, not a mistake.
        if (IsSliderStep(expect) && kind == Kind.SliderOther)
        {
            Debug.Log("[TCCheat] Feed " + kind + " progress " + Progress + " (mid-drag ignored)");
            return false;
        }
        // Repeated events at the same extreme (Unity fires onValueChanged while the
        // handle sits on the clamp, and a drag can re-enter that end) must not reset.
        if (IsSliderStep(expect) && IsSliderStep(kind) && kind != expect
            && Progress > 0 && kind == Expected[Progress - 1])
        {
            Debug.Log("[TCCheat] Feed " + kind + " progress " + Progress + " (repeat extreme ignored)");
            return false;
        }

        if (kind != expect)
        {
            Debug.Log("[TCCheat] Feed " + kind + " progress " + before + "->0 (reset, expected " + expect + ")");
            Reset();
            // A wrong input does not also start a new sequence.
            return false;
        }

        Progress++;
        if (Progress >= Expected.Length)
        {
            Reset();
            completed = true;
        }
        Debug.Log("[TCCheat] Feed " + kind + " progress " + before + "->" + Progress + (completed ? " COMPLETE" : ""));
        return completed;
    }

    public void Reset()
    {
        Progress = 0;
        StartTime = 0f;
    }

    public static Kind ClassifySlider(float value, float min, float max)
    {
        float range = Mathf.Max(1e-4f, max - min);
        float u = (value - min) / range;
        const float slop = 1e-4f;
        if (u <= ExtremeEps + slop) return Kind.SliderMin;
        if (u >= 1f - ExtremeEps - slop) return Kind.SliderMax;
        return Kind.SliderOther;
    }

    static bool IsSliderStep(Kind k) => k == Kind.SliderMin || k == Kind.SliderMax;

    /// Pure logic checks used by TCSelfTest / batchmode. Returns the number of failures.
    public static int RunSelfTest()
    {
        int fails = 0;
        void Check(bool ok, string what)
        {
            if (!ok) fails++;
            Debug.Log((ok ? "CHEAT ok   " : "CHEAT FAIL ") + what);
        }

        var c = new TCCheatCode();
        // Happy path at t=0..5
        Check(!c.Feed(Kind.Sound, 0.0f) && c.Progress == 1, "first Sound starts the sequence");
        Check(!c.Feed(Kind.Sound, 0.2f) && c.Progress == 2, "second Sound advances");
        Check(!c.Feed(Kind.Vibration, 0.4f) && c.Progress == 3, "first Vibration advances");
        Check(!c.Feed(Kind.Vibration, 0.6f) && c.Progress == 4, "second Vibration advances");
        Check(!c.Feed(Kind.SliderOther, 0.8f) && c.Progress == 4, "mid-drag ignored");
        Check(!c.Feed(Kind.SliderMin, 1.0f) && c.Progress == 5, "far left #1");
        Check(!c.Feed(Kind.SliderOther, 1.2f) && c.Progress == 5, "travel toward right ignored");
        Check(!c.Feed(Kind.SliderMax, 1.4f) && c.Progress == 6, "far right #1");
        Check(!c.Feed(Kind.SliderMin, 1.6f) && c.Progress == 7, "far left #2");
        Check(c.Feed(Kind.SliderMax, 1.8f) && c.Progress == 0, "far right #2 completes and resets");

        // Wrong button in the middle resets and does not complete
        c = new TCCheatCode();
        c.Feed(Kind.Sound, 0f); c.Feed(Kind.Sound, 0.1f);
        Check(!c.Feed(Kind.Music, 0.2f) && c.Progress == 0, "Music in between resets");
        Check(!c.Feed(Kind.Vibration, 0.3f) && c.Progress == 0, "Vibration after reset does not start a new sequence");

        // Wrong order (Vibration first) does nothing
        c = new TCCheatCode();
        Check(!c.Feed(Kind.Vibration, 0f) && c.Progress == 0, "Vibration first is ignored");

        // Wrong slider extreme
        c = new TCCheatCode();
        c.Feed(Kind.Sound, 0f); c.Feed(Kind.Sound, 0.1f);
        c.Feed(Kind.Vibration, 0.2f); c.Feed(Kind.Vibration, 0.3f);
        Check(!c.Feed(Kind.SliderMax, 0.4f) && c.Progress == 0, "far right when far left expected resets");

        // Timeout
        c = new TCCheatCode();
        c.Feed(Kind.Sound, 0f); c.Feed(Kind.Sound, 0.1f);
        c.Feed(Kind.Vibration, 0.2f); c.Feed(Kind.Vibration, 0.3f);
        Check(!c.Feed(Kind.SliderMin, 6.01f) && c.Progress == 0, "input after 6s timeout resets (does not advance)");

        // Timeout is measured from the first tap, even if later taps are on time relative to each other
        c = new TCCheatCode();
        c.Feed(Kind.Sound, 1.0f); c.Feed(Kind.Sound, 1.2f);
        c.Feed(Kind.Vibration, 1.4f); c.Feed(Kind.Vibration, 1.6f);
        c.Feed(Kind.SliderMin, 1.8f); c.Feed(Kind.SliderMax, 2.0f); c.Feed(Kind.SliderMin, 2.2f);
        Check(!c.Feed(Kind.SliderMax, 7.05f) && c.Progress == 0, "last extreme after 6s from first tap fails");

        // Completing just inside the window succeeds
        c = new TCCheatCode();
        c.Feed(Kind.Sound, 2.0f); c.Feed(Kind.Sound, 2.2f);
        c.Feed(Kind.Vibration, 3.0f); c.Feed(Kind.Vibration, 3.5f);
        c.Feed(Kind.SliderMin, 4.0f); c.Feed(Kind.SliderMax, 5.0f); c.Feed(Kind.SliderMin, 6.0f);
        Check(c.Feed(Kind.SliderMax, 7.99f) && c.Progress == 0, "completes at 5.99s");

        // Other input (Done / Privacy) mid-sequence
        c = new TCCheatCode();
        c.Feed(Kind.Sound, 0f); c.Feed(Kind.Sound, 0.1f);
        Check(!c.Feed(Kind.Other, 0.2f) && c.Progress == 0, "unrelated button resets");

        // Repeated same-extreme events during the drag half do not reset
        c = new TCCheatCode();
        c.Feed(Kind.Sound, 0f); c.Feed(Kind.Sound, 0.2f);
        c.Feed(Kind.Vibration, 0.4f); c.Feed(Kind.Vibration, 0.6f);
        Check(!c.Feed(Kind.SliderMin, 0.8f) && c.Progress == 5, "first min advances");
        Check(!c.Feed(Kind.SliderMin, 0.85f) && c.Progress == 5, "repeat min ignored, not reset");
        Check(!c.Feed(Kind.SliderOther, 0.9f) && c.Progress == 5, "travel ignored");
        Check(!c.Feed(Kind.SliderMax, 1.0f) && c.Progress == 6, "first max advances");
        Check(!c.Feed(Kind.SliderMax, 1.05f) && c.Progress == 6, "repeat max ignored");
        Check(!c.Feed(Kind.SliderMin, 1.2f) && c.Progress == 7, "second min advances");
        Check(c.Feed(Kind.SliderMax, 1.4f) && c.Progress == 0, "second max completes after repeats");

        // ONE continuous drag: dense onValueChanged samples (~60 fps) without any
        // pointer-up, sweeping mid->L->R->L->R. Mid values and end-stop repeats
        // must not reset, and the four extremes must complete the cheat.
        c = new TCCheatCode();
        c.Feed(Kind.Sound, 0f); c.Feed(Kind.Sound, 0.2f);
        c.Feed(Kind.Vibration, 0.4f); c.Feed(Kind.Vibration, 0.6f);
        float t = 0.8f;
        bool Sweep(float fromU, float toU, bool expectComplete)
        {
            bool hit = false;
            const float lo = 0.4f, hi = 2.2f;
            const int n = 16;
            for (int i = 0; i <= n; i++)
            {
                float u = Mathf.Lerp(fromU, toU, i / (float)n);
                t += 0.016f;
                if (c.Feed(ClassifySlider(Mathf.Lerp(lo, hi, u), lo, hi), t)) hit = true;
            }
            for (int i = 0; i < 3; i++)
            {
                t += 0.016f;
                if (c.Feed(ClassifySlider(Mathf.Lerp(lo, hi, toU), lo, hi), t)) hit = true;
            }
            return hit == expectComplete;
        }
        Check(Sweep(0.20f, 0f, false) && c.Progress == 5, "continuous: first left end");
        Check(Sweep(0f, 1f, false) && c.Progress == 6, "continuous: first right end");
        Check(Sweep(1f, 0f, false) && c.Progress == 7, "continuous: second left end");
        Check(Sweep(0f, 1f, true) && c.Progress == 0, "continuous: second right end completes (one drag)");

        // Separate drags with a pause (and implicit finger-up) between extremes
        c = new TCCheatCode();
        c.Feed(Kind.Sound, 0f); c.Feed(Kind.Sound, 0.2f);
        c.Feed(Kind.Vibration, 0.4f); c.Feed(Kind.Vibration, 0.6f);
        t = 0.8f;
        Check(Sweep(0.20f, 0f, false) && c.Progress == 5, "separate: first left end");
        t += 0.25f;
        Check(Sweep(0f, 1f, false) && c.Progress == 6, "separate: first right end");
        t += 0.25f;
        Check(Sweep(1f, 0f, false) && c.Progress == 7, "separate: second left end");
        t += 0.25f;
        Check(Sweep(0f, 1f, true) && c.Progress == 0, "separate: second right end completes");

        // Duplicate events within 50 ms (double click / double listener) count once
        c = new TCCheatCode();
        Check(!c.Feed(Kind.Sound, 0.0f) && c.Progress == 1, "first Sound starts");
        Check(!c.Feed(Kind.Sound, 0.02f) && c.Progress == 1, "duplicate Sound within 50ms ignored");
        Check(!c.Feed(Kind.Sound, 0.10f) && c.Progress == 2, "second real Sound advances");

        // ClassifySlider — 8% band at each end
        Check(ClassifySlider(0.4f, 0.4f, 2.2f) == Kind.SliderMin, "classify min");
        Check(ClassifySlider(0.4f + 0.08f * 1.8f, 0.4f, 2.2f) == Kind.SliderMin, "classify min at 8%");
        Check(ClassifySlider(2.2f, 0.4f, 2.2f) == Kind.SliderMax, "classify max");
        Check(ClassifySlider(2.2f - 0.08f * 1.8f, 0.4f, 2.2f) == Kind.SliderMax, "classify max at 8%");
        Check(ClassifySlider(1.3f, 0.4f, 2.2f) == Kind.SliderOther, "classify mid");

        return fails;
    }

    /// Unlock everything the cheat promises and persist it.
    public static void Grant()
    {
        var skins = TCSkins.All;
        for (int i = 0; i < skins.Length; i++) TCSave.UnlockSkin(i);
        // Shop upgrades are ids 1..6 with caps {_,5,2,3,3,5,5}
        int[] max = { 0, 5, 2, 3, 3, 5, 5 };
        for (int u = 1; u < max.Length; u++) TCSave.SetUpgrade(u, max[u]);
        TCSave.Coins = 9_000_000;
        TCSave.Gems = 9_000_000;
        TCSave.NoAds = true;
        TCServices.Banner(false);
        Debug.Log("TCCheatCode: granted all skins, max upgrades, 9e6 coins/gems, ads off");
    }
}

/// Forwards pointer-down on the sensitivity slider so an already-at-extreme
/// starting point counts, and so a single finger-down L-R-L-R sweep works
/// the same as separate drags. onValueChanged still carries the motion.
public class TCCheatSliderHook : MonoBehaviour, IPointerDownHandler
{
    Slider slider;
    Action<float> feed;

    public void Bind(Slider s, Action<float> onValue)
    {
        slider = s;
        feed = onValue;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (slider == null || feed == null) return;
        feed(slider.value);
    }
}
