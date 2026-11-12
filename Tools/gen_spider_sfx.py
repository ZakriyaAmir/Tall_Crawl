"""Procedural cute cartoon spider voice SFX for Tall Crawl (no samples, no copyrighted audio).
Run: python3 Tools/gen_spider_sfx.py  -> Assets/TC/Resources/Sfx/*.wav"""
import numpy as np, scipy.io.wavfile as w, os
SR=44100; OUT=os.path.join(os.path.dirname(__file__),'..','Assets','TC','Resources','Sfx')
def env(n,a=0.005,r=0.06):
    t=np.arange(n)/SR; e=np.minimum(1,t/a)*np.minimum(1,(n/SR-t)/r); return np.clip(e,0,1)
def squeak(f0,f1,d,vib=0,vr=28,form=2.0,curve=1.0):
    n=int(SR*d); t=np.arange(n)/SR; k=(t/d)**curve
    f=f0*(f1/f0)**k*(1+vib*np.sin(2*np.pi*vr*t)); ph=2*np.pi*np.cumsum(f)/SR
    s=np.sin(ph)+0.35*np.sin(2*ph)+0.12*np.sin(3*ph)            # soft toon voice
    s*=0.75+0.25*np.sin(np.pi*form*t/d)                          # tiny "mouth" formant wobble
    return s*env(n)
def boop(f,d): n=int(SR*d); t=np.arange(n)/SR; return np.sin(2*np.pi*f*t*(1-0.25*t/d))*np.exp(-t*18)*env(n,0.002,0.02)
def sil(d): return np.zeros(int(SR*d))
def cat(*p): return np.concatenate(p)
def save(name,s,g=0.5):
    s=s/np.max(np.abs(s))*g; w.write(os.path.join(OUT,name+'.wav'),SR,(s*32767).astype(np.int16))
os.makedirs(OUT,exist_ok=True)
save('menu_drop',  squeak(1700,620,0.45,0.02,9,1,0.7),0.35)                     # wheee down the thread
save('menu_flip',  cat(squeak(700,1500,0.12),squeak(1500,1900,0.07)),0.4)          # hup!
save('land',       cat(boop(260,0.09),squeak(900,1300,0.09,0.04)),0.45)           # boop + eep
save('wave',       cat(squeak(1100,1600,0.09),sil(0.04),squeak(1200,1800,0.13,0.05,30)),0.42)  # "hi-hiii!"
save('jump',       squeak(500,1400,0.16,0,0,1,0.6),0.4)                           # boing-yip
save('coin',       cat(squeak(1900,2600,0.05),squeak(2400,3000,0.05)),0.3)        # tiny chirp
save('heart',      cat(squeak(900,1200,0.07),squeak(1200,1600,0.07),squeak(1600,2100,0.1)),0.38)
save('chilli',     cat(boop(320,0.07),boop(300,0.07),squeak(1000,2200,0.14,0.08,40)),0.42)  # nom-nom-yip!
save('hit',        squeak(1600,700,0.16,0.06,35),0.45)                            # ouch eek
save('bees',       squeak(1300,800,0.2,0.12,45),0.42)                             # buzzed eek
save('death',      cat(squeak(1000,800,0.18,0.03),squeak(850,600,0.2,0.03),squeak(700,300,0.45,0.05,7)),0.45)  # wah-wah-waaah
save('revive',     cat(squeak(600,900,0.08),squeak(900,1300,0.08),squeak(1300,1900,0.16,0.04)),0.4)
print(sorted(os.listdir(OUT)))

# --- Menu / skin tap-action stingers (additive; do not overwrite the clips above) ---
def noise(n):
    return np.random.default_rng(1).standard_normal(n)

def whoosh(d=0.28, f0=700, f1=180, g=0.45):
    n=int(SR*d); t=np.arange(n)/SR
    # band-passed noise sweep + sine body
    sig=noise(n)
    # cheap high-to-low emphasis via envelope-modulated sine + noise
    f=f0*(f1/f0)**(t/d)
    ph=2*np.pi*np.cumsum(f)/SR
    tone=np.sin(ph)*0.35
    # noise shaped by a falling band (multiply by rising-then-falling env)
    envn=np.sin(np.pi*t/d)**0.6
    sig=sig*envn*0.7+tone
    sig*=env(n,0.008,0.08)
    return sig

def thwip(d=0.16):
    n=int(SR*d); t=np.arange(n)/SR
    # sharp pluck + short rising zip (web leaving the spinneret)
    click=np.sin(2*np.pi*90*t)*np.exp(-t*70)
    zip_=squeak(400,2200,d*0.7,0.02,40,1.4,0.4)
    # pad zip to n
    if len(zip_)<n: zip_=np.pad(zip_,(0,n-len(zip_)))
    else: zip_=zip_[:n]
    return (click*0.8+zip_*0.9)*env(n,0.001,0.05)

def taps(count=5, gap=0.09, f=420):
    parts=[]
    for i in range(count):
        fi=f*(1.0+0.08*((-1)**i))
        parts.append(boop(fi,0.06))
        if i<count-1: parts.append(sil(gap))
    return cat(*parts)

# somersault / turn
save('whoosh', whoosh(0.32, 900, 160), 0.42)
# hedgehog spin — faster, slightly longer whirl
save('spin_whoosh', cat(whoosh(0.18, 1400, 400), whoosh(0.22, 1100, 180)), 0.4)
# blink
save('boop', cat(boop(640,0.07), sil(0.02), boop(820,0.06)), 0.5)
# wink — softer, single higher chirp
save('wink', cat(squeak(1400,2000,0.07,0.04,24), sil(0.02), boop(980,0.05)), 0.42)
# web shot
save('thwip', thwip(0.18), 0.5)
# dance rhythmic taps (left-right-left-right-tap)
save('dance', taps(6, 0.08, 380), 0.48)
# wave chime (bell-ish + existing voice flavour)
def chime():
    n=int(SR*0.35); t=np.arange(n)/SR
    s=0.6*np.sin(2*np.pi*880*t)*np.exp(-t*6)+0.35*np.sin(2*np.pi*1320*t)*np.exp(-t*8)+0.2*np.sin(2*np.pi*1760*t)*np.exp(-t*10)
    return s*env(n,0.002,0.12)
save('chime', cat(chime(), sil(0.03), squeak(1200,1800,0.10,0.04,26)), 0.4)
# stand-up effort
save('rise', cat(squeak(500,900,0.14,0.03,12,1,0.8), sil(0.04), boop(300,0.08)), 0.4)
# happy wiggle
save('wiggle', cat(boop(520,0.06), sil(0.04), boop(640,0.06), sil(0.04), boop(780,0.08)), 0.45)
print('touch sfx:', [n for n in sorted(os.listdir(OUT)) if n.endswith('.wav')])
