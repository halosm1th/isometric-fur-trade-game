"""Cut a Gemini parts sheet (filled-in template.png) into pieces.

  python cut.py bare.jpg bare      -> parts/bare/<facing>_<part>.png + pieces.json

Gemini paints each part around its box but not always inside it (limbs run
long), so a piece is every non-magenta blob that touches its box, the
template's box lines and pivot crosses taken out first. Each piece keeps
its pivot (the cross), in its own pixels.
"""
import json, os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))


def main(sheet, name):
    spec = json.load(open(os.path.join(HERE, "template.json")))
    tw, th = Image.open(os.path.join(HERE, "template.png")).size
    img = Image.open(os.path.join(HERE, sheet)).convert("RGB")
    k = img.width / tw, img.height / th
    a = np.asarray(img).astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    # Magenta, allowing for JPEG: red and blue high, green low.
    bg = (r > 170) & (b > 170) & (g < 110)
    solid = ~bg
    # The template's own ink: the box outlines and crosses, dark and thin.
    dark = (r + g + b) < 200
    ink = np.zeros_like(solid)
    for p in spec["parts"].values():
        x0, y0, x1, y1 = [int(round(v * k[i % 2])) for i, v in enumerate(p["box"])]
        w = 4
        for xs, ys in ((slice(x0 - w, x1 + w), slice(y0 - w, y0 + w)), (slice(x0 - w, x1 + w), slice(y1 - w, y1 + w)),
                       (slice(x0 - w, x0 + w), slice(y0 - w, y1 + w)), (slice(x1 - w, x1 + w), slice(y0 - w, y1 + w))):
            ink[ys, xs] = True
        cx, cy = int(p["pivot"][0] * k[0]), int(p["pivot"][1] * k[1])
        ink[cy - 3:cy + 4, cx - 11:cx + 12] = True
        ink[cy - 11:cy + 12, cx - 3:cx + 4] = True
    # Box lines and crosses go: dark or purple (JPEG smears them toward the
    # magenta). Where a cross lay over the part it is filled from its
    # neighbours below.
    inky = ink & (dark | (b > g + 30))
    solid &= ~inky
    labels, n = ndimage.label(solid)
    out = os.path.join(HERE, "parts", name)
    os.makedirs(out, exist_ok=True)
    pieces = {}
    rgba = np.dstack([a, np.full(a.shape[:2], 255)]).astype(np.uint8)
    for key, p in spec["parts"].items():
        x0, y0, x1, y1 = [int(round(v * k[i % 2])) for i, v in enumerate(p["box"])]
        inside = labels[y0 + 6:y1 - 6, x0 + 6:x1 - 6]
        ids = [i for i in np.unique(inside) if i]
        # Keep blobs of a fair size only (JPEG specks, stray label pixels).
        ids = [i for i in ids if (labels == i).sum() > 150]
        # The part is its biggest blob: a stray bit of the next part (the
        # pipe in the torso box) is not.
        ids = [max(ids, key=lambda i: (labels == i).sum())] if ids else ids
        if not ids:
            print("empty", key)
            continue
        m = np.isin(labels, ids)
        # Where a box line ran across the part it left a slit: close it.
        m |= ink & ~bg & ndimage.binary_closing(m, iterations=5)
        m = ndimage.binary_fill_holes(m) & ~bg
        # A pixel's fringe of magenta from the JPEG comes off.
        m = ndimage.binary_erosion(m, iterations=1)
        ys, xs = np.nonzero(m)
        bx0, by0, bx1, by1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
        piece = rgba[by0:by1, bx0:bx1].copy()
        piece[..., 3] = np.where(m[by0:by1, bx0:bx1], 255, 0)
        # Fill the cross where it lay over the part, from the nearest paint.
        cross = (ink & m)[by0:by1, bx0:bx1]
        if cross.any():
            idx = ndimage.distance_transform_edt(cross, return_distances=False, return_indices=True)
            for c in range(3):
                piece[..., c] = piece[..., c][tuple(idx)]
        Image.fromarray(piece).save(os.path.join(out, key + ".png"))
        px, py = p["pivot"][0] * k[0] - bx0, p["pivot"][1] * k[1] - by0
        pieces[key] = {"pivot": [round(px, 1), round(py, 1)], "size": [int(bx1 - bx0), int(by1 - by0)]}
    with open(os.path.join(out, "pieces.json"), "w") as f:
        json.dump(pieces, f, indent=1)
    print(len(pieces), "pieces ->", out)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
