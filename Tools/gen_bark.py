"""Stylized hypercasual bark textures (tileable both ways) -> Assets/TC/Textures.
Bold rounded vertical plates, warm browns, light ridge highlights, round knots, moss + tiny leaves.
Run: python3 Tools/gen_bark.py"""
import numpy as np, os
from PIL import Image, ImageFilter
OUT=os.path.join(os.path.dirname(__file__),'..','Assets','TC','Textures')
def hexc(h): h=h.lstrip('#'); return np.array([int(h[i:i+2],16) for i in (0,2,4)],float)/255
def make(name,W,H,cols,base,dark,light,moss,seed,plates=7,knots=3,leaves=10,mossAmt=1.0):
    rng=np.random.default_rng(seed)
    y,x=np.mgrid[0:H,0:W]; u=x/W; v=y/H
    # periodic wobble so plates meander but tile
    wob=sum(rng.uniform(0.004,0.012)*np.sin(2*np.pi*(k*v+rng.random())) for k in (1,2))
    p=(u+wob)*plates; f=p-np.floor(p)                      # position across a plate
    idx=np.floor(p).astype(int)%plates
    # each plate gets a few horizontal breaks (rounded ends)
    off=rng.random(plates); seg=(v*2+off[idx]*2)%1.0
    across=np.abs(f-0.5)*2; along=np.abs(seg-0.5)*2
    d=np.sqrt(np.clip(across,0,1)**4+np.clip(along,0,1)**12)**0.25  # rounded-rect distance
    h=np.clip(1-d,0,1)**0.6                                  # height: plates domed, grooves low
    groove=np.clip((d-0.82)/0.12,0,1)
    tint=0.92+0.16*rng.random(plates)[idx]
    col=base[None,None]*tint[...,None]
    col=col*(1-groove[...,None])+dark[None,None]*groove[...,None]
    hl=np.clip((h-0.55)/0.4,0,1)*np.clip(1-(f-0.35)*3,0,1)   # light on the left ridge of each plate
    col=col+(light-base)[None,None]*hl[...,None]*0.75
    img=np.clip(col,0,1)
    def disc(cx,cy,r,c,soft=2.0,a=1.0):
        nonlocal img
        dx=np.minimum(np.abs(x-cx),W-np.abs(x-cx)); dy=np.minimum(np.abs(y-cy),H-np.abs(y-cy))
        m=np.clip((r-np.sqrt(dx*dx+dy*dy))/soft,0,1)*a
        img=img*(1-m[...,None])+np.array(c)[None,None]*m[...,None]; return m
    for k in range(knots):                                   # round knots with rings
        cx,cy,r=rng.uniform(0,W),rng.uniform(0,H),rng.uniform(W*0.035,W*0.06)
        disc(cx,cy,r*1.25,dark*0.9,3); disc(cx,cy,r,light*0.95,3); disc(cx,cy,r*0.55,base*0.85,2); disc(cx,cy,r*0.2,dark,2)
    for k in range(int(9*mossAmt)):                          # soft moss blobs (clusters of circles)
        cx,cy=rng.uniform(0,W),rng.uniform(0,H)
        for j in range(5):
            disc(cx+rng.normal(0,W*0.02),cy+rng.normal(0,W*0.02),rng.uniform(W*0.012,W*0.028),moss*rng.uniform(0.9,1.1),2.5,0.95)
    for k in range(leaves):                                  # tiny two-tone leaves
        cx,cy=rng.uniform(0,W),rng.uniform(0,H); r=W*0.016; ang=rng.uniform(0,np.pi)
        for s in (-1,1):
            disc(cx+s*np.cos(ang)*r*0.8,cy+s*np.sin(ang)*r*0.8,r,moss*1.15,2); 
        disc(cx,cy,r*0.35,moss*0.7,1.5)
    a=(np.clip(img,0,1)*255).astype(np.uint8)
    Image.fromarray(a).save(os.path.join(OUT,name+'.png'))
    # normal map from height
    hh=np.array(Image.fromarray((h*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2)),float)/255
    gx=np.roll(hh,-1,1)-np.roll(hh,1,1); gy=np.roll(hh,-1,0)-np.roll(hh,1,0); s=4.0
    n=np.dstack([-gx*s,gy*s,np.ones_like(hh)]); n/=np.linalg.norm(n,axis=2,keepdims=True)
    Image.fromarray(((n*0.5+0.5)*255).astype(np.uint8)).save(os.path.join(OUT,name+'_n.png'))
os.makedirs(OUT,exist_ok=True)
make('Bark_Trunk',1024,1024,None,hexc('#A8683D'),hexc('#6B3B22'),hexc('#E2A46A'),hexc('#7CC756'),3,plates=8,knots=3,leaves=12)
make('Bark_Branch',512,512,None,hexc('#B9774A'),hexc('#77432A'),hexc('#EDB47C'),hexc('#86D05E'),7,plates=5,knots=1,leaves=4,mossAmt=0.5)
print(os.listdir(OUT))
