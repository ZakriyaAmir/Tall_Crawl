from PIL import Image
import numpy as np

im = Image.open('Assets/Textures/textures/download 1.png')
arr = np.array(im)
print("Shape:", arr.shape)
print("Min/Max R:", arr[:,:,0].min(), arr[:,:,0].max())
print("Min/Max G:", arr[:,:,1].min(), arr[:,:,1].max())
print("Min/Max B:", arr[:,:,2].min(), arr[:,:,2].max())
print("Min/Max A:", arr[:,:,3].min(), arr[:,:,3].max())
print("Average color (opaque):", arr[arr[:,:,3] > 128][:,:3].mean(axis=0))
