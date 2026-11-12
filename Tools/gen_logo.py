#!/usr/bin/env python3
"""Clean, vector-like Tall Crawl logo and app icon.
Flat cel-shaded, bold outlines, no photoreal / AI-painterly artifacts.
"""
import math, os
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops
import numpy as np
from scipy.ndimage import binary_dilation
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

OUT_ICON = Path("/workspace/tc/Assets/TC/Icon")
OUT_RES = Path("/workspace/tc/Assets/resources")
TMP = Path("/tmp/tc_logo")
TMP.mkdir(exist_ok=True)

BALOO = "/usr/share/fonts/truetype/sand-box/google/Baloo 2/Baloo2-VariableFont_wght.ttf"
FREDOKA = "/usr/share/fonts/truetype/sand-box/google/Fredoka/Fredoka-VariableFont_wdth,wght.ttf"

# Palette matches in-game UI (TCUI)
COCOA = (74, 59, 63, 255)
CREAM = (255, 247, 238, 255)
BUTTER = (255, 213, 79, 255)
PEACH = (255, 159, 122, 255)
ORANGE = (255, 154, 60, 255)
TEAL = (46, 140, 148, 255)
TEAL_DK = (28, 90, 98, 255)
TEAL_LT = (92, 196, 196, 255)
NAVY = (42, 36, 48, 255)
WHITE = (255, 255, 255, 255)
SKY = (186, 224, 236, 255)
SKY_LT = (232, 245, 248, 255)
BARK = (140, 92, 62, 255)
BARK_DK = (102, 64, 42, 255)
BARK_LT = (176, 122, 84, 255)
LEAF = (90, 168, 92, 255)
LEAF_DK = (62, 128, 70, 255)
GOLD = (255, 196, 64, 255)
SHADOW = (40, 28, 32, 70)

def instantiate(src, dst, **axes):
    font = TTFont(src)
    inst = instantiateVariableFont(font, axes, overlap=True)
    inst.save(dst)
    return dst

def font(path, size):
    return ImageFont.truetype(path, size=size)

def new(w, h=None, color=(0, 0, 0, 0)):
    if h is None or isinstance(w, (tuple, list)):
        if isinstance(w, (tuple, list)):
            if h is not None and not isinstance(h, (int, float)):
                # new(size, color)
                color = h
            size = (int(w[0]), int(w[1]))
            return Image.new("RGBA", size, color)
        raise TypeError("new() needs width and height")
    return Image.new("RGBA", (int(w), int(h)), color)

def paste(dst, src, xy, center=False):
    if src.mode != "RGBA":
        src = src.convert("RGBA")
    x, y = xy
    if center:
        x -= src.width // 2
        y -= src.height // 2
    dst.alpha_composite(src, (int(x), int(y)))

def ellipse(draw, box, fill=None, outline=None, width=0):
    if outline and width:
        # draw outline as a thicker ellipse behind
        x0, y0, x1, y1 = box
        draw.ellipse((x0 - width, y0 - width, x1 + width, y1 + width), fill=outline)
        if fill is not None:
            draw.ellipse(box, fill=fill)
    else:
        draw.ellipse(box, fill=fill, outline=outline, width=width)

def rounded_rect(draw, box, r, fill=None, outline=None, width=0):
    if outline and width:
        x0, y0, x1, y1 = box
        draw.rounded_rectangle((x0 - width, y0 - width, x1 + width, y1 + width), r + width, fill=outline)
        if fill is not None:
            draw.rounded_rectangle(box, r, fill=fill)
    else:
        draw.rounded_rectangle(box, r, fill=fill, outline=outline, width=width)

def line(draw, a, b, fill, width, caps=True):
    draw.line([a, b], fill=fill, width=width)
    if caps:
        r = width / 2
        for p in (a, b):
            draw.ellipse((p[0] - r, p[1] - r, p[0] + r, p[1] + r), fill=fill)

def gradient_fill(size, c0, c1, vertical=True):
    w, h = size
    im = new(w, h)
    px = im.load()
    for y in range(h):
        t = y / max(1, h - 1) if vertical else 0
        if not vertical:
            pass
        r = int(c0[0] + (c1[0] - c0[0]) * t)
        g = int(c0[1] + (c1[1] - c0[1]) * t)
        b = int(c0[2] + (c1[2] - c0[2]) * t)
        a = int((c0[3] if len(c0) > 3 else 255) + ((c1[3] if len(c1) > 3 else 255) - (c0[3] if len(c0) > 3 else 255)) * t)
        for x in range(w):
            if not vertical:
                t2 = x / max(1, w - 1)
                r = int(c0[0] + (c1[0] - c0[0]) * t2)
                g = int(c0[1] + (c1[1] - c0[1]) * t2)
                b = int(c0[2] + (c1[2] - c0[2]) * t2)
                a = 255
            px[x, y] = (r, g, b, a)
    return im

def text_mask(text, fnt, pad=8):
    # Generous padding: some variable-font instances report tight/incorrect bboxes
    # and Baloo's rounds overflow the nominal box.
    tmp = new(8, 8)
    d = ImageDraw.Draw(tmp)
    bbox = d.textbbox((0, 0), text, font=fnt)
    try:
        gb = fnt.getbbox(text)
        bbox = (min(bbox[0], gb[0]), min(bbox[1], gb[1]), max(bbox[2], gb[2]), max(bbox[3], gb[3]))
    except Exception:
        pass
    pad = max(pad, 96)
    w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
    im = new(w + pad * 2, h + pad * 2)
    ImageDraw.Draw(im).text((pad - bbox[0], pad - bbox[1]), text, font=fnt, fill=WHITE)
    # Trim empty margins but keep a small safe pad
    a = im.split()[-1]
    box = a.getbbox()
    if box:
        x0, y0, x1, y1 = box
        m = 8
        im = im.crop((max(0, x0 - m), max(0, y0 - m), min(im.width, x1 + m), min(im.height, y1 + m)))
    return im

def stroke_mask(mask, width, color):
    """Expand mask by `width` and colorize (numpy dilation, not MaxFilter)."""
    a = np.array(mask.split()[-1])
    radius = max(1, int(width))
    yy, xx = np.ogrid[-radius:radius + 1, -radius:radius + 1]
    fp = (xx * xx + yy * yy) <= radius * radius
    grown = binary_dilation(a > 32, structure=fp)
    g = Image.fromarray((grown.astype(np.uint8) * 255), mode="L")
    g = g.filter(ImageFilter.GaussianBlur(0.8))
    out = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    out.paste(Image.new("RGBA", mask.size, color), mask=g)
    return out

def colorize_mask(mask, color):
    out = new(mask.size)
    out.paste(Image.new("RGBA", mask.size, color), mask=mask.split()[-1])
    return out

def gradient_mask(mask, c0, c1, vertical=True):
    g = gradient_fill(mask.size, c0, c1, vertical)
    out = new(mask.size)
    out.paste(g, mask=mask.split()[-1])
    return out

def highlight_band(mask, y0, y1, color):
    """A soft highlight stripe clipped to the glyph."""
    w, h = mask.size
    band = new((w, h))
    d = ImageDraw.Draw(band)
    d.rectangle((0, y0, w, y1), fill=color)
    band = band.filter(ImageFilter.GaussianBlur(6))
    out = new((w, h))
    # clip
    a = ImageChops.multiply(band.split()[-1], mask.split()[-1])
    out.paste(Image.new("RGBA", (w, h), color), mask=a)
    return out

def make_wordmark(scale=4):
    """Two-line TALL / CRAWL wordmark with bubble outline."""
    fpath = instantiate(BALOO, str(TMP / "baloo_extrabold.ttf"), wght=800)
    # Large letters
    fnt = font(fpath, 220 * scale // 4 * 4)  # will set properly below
    # We'll draw at 4x: letter height ~ 280 px at final 1016x572? Let's design at 4x 4064x2288 then downscale? 
    # MenuLogo is 1016x572. Work at 4x = 4064 x 2288.
    return fpath

def compose_word(text, fnt, fill0, fill1, stroke_outer, stroke_inner, sw_outer, sw_inner):
    m = text_mask(text, fnt, pad=sw_outer + 12)
    layers = new(m.size)
    # outer (cream / teal bubble)
    layers.alpha_composite(stroke_mask(m, sw_outer, stroke_outer))
    # inner dark outline
    layers.alpha_composite(stroke_mask(m, sw_inner, stroke_inner))
    # gradient fill
    fill = gradient_mask(m, fill0, fill1, vertical=True)
    layers.alpha_composite(fill)
    # top highlight
    h = m.size[1]
    layers.alpha_composite(highlight_band(m, int(h * 0.12), int(h * 0.42), (255, 255, 255, 70)))
    return layers

def draw_spider(S, pose="perch"):
    """Cute chibi spider matching the in-game stylized look.
    S is the body radius in pixels. Returns an RGBA image, spider roughly centered.
    pose: 'perch' (slight 3/4, legs wrapping), 'climb' (clinging, more vertical).
    """
    W = int(S * 5.2)
    H = int(S * 5.4)
    im = new((W, H))
    d = ImageDraw.Draw(im)
    cx, cy = W // 2, int(H * 0.52)
    ol = max(3, int(S * 0.08))  # outline thickness

    # Colors: teal abdomen like a mascot, darker teal cephalothorax, cream eyes
    ab_fill = TEAL
    ab_shade = TEAL_DK
    ab_hi = TEAL_LT
    head_fill = (36, 110, 118, 255)
    head_hi = (70, 160, 164, 255)
    leg = (32, 86, 94, 255)
    leg_ol = NAVY

    def thick_leg(p0, p1, p2, width):
        """Bent leg with outline: p0 hip, p1 knee, p2 foot."""
        wo = width + ol * 2
        # outline
        line(d, p0, p1, NAVY, wo)
        line(d, p1, p2, NAVY, int(wo * 0.85))
        # fill
        line(d, p0, p1, leg, width)
        line(d, p1, p2, leg, int(width * 0.85))
        # knee joint
        r = width * 0.55
        ellipse(d, (p1[0] - r, p1[1] - r, p1[0] + r, p1[1] + r), fill=leg, outline=NAVY, width=max(2, ol // 2))

    # Legs first (behind body). 6 legs, 3 per side.
    lw = max(8, int(S * 0.28))
    if pose == "climb":
        # clinging to a trunk on the right; legs wrap around
        specs = [
            # left (free side)  (hip offset, knee, foot) relative
            (-0.7, -0.15, -1.55, -0.85, -2.05, -0.15),
            (-0.85, 0.15, -1.75, 0.05, -2.15, 0.70),
            (-0.65, 0.50, -1.40, 0.95, -1.70, 1.55),
            # right (clinging)
            (0.70, -0.20, 1.45, -0.95, 1.85, -0.25),
            (0.85, 0.10, 1.70, -0.10, 2.10, 0.55),
            (0.65, 0.50, 1.35, 0.90, 1.65, 1.50),
        ]
    else:
        # perched on letters, slightly spread, front facing 3/4
        specs = [
            (-0.75, -0.25, -1.55, -0.95, -1.85, -0.20),
            (-0.90, 0.10, -1.80, -0.05, -2.15, 0.65),
            (-0.70, 0.50, -1.45, 0.85, -1.65, 1.50),
            (0.75, -0.25, 1.50, -1.00, 1.90, -0.25),
            (0.90, 0.10, 1.80, -0.10, 2.15, 0.55),
            (0.70, 0.50, 1.40, 0.90, 1.70, 1.50),
        ]
    for sx0, sy0, sx1, sy1, sx2, sy2 in specs:
        p0 = (cx + sx0 * S, cy + sy0 * S)
        p1 = (cx + sx1 * S, cy + sy1 * S)
        p2 = (cx + sx2 * S, cy + sy2 * S)
        thick_leg(p0, p1, p2, lw)

    # Abdomen (big round butt behind/below)
    ab_cx, ab_cy = cx, cy + S * 0.28
    ab_rx, ab_ry = S * 1.15, S * 1.02
    ellipse(d, (ab_cx - ab_rx, ab_cy - ab_ry, ab_cx + ab_rx, ab_cy + ab_ry),
            fill=ab_fill, outline=NAVY, width=ol)
    # shade crescent bottom-right
    shade = new((W, H))
    sd = ImageDraw.Draw(shade)
    ellipse(sd, (ab_cx - ab_rx * 0.7, ab_cy - ab_ry * 0.2, ab_cx + ab_rx * 1.05, ab_cy + ab_ry * 1.05), fill=ab_shade)
    # clip to abdomen by using a mask
    ab_mask = new((W, H))
    ImageDraw.Draw(ab_mask).ellipse((ab_cx - ab_rx + ol, ab_cy - ab_ry + ol, ab_cx + ab_rx - ol, ab_cy + ab_ry - ol), fill=WHITE)
    shade.putalpha(ImageChops.multiply(shade.split()[-1], ab_mask.split()[-1]))
    # lower shade opacity
    a = shade.split()[-1].point(lambda v: int(v * 0.45))
    shade.putalpha(a)
    im.alpha_composite(shade)
    # highlight
    hi = new((W, H))
    hd = ImageDraw.Draw(hi)
    ellipse(hd, (ab_cx - ab_rx * 0.55, ab_cy - ab_ry * 0.85, ab_cx + ab_rx * 0.15, ab_cy - ab_ry * 0.05), fill=ab_hi)
    hi = hi.filter(ImageFilter.GaussianBlur(max(1, S * 0.06)))
    hi.putalpha(ImageChops.multiply(hi.split()[-1].point(lambda v: int(v * 0.55)), ab_mask.split()[-1]))
    im.alpha_composite(hi)
    # simple chevron / stripe marks (2 short bands) — geometric, not painterly
    d = ImageDraw.Draw(im)
    for i, off in enumerate((-0.18, 0.12)):
        y = ab_cy + off * S
        # clipped bands via short rounded rects
        bw = S * (0.7 - abs(off) * 0.3)
        bh = S * 0.13
        rounded_rect(d, (ab_cx - bw, y - bh, ab_cx + bw, y + bh), bh, fill=ab_hi)

    # Cephalothorax / head
    hx, hy = cx, cy - S * 0.62
    hr = S * 0.72
    ellipse(d, (hx - hr, hy - hr * 0.92, hx + hr, hy + hr * 1.02), fill=head_fill, outline=NAVY, width=ol)
    # head highlight
    ellipse(d, (hx - hr * 0.5, hy - hr * 0.7, hx + hr * 0.15, hy - hr * 0.05), fill=head_hi)

    # Eyes — two big friendly circles, slightly toward camera
    eye_r = S * 0.38
    gap = S * 0.16
    for sgn, wink in ((-1, False), (1, False)):
        ex = hx + sgn * (eye_r * 0.72 + gap * 0.3)
        ey = hy - S * 0.02
        # white sclera
        ellipse(d, (ex - eye_r, ey - eye_r, ex + eye_r, ey + eye_r), fill=WHITE, outline=NAVY, width=max(2, ol - 1))
        # pupil (cocoa/navy, slightly down)
        pr = eye_r * 0.48
        px, py = ex + sgn * eye_r * 0.05, ey + eye_r * 0.08
        ellipse(d, (px - pr, py - pr, px + pr, py + pr), fill=NAVY)
        # iris ring teal
        ir = pr * 0.62
        ellipse(d, (px - ir, py - ir, px + ir, py + ir), fill=TEAL_DK)
        # pupil
        pr2 = pr * 0.38
        ellipse(d, (px - pr2, py - pr2, px + pr2, py + pr2), fill=NAVY)
        # shine
        sr = eye_r * 0.16
        ellipse(d, (ex - eye_r * 0.35 - sr, ey - eye_r * 0.40 - sr,
                    ex - eye_r * 0.35 + sr, ey - eye_r * 0.40 + sr), fill=WHITE)
        sr2 = eye_r * 0.08
        ellipse(d, (ex + eye_r * 0.15 - sr2, ey + eye_r * 0.10 - sr2,
                    ex + eye_r * 0.15 + sr2, ey + eye_r * 0.10 + sr2), fill=WHITE)

    # Tiny smile
    smile_w = S * 0.28
    smile_y = hy + hr * 0.55
    d.arc((hx - smile_w, smile_y - S * 0.12, hx + smile_w, smile_y + S * 0.22),
          start=15, end=165, fill=NAVY, width=max(3, ol - 1))

    # Two tiny fangs
    fang_w = S * 0.08
    for sgn in (-1, 1):
        fx = hx + sgn * S * 0.12
        fy = smile_y + S * 0.10
        d.polygon([(fx, fy), (fx - fang_w, fy + S * 0.16), (fx + fang_w * 0.4, fy + S * 0.06)], fill=CREAM, outline=NAVY)

    return im

def make_simple_web(w, h, origin, strands=5):
    """A few clean geometric web strands, not a photoreal cobweb."""
    im = new((w, h))
    d = ImageDraw.Draw(im)
    col = (255, 255, 255, 160)
    ox, oy = origin
    for i in range(strands):
        ang = math.radians(-30 + i * 18)
        length = min(w, h) * (0.35 + (i % 3) * 0.08)
        x2 = ox + math.cos(ang) * length
        y2 = oy + math.sin(ang) * length
        d.line([(ox, oy), (x2, y2)], fill=col, width=max(3, w // 400))
    # 2 connecting arcs
    for rfac in (0.18, 0.30):
        r = min(w, h) * rfac
        bbox = (ox - r, oy - r, ox + r, oy + r)
        d.arc(bbox, start=-30, end=50, fill=col, width=max(3, w // 400))
    return im

def build_menu_logo():
    # Final 1016x572, work at 4x
    FW, FH = 1016, 572
    scale = 2
    W, H = FW * scale, FH * scale
    canvas = new((W, H))

    fpath = instantiate(BALOO, str(TMP / "baloo_800.ttf"), wght=800)
    fnt = font(fpath, 168 * scale)

    def compose(text):
        return compose_word(text, fnt,
                            (255, 224, 90, 255), (255, 150, 55, 255),
                            CREAM, NAVY,
                            sw_outer=int(16 * scale), sw_inner=int(9 * scale))

    tall = compose("TALL")
    crawl = compose("CRAWL")

    def fit(im, max_w):
        if im.width <= max_w:
            return im
        s = max_w / im.width
        return im.resize((int(im.width * s), int(im.height * s)), Image.Resampling.LANCZOS)

    pad = int(40 * scale)
    crawl = fit(crawl, W - pad * 2)
    tall = fit(tall, int((W - pad * 2) * 0.78))

    gap = int(-8 * scale)
    total_h = tall.height + crawl.height + gap
    cx = W // 2
    top = max(int(20 * scale), (H - total_h) // 2 + int(16 * scale))
    crawl_x = cx - crawl.width // 2
    tall_x = crawl_x
    tall_y = top
    crawl_y = top + tall.height + gap

    # Soft drop shadow under the wordmark
    def shadow_of(im, ox, oy):
        a = im.split()[-1]
        sh = new(im.size, (40, 28, 32, 255))
        sh.putalpha(a.point(lambda v: int(v * 0.28)))
        sh = sh.filter(ImageFilter.GaussianBlur(int(10 * scale)))
        paste(canvas, sh, (ox + int(8 * scale), oy + int(14 * scale)))

    shadow_of(tall, tall_x, tall_y)
    shadow_of(crawl, crawl_x, crawl_y)

    paste(canvas, tall, (tall_x, tall_y))
    paste(canvas, crawl, (crawl_x, crawl_y))

    # Spider perched on the last L of TALL
    spider = draw_spider(int(62 * scale), pose="perch")
    sx = tall_x + tall.width - int(spider.width * 0.42)
    sy = tall_y - int(spider.height * 0.70)
    # Keep the spider on-canvas
    sx = min(max(0, sx), W - spider.width)
    sy = min(max(0, sy), H - spider.height)
    paste(canvas, spider, (sx, sy))

    # Downscale with LANCZOS for crisp-but-smooth edges
    out = canvas.resize((FW, FH), Image.Resampling.LANCZOS)
    return out

def build_app_icon():
    """1024 app icon: spider climbing a stylized trunk, flat sky, clean outlines."""
    S = 1024
    work = 1024 * 2  # 2x then down
    im = new((work, work))
    d = ImageDraw.Draw(im)

    # Background: soft vertical sky
    bg = gradient_fill((work, work), (214, 238, 244, 255), (168, 214, 226, 255), vertical=True)
    im.alpha_composite(bg)

    # Simple sun disc (flat)
    sun_r = int(work * 0.13)
    sx, sy = int(work * 0.78), int(work * 0.20)
    ellipse(d, (sx - sun_r, sy - sun_r, sx + sun_r, sy + sun_r), fill=(255, 236, 150, 255))

    # Two simple flat leaves in the corners (geometric teardrops)
    def leaf(pos, rot_deg, size, fill, outline):
        lf = new((size * 2, size * 2))
        ld = ImageDraw.Draw(lf)
        cx, cy = size, size
        # teardrop approximated by ellipse + triangle
        ellipse(ld, (cx - size * 0.45, cy - size * 0.7, cx + size * 0.45, cy + size * 0.35), fill=fill, outline=outline, width=max(4, size // 18))
        # stem tip
        ld.polygon([(cx, cy + size * 0.25), (cx - size * 0.12, cy + size * 0.85), (cx + size * 0.12, cy + size * 0.85)], fill=fill)
        lf = lf.rotate(rot_deg, resample=Image.Resampling.BICUBIC, expand=True)
        paste(im, lf, pos, center=True)

    leaf((int(work * 0.18), int(work * 0.16)), -30, int(work * 0.10), LEAF, NAVY)
    leaf((int(work * 0.88), int(work * 0.78)), 140, int(work * 0.09), LEAF_DK, NAVY)

    # Stylized trunk on the right — rounded rectangle with 3 bark lines
    trunk_x0 = int(work * 0.58)
    trunk_x1 = int(work * 1.02)
    trunk_y0 = int(work * -0.02)
    trunk_y1 = int(work * 1.02)
    rounded_rect(d, (trunk_x0, trunk_y0, trunk_x1, trunk_y1), int(work * 0.06), fill=BARK, outline=NAVY, width=max(8, work // 90))
    # bark highlight strip
    rounded_rect(d, (trunk_x0 + int(work * 0.04), trunk_y0 + int(work * 0.04),
                     trunk_x0 + int(work * 0.14), trunk_y1 - int(work * 0.04)),
                 int(work * 0.04), fill=BARK_LT)
    # 3 simple curved bark grooves
    for i, y in enumerate((0.22, 0.45, 0.70)):
        y0 = int(work * y)
        d.arc((trunk_x0 + int(work * 0.10), y0, trunk_x1 - int(work * 0.08), y0 + int(work * 0.18)),
              start=200, end=340, fill=BARK_DK, width=max(6, work // 120))

    # Spider climbing the trunk, body to the left of the bark
    spider = draw_spider(int(work * 0.16), pose="climb")
    # Place so right legs grab the trunk
    px = int(work * 0.40)
    py = int(work * 0.48)
    paste(im, spider, (px, py), center=True)

    out = im.resize((S, S), Image.Resampling.LANCZOS)
    # Ensure opaque RGB for the store icon (no checker)
    rgb = Image.new("RGB", (S, S), (214, 238, 244))
    rgb.paste(out, mask=out.split()[-1])
    return rgb.convert("RGBA"), out  # full opaque, and rgba (same)

def build_adaptive_fg():
    """Transparent foreground: just the spider, padded for adaptive safe zone (inner 66%)."""
    S = 1024
    work = 2048
    im = new((work, work))
    spider = draw_spider(int(work * 0.18), pose="perch")
    paste(im, spider, (work // 2, work // 2 + int(work * 0.02)), center=True)
    return im.resize((S, S), Image.Resampling.LANCZOS)

def main():
    OUT_ICON.mkdir(parents=True, exist_ok=True)
    logo = build_menu_logo()
    logo_path = OUT_ICON / "MenuLogo.png"
    logo.save(logo_path, "PNG", optimize=True)
    print("wrote", logo_path, logo.size, logo.mode)

    icon_rgb, icon_rgba = build_app_icon()
    # AppIcon is RGB opaque
    icon_path = OUT_ICON / "AppIcon.png"
    icon_rgb.convert("RGB").save(icon_path, "PNG", optimize=True)
    print("wrote", icon_path, icon_rgb.size)

    fg = build_adaptive_fg()
    fg_path = OUT_ICON / "AppIconFg.png"
    fg.save(fg_path, "PNG", optimize=True)
    print("wrote", fg_path, fg.size, "alpha extrema", fg.split()[-1].getextrema())

    # Also replace the old resources/Logo.png with the icon (legacy slot)
    (OUT_RES / "Logo.png").parent.mkdir(parents=True, exist_ok=True)
    icon_rgb.convert("RGB").save(OUT_RES / "Logo.png", "PNG", optimize=True)
    print("wrote", OUT_RES / "Logo.png")

    # previews
    logo.resize((508, 286), Image.Resampling.LANCZOS).save("/tmp/new_menulogo_prev.png")
    icon_rgb.resize((256, 256), Image.Resampling.LANCZOS).save("/tmp/new_appicon_prev.png")
    fg.resize((256, 256), Image.Resampling.LANCZOS).save("/tmp/new_appiconfg_prev.png")
    print("previews ready")

if __name__ == "__main__":
    main()
