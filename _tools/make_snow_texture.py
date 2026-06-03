#!/usr/bin/env python3
"""
Procedural CLEAN snow terrain texture — near-uniform, no features, no baked shadows.
MidJourney can't make a flat featureless ground texture (it always adds drama), so for
near-uniform layers (snow, plain sand) we generate them directly. Seamless/tileable by
construction (integer-frequency sine variation -> wraps perfectly).

Output: Assets/Materials/Textures/Snow.png  (1024, seamless)
"""
import math, os, random
from PIL import Image

N = 1024
BASE = (233, 239, 247)     # soft snow white, faint cool tint
AMP  = 11                  # very subtle brightness variation (of 255)
random.seed(3)

# (fx, fy, phase, weight) — integer freqs -> seamless tiling
TERMS = [(2,3,0.4,1.0), (3,5,1.1,0.55), (5,8,2.0,0.30), (8,5,0.7,0.22), (4,11,1.6,0.16)]
norm = sum(w for *_, w in TERMS)

def clamp(v): return 0 if v < 0 else (255 if v > 255 else int(v))

px = []
for y in range(N):
    fy = y / N
    for x in range(N):
        fx = x / N
        v = 0.0
        for a, b, ph, w in TERMS:
            v += w * math.sin(2*math.pi*a*fx + ph) * math.sin(2*math.pi*b*fy + ph*1.3)
        v /= norm                                   # ~[-1, 1]
        g = AMP * v + random.uniform(-1.5, 1.5)     # subtle grain (tiny, no visible seam)
        # dips read very slightly cooler/bluer, like soft snow shadow without a hard shadow
        px.append((clamp(BASE[0] + g), clamp(BASE[1] + g), clamp(BASE[2] + g*0.6 + (2.5 if v < 0 else 0))))

out = os.path.normpath(os.path.join(os.path.dirname(__file__), "..",
      "Assets/Materials/Textures/Snow.png"))
img = Image.new("RGB", (N, N)); img.putdata(px); img.save(out)
print("Clean snow texture written:", out)
