import wave
import numpy as np

with wave.open("Assets/TC/Resources/Sfx/coin.wav", "rb") as w:
    frames = w.readframes(w.getnframes())
    samples = np.frombuffer(frames, dtype=np.int16)
    print("coin.wav duration:", len(samples) / w.getframerate(), "s, peak amplitude:", np.max(np.abs(samples)))
