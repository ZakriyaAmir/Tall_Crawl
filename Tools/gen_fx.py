#!/usr/bin/env python3
"""Procedural VFX textures for Tall Crawl (pickups, bursts, boost fire/shield, speed lines).
Writes PNGs into Assets/TC/Resources/FX (or argv[1]). White/greyscale-with-alpha textures are tinted
by particle vertex colour; flame/noise are full colour."""
import sys, os, math
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', 'Assets', 'TC', 'Resources', 'FX')
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)

def grid(n):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    return (x + 0.5) / n * 2 - 1, -((y + 0.5) / n * 2 - 1)

def save(name, rgb, a):
    a = np.clip(a, 0, 1)
    if rgb is None: rgb = np.ones(a.shape + (3,), np.float32)
    img = np.dstack([np.clip(rgb, 0, 1), a])
    Image.fromarray((img * 255 + 0.5).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name + '.png'), optimize=True)

def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

N = 256
x, y = grid(N); r = np.hypot(x, y); ang = np.arctan2(y, x)

# soft glow (gaussian + tight core)
save('fx_glow', None, np.exp(-(r / 0.42) ** 2) * 0.85 + np.exp(-(r / 0.12) ** 2) * 0.35)
# thin soft ring (shockwave)
ring = np.exp(-((r - 0.82) / 0.07) ** 2) + 0.35 * np.exp(-((r - 0.74) / 0.16) ** 2)
save('fx_ring', None, ring * smooth(1.0, 0.92, r))
# starburst rays (12 tapered rays, alternating length)
k = 12
rays = np.abs(np.cos(ang * k / 2)) ** 18
long_ = 0.55 + 0.45 * (np.cos(ang * k / 2 * 0) * 0 + (np.cos(ang * k) > 0))
rays_a = rays * np.clip(1 - r / (0.55 + 0.4 * (np.cos(ang * k / 2 + 0.0) ** 2 > 0.5)), 0, 1) ** 1.5
save('fx_rays', None, np.clip(rays_a * smooth(0.06, 0.2, r) + np.exp(-(r / 0.18) ** 2) * 0.4, 0, 1))

n = 128
x, y = grid(n); r = np.hypot(x, y); ang = np.arctan2(y, x)
# 4-point twinkle star
star = np.exp(-np.abs(x) / 0.05) * np.exp(-np.abs(y) / 0.55) + np.exp(-np.abs(y) / 0.05) * np.exp(-np.abs(x) / 0.55)
star += 0.45 * (np.exp(-np.abs(x + y) / 0.07) * np.exp(-np.abs(x - y) / 0.5) + np.exp(-np.abs(x - y) / 0.07) * np.exp(-np.abs(x + y) / 0.5)) * 0.5
star += np.exp(-(r / 0.16) ** 2)
save('fx_star', None, np.clip(star, 0, 1) * smooth(1.0, 0.8, r))
# round spark (stretched into lines by stretched billboards)
save('fx_spark', None, np.exp(-(r / 0.35) ** 2) * 0.6 + np.exp(-(r / 0.15) ** 2) * 0.6)
# heart (white, soft edge, highlight darker rim so the pink tint reads with depth)
hx, hy = x * 1.15, y * 1.15 + 0.12
heart = (hx ** 2 + hy ** 2 - 0.55) ** 3 - hx ** 2 * hy ** 3  # implicit heart
inside = heart < 0
d = np.where(inside, 1.0, 0.0)
from PIL import ImageFilter
hm = Image.fromarray((d * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
ha = np.asarray(hm, np.float32) / 255
shade = 0.78 + 0.22 * np.clip(1 - np.hypot(x + 0.3, y - 0.3) / 0.9, 0, 1)
hl = 0.8 * np.exp(-(np.hypot(x + 0.36, y - 0.3) / 0.075) ** 2)
rgb = np.dstack([shade + hl, shade + hl, shade + hl])
glow = np.asarray(Image.fromarray((d * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(9)), np.float32) / 255
save('fx_heart', rgb, np.maximum(ha, glow * 0.55))
def seg(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay; wx, wy = px - ax, py - ay
    t = np.clip((wx * vx + wy * vy) / (vx * vx + vy * vy), 0, 1)
    return np.hypot(wx - t * vx, wy - t * vy)
# snowflake: 6 arms, each with two pairs of V twigs
sd = np.full(r.shape, 9.0)
for i in range(6):
    a = i * math.pi / 3; ca, sa = math.cos(a), math.sin(a)
    sd = np.minimum(sd, seg(x, y, 0, 0, 0.86 * ca, 0.86 * sa) - 0.05)
    for t, L in ((0.42, 0.3), (0.64, 0.2)):
        for s_ in (1, -1):
            b = a + s_ * math.pi / 3
            sd = np.minimum(sd, seg(x, y, t * ca, t * sa, t * ca + L * math.cos(b), t * sa + L * math.sin(b)) - 0.035)
sd = np.minimum(sd, r - 0.16)
sf = smooth(0.02, -0.02, sd)
sfg = np.exp(-np.clip(sd, 0, None) / 0.08)
save('fx_snow', None, np.maximum(sf, sfg * 0.5))
# lightning bolt: tapered zigzag polyline
P = [(0.12, 0.92), (-0.22, 0.12), (0.12, 0.08), (-0.18, -0.92)]
bd = np.full(r.shape, 9.0)
for j in range(len(P) - 1):
    w = 0.13 - 0.03 * j
    bd = np.minimum(bd, seg(x, y, *P[j], *P[j + 1]) - w)
bs = smooth(0.02, -0.02, bd); bg = np.exp(-np.clip(bd, 0, None) / 0.1)
save('fx_bolt', None, np.maximum(bs, bg * 0.65))
# smoke puff (lumpy soft cloud)
puff = np.zeros_like(r)
for i in range(7):
    cx, cy, rr = rng.uniform(-0.35, 0.35), rng.uniform(-0.35, 0.35), rng.uniform(0.3, 0.5)
    puff = np.maximum(puff, np.exp(-((np.hypot(x - cx, y - cy)) / rr) ** 2))
save('fx_smoke', None, puff * smooth(1.0, 0.7, r) * 0.9)
# flame tongue (colour): teardrop pointing up, white-yellow core -> orange -> red edge
fy = (y + 1) / 2  # 0 bottom 1 top
width = 0.62 * np.sqrt(np.clip(1 - fy, 0, 1)) * np.clip(fy * 3.2, 0, 1) ** 0.5
wob = x + 0.06 * np.sin(fy * 9)
dist = np.abs(wob) / np.maximum(width, 1e-3)
body = smooth(1.0, 0.55, dist) * smooth(0.98, 0.7, fy) * smooth(0.0, 0.12, fy)
core = smooth(0.75, 0.0, dist) * smooth(0.75, 0.15, fy)
col = np.dstack([np.ones_like(x), 0.35 + 0.55 * core + 0.1 * body, 0.08 + 0.75 * core ** 2])
save('fx_flame', col, np.clip(body * 0.95 + core * 0.2, 0, 1))

# tileable textures (256)
N = 256
def tile_noise(N, octaves=5, seed=1):
    g = np.random.default_rng(seed); acc = np.zeros((N, N), np.float32); amp = 1; tot = 0
    for o in range(octaves):
        f = 4 * 2 ** o
        base = g.random((f, f)).astype(np.float32)
        im = Image.fromarray((base * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC)
        # make tileable: wrap by resizing a tiled 3x version and cropping centre
        big = np.tile(base, (3, 3))
        im = Image.fromarray((big * 255).astype(np.uint8)).resize((N * 3, N * 3), Image.BICUBIC).crop((N, N, 2 * N, 2 * N))
        acc += amp * np.asarray(im, np.float32) / 255; tot += amp; amp *= 0.5
    acc /= tot; acc = (acc - acc.min()) / (acc.max() - acc.min()); return acc
nz = tile_noise(N)
save('fx_noise', np.dstack([nz, nz, nz]), np.ones_like(nz))
# hex grid (tileable: hex width = N/4 columns)
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
s = N / 8.0
q = xx / s; rr_ = yy / (s * math.sqrt(3) / 1.0)
# distance to hex edges via axial trick
def hexdist(px, py):
    # px,py in hex-size units; returns distance to nearest hex centre edge (0 centre, 1 edge)
    a = np.stack([px, py])
    best = np.full(px.shape, 9.0)
    for ox in (0, 0.5):
        cx = np.round(px - ox) + ox
        cy = np.round(py / math.sqrt(3) - (ox * 2) / 2) * math.sqrt(3) + ox * math.sqrt(3)
        dx = np.abs(px - cx); dy = np.abs(py - cy)
        d = np.maximum(dx * 2 / 1, (dx + dy * math.sqrt(3)) )  # hex metric
        best = np.minimum(best, d)
    return best
px = xx / (N / 8.0); py = yy / (N / 8.0) * (4 * 2 * math.sqrt(3) / 8.0) / 1.0 * (8.0 / (4 * 2 * math.sqrt(3))) * (4 * math.sqrt(3) * 2 / 8.0)
hd = hexdist(px, py)
edge = smooth(0.80, 0.95, hd)
save('fx_hex', None, np.clip(edge, 0, 1))
print('wrote', sorted(os.listdir(OUT)))
