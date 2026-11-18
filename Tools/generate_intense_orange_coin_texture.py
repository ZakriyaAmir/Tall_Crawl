from PIL import Image
import numpy as np

im = Image.open('Assets/Textures/textures/download 1.png')
arr = np.array(im, dtype=np.float32)

mask = arr[:,:,3] > 0
out = np.zeros_like(arr)
out[:,:,3] = arr[:,:,3]

# Compute grayscale luminance
lum = (0.299 * arr[:,:,0] + 0.587 * arr[:,:,1] + 0.114 * arr[:,:,2]) / 255.0

# Deep, blazing fiery neon-orange:
# Base hue: RGB (255, 75, 0) - unmistakably blazing orange (pure contrast against gold/yellow)
# Highlights: RGB (255, 140, 20)
# Shadows: RGB (185, 40, 0)
for y in range(512):
    for x in range(512):
        if arr[y, x, 3] > 0:
            l = lum[y, x]
            # Deep electric orange with sculpted bevel shadows and gleam
            out[y, x, 0] = np.clip(255.0 * np.power(l, 0.65), 0, 255)
            out[y, x, 1] = np.clip(78.0 * np.power(l, 0.85), 0, 255)
            out[y, x, 2] = np.clip(0.0, 0, 255)

out_im = Image.fromarray(out.astype(np.uint8))
out_im.save('Assets/Textures/textures/coin_orange.png')
print("Successfully generated intense blazing fiery orange texture!")
