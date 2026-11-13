import numpy as np
from PIL import Image, ImageFilter
from scipy.ndimage import gaussian_filter
import os

OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'TC', 'Textures')
PREVIEW = '/Users/zakriyaamir/.gemini/antigravity/brain/7241d5d2-9145-4629-a36a-bcd9dfb366ff'

def hexc(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i+2], 16) for i in (0, 2, 4)], float) / 255.0

# Warm Stylized Cel Palette (inspired by top hand-painted game art)
C_DARK_CREVICE  = hexc('#36160A') # Deep dark espresso fissure shadow
C_DARK_BARK     = hexc('#582813') # Shadowed bark flank
C_BASE_BARK     = hexc('#9E5A2E') # Rich warm chestnut body
C_WARM_BARK     = hexc('#BA6C36') # Vibrant amber wood tone
C_LIGHT_BARK    = hexc('#E09E5E') # Sunlit honey ridge
C_CREST_BARK    = hexc('#F5C78A') # Fine sunlit crest highlight
C_RIM_BARK      = hexc('#FDE0B6') # Ultra-crisp rim highlight

C_MOSS_DEEP     = hexc('#284E18') # Deep moss shadow
C_MOSS_BASE     = hexc('#4F8E26') # Forest green moss body
C_MOSS_LIGHT    = hexc('#7EC73E') # Bright sunlit moss velvet
C_MOSS_TIP      = hexc('#ACEE58') # Fresh chartreuse moss edge
C_CLOVER        = hexc('#68B830') # Crisp clover leaf green
C_CLOVER_HIGH   = hexc('#A4F252') # Sunlit clover rim

def periodic_noise_2d(W, H, num_harmonics, rng, power=1.5, x_scale=1.0, y_scale=1.0):
    """Generate strictly periodic 2D noise using Fourier synthesis (guaranteed 100% seamless)."""
    kx = np.fft.fftfreq(W) * W
    ky = np.fft.fftfreq(H) * H
    KX, KY = np.meshgrid(kx, ky)
    
    # Anisotropic scaling: higher frequency in X than Y
    freq_sq = (KX * x_scale)**2 + (KY * y_scale)**2
    freq_sq[0, 0] = 1.0 # avoid div by zero
    
    # Power spectrum (1 / f^power)
    amplitude = 1.0 / (freq_sq**(power / 2.0))
    amplitude[0, 0] = 0.0
    
    # Limit max harmonics for smoothness
    cutoff = num_harmonics
    amplitude[freq_sq > cutoff**2] = 0.0
    
    # Random phases
    phase = rng.uniform(0, 2 * np.pi, (H, W))
    fourier = amplitude * (np.cos(phase) + 1j * np.sin(phase))
    
    # Inverse FFT to get real-space periodic noise
    noise = np.fft.ifft2(fourier).real
    noise = (noise - noise.mean()) / (noise.std() + 1e-6)
    return noise

def generate_pro_bark(name, W=1024, H=1024, is_branch=False, seed=42):
    rng = np.random.default_rng(seed)
    
    # Normalized periodic coordinates [0, 1)
    y, x = np.mgrid[0:H, 0:W]
    u = x / float(W)
    v = y / float(H)

    # 1. Large-scale periodic domain warp (organic meandering of tree trunk)
    warp_x = 0.025 * np.sin(2 * np.pi * (v * 1.0 + 0.15)) + \
             0.015 * np.sin(2 * np.pi * (v * 2.0 + 0.45)) + \
             0.008 * np.sin(2 * np.pi * (v * 4.0 + 0.85))
             
    warp_y = 0.006 * np.cos(2 * np.pi * (u * 1.0 + 0.35)) + \
             0.004 * np.cos(2 * np.pi * (u * 3.0 + 0.65))

    u_warped = (u + warp_x) % 1.0
    v_warped = (v + warp_y) % 1.0

    # 2. Organic Wood Knots with streamline deflection
    knots = [
        {'u': 0.32, 'v': 0.28, 'r': 0.055, 'core_r': 0.018},
        {'u': 0.76, 'v': 0.65, 'r': 0.065, 'core_r': 0.022},
        {'u': 0.18, 'v': 0.82, 'r': 0.045, 'core_r': 0.015}
    ] if not is_branch else [
        {'u': 0.40, 'v': 0.35, 'r': 0.045, 'core_r': 0.015},
        {'u': 0.80, 'v': 0.70, 'r': 0.040, 'core_r': 0.012}
    ]

    knot_mask = np.zeros((H, W), float)
    knot_rings = np.zeros((H, W), float)
    stream_u = u_warped.copy()
    stream_v = v_warped.copy()

    for k in knots:
        # Periodic distance to knot center
        du = np.minimum(np.abs(stream_u - k['u']), 1.0 - np.abs(stream_u - k['u']))
        # signed du with periodic wrap
        du_raw = stream_u - k['u']
        du_signed = np.where(du_raw > 0.5, du_raw - 1.0, np.where(du_raw < -0.5, du_raw + 1.0, du_raw))
        dv_raw = stream_v - k['v']
        dv_signed = np.where(dv_raw > 0.5, dv_raw - 1.0, np.where(dv_raw < -0.5, dv_raw + 1.0, dv_raw))
        
        # Elliptical distance (wood knots are vertically elongated)
        dist_knot = np.sqrt((du_signed / 0.85)**2 + (dv_signed / 1.35)**2)
        
        # Streamline deflection around obstacle: pushes wood fibers outward around the knot
        influence = np.exp(-((dist_knot / (k['r'] * 2.2))**2))
        deflect = (du_signed / (dist_knot + 0.01)) * k['r'] * 0.9 * influence
        stream_u = (stream_u + deflect) % 1.0

        # Knot interior mask & concentric growth rings
        km = np.clip((k['r'] * 1.3 - dist_knot) / (k['r'] * 0.35), 0.0, 1.0)
        knot_mask = np.maximum(knot_mask, km)
        
        # Swirling growth rings inside and around knot
        ring_phase = dist_knot / k['r'] * 6.0 * np.pi
        ring = (0.5 + 0.5 * np.cos(ring_phase)) * km
        knot_rings = np.maximum(knot_rings, ring)

    # 3. Primary Bark Ridges (11-13 continuous flowing vertical ridges)
    num_ridges = 11 if not is_branch else 7
    ridge_coord = stream_u * num_ridges
    ridge_frac = ridge_coord % 1.0 # 0..1 across each individual ridge
    
    # Natural organic ridge height profile: beveled dome with deep crevice groove
    # Asymmetric ridge profile creates natural directional form shading (sunlit left flank, shadowed right)
    ridge_h = np.sin(ridge_frac * np.pi)**0.75
    
    # Medium-scale meandering furrow noise
    med_noise = periodic_noise_2d(W, H, num_harmonics=28, rng=rng, power=1.8, x_scale=1.0, y_scale=0.25)
    ridge_h = np.clip(ridge_h + med_noise * 0.18, 0.0, 1.0)
    
    # 4. Fine Wood Fiber Striations (Longitudinal Grain)
    fine_grain = periodic_noise_2d(W, H, num_harmonics=160, rng=rng, power=1.2, x_scale=1.0, y_scale=0.08)
    ultra_grain = periodic_noise_2d(W, H, num_harmonics=300, rng=rng, power=1.0, x_scale=1.0, y_scale=0.04)
    wood_fiber = fine_grain * 0.16 + ultra_grain * 0.08
    
    # Combine into full heightmap
    h_map = np.clip(ridge_h * 0.85 + wood_fiber * 0.25 - knot_mask * 0.20, 0.0, 1.0)
    
    # 5. Crevice / Ambient Occlusion Mask
    # Deep crevices occur where ridge_h is low
    crevice_mask = np.clip((0.35 - ridge_h) / 0.28, 0.0, 1.0)**1.4
    
    # 6. Base Color Synthesizer (Warm hand-painted layering)
    # Start with base warm bark
    albedo = np.zeros((H, W, 3), float)
    
    # Gradient interpolation along ridge height
    # Deep crevice -> Dark bark -> Base bark -> Warm bark -> Light bark -> Crest
    t_h = np.clip(ridge_h, 0.0, 1.0)
    
    # Warm base gradient
    base_layer = np.where(
        t_h[..., None] < 0.4,
        (1.0 - t_h[..., None] / 0.4) * C_DARK_BARK[None, None] + (t_h[..., None] / 0.4) * C_BASE_BARK[None, None],
        (1.0 - (t_h[..., None] - 0.4) / 0.6) * C_BASE_BARK[None, None] + ((t_h[..., None] - 0.4) / 0.6) * C_WARM_BARK[None, None]
    )
    
    # Sunlit flank light (left-facing side of each ridge: ridge_frac between 0.15 and 0.50)
    sunlit_flank = np.clip(np.sin(np.clip((ridge_frac - 0.08) / 0.45, 0, 1) * np.pi), 0, 1)**1.2
    sunlit_light = (C_LIGHT_BARK - C_WARM_BARK)[None, None] * (sunlit_flank[..., None] * 0.85)
    
    # Crisp peak crest highlight (along the ridge peak around ridge_frac ~ 0.38)
    crest_peak = np.clip(1.0 - np.abs(ridge_frac - 0.36) * 7.0, 0.0, 1.0)**2.0
    crest_light = (C_CREST_BARK - C_LIGHT_BARK)[None, None] * (crest_peak[..., None] * 0.95)
    
    # Wood fiber texture modulation
    fiber_mod = 1.0 + wood_fiber[..., None] * 0.45
    
    albedo = (base_layer + sunlit_light + crest_light) * fiber_mod
    
    # Deep crevice AO
    albedo = albedo * (1.0 - crevice_mask[..., None]) + C_DARK_CREVICE[None, None] * crevice_mask[..., None]

    # Knot shading: warm concentric rings + deep dark core
    for k in knots:
        du = np.minimum(np.abs(stream_u - k['u']), 1.0 - np.abs(stream_u - k['u']))
        du_raw = stream_u - k['u']
        du_signed = np.where(du_raw > 0.5, du_raw - 1.0, np.where(du_raw < -0.5, du_raw + 1.0, du_raw))
        dv_raw = stream_v - k['v']
        dv_signed = np.where(dv_raw > 0.5, dv_raw - 1.0, np.where(dv_raw < -0.5, dv_raw + 1.0, dv_raw))
        dist_knot = np.sqrt((du_signed / 0.85)**2 + (dv_signed / 1.35)**2)
        
        # Core shadow
        core = np.clip((k['core_r'] - dist_knot) / (k['core_r'] * 0.4), 0.0, 1.0)
        core_col = C_DARK_CREVICE * 0.85
        albedo = albedo * (1.0 - core[..., None]) + core_col[None, None] * core[..., None]
        
        # Swirling ring tint
        ring_zone = np.clip((k['r'] * 1.25 - dist_knot) / (k['r'] * 0.5), 0.0, 1.0) * (1.0 - core)
        ring_val = 0.5 + 0.5 * np.cos(dist_knot / k['r'] * 7.0 * np.pi)
        ring_tint = (1.0 + (ring_val - 0.5) * 0.35)[..., None]
        albedo = albedo * (1.0 - ring_zone[..., None] * 0.5) + (albedo * ring_tint) * (ring_zone[..., None] * 0.5)

    # 7. Natural Crevice Forest Moss & Lichen
    # Moss grows preferentially in shaded crevices and knot hollows
    moss_potential = crevice_mask * 0.75 + knot_mask * 0.45
    moss_noise = periodic_noise_2d(W, H, num_harmonics=45, rng=rng, power=1.4, x_scale=1.0, y_scale=1.0)
    moss_mask = np.clip((moss_potential + moss_noise * 0.38 - 0.58) / 0.22, 0.0, 1.0)
    moss_mask = gaussian_filter(moss_mask, 1.2, mode='wrap')
    
    # Moss shading: velvety dark base, vibrant green body, sunlit tips
    moss_edge = np.clip(moss_mask * (1.0 - moss_mask) * 4.0, 0.0, 1.0)
    moss_core = np.clip((moss_mask - 0.5) * 2.0, 0.0, 1.0)
    
    moss_color = C_MOSS_DEEP[None, None] * (1.0 - moss_mask[..., None]) + \
                 C_MOSS_BASE[None, None] * moss_mask[..., None] + \
                 (C_MOSS_LIGHT - C_MOSS_BASE)[None, None] * (moss_core[..., None] * 0.65) + \
                 (C_MOSS_TIP - C_MOSS_LIGHT)[None, None] * (moss_edge[..., None] * 0.45)

    albedo = albedo * (1.0 - moss_mask[..., None]) + moss_color * moss_mask[..., None]

    # 8. Delicate Stylized Clover/Leaf Clusters
    # Place cute 3-lobed clovers in some moss patches
    num_clovers = 18 if not is_branch else 10
    clover_mask = np.zeros((H, W), float)
    clover_rim = np.zeros((H, W), float)
    
    clover_pts = []
    # Find candidate locations with high moss
    moss_indices = np.argwhere(moss_mask > 0.7)
    if len(moss_indices) > 0:
        step = len(moss_indices) // num_clovers
        for i in range(num_clovers):
            idx = moss_indices[(i * step + rng.integers(0, max(1, step))) % len(moss_indices)]
            cy_norm = idx[0] / float(H)
            cx_norm = idx[1] / float(W)
            clover_pts.append((cx_norm, cy_norm))

    leaf_r = 0.009 * (W / 1024.0)
    for cx_norm, cy_norm in clover_pts:
        ang_base = rng.uniform(0, 2 * np.pi)
        # 3 lobes
        for lobe in range(3):
            ang = ang_base + lobe * (2 * np.pi / 3.0)
            lx = (cx_norm + np.cos(ang) * leaf_r * 1.3) % 1.0
            ly = (cy_norm + np.sin(ang) * leaf_r * 1.3) % 1.0
            
            # Periodic distance
            pdx = np.minimum(np.abs(u - lx), 1.0 - np.abs(u - lx)) * W
            pdy = np.minimum(np.abs(v - ly), 1.0 - np.abs(v - ly)) * H
            pdist = np.sqrt(pdx*pdx + pdy*pdy)
            
            lobe_m = np.clip((leaf_r * W - pdist) / 1.5, 0.0, 1.0)
            rim_m = np.clip((leaf_r * W - pdist) / 1.0, 0.0, 1.0) * np.clip((pdist - leaf_r * W * 0.6) / 1.0, 0.0, 1.0)
            clover_mask = np.maximum(clover_mask, lobe_m)
            clover_rim = np.maximum(clover_rim, rim_m)

    clover_col = C_CLOVER[None, None] * (1.0 - clover_rim[..., None]) + C_CLOVER_HIGH[None, None] * clover_rim[..., None]
    albedo = albedo * (1.0 - clover_mask[..., None]) + clover_col * clover_mask[..., None]

    # Heightmap update for moss and clovers (subtle pop)
    h_map = np.clip(h_map + moss_mask * 0.12 + clover_mask * 0.18, 0.0, 1.0)

    # 9. Save Albedo Texture (100% strictly seamless)
    rgb_bytes = (np.clip(albedo, 0.0, 1.0) * 255.0).astype(np.uint8)
    albedo_img = Image.fromarray(rgb_bytes, "RGB")
    albedo_path = os.path.join(PREVIEW, f"{name}.png")
    albedo_img.save(albedo_path, "PNG")
    print(f"Saved Albedo: {albedo_path} ({W}x{H})")

    # 10. Generate 2x2 Tiled Visual Proof
    tiled = Image.new('RGB', (W * 2, H * 2))
    tiled.paste(albedo_img, (0, 0))
    tiled.paste(albedo_img, (W, 0))
    tiled.paste(albedo_img, (0, H))
    tiled.paste(albedo_img, (W, H))
    tiled_path = os.path.join(PREVIEW, f"{name}_tiled2x2.png")
    tiled.resize((1024, 1024), Image.LANCZOS).save(tiled_path)
    print(f"Saved 2x2 Tiled Proof: {tiled_path}")

    # 11. Normal Map (Sobel with periodic wrapping)
    h_smooth = gaussian_filter(h_map, 1.5, mode='wrap')
    strength = 5.0
    gx = (np.roll(h_smooth, -1, axis=1) - np.roll(h_smooth, 1, axis=1)) * strength
    gy = (np.roll(h_smooth, -1, axis=0) - np.roll(h_smooth, 1, axis=0)) * strength
    gz = np.ones_like(gx)
    
    norm = np.sqrt(gx*gx + gy*gy + gz*gz)
    nx = -gx / norm
    ny = -gy / norm # Unity OpenGL Y-up format
    nz = gz / norm
    
    normal_rgb = np.zeros((H, W, 3), dtype=np.uint8)
    normal_rgb[:, :, 0] = np.clip((nx * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    normal_rgb[:, :, 1] = np.clip((ny * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    normal_rgb[:, :, 2] = np.clip((nz * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    
    normal_img = Image.fromarray(normal_rgb, "RGB")
    normal_path = os.path.join(PREVIEW, f"{name}_n.png")
    normal_img.save(normal_path, "PNG")
    print(f"Saved Normal: {normal_path} ({W}x{H})")

    return albedo_path, tiled_path, normal_path

if __name__ == '__main__':
    generate_pro_bark('Bark_Trunk_Pro', 1024, 1024, is_branch=False, seed=123)
    print("Generation complete!")
