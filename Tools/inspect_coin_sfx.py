import wave

with wave.open("Assets/TC/Resources/Sfx/coin.wav", "rb") as w:
    print("coin.wav:", w.getnchannels(), "channels,", w.getsampwidth(), "bytes/samp,", w.getframerate(), "Hz,", w.getnframes(), "frames (", w.getnframes()/w.getframerate(), "s)")
