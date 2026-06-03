#!/usr/bin/env python3
"""
Procedural CLEAN mountain stone texture — soft mottled grey, no crack-shadows, no hard features.
Same idea as the snow: MidJourney can't make flat stone, so we generate it. Character on the
cliffs comes from the terrain geometry + lighting + scattered rock meshes, NOT baked shadows.

Output: Assets/Materials/Textures/SnowRock.png  (1024, seamless)
"""
import math, os, random
from PIL import Image

N = 1024
BASE = (139, 147, 156)     # cool mid grey
AMP  = 17                  # soft tonal mottle (of 255) — visible as stone, not as shadow
random.seed(5)

# integer-frequency sine terms -> seamless. two scales: broad blotches + medium mottle.
TERMS = [(1,2,0.3,1.0), (2,3,1.2,0.7), (3,5,2.1,0.45), (5,4,0.6,0.3), (7,6,1.7,0.2), (4,9,2.6,0.15)]
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
        g = AMP * v + random.uniform(-2.5, 2.5)      # subtle grain for a painted-stone feel
        # cooler in the dips, very slightly warmer on the rises — keeps it reading as stone, not flat clay
        px.append((clamp(BASE[0] + g + (1.5 if v > 0 else 0)),
                   clamp(BASE[1] + g),
                   clamp(BASE[2] + g - (2.0 if v < 0 else 0))))

out = os.path.normpath(os.path.join(os.path.dirname(__file__), "..",
      "Assets/Materials/Textures/SnowRock.png"))
img = Image.new("RGB", (N, N)); img.putdata(px); img.save(out)
print("Clean stone texture written:", out)
