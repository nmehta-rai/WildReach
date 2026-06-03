#!/usr/bin/env python3
"""
WildReach — Island 1 heightmap FROM an AI-generated grayscale image.

Takes a top-down grayscale heightmap (brightness = elevation, black = sea) and converts it
into the 16-bit RAW that BuildIsland.cs reads, with the project's sea level + coastline applied.
Use this instead of the procedural heightmap_island1.py when shaping the island from a painted map.

Input : _tools/island_source.png   (the ChatGPT/Midjourney top-down grayscale heightmap)
Output: Assets/Terrain/Island1_Heightmap.raw   (16-bit LE, 1025x1025 — same format BuildIsland reads)
        _tools/island1_preview.png             (colorized check)

Knobs below: SEA (sea level), GAMMA (elevation curve), BLUR (smooth AI noise), SEA_FLOOR.
Pure stdlib + PIL (numpy-free).
"""
import struct, os
from PIL import Image, ImageFilter

# ---- knobs ----
OUT_N     = 1025
SEA       = 0.10     # sea level, normalized (must match BuildIsland.cs SeaNorm)
SEA_FLOOR = 0.02     # how deep the pure-black ocean sits
GAMMA     = 1.2      # >1 darkens/flattens lowlands and keeps peaks tall (high-low contrast)
BLUR      = 6.0      # gaussian radius to smooth AI noise into rolling terrain (kills needle spikes + islet cones)

ROOT      = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
SRC       = os.path.join(ROOT, "_tools/island_source.png")
RAW_OUT   = os.path.join(ROOT, "Assets/Terrain/Island1_Heightmap.raw")
PREV_OUT  = os.path.join(ROOT, "_tools/island1_preview.png")

def clamp01(x): return 0.0 if x < 0 else (1.0 if x > 1 else x)

if not os.path.exists(SRC):
    raise SystemExit(f"Put the grayscale heightmap at {SRC} first.")

img = Image.open(SRC).convert("L").resize((OUT_N, OUT_N), Image.BICUBIC)
if BLUR > 0:
    img = img.filter(ImageFilter.GaussianBlur(BLUR))
# image has mountains at the TOP; BuildIsland treats raw row 0 as the SOUTH beach, so flip
# vertically -> beach (image bottom) becomes row 0 (south), mountains end up north (+Z).
img = img.transpose(Image.FLIP_TOP_BOTTOM)

px = list(img.getdata())
lo, hi = min(px), max(px)
span = max(1, hi - lo)

# map grayscale -> normalized terrain height:
#   black (sea) -> below sea level (down to SEA_FLOOR); brighter -> up toward 1.0.
# the land/sea split is taken at a small brightness threshold so the coastline lands cleanly.
SEA_GRAY = 0.06   # fraction of the brightness range treated as "at the waterline"
hd = []
for v in px:
    g = (v - lo) / span                 # 0..1 normalized brightness
    g = g ** GAMMA
    if g <= SEA_GRAY:
        h = SEA_FLOOR + (SEA - SEA_FLOOR) * (g / SEA_GRAY)     # ocean floor -> sea level
    else:
        h = SEA + (1.0 - SEA) * ((g - SEA_GRAY) / (1.0 - SEA_GRAY))  # beach -> peak
    hd.append(clamp01(h))

# 16-bit RAW (row-major; image row 0 = north/top here, which BuildIsland treats as +Z)
with open(RAW_OUT, "wb") as f:
    f.write(struct.pack("<%dH" % len(hd), *[int(round(clamp01(x) * 65535)) for x in hd]))

# colorized preview
def shade(h):
    if h < SEA - 0.045: return (12, 70, 120)
    if h < SEA:         return (24, 110, 150)
    if h < SEA + 0.035: return (224, 200, 110)
    if h < 0.42:        return (70, 140, 70)
    if h < 0.62:        return (90, 110, 95)
    if h < 0.80:        return (140, 150, 160)
    return (235, 240, 245)
prev = Image.new("RGB", (OUT_N, OUT_N)); prev.putdata([shade(x) for x in hd])
prev = prev.transpose(Image.FLIP_TOP_BOTTOM)   # north back up for reading
prev.save(PREV_OUT)

# hillshade relief — reveals whether the surface is smooth or spiky (light from NW)
N = OUT_N
relief = [0] * (N * N)
hscale = 300.0 / 1024.0 * (N - 1)   # vertical metres per horizontal sample, exaggerate readability
lx, ly, lz = -0.5, -0.5, 0.7
ll = (lx*lx + ly*ly + lz*lz) ** 0.5
lx, ly, lz = lx/ll, ly/ll, lz/ll
for j in range(N):
    for i in range(N):
        xm = hd[j*N + max(0, i-1)] * 300.0; xp = hd[j*N + min(N-1, i+1)] * 300.0
        ym = hd[max(0, j-1)*N + i] * 300.0; yp = hd[min(N-1, j+1)*N + i] * 300.0
        nx = (xm - xp); ny = (ym - yp); nz = 2.0 * (1024.0 / (N - 1))
        nl = (nx*nx + ny*ny + nz*nz) ** 0.5 or 1.0
        d = (nx/nl)*lx + (ny/nl)*ly + (nz/nl)*lz
        relief[j*N + i] = max(0, min(255, int((0.25 + 0.75 * max(0, d)) * 255)))
rel = Image.new("L", (N, N)); rel.putdata(relief)
rel = rel.transpose(Image.FLIP_TOP_BOTTOM)
RELIEF_OUT = os.path.join(ROOT, "_tools/island1_relief.png")
rel.save(RELIEF_OUT)

land = [x for x in hd if x >= SEA]
print("Heightmap built from image.")
print("  src     :", SRC)
print("  raw     :", RAW_OUT)
print("  preview :", PREV_OUT)
print("  land coverage: %.1f%%   max height: %.1f m" % (100.0*len(land)/len(hd), max(hd)*300.0))
