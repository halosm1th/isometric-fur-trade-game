"""The painted sprites' ink line, drawn on a rendered frame afterwards: where
one part of him stands before another (an arm across his chest), and round
the outside, alpha made hard.

  python outline.py IN_DIR OUT_DIR

A frame rendered several times over (render.py with DOLL_SS) is inked at
that size and brought down to the game's; a frame with a <name>_depth0001.png
beside it (render.py, for a projected man) gets the inside lines.
"""
import os, sys
import numpy as np
from PIL import Image

INK = (52, 4, 18)      # the painted voyageur's outline colour
W, H = 180, 170
STEP = 0.08            # metres the surface must step back to earn a line
DEPTH_RANGE = 2.0      # render.py maps 19..21 m from the camera onto 0..65535


def inside_lines(a, depth, k):
    """Ink, at the big size, on the far side of every step back in depth."""
    al = a[..., 3] > 127
    d = np.where(al, depth.astype(float) / 65535 * DEPTH_RANGE, 1e9)
    near = np.full(d.shape, 1e9)
    r = k                      # a pixel wide once shrunk
    p = np.pad(d, r, constant_values=1e9)
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r:
                near = np.minimum(near, p[r + dy:r + dy + d.shape[0], r + dx:r + dx + d.shape[1]])
    line = al & (d - near > STEP)
    a = a.copy()
    a[line, :3] = INK
    return a


def shrink(a):
    """Down to the game's size as the painted sheets were: averaged, not picked."""
    if a.shape[:2] == (H, W):
        return a
    a = a.astype(float)
    k = a.shape[1] // W
    # Average colour by coverage, so the edge doesn't darken toward empty.
    al = a[..., 3:] / 255
    box = lambda x: x.reshape(H, k, W, k, x.shape[-1]).mean((1, 3))
    cov = box(al)
    rgb = box(a[..., :3] * al) / np.maximum(cov, 1e-6)
    return np.dstack([rgb, cov * 255]).clip(0, 255).astype(np.uint8)


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


def frame(path):
    a = np.array(Image.open(path).convert("RGBA"))
    dp = os.path.splitext(path)[0] + "_depth0001.png"
    if os.path.exists(dp):
        a = inside_lines(a, np.array(Image.open(dp)), a.shape[1] // W)
    return ink(shrink(a))


if __name__ == "__main__":
    src, dst = sys.argv[1], sys.argv[2]
    os.makedirs(dst, exist_ok=True)
    for f in sorted(os.listdir(src)):
        if f.endswith(".png") and "_depth" not in f:
            Image.fromarray(frame(os.path.join(src, f))).save(os.path.join(dst, f))
