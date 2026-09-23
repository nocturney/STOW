from PIL import Image
import os
import shutil

ROOT = r"C:\Users\Chris\STOW"
SOURCE = os.path.join(ROOT, "assets", "source", "STOW-icon-master.png")
OUT_PNG = os.path.join(ROOT, "assets", "STOW.png")
OUT_ICO = os.path.join(ROOT, "assets", "STOW.ico")
ICON_SIZES = [
    (16, 16), (20, 20), (24, 24), (32, 32), (40, 40),
    (48, 48), (64, 64), (96, 96), (128, 128), (256, 256),
]

if not os.path.exists(SOURCE):
    raise FileNotFoundError(f"Missing approved STOW icon source: {SOURCE}")

master = Image.open(SOURCE).convert("RGBA")
if master.width != master.height:
    raise ValueError("Approved STOW icon source must be square.")

# Keep the shipped PNG pixel-identical to the approved handoff source.
shutil.copyfile(SOURCE, OUT_PNG)

# Derive the Windows multi-resolution icon from the same approved source.
master.save(OUT_ICO, "ICO", sizes=ICON_SIZES)

print(f"source: {SOURCE}")
print(f"png:    {OUT_PNG}")
print(f"ico:    {OUT_ICO}")
