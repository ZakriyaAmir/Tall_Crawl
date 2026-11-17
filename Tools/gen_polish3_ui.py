"""polish3: palette UI sprites (bevel, ring, ribbon, card gradient, leaf, wash, sparkle).
All sprites are white/neutral so Image.color tints them; ribbon tails are pre-shaded grey so a
crimson tint gives darker folded tails. Run from the repo root: python3 Tools/gen_polish3_ui.py"""
import math, os, random, re, uuid
from PIL import Image, ImageDraw, ImageFilter
OUT = 'Assets/TC/Resources/TCUI'
SS = 4  # supersampling

def rr_mask(w, h, r, inset=0):
    m = Image.new('L', (w * SS, h * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle([inset * SS, inset * SS, (w - inset) * SS - 1, (h - inset) * SS - 1], r * SS, fill=255)
    return m.resize((w, h), Image.LANCZOS)

def bevel():
    # 64x64, radius 26 (matches round.png, border 26). Top glossy highlight + bottom shade.
    W = H = 64; img = Image.new('RGBA', (W, H), (0, 0, 0, 0)); px = img.load()
    shape = rr_mask(W, H, 26); inner = rr_mask(W, H, 22, 4)
    for y in range(H):
        for x in range(W):
            a_shape = shape.getpixel((x, y)) / 255
            if a_shape <= 0: continue
            t = y / (H - 1)
            # highlight band (inset so the rim stays crisp)
            hi = max(0.0, 1 - t / 0.46) ** 1.6 * 0.55 * inner.getpixel((x, y)) / 255
            # tiny bright glint line near the top
            if 5 <= y <= 7: hi = max(hi, 0.62 * inner.getpixel((x, y)) / 255 * (1 - abs(x - 32) / 34))
            lo = max(0.0, (t - 0.58) / 0.42) ** 1.35 * 0.34 * a_shape
            if hi >= lo: px[x, y] = (255, 255, 255, int(255 * min(1, hi)))
            else: px[x, y] = (74, 49, 27, int(255 * min(1, lo)))
    return img

def ring(th=5, r=26, size=64):
    o = rr_mask(size, size, r); i = rr_mask(size, size, r - th, th)
    a = Image.eval(Image.merge('L', [o]), lambda v: v)
    from PIL import ImageChops
    m = ImageChops.subtract(o, i)
    img = Image.new('RGBA', (size, size), (255, 255, 255, 0)); img.putalpha(m); return img

def cardgrad():
    # soft card shading: white sheen at the top fading out, warm tan at the bottom
    W = H = 64; img = Image.new('RGBA', (W, H), (0, 0, 0, 0)); px = img.load(); shape = rr_mask(W, H, 26)
    for y in range(H):
        t = y / (H - 1)
        for x in range(W):
            s = shape.getpixel((x, y)) / 255
            if s <= 0: continue
            if t < 0.5: px[x, y] = (255, 255, 255, int(255 * s * 0.55 * (1 - t / 0.5) ** 1.5))
            else: px[x, y] = (217, 165, 119, int(255 * s * 0.42 * ((t - 0.5) / 0.5) ** 1.6))
    return img

def ribbon():
    # 240x150: band (top 120px, x 40..200) + folded notched tails (lower, darker) on both ends.
    W, H = 240, 150; S = SS
    img = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    tail = (158, 158, 158, 255); fold = (92, 92, 92, 255); band = (255, 255, 255, 255)
    for side in (0, 1):
        def X(x): return (x if side == 0 else W - x) * S
        # tail: x 0..58, y 30..150 with V notch at the outer edge
        pts = [(X(58), 30 * S), (X(0), 30 * S), (X(20), 90 * S), (X(0), 150 * S), (X(58), 150 * S)]
        d.polygon(pts, fill=tail)
        # fold triangle under the band end
        d.polygon([(X(40), 120 * S), (X(58), 120 * S), (X(58), 150 * S)], fill=fold)
    d.rounded_rectangle([40 * S, 0, (W - 40) * S - 1, 120 * S - 1], 14 * S, fill=band)
    return img.resize((W, H), Image.LANCZOS)

def leaf():
    W, H = 64, 96; S = SS
    img = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    pts = []
    for i in range(61):
        t = i / 60; y = 6 + t * 84
        w = math.sin(math.pi * t) ** 0.85 * 26
        pts.append((32 + w, y))
    pts += [(32 - x + 32 - 32, y) for (x, y) in []]
    left = [(64 - x, y) for (x, y) in reversed(pts)]
    d.polygon([(x * S, y * S) for (x, y) in pts + left], fill=(255, 255, 255, 255))
    d.line([(32 * S, 8 * S), (32 * S, 92 * S)], fill=(200, 200, 200, 255), width=3 * S)
    for k in range(4):
        y0 = 26 + k * 15
        d.line([(32 * S, y0 * S), (46 * S, (y0 - 9) * S)], fill=(214, 214, 214, 255), width=2 * S)
        d.line([(32 * S, y0 * S), (18 * S, (y0 - 9) * S)], fill=(214, 214, 214, 255), width=2 * S)
    return img.resize((W, H), Image.LANCZOS)

def wash():
    # menu warm wash: transparent at the top, cream band behind the logo fades out, tan at the bottom
    W, H = 4, 256; img = Image.new('RGBA', (W, H)); px = img.load()
    for y in range(H):
        t = y / (H - 1)  # 0 = top of image (top of screen)
        if t > 0.62: c = (217, 165, 119, int(255 * 0.62 * ((t - 0.62) / 0.38) ** 1.3))
        elif t < 0.12: c = (242, 233, 216, int(255 * 0.30 * (1 - t / 0.12)))
        else: c = (242, 233, 216, 0)
        for x in range(W): px[x, y] = c
    return img

def sparkle():
    W = 64; S = SS; img = Image.new('RGBA', (W * S, W * S), (0, 0, 0, 0)); d = ImageDraw.Draw(img); c = W * S / 2
    pts = []
    for i in range(8):
        a = i * math.pi / 4; r = (30 if i % 2 == 0 else 7) * S
        pts.append((c + math.cos(a) * r, c + math.sin(a) * r))
    d.polygon(pts, fill=(255, 255, 255, 255))
    return img.resize((W, W), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.6))

TEMPLATE = open(os.path.join(OUT, 'round.png.meta')).read()
def save(img, name, border):
    p = os.path.join(OUT, name + '.png'); img.save(p)
    mp = p + '.meta'
    if os.path.exists(mp):
        m = open(mp).read()
    else:
        m = re.sub(r'guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, TEMPLATE, count=1)
        m = re.sub(r'internalIDToNameTable:\n(  - .*\n|      .*\n|    .*\n)*', 'internalIDToNameTable: []\n', m, count=1)
        m = re.sub(r'(  spriteSheet:\n    serializedVersion: 2\n    sprites:)\n(    - .*\n|      .*\n|        .*\n)*', r'\1 []\n', m, count=1)
    l, b, r, t = border
    m = re.sub(r'spriteBorder: \{[^}]*\}', 'spriteBorder: {x: %d, y: %d, z: %d, w: %d}' % (l, b, r, t), m)
    open(mp, 'w').write(m)
    print('wrote', p, img.size, border)

if __name__ == '__main__':
    save(bevel(), 'ui_bevel', (26, 26, 26, 26))
    save(ring(), 'ui_ring', (26, 26, 26, 26))
    save(cardgrad(), 'ui_cardgrad', (26, 26, 26, 26))
    save(ribbon(), 'ui_ribbon', (90, 0, 90, 0))
    save(leaf(), 'ui_leaf', (0, 0, 0, 0))
    save(wash(), 'ui_wash', (0, 0, 0, 0))
    save(sparkle(), 'ui_sparkle', (0, 0, 0, 0))
