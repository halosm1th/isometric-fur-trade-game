"""Cut the combat and fire effect sheets into frames in assets/sprites/fx/.

  python effects.py [review]

The combat sheet draws its muzzle flash with the musket attached, and the
detector drops the faintest frames (a thin spatter of blood is mostly
background), so the frames here are given as plain rectangles on the sheet
rather than by part number. `tools/sprites/out/<sheet>.contact.png` is the
map to read them off.
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

# name: (sheet, [(x0, y0, x1, y1), ...])
CUTS = {
    # The flash and the smoke only: the musket in the drawing is the gun the
    # man is already holding.
    "muzzle_flash": ("Combat effects in theory", [(236, 42, 371, 153), (646, 32, 755, 173), (946, 22, 1148, 174)]),
    "pan_flash": ("Combat effects in theory", [(78, 203, 162, 277), (292, 198, 392, 282)]),
    "blood_hit": ("Combat effects in theory", [(73, 342, 181, 429), (281, 324, 457, 429), (508, 313, 676, 427), (737, 313, 908, 427)]),
    "blood_pool": ("Combat effects in theory", [(88, 498, 178, 562), (295, 494, 441, 571), (511, 407, 710, 584)]),
    "impact_dust": ("Combat effects in theory", [(92, 666, 161, 729), (293, 632, 440, 736), (525, 639, 696, 734)]),
    "arrow": ("Combat effects in theory", [(20, 804, 263, 837)]),
    # Off the generic effects sheet, the three rows that belong in 1811: a
    # blade's arc, a powder blast, and lightning for a summer storm. The
    # fireballs, healing light, ice spikes, poison and dark orbs on the rest
    # of that sheet are another game's.
    "slash": ("Visual FX Sheet", [(222, 500, 352, 570), (368, 500, 500, 570), (512, 500, 630, 570), (660, 500, 776, 570)]),
    "blast": ("Visual FX Sheet", [(228, 672, 352, 764), (366, 672, 492, 764), (508, 672, 630, 764),
                                  (650, 672, 772, 764), (786, 672, 910, 764), (928, 672, 1052, 764)]),
    "lightning": ("Visual FX Sheet", [(248, 222, 318, 312), (390, 222, 500, 312), (530, 222, 660, 312), (676, 222, 806, 312)]),
}


def cut(im, box):
    x0, y0, x1, y1 = box
    sub = im[y0:y1, x0:x1]
    fg = sl.key_magenta(sub)
    fg = ndimage.binary_closing(fg, iterations=1)
    rgba = np.zeros(sub.shape[:2] + (4,), np.uint8)
    rgba[..., :3] = sub
    rgba[..., 3] = np.where(fg, 255, 0)
    rgba = sl.despill(rgba)
    img = Image.fromarray(rgba)
    bb = img.getbbox()
    return img.crop(bb) if bb else img


def run(review=False):
    os.makedirs(OUT, exist_ok=True)
    sheets, made = {}, []
    for name, (sheet, boxes) in CUTS.items():
        if sheet not in sheets:
            sheets[sheet] = np.array(Image.open(os.path.join(SRC, sheet + ".jpg")).convert("RGB"))
        im = sheets[sheet]
        for i, box in enumerate(boxes):
            img = cut(im, box)
            img.save(os.path.join(OUT, f"{name}_{i}.png"))
        made.append(f"{name} x{len(boxes)}")
    print("; ".join(made))
    if review:
        rows = []
        for name, (_, boxes) in CUTS.items():
            row = [Image.open(os.path.join(OUT, f"{name}_{i}.png")).convert("RGBA") for i in range(len(boxes))]
            w = max(f.width for f in row) + 8
            h = max(f.height for f in row)
            img = Image.new("RGBA", (w * len(row), h), (40, 45, 55, 255))
            for i, f in enumerate(row):
                img.alpha_composite(f, (i * w, h - f.height))
            rows.append(img)
        W = max(r.width for r in rows)
        out = Image.new("RGBA", (W, sum(r.height for r in rows)), (40, 45, 55, 255))
        y = 0
        for r in rows:
            out.alpha_composite(r, (0, y)); y += r.height
        out.save(os.path.join(HERE, "out", "review_fx.png"))


if __name__ == "__main__":
    run("review" in sys.argv)
