import urllib.request
import os

# Let's inspect Assets/Textures/Materials/images.mat texture guid
meta_path = "Assets/Textures/Materials/images.mat"
with open(meta_path, "r") as f:
    text = f.read()
print("images.mat lines around MainTex:")
for line in text.splitlines():
    if "guid" in line or "Texture" in line or "Color" in line:
        print(line)
