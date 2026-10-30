"""Stone walls for Prince of Wales Fort, made from the log-wall tiles.

The whitewashed log walls in assets/tiles/buildings/ already have the right
shape on the ground diamond (walls.py fitted them), so the stone keeps each
tile's silhouette and shading and lays coursed masonry over it: grey-brown
quartzite blocks, mortar courses running parallel to the wall's foot, joints
staggered course by course, a little lichen. Each wall also gets a broken
twin whose top is knocked down unevenly (the French blew what they could in
1782), and the ruin's rubble uses the existing rocks prop.

  python stone_walls.py            write the tiles
  python stone_walls.py review     also write tools/sprites/out/review_stone.png
"""
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
TILES = os.path.join(ROOT, "assets", "tiles", "buildings")

STONE = np.array([132, 124, 112], float)     # weathered quartzite, warm grey
MORTAR = np.array([86, 80, 72], float)
LICHEN = np.array([150, 140, 70], float)
COURSE = 11                                   # px between mortar courses
BLOCK = 18                                    # px between joints along a course


def noise(shape, seed, scale):
    """Smooth value noise in [0,1)."""
    rng = np.random.default_rng(seed)
    h, w = shape
    g = rng.random((h // scale + 2, w // scale + 2))
    ys, xs = np.mgrid[0:h, 0:w] / scale
    y0, x0 = ys.astype(int), xs.astype(int)
    fy, fx = ys - y0, xs - x0
    a = g[y0, x0] * (1 - fx) + g[y0, x0 + 1] * fx
    b = g[y0 + 1, x0] * (1 - fx) + g[y0 + 1, x0 + 1] * fx
    return a * (1 - fy) + b * fy


def stone(src, seed, broken=False):
    rgba = np.array(Image.open(src).convert("RGBA")).astype(float)
    a = rgba[..., 3] > 0
    h, w = a.shape
    lum = rgba[..., :3].mean(axis=2) / 255.0
    ys = np.arange(h)[:, None].repeat(w, axis=1)
    # Each column's foot and head: courses run parallel to the foot.
    foot = np.full(w, -1); head = np.full(w, -1)
    for x in range(w):
        col = np.nonzero(a[:, x])[0]
        if len(col): head[x], foot[x] = col.min(), col.max()
    up = foot[None, :] - ys                       # height above the foot
    course = np.floor(up / COURSE).astype(int)
    xs = np.arange(w)[None, :].repeat(h, axis=0)
    # The joint pattern runs along the wall: stagger alternate courses.
    joint = ((xs + (course % 2) * (BLOCK // 2)) % BLOCK) == 0
    bed = (up % COURSE) < 1.2
    # Each block a slightly different stone.
    block_id = course * 97 + (xs + (course % 2) * (BLOCK // 2)) // BLOCK
    rng = np.random.default_rng(seed)
    tint = rng.uniform(0.85, 1.12, size=block_id.max() + 2)[np.clip(block_id, 0, None)]
    shade = 0.55 + 0.6 * lum                       # keep the tile's light and dark
    rough = 0.9 + 0.2 * noise((h, w), seed + 1, 3)
    out = STONE[None, None, :] * (shade * tint * rough)[..., None]
    lichen = noise((h, w), seed + 2, 9) > 0.72
    out[lichen] = out[lichen] * 0.6 + LICHEN * 0.4 * shade[lichen][..., None]
    out[bed | joint] = MORTAR * shade[bed | joint][..., None]
    # The very top course is a coping, a shade lighter.
    top = (ys - head[None, :]) < 4
    out[top] = np.minimum(out[top] * 1.15, 255)
    alpha = rgba[..., 3].copy()
    if broken:
        # Knock the top down to between a third and two thirds of the height,
        # in ragged steps, and leave the break lighter (fresh stone).
        n = noise((1, w), seed + 3, 14)[0]
        keep = np.where(foot >= 0, foot - (foot - head) * (0.3 + 0.4 * n), 0)
        keep = keep + (np.floor(np.arange(w) / 6) % 3) * 3       # stepped, not smooth
        cut = ys < keep[None, :]
        alpha[cut] = 0
        edge = (ys >= keep[None, :]) & (ys < keep[None, :] + 3) & a
        out[edge] = np.minimum(out[edge] * 1.25, 255)
    rgba[..., :3] = np.clip(out, 0, 255)
    rgba[..., 3] = np.where(a, alpha, 0)
    return Image.fromarray(rgba.astype(np.uint8))


JOBS = [
    ("wall_N", "stone_N"), ("wall_E", "stone_E"), ("wall_S", "stone_S"), ("wall_W", "stone_W"),
    ("wall_corner_back", "stone_corner_back"), ("wall_corner_front", "stone_corner_front"),
]


def main():
    made = []
    for i, (src, dst) in enumerate(JOBS):
        im = stone(os.path.join(TILES, src + ".png"), 11 + i)
        im.save(os.path.join(TILES, dst + ".png")); made.append(im)
        if "corner" not in dst:
            br = stone(os.path.join(TILES, src + ".png"), 11 + i, broken=True)
            br.save(os.path.join(TILES, dst.replace("stone_", "stone_broken_") + ".png")); made.append(br)
    if len(sys.argv) > 1 and sys.argv[1] == "review":
        sheet = Image.new("RGBA", (128 * len(made), 256), (96, 112, 70, 255))
        for i, im in enumerate(made): sheet.alpha_composite(im, (128 * i, 0))
        os.makedirs(os.path.join(HERE, "out"), exist_ok=True)
        sheet.save(os.path.join(HERE, "out", "review_stone.png"))
    print("wrote", len(made), "tiles")


if __name__ == "__main__":
    main()
