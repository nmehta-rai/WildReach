#!/usr/bin/env python3
"""
Warm cel heart sprites for the health HUD (plain, no rune accent).
Generates a filled heart and an empty (depleted) heart, both with a dark outline,
soft vertical shading and a small highlight. Supersampled for clean AA edges.

Output: Assets/UI/Heart.png, Assets/UI/Heart_Empty.png  (128px, transparent)
"""
import os
from PIL import Image, ImageChops, ImageFilter

N = 128
S = 5                      # supersample factor
M = N * S

def heart_mask():
    m = Image.new("L", (M, M), 0)
    px = m.load()
    for j in range(M):
        y = -((j / M) * 2 - 1) * 1.45 + 0.30      # y up, nudged down to centre in frame
        for i in range(M):
            x = ((i / M) * 2 - 1) * 1.45
            v = (x * x + y * y - 1) ** 3 - x * x * (y ** 3)
            if v <= 0:
                px[i, j] = 255
    return m.resize((N, N), Image.LANCZOS)

def shade(top, bot):
    g = Image.new("RGB", (N, N))
    for j in range(N):
        t = j / (N - 1)
        row = tuple(int(top[k] + (bot[k] - top[k]) * t) for k in range(3))
        for i in range(N):
            g.putpixel((i, j), row)
    return g

def build(mask, top, bot, outline, hi_alpha):
    out = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    # outline = mask dilated minus mask
    dil = mask.filter(ImageFilter.MaxFilter(7))
    ring = ImageChops.subtract(dil, mask)
    out.paste(outline, (0, 0), dil)                       # dark base (becomes the rim)
    fill = shade(top, bot)
    out.paste(fill, (0, 0), mask)                         # shaded fill on top
    # soft highlight, upper-left lobe
    hi = Image.new("L", (N, N), 0)
    hd = hi.load()
    for j in range(N):
        for i in range(N):
            dx, dy = (i - N * 0.36), (j - N * 0.34)
            if dx * dx + dy * dy < (N * 0.14) ** 2:
                hd[i, j] = hi_alpha
    hi = hi.filter(ImageFilter.GaussianBlur(N * 0.04))
    hi = ImageChops.multiply(hi, mask)                    # clip to heart
    white = Image.new("RGBA", (N, N), (255, 255, 255, 0))
    white.putalpha(hi)
    out = Image.alpha_composite(out, white)
    return out

mask = heart_mask()
dst = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "Assets/UI"))
os.makedirs(dst, exist_ok=True)

full  = build(mask, (238, 96, 102), (198, 44, 56), (104, 26, 36), 150)
empty = build(mask, (74, 52, 56),  (54, 36, 40),  (104, 26, 36), 40)

full.save(os.path.join(dst, "Heart.png"))
empty.save(os.path.join(dst, "Heart_Empty.png"))
print("Hearts written to", dst)
