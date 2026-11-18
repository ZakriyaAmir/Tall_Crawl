import numpy as np
import wave

SR = 44100

def generate_soft_rich_coin_bonus_sfx():
    # A rich, pleasing double-ring golden coin chime (warm major 3rd interval: C#6 (1108 Hz) -> E6 (1318 Hz))
    # It sounds like high-grade solid heavy gold coins clinking together with a warm, satisfying resonance
    dur1 = 0.05
    dur2 = 0.22
    total_dur = dur1 + dur2
    n = int(SR * total_dur)
    t = np.arange(n) / SR

    # First coin clink note (C#6: 1108.73 Hz)
    t1 = t[t < dur1]
    att1 = np.minimum(1.0, t1 / 0.0015)
    f1 = 1108.73
    s1 = (np.sin(2 * np.pi * f1 * t1) +
          0.35 * np.sin(2 * np.pi * (f1 * 2) * t1) * np.exp(-t1 * 25.0) +
          0.15 * np.sin(2 * np.pi * (f1 * 3) * t1) * np.exp(-t1 * 40.0) +
          0.06 * np.sin(2 * np.pi * (f1 * 4.2) * t1) * np.exp(-t1 * 60.0))
    s1 = s1 * att1 * np.exp(-t1 * 22.0)

    # Second rewarding coin resolution note (E6: 1318.51 Hz) with a warm golden sustain
    t2 = t[t >= dur1] - dur1
    att2 = np.minimum(1.0, t2 / 0.0015)
    f2 = 1318.51
    s2 = (np.sin(2 * np.pi * f2 * t2) +
          0.40 * np.sin(2 * np.pi * (f2 * 2) * t2) * np.exp(-t2 * 18.0) +
          0.18 * np.sin(2 * np.pi * (f2 * 3) * t2) * np.exp(-t2 * 30.0) +
          0.08 * np.sin(2 * np.pi * (f2 * 4.15) * t2) * np.exp(-t2 * 45.0))
    decay2 = np.exp(-t2 * 14.0)
    fade_end = np.clip((dur2 - t2) / 0.02, 0, 1)
    s2 = s2 * att2 * decay2 * fade_end

    output = np.concatenate([s1, s2])

    # Warm, soft peak amplitude matching the regular coin volume (~0.38-0.42 peak, pleasing and never harsh or piercing)
    peak = np.max(np.abs(output))
    if peak > 0:
        output = (output / peak) * 0.42

    int_samples = (output * 32767).astype(np.int16)

    out_file = "Assets/TC/Resources/Sfx/coin_bonus.wav"
    with wave.open(out_file, "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(int_samples.tobytes())

    print("Successfully generated gentle, rich golden coin bonus sound at", out_file)

if __name__ == "__main__":
    generate_soft_rich_coin_bonus_sfx()
