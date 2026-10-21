"""Cut the fire sheet into frames in assets/sprites/fx/.

  python fire.py [review]

The small fire is drawn in its ring of stones and the large one against a
masonry back; the world already has both of those as tiles, so what is cut
here is the flame alone - everything fire-coloured, with the stone and the
brick dropped - and it is laid over whatever hearth the tile shows. Smoke
and sparks come off the magenta in the ordinary way.
"""
import os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(ROOT, "assets", "external", "NEW HBC ASSETS")
OUT = os.path.join(ROOT, "assets", "sprites", "fx")
sys.path.insert(0, HERE)
import slice as sl

# The part numbers `slice.py detect fire.jpg --merge 45` prints.
FLAME = {"campfire": [0, 1, 2, 3, 4, 5], "hearthfire": [6, 7, 8, 9, 10, 11], "embers": [18, 19, 20, 21]}
KEYED = {"smoke": [12, 13, 14, 15, 16, 17]}
# The sparks are a scatter of single pixels the figure finder throws away.
SPARKS = [(20, 2360, 380, 2700), (380, 2360, 740, 2700), (740, 2360, 1100, 2700), (1100, 2360, 1440, 2700)]


def fiery(rgb):
    """Flame, as against stone, brick, ash and magenta. The hard case is the
    masonry: a warm brown is also red-ish, so flame is taken to be either
    bright (the yellow heart and the orange of it) or deeply red in a way no
    stone is."""
    r, g, b = [rgb[..., i].astype(int) for i in range(3)]
    bright = (r > 196) & (r - b > 75) & (g > 70)
    deep = (r > 150) & (r - g > 78) & (r - b > 95)
    fire = bright | deep
    # The magenta ground is red and blue together; flame is not.
    fire &= ~((r - g > 70) & (b - g > 70))
    return fire


def smoke(rgba_sub):
    """Grey smoke blended into the magenta: the green channel is what is
    left of it, so that is the alpha, and the colour is the grey itself."""
    g = rgba_sub[..., 1].astype(float)
    a = np.clip(g / 190.0, 0, 1)
    out = np.zeros(rgba_sub.shape[:2] + (4,), np.uint8)
    v = np.clip(g / np.maximum(a, 1e-6), 0, 255).astype(np.uint8)
    for i in range(3):
        out[..., i] = v
    out[..., 3] = (a * 255).astype(np.uint8)
    return out


def frame(im, box, mask):
    x0, y0, x1, y1 = box
    sub = im[y0:y1, x0:x1]
    m = mask(sub)
    m = ndimage.binary_closing(m, iterations=1)
    m = ndimage.binary_opening(m, iterations=1)
    rgba = np.zeros(sub.shape[:2] + (4,), np.uint8)
    rgba[..., :3] = sub
    rgba[..., 3] = np.where(m, 255, 0)
    img = Image.fromarray(sl.despill(rgba))
    bb = img.getbbox()
    return img.crop(bb) if bb else img


def run(review=False):
    os.makedirs(OUT, exist_ok=True)
    im, _, _, ps = sl.parts(os.path.join(SRC, "fire.jpg"), merge=45)
    byid = {p["id"]: p["box"] for p in ps}
    made = []
    for name, ids in FLAME.items():
        for i, pid in enumerate(ids):
            frame(im, byid[pid], fiery).save(os.path.join(OUT, f"{name}_{i}.png"))
        made.append(f"{name} x{len(ids)}")
    for name, ids in KEYED.items():
        for i, pid in enumerate(ids):
            x0, y0, x1, y1 = byid[pid]
            img = Image.fromarray(smoke(im[y0:y1, x0:x1]))
            bb = img.getbbox()
            (img.crop(bb) if bb else img).save(os.path.join(OUT, f"{name}_{i}.png"))
        made.append(f"{name} x{len(ids)}")
    for i, box in enumerate(SPARKS):
        frame(im, box, sl.key_magenta).save(os.path.join(OUT, f"sparks_{i}.png"))
    made.append(f"sparks x{len(SPARKS)}")
    print("; ".join(made))
    if review:
        rows = []
        for name, ids in list(FLAME.items()) + list(KEYED.items()) + [("sparks", SPARKS)]:
            row = [Image.open(os.path.join(OUT, f"{name}_{i}.png")).convert("RGBA") for i in range(len(ids))]
            w = max(f.width for f in row) + 8
            h = max(f.height for f in row)
            img = Image.new("RGBA", (w * len(row), h), (28, 30, 38, 255))
            for i, f in enumerate(row):
                img.alpha_composite(f, (i * w, h - f.height))
            rows.append(img)
        W = max(r.width for r in rows)
        out = Image.new("RGBA", (W, sum(r.height for r in rows)), (28, 30, 38, 255))
        y = 0
        for r in rows:
            out.alpha_composite(r, (0, y)); y += r.height
        out.resize((out.width // 2, out.height // 2), Image.LANCZOS).save(os.path.join(HERE, "out", "review_fire.png"))


if __name__ == "__main__":
    run("review" in sys.argv)
