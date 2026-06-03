#!/usr/bin/env python3
"""
WildReach — Island 1 heightmap generator.

Authors the home island from the locked concept map
(Blueprints/WildReach/assets/island1-map-reference.png):
  - snowy mountain range + central peak across the NORTH (high v / +Z)
  - a flattened vale shelf on the left slope for the home tree
  - a forest bowl sloping down through the middle
  - a warm sand beach across the SOUTH (low v / -Z)
  - ocean on all sides

Outputs:
  Assets/Terrain/Island1_Heightmap.raw  -> 16-bit LE, 1025x1025, row-major (read by BuildIsland.cs)
  _tools/island1_preview.png            -> colorized top-down preview (for eyeballing the shape)

Conventions (must match BuildIsland.cs):
  TERRAIN_SIZE = 1024 m,  TERRAIN_HEIGHT = 300 m,  SEA_LEVEL = 30 m (normalized 0.10)
  Heightmap row 0 = beach (south, Z index 0); last row = mountains (north).

Pure stdlib + PIL (numpy is not installed on this machine). Deterministic (seeded).
"""
import math, random, struct, os
from PIL import Image

# ---- knobs (terrain shape lives here; iterate these, regenerate, re-click Build) ----
COMPUTE_N   = 513          # compute resolution (upscaled to OUT_N at the end)
OUT_N       = 1025         # Unity heightmap resolution (2^n + 1)
SEA         = 0.10         # sea level, normalized (30 m of 300 m)
SEED        = 7

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
RAW_OUT     = os.path.join(ROOT, "Assets/Terrain/Island1_Heightmap.raw")
PREVIEW_OUT = os.path.join(ROOT, "_tools/island1_preview.png")

random.seed(SEED)

def smoothstep(a, b, x):
    if a == b: return 0.0 if x < a else 1.0
    t = (x - a) / (b - a)
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)

def clamp01(x):
    return 0.0 if x < 0 else (1.0 if x > 1 else x)

def noise_field(octaves):
    """fBm-ish field via PIL bicubic upsampling of small random grids (interp runs in C)."""
    buf = [0.0] * (COMPUTE_N * COMPUTE_N)
    for g, amp in octaves:
        small = Image.new('L', (g, g))
        small.putdata([random.randint(0, 255) for _ in range(g * g)])
        big = small.resize((COMPUTE_N, COMPUTE_N), Image.BICUBIC)
        d = list(big.getdata())
        for i in range(COMPUTE_N * COMPUTE_N):
            buf[i] += amp * (d[i] / 255.0 - 0.5)
    mx = max(1e-6, max(abs(min(buf)), abs(max(buf))))
    return [x / mx for x in buf]

fbm   = noise_field([(6,1.0),(12,0.5),(24,0.27),(48,0.14),(96,0.07)])
rsrc  = noise_field([(8,1.0),(16,0.5),(32,0.25)])
ridge = [1.0 - abs(2*(0.5 + 0.5*x) - 1) for x in rsrc]   # 0..1 ridged for craggy peaks

N = COMPUTE_N
height = [0.0] * (N * N)

for j in range(N):                 # j -> v (south->north). v=1 = mountains
    v = j / (N - 1)
    for i in range(N):
        u = i / (N - 1)            # u -> east-west, 0.5 = center
        idx = j * N + i
        du, dv = (u - 0.5), (v - 0.5)

        # --- organic island mask (1 interior, 0 ocean) ---
        ang  = math.atan2(dv, du)
        lump = 0.06*math.sin(3*ang + 1.3) + 0.04*math.sin(5*ang + 0.5) + 0.05*fbm[idx]
        r    = math.sqrt((du*1.15)**2 + (dv*0.95)**2)
        coast = 0.42 + lump
        edge  = 0.085 + 0.075*smoothstep(0.55, 0.05, v)   # wider, gentler shore on the beach side
        mask  = 1.0 - smoothstep(coast - edge, coast + edge, r)

        # --- land elevation profile (cold/high north -> warm/low south) ---
        base  = 0.155                                      # forest floor: interior is land, not sand
        base += 0.30 * smoothstep(0.24, 0.60, v)          # forest slope rise
        base += 0.55 * smoothstep(0.56, 0.93, v)          # mountain rise

        mtn   = smoothstep(0.56, 0.80, v)
        hill  = smoothstep(0.22, 0.55, v) * (1 - mtn)
        base += mtn  * (0.18*ridge[idx] + 0.10*fbm[idx])  # craggy mountain detail
        base += 0.05 * hill * fbm[idx]                     # rolling forest hills
        base += 0.012 * fbm[idx]                           # gentle everywhere

        # central peak + two shoulders
        base += 0.18 * math.exp(-(((u-0.50)/0.10)**2 + ((v-0.88)/0.10)**2))
        base += 0.11 * math.exp(-(((u-0.32)/0.08)**2 + ((v-0.80)/0.09)**2))
        base += 0.09 * math.exp(-(((u-0.70)/0.09)**2 + ((v-0.82)/0.10)**2))

        # the launch beach: a sandy bowl pulled down into the south-centre coast
        base -= 0.060 * math.exp(-(((u-0.50)/0.20)**2 + ((v-0.085)/0.075)**2))

        # home-tree vale: flatten a buildable shelf into the left lower slope
        db = math.sqrt(((u-0.36)/0.075)**2 + ((v-0.655)/0.060)**2)
        w  = smoothstep(1.5, 0.3, db)
        base = base*(1 - w) + 0.40*w

        # soft ceiling so the highest ground rounds into a peak instead of a flat plateau
        if base > 0.85:
            base = 0.85 + (base - 0.85) * 0.45

        # --- ocean floor, then blend land over it by the mask ---
        ocean = 0.092 - 0.066 * smoothstep(coast, coast + 0.30, r)
        h = ocean*(1 - mask) + base*mask
        height[idx] = clamp01(h)

# --- upscale the field to Unity resolution (bicubic, in C) ---
fimg = Image.new('F', (N, N)); fimg.putdata(height)
big  = fimg.resize((OUT_N, OUT_N), Image.BICUBIC)
hd   = list(big.getdata())

# --- write 16-bit RAW (row-major, row 0 = south/beach) ---
with open(RAW_OUT, 'wb') as f:
    f.write(struct.pack('<%dH' % len(hd), *[int(round(clamp01(x) * 65535)) for x in hd]))

# --- colorized preview so the shape reads at a glance ---
def shade(h):
    if h < SEA - 0.045: return (12, 70, 120)      # deep ocean
    if h < SEA:         return (24, 110, 150)      # shallows
    if h < SEA + 0.035: return (224, 200, 110)     # sand beach
    if h < 0.42:        return (70, 140, 70)        # forest
    if h < 0.62:        return (90, 110, 95)         # tree-line / rock
    if h < 0.80:        return (140, 150, 160)       # rock
    return (235, 240, 245)                            # snow
prev = Image.new('RGB', (OUT_N, OUT_N))
prev.putdata([shade(clamp01(x)) for x in hd])
prev = prev.transpose(Image.FLIP_TOP_BOTTOM)         # put north/mountains at the top for reading
prev.save(PREVIEW_OUT)

# --- report ---
land = [x for x in hd if x >= SEA]
print("Island1 heightmap written.")
print("  raw     :", RAW_OUT, "(%d x %d, 16-bit)" % (OUT_N, OUT_N))
print("  preview :", PREVIEW_OUT)
print("  land coverage: %.1f%%   max height: %.1f m   peak norm: %.3f"
      % (100.0*len(land)/len(hd), max(hd)*300.0, max(hd)))
