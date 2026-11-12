using UnityEngine;

/// Hidden settings Easter-egg. Sequence (all within 6 s of the first tap):
/// Sound, Sound, Vibration, Vibration, slider far-left, far-right, far-left, far-right.
/// Intermediate slider values during the drag phase are ignored. Any other control,
/// a wrong extreme, a wrong button, or a timeout resets the sequence. Sound and
/// Vibration still toggle their settings; the slider still writes sensitivity.
public class TCCheatCode
{
    public const float Timeout = 6f;
    public const float ExtremeEps = 0.04f; // fraction of slider range

    public enum Kind { Sound, Vibration, Music, SliderMin, SliderMax, SliderOther, Other }

    public int Progress { get; private set; } // 0 = idle, 1..8 in progress, 8 just completed before reset
    public bool Active => Progress > 0;
    public float StartTime { get; private set; }

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

        if (!Active)
        {
            if (kind == Kind.Sound)
            {
                Progress = 1;
                StartTime = now;
            }
            return false;
        }

        Kind expect = Expected[Progress];
        // During the slider half, in-between values are part of the drag, not a mistake.
        if (IsSliderStep(expect) && kind == Kind.SliderOther)
            return false;

        if (kind != expect)
        {
            Reset();
            // A wrong input does not also start a new sequence.
            return false;
        }

        Progress++;
        if (Progress >= Expected.Length)
        {
            Reset();
            return true;
        }
        return false;
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
        if (u <= ExtremeEps) return Kind.SliderMin;
        if (u >= 1f - ExtremeEps) return Kind.SliderMax;
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

        // ClassifySlider
        Check(ClassifySlider(0.4f, 0.4f, 2.2f) == Kind.SliderMin, "classify min");
        Check(ClassifySlider(2.2f, 0.4f, 2.2f) == Kind.SliderMax, "classify max");
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
