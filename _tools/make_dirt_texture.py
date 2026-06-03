#!/usr/bin/env python3
"""
Procedural CLEAN town dirt texture — warm packed earth, soft mottle, no rocks/plants/shadows.
Same principle as snow + stone: flat and feature-free so it tiles invisibly; props on top do detail.

Output: Assets/Materials/Textures/Dirt_Rocky.png  (1024, seamless) — the town ground layer
"""
import math, os, random
from PIL import Image

N = 1024
BASE = (122, 88, 60)       # warm brown packed earth
AMP  = 20                  # dirt reads with a touch more tonal variation than snow/stone
random.seed(11)

# two scales: broad damp/dry patches + medium mottle. integer freqs -> seamless.
TERMS = [(1,1,0.2,1.0), (2,3,1.0,0.7), (3,2,2.2,0.5), (5,4,0.5,0.32), (4,7,1.8,0.22), (8,6,2.7,0.15)]
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
        v /= norm                                    # ~[-1, 1]
        g = AMP * v + random.uniform(-3, 3)          # grain for an earthy feel
        # rises read drier/lighter-warm, dips read damper/darker — subtle, no hard edges
        px.append((clamp(BASE[0] + g*1.05 + (3 if v > 0 else 0)),
                   clamp(BASE[1] + g*0.95),
                   clamp(BASE[2] + g*0.8 - (3 if v < 0 else 0))))

out = os.path.normpath(os.path.join(os.path.dirname(__file__), "..",
      "Assets/Materials/Textures/Dirt_Rocky.png"))
img = Image.new("RGB", (N, N)); img.putdata(px); img.save(out)
print("Clean dirt texture written:", out)
