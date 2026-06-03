#!/usr/bin/env python3
"""
Cut MidJourney UI icons (flat teal background) into game-ready transparent PNGs.

For each icon: rembg removes the teal/grunge background, then we auto-trim to the
subject's bounding box, pad slightly, center on a transparent SQUARE canvas, and
resize to a standard size. Reusable for every HUD/icon batch.

Usage:
  cutout_icons.py --in <src_dir> --out <dst_dir> --size 256 \
      keyword=OutName  keyword=OutName ...

Each `keyword=OutName` pair finds the single source PNG in --in whose filename
contains `keyword` (case-insensitive) and writes <dst>/OutName.png.
"""
import argparse, glob, os, sys
from PIL import Image, ImageFilter
from rembg import remove, new_session

def find_one(src_dir, keyword):
    hits = [p for p in glob.glob(os.path.join(src_dir, "*.png"))
            if keyword.lower() in os.path.basename(p).lower()]
    if len(hits) == 0: sys.exit(f"  ! no match for '{keyword}' in {src_dir}")
    if len(hits) > 1:  sys.exit(f"  ! '{keyword}' matched {len(hits)} files: {[os.path.basename(h) for h in hits]}")
    return hits[0]

def cut(src, dst, size, pad_frac, session, square=True):
    img = Image.open(src).convert("RGBA")
    cut = remove(img, session=session)            # alpha isolates the subject
    # Kill the teal fringe: harden alpha to a clean edge, then erode 2px inward so the
    # semi-transparent boundary ring (which still carries background colour) is dropped.
    r, g, b, alpha = cut.split()
    alpha = alpha.point(lambda v: 255 if v >= 128 else 0)   # hard edge
    alpha = alpha.filter(ImageFilter.MinFilter(5))          # erode ~2px -> no halo
    alpha = alpha.filter(ImageFilter.GaussianBlur(0.6))     # 1px feather so it isn't jagged
    cut = Image.merge("RGBA", (r, g, b, alpha))
    bbox = cut.getbbox()                          # tight box around non-transparent pixels
    if bbox is None: sys.exit(f"  ! {src} came back fully transparent")
    sub = cut.crop(bbox)
    w, h = sub.size
    if square:                                    # icons: centre on a transparent square
        side = int(max(w, h) * (1 + 2 * pad_frac))
        canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        canvas.paste(sub, ((side - w) // 2, (side - h) // 2), sub)
        canvas = canvas.resize((size, size), Image.LANCZOS)
    else:                                         # panels/frames: keep native aspect, pad evenly
        pad = int(max(w, h) * pad_frac)
        canvas = Image.new("RGBA", (w + 2 * pad, h + 2 * pad), (0, 0, 0, 0))
        canvas.paste(sub, (pad, pad), sub)
        scale = size / max(canvas.size)
        canvas = canvas.resize((round(canvas.width * scale), round(canvas.height * scale)), Image.LANCZOS)
    canvas.save(dst)
    return w, h

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src", required=True)
    ap.add_argument("--out", dest="dst", required=True)
    ap.add_argument("--size", type=int, default=256)
    ap.add_argument("--pad", type=float, default=0.06)
    ap.add_argument("--no-square", action="store_true", help="keep native aspect (panels/frames)")
    ap.add_argument("pairs", nargs="+", help="keyword=OutName")
    a = ap.parse_args()
    os.makedirs(a.dst, exist_ok=True)
    session = new_session("isnet-general-use")    # sharp edges, good for crisp cel icons
    for pair in a.pairs:
        key, name = pair.split("=", 1)
        src = find_one(a.src, key)
        out = os.path.join(a.dst, name + ".png")
        w, h = cut(src, out, a.size, a.pad, session, square=not a.no_square)
        print(f"  {name:10s} <- {os.path.basename(src)[:40]:40s} ({w}x{h} -> {a.size})")

if __name__ == "__main__":
    main()
