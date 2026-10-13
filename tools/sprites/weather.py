"""Cut the weather sheets into overlay frames in assets/ui/weather/.

  python weather.py [review]

These are not sprites. They are full-screen overlays drawn over the world,
so they want soft alpha, not the hard one-bit edge the character cutter
gives: a snowflake blended into the magenta at its rim should come out
half transparent, and fog is nothing but soft edges.

A pixel on a magenta sheet is c*a + magenta*(1-a). The green channel of
magenta is zero, so a pixel's green is a*c_green and, the content here
being white or near-white, the green channel IS the alpha. The red and
blue are then unmixed back to the content's own colour.

Each sheet is one row of four frames of the same animation, cut apart at
the empty columns between them.
"""
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(ROOT, "assets", "external", "NEW HBC ASSETS")
OUT = os.path.join(ROOT, "assets", "ui", "weather")

# sheet: (out name, how bright the content is at full opacity)
# sheet: (out name, how bright the content is at full opacity, solid)
# The whiteout of a blizzard is not in the art - the sheet draws driven
# snow on the same keyed ground as the rest - so it comes off the same soft
# alpha, and the overlay lays a white veil behind it.
SHEETS = {
    # The light snow sheet's title is drawn over the first frame, so that
    # frame starts below it (and all four are cut the same, to animate).
    "light snow": ("snow_light", 255, False, 52),
    "Heavy snow": ("snow_heavy", 255, False, 0),
    "blizzard": ("blizzard", 255, False, 0),
    "rain": ("rain", 235, False, 0),
    "fog": ("fog", 215, False, 0),
}
# The aurora is one long seamless band, not an animation.
BANDS = {"Night Sky": "aurora"}


def unmix(im, white, solid=False):
    """RGBA from a frame drawn on a flat keyed panel, with soft alpha.

    The panel's own colour is whatever most of the frame is; the content is
    white or near-white. Green separates them cleanly on these sheets, so
    the alpha comes off the green channel and the red and blue are unmixed
    back to the content's own colour."""
    bg = np.array([np.median(im[..., i]) for i in range(3)], float)
    g = im[..., 1].astype(float)
    a = np.clip((g - bg[1]) / max(8.0, white - bg[1]), 0, 1)
    out = np.zeros(im.shape[:2] + (4,), np.uint8)
    safe = np.maximum(a, 1e-6)
    for i in range(3):
        c = (im[..., i].astype(float) - bg[i] * (1 - a)) / safe
        out[..., i] = np.clip(np.where(a > 0.02, c, 0), 0, 255)
    out[..., 3] = 255 if solid else (a * 255).astype(np.uint8)
    if solid:
        for i in range(3):
            out[..., i] = im[..., i]
    return out


def frames(im, white):
    """The four frames of a sheet.

    Each frame is drawn on its own panel, a shade off the sheet's magenta,
    with pure magenta between them - so the panels are what to look for,
    not the snow on them (a sparse sheet has empty columns inside a frame
    as often as between two)."""
    corner = im[2, 2].astype(int)
    off = np.abs(im.astype(int) - corner).max(axis=2)
    # How far off the sheet's magenta a panel sits varies from sheet to
    # sheet - the blizzard's is obvious, the light snow's is two values -
    # so try the strict reading first and loosen until four frames fall out.
    runs, panel = [], None
    for t in (22, 12, 5, 3):
        # A sheet's title is near-black and sits against the top of the
        # first panel; it is not part of the frame.
        panel = (off > t) & (im.max(axis=2) > 70)
        rows = np.where(panel.sum(axis=1) > 200)[0]
        if len(rows) == 0:
            continue
        y0, y1 = rows.min(), rows.max() + 1
        dens = panel[y0:y1].sum(axis=0)
        on = dens > (y1 - y0) * 0.5
        runs, start = [], None
        for x in range(len(on)):
            if on[x] and start is None:
                start = x
            elif not on[x] and start is not None:
                runs.append((start, x)); start = None
        if start is not None:
            runs.append((start, len(on)))
        runs = [r for r in runs if r[1] - r[0] > 40]
        if len(runs) == 4:
            break
    if len(runs) != 4:
        raise SystemExit(f"expected four frames, found {len(runs)}: {runs}")
    # The frame's own height, measured inside the first panel rather than
    # across the whole sheet (these sheets fade out below the frames).
    col = panel[:, runs[0][0] + 4:runs[0][1] - 4]
    tall = np.where(col.sum(axis=1) > (runs[0][1] - runs[0][0] - 8) * 0.5)[0]
    y0, y1 = tall.min(), tall.max() + 1
    return [(r[0], y0, r[1], y1) for r in runs]


def run(review=False):
    os.makedirs(OUT, exist_ok=True)
    made = []
    for sheet, (name, white, solid, top) in SHEETS.items():
        im = np.array(Image.open(os.path.join(SRC, sheet + ".jpg")).convert("RGB"))
        boxes = frames(im, white)
        w = min(b[2] - b[0] for b in boxes)
        h = min(b[3] - b[1] for b in boxes)
        # A few pixels in off each edge: the panels have a seam, and the
        # sheet's title sits against the top of the first one.
        m = 5
        for i, (bx, by, _, _) in enumerate(boxes):
            rgba = unmix(im[by + m + top:by + h - m, bx + m:bx + w - m], white, solid)
            Image.fromarray(rgba).save(os.path.join(OUT, f"{name}_{i}.png"))
        made.append(f"{name} 4 x {w}x{h}")
    for sheet, name in BANDS.items():
        im = Image.open(os.path.join(SRC, sheet + ".jpg")).convert("RGB")
        im.save(os.path.join(OUT, name + ".png"))
        made.append(f"{name} {im.size[0]}x{im.size[1]}")
    print("; ".join(made))
    if review:
        strip = []
        for name, *_ in SHEETS.values():
            row = [Image.open(os.path.join(OUT, f"{name}_{i}.png")) for i in range(4)]
            W, H = row[0].size
            img = Image.new("RGBA", (W * 4, H), (40, 50, 70, 255))
            for i, f in enumerate(row):
                img.alpha_composite(f, (i * W, 0))
            strip.append(img)
        W = max(s.width for s in strip)
        out = Image.new("RGBA", (W, sum(s.height for s in strip)), (40, 50, 70, 255))
        y = 0
        for s in strip:
            out.alpha_composite(s, (0, y)); y += s.height
        out.save(os.path.join(HERE, "out", "review_weather.png"))


if __name__ == "__main__":
    run("review" in sys.argv)
