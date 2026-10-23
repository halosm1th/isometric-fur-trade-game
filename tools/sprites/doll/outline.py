"""The painted sprites' ink line, drawn round a rendered frame afterwards:
alpha made hard, and a one-pixel dark ring outside the silhouette.

  python outline.py IN_DIR OUT_DIR
"""
import os, sys
import numpy as np
from PIL import Image

INK = (52, 4, 18)      # the painted voyageur's outline colour


def ink(a):
    al = a[..., 3] > 127
    a = a.copy()
    a[..., 3] = np.where(al, 255, 0)
    # Neighbours without wrapping round the frame's edges (np.roll would put
    # the soles' ink at the top).
    p = np.pad(al, 1)
    ring = p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    ring &= ~al
    a[ring] = (*INK, 255)
    return a


if __name__ == "__main__":
    src, dst = sys.argv[1], sys.argv[2]
    os.makedirs(dst, exist_ok=True)
    for f in sorted(os.listdir(src)):
        if f.endswith(".png"):
            Image.fromarray(ink(np.array(Image.open(os.path.join(src, f)).convert("RGBA")))).save(os.path.join(dst, f))
