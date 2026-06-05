#!/usr/bin/env python3
# Simple, seamless, near-flat cel forest-floor ground texture. NO recognizable features
# (no leaves/acorns/twigs) so it tiles invisibly across the terrain; the 3D grass + props
# carry all the visual detail (the BotW model). Tileable because FFT low-pass of white noise
# is inherently periodic. Tune the knobs at the top, re-run, re-judge.
import sys
import numpy as np
from PIL import Image

SIZE   = 1024
SEED   = 7
LEVELS = 10                  # cel posterization (fewer = flatter bands)

# ONE earthy hue, only gentle BRIGHTNESS mottle (coupling hue to the noise = camo, so don't).
BASE  = (0.37, 0.33, 0.20)   # warm earthy soil/moss
VAR   = 0.16                 # soft lightness variation (0 = flat, 0.4 = busy)
GRAIN = 0.07                 # fine grain amount

# faint pebble-detail layer composited on top of the soil base (shapes show, doesn't dominate)
OVERLAY    = "Assets/Materials/Textures/Dirt_Rocky.png"
OVERLAY_OP = 0.22

OUT_FOREST  = "Assets/Materials/Textures/Forest.png"   # the live forest ground layer
OUT_PREVIEW = "_tools/forest_floor_preview.png"
OUT_TILED   = "_tools/forest_floor_tiled.png"

def tnoise(size, wavelength_px, rng):
    """Tileable smooth noise: white noise low-passed in the frequency domain."""
    f = np.fft.fft2(rng.standard_normal((size, size)))
    fy = np.fft.fftfreq(size)[:, None]
    fx = np.fft.fftfreq(size)[None, :]
    freq = np.sqrt(fx * fx + fy * fy)
    sigma = 1.0 / wavelength_px
    filt = np.exp(-(freq * freq) / (2 * sigma * sigma))
    out = np.real(np.fft.ifft2(f * filt))
    out -= out.min()
    out /= (out.max() + 1e-9)
    return out

def main():
    rng = np.random.default_rng(SEED)
    big  = tnoise(SIZE, 240, rng)   # large soft variation
    med  = tnoise(SIZE, 70,  rng)   # mid variation
    fine = tnoise(SIZE, 14,  rng)   # soft grain

    base = np.array(BASE)
    var = (big - 0.5) * 0.6 + (med - 0.5) * 0.4                 # soft, centred ~0
    grain = 1.0 - GRAIN * 0.5 + GRAIN * fine
    bright = (1.0 + VAR * 2.0 * var) * grain                    # one brightness field
    bright = np.clip(bright, 0.2, 1.6)
    bright = np.round(bright * LEVELS) / LEVELS                 # cel bands on BRIGHTNESS only -> hue stays put
    base = np.clip(base[None, None, :] * bright[..., None], 0, 1)

    # composite the faint pebble-detail overlay on top (its shapes show, base colour stays)
    if OVERLAY_OP > 0:
        ov = np.asarray(Image.open(OVERLAY).convert("RGB").resize((SIZE, SIZE)), dtype=np.float32) / 255.0
        base = np.clip(base * (1 - OVERLAY_OP) + ov * OVERLAY_OP, 0, 1)

    arr = np.clip(base * 255, 0, 255).astype(np.uint8)
    img = Image.fromarray(arr)
    img.save(OUT_FOREST)
    img.save(OUT_PREVIEW)

    tiled = Image.new("RGB", (SIZE * 2, SIZE * 2))
    for i in range(2):
        for j in range(2):
            tiled.paste(img, (i * SIZE, j * SIZE))
    tiled.resize((1024, 1024)).save(OUT_TILED)
    print("saved", OUT_PREVIEW, "and", OUT_TILED)

if __name__ == "__main__":
    main()
