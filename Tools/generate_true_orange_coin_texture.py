from PIL import Image
import numpy as np

im = Image.open('Assets/Textures/textures/download 1.png')
arr = np.array(im, dtype=np.float32)

mask = arr[:,:,3] > 0
out = np.zeros_like(arr)
out[:,:,3] = arr[:,:,3]

# Compute grayscale luminance
lum = (0.299 * arr[:,:,0] + 0.587 * arr[:,:,1] + 0.114 * arr[:,:,2]) / 255.0

# True Pure Juicy Orange (RGB approx 255, 130-140, 0):
# Neither red nor yellow: a classic vibrant tangerine / traffic cone orange.
for y in range(512):
    for x in range(512):
        if arr[y, x, 3] > 0:
            l = lum[y, x]
            out[y, x, 0] = np.clip(255.0 * np.power(l, 0.70), 0, 255)
            out[y, x, 1] = np.clip(135.0 * np.power(l, 0.85), 0, 255)
            out[y, x, 2] = np.clip(10.0 * np.power(l, 1.10), 0, 255)

out_im = Image.fromarray(out.astype(np.uint8))
out_im.save('Assets/Textures/textures/coin_orange.png')
print("Successfully generated true pure orange coin texture (R=255, G=135, B=10)!")
