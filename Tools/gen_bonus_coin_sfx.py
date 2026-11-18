import numpy as np
import wave
import struct

SR = 44100

def generate_string_bonus_coin_sfx():
    # 4-note ascending celestial harmonic arpeggio (E6 -> G#6 -> B6 -> E7) with shimmering bell harmonics & lingering crystal sparkle
    notes = [1318.51, 1661.22, 1975.53, 2637.02]
    step_duration = 0.055
    tail_duration = 0.40
    total_duration = len(notes) * step_duration + tail_duration
    total_samples = int(SR * total_duration)
    output = np.zeros(total_samples, dtype=np.float32)

    for i, freq in enumerate(notes):
        start_t = i * step_duration
        start_idx = int(start_t * SR)
        dur = total_duration - start_t
        n_samples = int(dur * SR)
        t = np.arange(n_samples) / SR

        # Crisp percussive hammer strike attack + smooth exponential decay
        attack = np.minimum(1.0, t / 0.002)
        decay = np.exp(-t * (10.0 if i < len(notes) - 1 else 6.5))

        # Fundamental + harmonic overtones + high crystalline shimmer
        fundamental = np.sin(2 * np.pi * freq * t)
        h2 = 0.40 * np.sin(2 * np.pi * (freq * 2.0) * t) * np.exp(-t * 12.0)
        h3 = 0.20 * np.sin(2 * np.pi * (freq * 3.0) * t) * np.exp(-t * 20.0)
        h4 = 0.12 * np.sin(2 * np.pi * (freq * 4.0) * t) * np.exp(-t * 28.0)
        shimmer = 0.08 * np.sin(2 * np.pi * (freq * 4.02) * t) * np.exp(-t * 15.0)

        # Extra triumphant emphasis on the final resolution note (E7)
        gain = 1.35 if i == len(notes) - 1 else 0.85
        note_wave = (fundamental + h2 + h3 + h4 + shimmer) * attack * decay * gain

        # Add into output buffer
        end_idx = min(total_samples, start_idx + n_samples)
        output[start_idx:end_idx] += note_wave[:end_idx - start_idx]

    # Peak normalize cleanly
    output = output / (np.max(np.abs(output)) + 1e-6)
    output *= 0.88

    # Convert to 16-bit PCM
    int_samples = (output * 32767).astype(np.int16)

    out_file = "Assets/TC/Resources/Sfx/coin_bonus.wav"
    with wave.open(out_file, "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(int_samples.tobytes())

    print("Generated string bonus coin sound at:", out_file)

if __name__ == "__main__":
    generate_string_bonus_coin_sfx()
