# Generates the snake SFX (cheap procedural): hiss.wav (soft cartoon "ssss") and snake_kill.wav (boing-pop squeak).
import wave, struct, math, random, uuid, os
SR = 44100
out = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'TC', 'Resources', 'Sfx')
def write(name, s):
    peak = max(1e-6, max(abs(x) for x in s)); s = [x / peak * 0.85 for x in s]
    with wave.open(os.path.join(out, name + '.wav'), 'w') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b''.join(struct.pack('<h', int(x * 32767)) for x in s))
    meta = os.path.join(out, name + '.wav.meta')
    if not os.path.exists(meta):
        tpl = open(os.path.join(out, 'bat.wav.meta')).read().splitlines()
        tpl[1] = 'guid: ' + uuid.uuid4().hex
        open(meta, 'w').write('\n'.join(tpl) + '\n')
random.seed(7)
# hiss: high-passed noise, two-pole resonance around 6 kHz, tongue-flutter AM, 0.75 s
n = int(SR * 0.75); s = []; lp = hp = 0.0; b1 = b2 = 0.0
f0 = 5800.0; r = 0.93; c = 2 * r * math.cos(2 * math.pi * f0 / SR)
for i in range(n):
    t = i / SR; x = random.uniform(-1, 1)
    y = x + c * b1 - r * r * b2; b2, b1 = b1, y
    hp = 0.85 * (hp + y - lp); lp = y
    env = min(1, t / 0.06) * (1 - t / 0.75) ** 1.6
    am = 0.75 + 0.25 * math.sin(2 * math.pi * 23 * t)
    s.append(hp * env * am)
# entry: short rattle burst (clicky grains ~28/s) leading into the hiss
rat = []
for i in range(int(SR * 0.32)):
    t = i / SR; g = (t * 28) % 1.0
    click = math.exp(-g * 30) * random.uniform(-1, 1)
    rat.append(click * min(1, t / 0.02) * (1 - t / 0.32) * 0.9)
mix = rat + [0.0] * (len(s))
for i, x in enumerate(s): mix[int(SR * 0.18) + i if int(SR * 0.18) + i < len(mix) else -1] += x
write('hiss', mix)
# snake_kill: cartoon squeak sliding down + soft pop
n = int(SR * 0.42); s = []; ph = 0
for i in range(n):
    t = i / SR; f = 1400 * (1 - t / 0.42) ** 1.5 + 260
    ph += 2 * math.pi * f / SR
    v = math.sin(ph) + 0.3 * math.sin(2 * ph) + 0.15 * math.sin(3 * ph + 0.5)
    env = min(1, t / 0.01) * math.exp(-t * 7)
    pop = math.exp(-t * 60) * random.uniform(-1, 1) * 0.6
    s.append(v * env * 0.7 + pop)
write('snake_kill', s)
# snake_hit: angry strike — sharp breathy 'HHKKSS' burst (wider band, fast attack) + low strike thump
n = int(SR * 0.55); s = []; b1 = b2 = 0.0; lp = hp = 0.0
f0 = 4200.0; r = 0.86; c = 2 * r * math.cos(2 * math.pi * f0 / SR)
for i in range(n):
    t = i / SR; x = random.uniform(-1, 1)
    y = x + c * b1 - r * r * b2; b2, b1 = b1, y
    hp = 0.8 * (hp + y - lp); lp = y
    env = min(1, t / 0.008) * math.exp(-t * 4.2)
    thump = math.sin(2 * math.pi * (140 - 80 * t) * t) * math.exp(-t * 22) * 2.2
    s.append(hp * env + thump)
write('snake_hit', s)
print('ok')
