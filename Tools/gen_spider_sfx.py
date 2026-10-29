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
