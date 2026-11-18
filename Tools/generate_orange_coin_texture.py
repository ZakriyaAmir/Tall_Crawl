from PIL import Image
import numpy as np

im = Image.open('Assets/Textures/textures/download 1.png')
arr = np.array(im, dtype=np.float32)

# Normal coin texture average is roughly R=195, G=160, B=38 (Yellow-Gold).
# To make it vibrant, striking ORANGE:
# We boost Red towards 255, shift Green down (from 160 -> ~95-100), and keep Blue very low (~10-15).
# Specifically:
# R = np.clip(arr[:,:,0] * 1.15, 0, 255)
# G = np.clip(arr[:,:,1] * 0.58, 0, 255)
# B = np.clip(arr[:,:,2] * 0.20, 0, 255)

out = np.zeros_like(arr)
out[:,:,0] = np.clip(arr[:,:,0] * 1.25, 0, 255)
out[:,:,1] = np.clip(arr[:,:,1] * 0.55, 0, 255)
out[:,:,2] = np.clip(arr[:,:,2] * 0.15, 0, 255)
out[:,:,3] = arr[:,:,3] # Keep original alpha / transparency intact

out_im = Image.fromarray(out.astype(np.uint8))
out_path = 'Assets/Textures/textures/coin_orange.png'
out_im.save(out_path)
print("Saved orange coin texture to", out_path)
