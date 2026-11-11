#!/usr/bin/env python3
"""HUD lives-panel sprites (polish2): glossy candy heart, empty heart slot, soft glow."""
import os, numpy as np
from PIL import Image, ImageFilter
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'TC', 'Resources', 'TCUI')
N = 160
y, x = np.mgrid[0:N, 0:N].astype(np.float32)
x = (x + 0.5) / N * 2 - 1; y = -((y + 0.5) / N * 2 - 1)
def heart_mask(scale, dy=0.0, ss=4):
    # supersampled implicit heart
    big = N * ss
    yy, xx = np.mgrid[0:big, 0:big].astype(np.float32)
    X = ((xx + 0.5) / big * 2 - 1) / scale; Y = (-((yy + 0.5) / big * 2 - 1) + dy) / scale
    Y = Y + 0.1
    f = (X * X + Y * Y - 0.42) ** 3 - X * X * Y ** 3 * 1.15
    m = (f < 0).astype(np.float32)
    return np.asarray(Image.fromarray((m * 255).astype(np.uint8)).resize((N, N), Image.LANCZOS), np.float32) / 255
def sm(a, b, v):
    t = np.clip((v - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
outline = heart_mask(1.0)
fill = heart_mask(0.84, dy=-0.035)
cocoa = np.array([0x4A, 0x3B, 0x3F]) / 255
top = np.array([0xFF, 0x7A, 0x9A]) / 255; bot = np.array([0xE8, 0x2E, 0x5C]) / 255
g = np.clip((y + 0.7) / 1.4, 0, 1)[..., None]
body = bot * (1 - g) + top * g
# inner bottom shade + glossy highlight blob + small sparkle
shade = sm(0.2, -0.9, y)[..., None] * 0.18
body = body * (1 - shade)
hl = np.exp(-(((x + 0.32) / 0.2) ** 2 + ((y - 0.3) / 0.13) ** 2))
hl2 = np.exp(-(((x + 0.08) / 0.06) ** 2 + ((y - 0.42) / 0.06) ** 2))
body = body + (1 - body) * np.clip(hl * 0.95 + hl2 * 0.9, 0, 1)[..., None]
rgb = cocoa * (1 - fill[..., None]) + body * fill[..., None]
Image.fromarray((np.dstack([rgb, outline]) * 255 + 0.5).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'life_heart.png'))
# empty slot: cocoa outline, translucent dusty inside
inner = np.array([0xE9, 0xDD, 0xD6]) / 255
rgb = cocoa * (1 - fill[..., None]) + inner * fill[..., None]
a = outline * (1 - fill) * 0.85 + fill * 0.55
Image.fromarray((np.dstack([rgb, a]) * 255 + 0.5).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'life_heart_empty.png'))
# soft glow
r = np.hypot(x, y)
ga = np.exp(-(r / 0.55) ** 2) * sm(1.0, 0.85, r)
Image.fromarray((np.dstack([np.ones_like(x), np.ones_like(x), np.ones_like(x), ga]) * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'life_glow.png'))
print('ok')
# button shine bar (soft vertical band, sweeps across chunky buttons)
W, H = 64, 128
xx = (np.arange(W) + 0.5) / W * 2 - 1
band = np.exp(-(xx / 0.45) ** 2) * 0.9 + np.exp(-((xx - 0.62) / 0.12) ** 2) * 0.5
a = np.tile(band[None, :], (H, 1))
Image.fromarray((np.dstack([np.ones_like(a), np.ones_like(a), np.ones_like(a), np.clip(a, 0, 1)]) * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'ui_shine.png'))
print('shine ok')
