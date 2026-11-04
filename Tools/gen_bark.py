"""Stylized hypercasual bark textures with finely baked lighting -> Assets/TC/Textures.
Features:
- Domed vertical bark plates with organic meandering curvature
- Finely baked directional keylight, form shading, and sunlit crest highlights
- Deep fissure Ambient Occlusion (AO) for rich 3D depth and shadow volume
- Horizontal cylindrical top-down sunlight wash and underside ambient roll-off for branches
- Stylized concentric wood knots, soft forest moss clusters, and crisp fresh leaf accents
- Seamless tangent-space normal maps
Run: python3 Tools/gen_bark.py
"""
import numpy as np, os, math
from PIL import Image, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'TC', 'Textures')
os.makedirs(OUT, exist_ok=True)

def hexc(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i+2], 16) for i in (0, 2, 4)], float) / 255.0

def lerp(a, b, t):
    return a * (1.0 - t) + b * t

# Warm Cel/Hypercasual Forest Palette:
C_BASE_TRUNK   = hexc('#A6643B') # Warm golden-brown bark
C_DARK_TRUNK   = hexc('#4A2010') # Deep crevice AO
C_LIGHT_TRUNK  = hexc('#E8A86C') # Sunlit caramel ridge
C_CREST_TRUNK  = hexc('#F8C892') # Fine sunlit specular highlight
C_MOSS         = hexc('#7CC756') # Fresh green forest moss
C_MOSS_LIGHT   = hexc('#9AE86E') # Sunlit moss highlight
C_MOSS_SHADOW  = hexc('#48822E') # Deep moss shadow

C_BASE_BRANCH  = hexc('#B47044') # Slightly lighter branch bark
C_DARK_BRANCH  = hexc('#502414') # Branch crevice AO
C_LIGHT_BRANCH = hexc('#EFB47E') # Sunlit branch ridge
C_CREST_BRANCH = hexc('#FCD0A2') # Branch top highlight

def generate_finely_baked_bark(name, W, H, is_branch, plates, knots_count, moss_clusters, leaves_count, seed):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:H, 0:W]
    u = x / float(W)
    v = y / float(H)

    # 1. Periodic organic meandering warp across the cylinder
    wobble1 = np.sin(2.0 * np.pi * (v * 1.0 + rng.random())) * 0.014
    wobble2 = np.sin(2.0 * np.pi * (v * 3.0 + rng.random())) * 0.007
    wobble = wobble1 + wobble2
    
    # Plate coordinate across circumference
    p = (u + wobble) * plates
    plate_idx = np.floor(p).astype(int) % plates
    f = p - np.floor(p) # 0..1 across each individual plate
    
    # 2. Longitudinal breaks in plates (rounded segments)
    breaks = rng.random(plates)
    segment = (v * (3 if not is_branch else 2) + breaks[plate_idx] * 2.0) % 1.0
    
    # Rounded rectangular distance for each plate
    across = np.abs(f - 0.5) * 2.0
    along = np.abs(segment - 0.5) * 2.0
    d_plate = np.sqrt(np.clip(across, 0, 1)**4 + np.clip(along, 0, 1)**10)**0.25
    
    # 3. Plate heightmap: domed crest, smooth bevel, deep groove
    h = np.clip(1.0 - d_plate, 0.0, 1.0)**0.65
    groove = np.clip((d_plate - 0.78) / 0.16, 0.0, 1.0) # 1 in deep fissures
    
    # 4. Finely Baked Lighting:
    # A) Base plate color with subtle organic tint per plate
    plate_tints = 0.94 + 0.12 * rng.random(plates)
    tint = plate_tints[plate_idx]
    base_col = C_BASE_BRANCH if is_branch else C_BASE_TRUNK
    dark_col = C_DARK_BRANCH if is_branch else C_DARK_TRUNK
    light_col = C_LIGHT_BRANCH if is_branch else C_LIGHT_TRUNK
    crest_col = C_CREST_BRANCH if is_branch else C_CREST_TRUNK
    
    col = base_col[None, None] * tint[..., None]
    
    # B) Ambient Occlusion in deep fissures (smooth quadratic falloff)
    ao_fissure = groove**1.5
    col = col * (1.0 - ao_fissure[..., None]) + dark_col[None, None] * ao_fissure[..., None]
    
    # C) Directional Keylight & Form Shading on each plate crest:
    # The left-facing slope of each domed plate (f in 0.15..0.45) receives warm diffuse sunlight
    plate_slope_light = np.clip((h - 0.45) / 0.45, 0.0, 1.0) * np.clip(1.0 - np.abs(f - 0.32) * 3.5, 0.0, 1.0)
    col = col + (light_col - base_col)[None, None] * (plate_slope_light[..., None] * 0.85)
    
    # D) Fine sunlit ridge specular / crest highlight on the peak edge
    crest_highlight = np.clip((h - 0.75) / 0.20, 0.0, 1.0) * np.clip(1.0 - np.abs(f - 0.35) * 6.0, 0.0, 1.0)
    col = col + (crest_col - light_col)[None, None] * (crest_highlight[..., None] * 0.90)
    
    # E) For branches: Cylindrical top-down skylight and underside shadow
    if is_branch:
        # Branch is horizontal: V = 0 is top, V = 0.5 is bottom, V = 1 is top again
        cyl_angle = v * 2.0 * np.pi
        cyl_light = np.cos(cyl_angle) # +1 at top, -1 at bottom
        
        # Top sunlit wash
        top_wash = np.clip(cyl_light, 0.0, 1.0)**0.85
        col = col + (crest_col - base_col)[None, None] * (top_wash[..., None] * 0.35)
        
        # Underside soft ambient roll-off
        bottom_shadow = np.clip(-cyl_light, 0.0, 1.0)**0.9
        col = col * (1.0 - bottom_shadow[..., None] * 0.35) + dark_col[None, None] * (bottom_shadow[..., None] * 0.25)
    else:
        # For tree trunk: subtle vertical cylinder curvature roll-off
        trunk_ao = np.sin(2.0 * np.pi * v * 0.5) * 0.04
        col = np.clip(col + trunk_ao[..., None], 0.0, 1.0)

    # 5. Wood Knots with concentric rings & ambient shadow
    img = np.clip(col, 0.0, 1.0)
    
    def draw_disc(cx, cy, r, c, soft=2.0, a=1.0):
        nonlocal img, h
        dx = np.minimum(np.abs(x - cx), W - np.abs(x - cx))
        dy = np.minimum(np.abs(y - cy), H - np.abs(y - cy))
        dist = np.sqrt(dx*dx + dy*dy)
        m = np.clip((r - dist) / soft, 0.0, 1.0) * a
        img = img * (1.0 - m[..., None]) + np.array(c)[None, None] * m[..., None]
        # Knot indentation in heightmap
        h = h * (1.0 - m * 0.4)
        return m

    for k in range(knots_count):
        cx = rng.uniform(0, W)
        cy = rng.uniform(0, H)
        r = rng.uniform(W * 0.04, W * 0.07)
        # Concentric woodgrain knot rings with baked shadow
        draw_disc(cx, cy, r * 1.35, dark_col * 0.85, soft=4.0, a=0.95)
        draw_disc(cx, cy, r * 1.10, light_col * 0.95, soft=3.0, a=0.90)
        draw_disc(cx, cy, r * 0.65, base_col * 0.85, soft=2.5, a=0.95)
        draw_disc(cx, cy, r * 0.28, dark_col * 0.65, soft=2.0, a=1.00)

    # 6. Soft Velvet Forest Moss Clusters (in grooves and around knots)
    for m in range(moss_clusters):
        cx = rng.uniform(0, W)
        cy = rng.uniform(0, H)
        cluster_r = rng.uniform(W * 0.02, W * 0.04)
        for sub in range(6):
            sx = cx + rng.normal(0, W * 0.015)
            sy = cy + rng.normal(0, W * 0.015)
            sr = rng.uniform(cluster_r * 0.4, cluster_r * 0.9)
            moss_shade = lerp(C_MOSS_SHADOW, C_MOSS_LIGHT, rng.uniform(0.3, 0.95))
            draw_disc(sx, sy, sr, moss_shade, soft=2.5, a=0.92)

    # 7. Tiny Crisp Stylized Leaves (fresh canopy details)
    for l in range(leaves_count):
        cx = rng.uniform(0, W)
        cy = rng.uniform(0, H)
        lr = W * 0.015
        ang = rng.uniform(0, np.pi)
        for s in (-1, 1):
            lx = cx + s * np.cos(ang) * lr * 0.75
            ly = cy + s * np.sin(ang) * lr * 0.75
            draw_disc(lx, ly, lr, C_MOSS_LIGHT * 1.1, soft=2.0, a=0.95)
        draw_disc(cx, cy, lr * 0.35, C_MOSS_SHADOW, soft=1.5, a=0.85)

    # 8. Save Albedo Texture
    rgb_bytes = (np.clip(img, 0.0, 1.0) * 255.0).astype(np.uint8)
    albedo_img = Image.fromarray(rgb_bytes, "RGB")
    albedo_path = os.path.join(OUT, f"{name}.png")
    albedo_img.save(albedo_path, "PNG")
    print(f"Saved {albedo_path} ({W}x{H})")

    # 9. High-Quality Tangent-Space Normal Map (Sobel filter with periodic boundary wrapping)
    h_smooth = np.array(Image.fromarray((h * 255.0).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.8)), float) / 255.0
    
    strength = 4.5
    gx = (np.roll(h_smooth, -1, axis=1) - np.roll(h_smooth, 1, axis=1)) * strength
    gy = (np.roll(h_smooth, -1, axis=0) - np.roll(h_smooth, 1, axis=0)) * strength
    gz = np.ones_like(gx)
    
    norm = np.sqrt(gx*gx + gy*gy + gz*gz)
    nx = -gx / norm
    ny = -gy / norm # Unity OpenGL normal format (Y up)
    nz = gz / norm
    
    normal_rgb = np.zeros((H, W, 3), dtype=np.uint8)
    normal_rgb[:, :, 0] = np.clip((nx * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    normal_rgb[:, :, 1] = np.clip((ny * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    normal_rgb[:, :, 2] = np.clip((nz * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    
    normal_img = Image.fromarray(normal_rgb, "RGB")
    normal_path = os.path.join(OUT, f"{name}_n.png")
    normal_img.save(normal_path, "PNG")
    print(f"Saved {normal_path} ({W}x{H})")

if __name__ == '__main__':
    generate_finely_baked_bark('Bark_Trunk', 1024, 1024, is_branch=False, plates=8, knots_count=3, moss_clusters=12, leaves_count=14, seed=42)
    generate_finely_baked_bark('Bark_Branch', 512, 512, is_branch=True, plates=5, knots_count=2, moss_clusters=6, leaves_count=6, seed=84)
    print("Done generating bark textures.")
