import numpy as np
import scipy.io.wavfile as w
import os

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'TC', 'Resources', 'Sfx')

def env(n, a=0.005, r=0.06):
    t = np.arange(n) / SR
    e = np.minimum(1, t / a) * np.minimum(1, (n / SR - t) / r)
    return np.clip(e, 0, 1)

def noise(n):
    return np.random.default_rng(42).standard_normal(n)

def boop(f, d, decay=18):
    n = int(SR * d)
    t = np.arange(n) / SR
    return np.sin(2 * np.pi * f * t * (1 - 0.25 * t / d)) * np.exp(-t * decay) * env(n, 0.002, 0.02)

def squeak(f0, f1, d, vib=0, vr=28, form=2.0, curve=1.0):
    n = int(SR * d)
    t = np.arange(n) / SR
    k = (t / d) ** curve
    f = f0 * (f1 / f0) ** k * (1 + vib * np.sin(2 * np.pi * vr * t))
    ph = 2 * np.pi * np.cumsum(f) / SR
    s = np.sin(ph) + 0.35 * np.sin(2 * ph) + 0.12 * np.sin(3 * ph)
    s *= 0.75 + 0.25 * np.sin(np.pi * form * t / d)
    return s * env(n, 0.003, 0.05)

def buzz(f0, f1, d, buzz_rate=120):
    n = int(SR * d)
    t = np.arange(n) / SR
    f = f0 + (f1 - f0) * (t / d)
    ph = 2 * np.pi * np.cumsum(f) / SR
    # Harsh triangular / saw-like harmonics for insect buzz
    s = np.sin(ph) + 0.6 * np.sin(2 * ph) + 0.4 * np.sin(3 * ph) + 0.25 * np.sin(4 * ph)
    mod = 0.5 + 0.5 * np.sin(2 * np.pi * buzz_rate * t)
    return s * mod * env(n, 0.002, 0.04)

def save(name, s, g=0.55):
    s = s / np.max(np.abs(s)) * g
    path = os.path.join(OUT, name + '.wav')
    w.write(path, SR, (s * 32767).astype(np.int16))
    print(f"Generated {name}.wav -> {path}")

# 1. Wasp Kill SFX:
# Punchy crunchy impact pop + high-pitched buzz squeal spiraling down and cutting out
def gen_wasp_kill():
    d = 0.36
    n = int(SR * d)
    t = np.arange(n) / SR
    
    # Impact crunch (low thump + snappy high pop)
    thump = boop(180, 0.12, decay=30)
    crunch = noise(int(SR * 0.14)) * np.exp(-np.arange(int(SR * 0.14)) / SR * 40)
    crunch *= env(len(crunch), 0.001, 0.02) * 0.7
    
    # Dying wasp zap / buzz pitch dive (650Hz diving to 140Hz)
    wasp_dive = buzz(680, 130, 0.30, buzz_rate=140)
    
    # Splat squeal
    splat_squeal = squeak(1400, 320, 0.26, vib=0.08, vr=35, curve=1.5)
    
    # Combine layers
    sig = np.zeros(n)
    sig[:len(thump)] += thump * 0.8
    sig[:len(crunch)] += crunch * 0.9
    sig[:len(wasp_dive)] += wasp_dive * 0.65
    sig[:len(splat_squeal)] += splat_squeal * 0.5
    return sig * env(n, 0.002, 0.08)

# 2. Bees Swarm Kill SFX:
# Fiery whoosh poof + multi-tone bee swarm scatter / dispersed pop & fizzle
def gen_bees_kill():
    d = 0.38
    n = int(SR * d)
    t = np.arange(n) / SR
    
    # Hot fire burst / poof
    fire_poof = noise(n) * np.exp(-t * 12) * np.sin(np.pi * np.clip(t / 0.18, 0, 1))
    
    # Swarm buzzing chaotic pop (multiple detuned high-freq bursts dispersing)
    swarm1 = buzz(440, 180, 0.28, buzz_rate=95)
    swarm2 = buzz(520, 220, 0.24, buzz_rate=130)
    swarm3 = squeak(1100, 400, 0.25, vib=0.15, vr=50)
    
    sig = fire_poof * 0.65
    sig[:len(swarm1)] += swarm1 * 0.45
    sig[:len(swarm2)] += swarm2 * 0.40
    sig[:len(swarm3)] += swarm3 * 0.45
    
    # Soft burst pop at start
    pop = boop(220, 0.10, decay=25)
    sig[:len(pop)] += pop * 0.6
    return sig * env(n, 0.003, 0.09)

# 3. Squirrel Kill SFX:
# Cartoon squirrel comical high-pitched squeak "YIPE!" + tumbling chirp & thump
def gen_squirrel_kill():
    d = 0.35
    n = int(SR * d)
    t = np.arange(n) / SR
    
    # Comical sharp startled screech/chirp rising then dropping ("CHIRP-EEP!")
    yipe1 = squeak(850, 2400, 0.11, vib=0.04, vr=25, curve=0.6)
    yipe2 = squeak(2400, 950, 0.18, vib=0.06, vr=30, curve=1.4)
    yipe = np.concatenate([yipe1, yipe2])
    
    # Cartoon impact thud
    thump = boop(160, 0.14, decay=20)
    
    sig = np.zeros(n)
    sig[:len(thump)] += thump * 0.75
    l_yipe = min(len(sig), len(yipe))
    sig[:l_yipe] += yipe[:l_yipe] * 0.85
    return sig * env(n, 0.002, 0.06)

save('wasp_kill', gen_wasp_kill(), g=0.55)
save('bees_kill', gen_bees_kill(), g=0.55)
save('squirrel_kill', gen_squirrel_kill(), g=0.55)
