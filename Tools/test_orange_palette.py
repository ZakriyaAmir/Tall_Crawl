from PIL import Image
import numpy as np

im = Image.open('Assets/Textures/textures/download 1.png')
arr = np.array(im, dtype=np.float32)

# Create an intense, unmistakably orange texture
# Pure vibrant orange has high Red (~255), medium-low Green (~110-120), very low Blue (~0-20)
mask = arr[:,:,3] > 0
out = np.zeros_like(arr)
out[:,:,3] = arr[:,:,3]

# Compute luminance to preserve the coin's 3D embossing/ridges and bevels
# Standard luminance weights: 0.299 R + 0.587 G + 0.114 B
lum = (0.299 * arr[:,:,0] + 0.587 * arr[:,:,1] + 0.114 * arr[:,:,2]) / 255.0

# Base orange color: [255, 115, 5] (vibrant juicy orange)
# Highlights: [255, 175, 40]
# Shadows: [200, 70, 0]
for y in range(512):
    for x in range(512):
        if arr[y, x, 3] > 0:
            l = lum[y, x]
            out[y, x, 0] = np.clip(255.0 * np.power(l, 0.7), 0, 255)
            out[y, x, 1] = np.clip(115.0 * np.power(l, 0.9), 0, 255)
            out[y, x, 2] = np.clip(8.0 * np.power(l, 1.2), 0, 255)

out_im = Image.fromarray(out.astype(np.uint8))
out_im.save('Assets/Textures/textures/coin_orange.png')
print("Successfully generated vivid orange coin texture with embossed 3D relief!")
