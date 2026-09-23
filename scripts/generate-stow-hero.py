from collections import deque
from PIL import Image, ImageFilter
import os

ROOT = r"C:\Users\Chris\STOW"
SOURCE = os.path.join(ROOT, "assets", "source", "STOW-icon-master.png")
OUT = os.path.join(ROOT, "assets", "STOWHero.png")

# This crop sits inside the rounded-square tile and contains the complete
# tray/cards foreground from the approved handoff artwork.
CROP = (45, 70, 467, 455)

def is_background(pixel):
    r, g, b, a = pixel
    if a == 0:
        return True
    # Calibrated to the approved master: connected dark navy is the tile;
    # the blue/cyan glass and tray stay foreground.
    return r < 105 and g < 130 and b < 175 and max(r, g, b) < 180

if not os.path.exists(SOURCE):
    raise FileNotFoundError(f"Missing approved STOW icon source: {SOURCE}")
master = Image.open(SOURCE).convert("RGBA")
crop = master.crop(CROP)
w, h = crop.size
pixels = crop.load()

background = bytearray(w * h)
queue = deque()

def enqueue(x, y):
    index = y * w + x
    if background[index] or not is_background(pixels[x, y]):
        return
    background[index] = 1
    queue.append((x, y))

for x in range(w):
    enqueue(x, 0)
    enqueue(x, h - 1)
for y in range(h):
    enqueue(0, y)
    enqueue(w - 1, y)

while queue:
    x, y = queue.popleft()
    if x > 0:
        enqueue(x - 1, y)
    if x + 1 < w:
        enqueue(x + 1, y)
    if y > 0:
        enqueue(x, y - 1)
    if y + 1 < h:
        enqueue(x, y + 1)

mask = Image.new("L", (w, h), 255)
mask_pixels = mask.load()
for y in range(h):
    row = y * w
    for x in range(w):
        if background[row + x]:
            mask_pixels[x, y] = 0

# A sub-pixel feather keeps the source antialiasing without re-drawing it.
mask = mask.filter(ImageFilter.GaussianBlur(0.8))
crop.putalpha(mask)

bbox = mask.getbbox()
if bbox is None:
    raise RuntimeError("Hero extraction produced an empty foreground.")

margin = 8
left = max(0, bbox[0] - margin)
top = max(0, bbox[1] - margin)
right = min(w, bbox[2] + margin)
bottom = min(h, bbox[3] + margin)
hero = crop.crop((left, top, right, bottom))
hero.save(OUT, "PNG")

print(f"source: {SOURCE}")
print(f"hero:   {OUT} ({hero.width}x{hero.height})")
