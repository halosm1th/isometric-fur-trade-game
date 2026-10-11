"""Fit the Gemini "Factory Segment Sprites" sheet onto the building tiles.

The game paints walls and the fort's fence from 128x256 tiles (the old
Kenney set) in assets/tiles/buildings/: each wall piece fills one half of a
tile and runs along one edge of its ground diamond at the 2:1 iso slope.
Gemini drew the whitewashed log walls and the palisade flatter than that, so
a plain resize would leave gaps at every corner. Instead each piece is
stretched column by column so its top and bottom edges land exactly on the
top and bottom edges of the tile it replaces, and corners, joins and the
tile's place on the ground stay what they were.

  python walls.py            write the tiles (the Kenney originals are in git)
  python walls.py review     also write tools/sprites/out/review_walls.png
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SHEET = os.path.join(ROOT, "assets", "external", "NEW HBC ASSETS", "Facotry Segment Sprites.jpg")
TILES = os.path.join(ROOT, "assets", "tiles", "buildings")
sys.path.insert(0, HERE)
import slice as sl

# (top, left, height, width) of each piece on the sheet, from scipy labelling.
PIECES = {
    "white_dl": (34, 25, 116, 107), "white_dr": (34, 163, 116, 108),
    "white_window_dl": (31, 580, 117, 106), "white_window_dr": (31, 700, 117, 106),
    "white_door_dl": (184, 24, 119, 107), "white_door_open_dl": (180, 159, 119, 107),
    "white_door_dr": (181, 447, 119, 107), "white_door_open_dr": (183, 580, 119, 107),
    "palisade_dl": (34, 865, 142, 109), "palisade_dr": (34, 1031, 140, 110),
}


def piece(sheet, name, pad=3):
    t, l, h, w = PIECES[name]
    rgb = sheet[t - pad: t + h + pad, l - pad: l + w + pad]
    fg = sl.key_magenta(rgb)
    # Keep the biggest blob: no stray label letters.
    lab, n = ndimage.label(fg)
    if n > 1:
        sizes = ndimage.sum(fg, lab, range(1, n + 1))
        fg = lab == (1 + int(np.argmax(sizes)))
    # (no hole filling: the window panes are magenta, i.e. see-through)
    rgba = np.dstack([rgb, (fg * 255).astype(np.uint8)])
    rgba = sl.despill(rgba)
    # Window panes are drawn magenta: glaze them dark rather than leave holes.
    a = rgba[..., 3] > 0
    panes = ndimage.binary_fill_holes(a) & ~a
    rgba[panes] = (46, 56, 66, 255)
    ys, xs = np.nonzero(rgba[..., 3])
    return rgba[ys.min(): ys.max() + 1, xs.min(): xs.max() + 1]


def edges(rgba, inner=0.2):
    """The top and bottom edges as lines y = a + b*x, fitted over the middle
    of the piece so the posts and their caps at either end don't bend them."""
    a = rgba[..., 3] > 0
    w = a.shape[1]
    xs, tops, bots = [], [], []
    for x in range(int(w * inner), int(w * (1 - inner))):
        col = np.nonzero(a[:, x])[0]
        if len(col) < 4: continue
        xs.append(x); tops.append(col.min()); bots.append(col.max())
    xs = np.array(xs, float)
    bt = np.polyfit(xs, tops, 1); bb = np.polyfit(xs, bots, 1)
    return (bt[1], bt[0]), (bb[1], bb[0])   # (intercept, slope)


def fit(src, dst_box, dst_top, dst_bot, size=(128, 256)):
    """Resample src so that, column by column, its top/bottom edge lines land
    on dst_top/dst_bot (lines in tile coordinates) across dst_box's x span."""
    (st0, st1), (sb0, sb1) = edges(src)
    x0, x1 = dst_box
    out = np.zeros((size[1], size[0], 4), np.float32)
    sw = src.shape[1]
    srcf = src.astype(np.float32)
    srcf[..., :3] *= srcf[..., 3:4] / 255.0   # premultiply
    Y = np.arange(size[1], dtype=np.float32)
    for X in range(x0, x1 + 1):
        x = (X - x0) / max(1, (x1 - x0)) * (sw - 1)
        ts, bs = st0 + st1 * x, sb0 + sb1 * x
        td, bd = dst_top[0] + dst_top[1] * X, dst_bot[0] + dst_bot[1] * X
        y = ts + (Y - td) / max(1e-3, (bd - td)) * (bs - ts)
        coords = np.vstack([y, np.full_like(y, x)])
        for c in range(4):
            out[:, X, c] = ndimage.map_coordinates(srcf[..., c], coords, order=1, mode="constant", cval=0)
    a = out[..., 3:4]
    out[..., :3] = np.where(a > 0, out[..., :3] / np.maximum(a, 1e-3) * 255.0, 0)
    out = np.clip(out, 0, 255).astype(np.uint8)
    out[out[..., 3] < 40] = 0
    return out


def geometry(path):
    """Where a Kenney tile's piece sits: its x span and its edge lines."""
    k = np.array(Image.open(path).convert("RGBA"))
    ys, xs = np.nonzero(k[..., 3])
    x0, x1 = xs.min(), xs.max()
    (t0, t1), (b0, b1) = edges(k[:, x0: x1 + 1])
    # back to tile coordinates
    return (x0, x1), (t0 - t1 * x0, t1), (b0 - b1 * x0, b1)


def slope_kind(path):
    _, _, (b0, b1) = geometry(path)
    return "dr" if b1 > 0 else "dl"   # bottom edge falling to the right = "\"


def build(review=False):
    sheet = np.array(Image.open(SHEET).convert("RGB"))
    kenney = {f[:-4]: os.path.join(TILES, f) for f in os.listdir(TILES) if f.endswith(".png")}
    made = {}

    def make(tile, src_name, tall_from=None, geo_from=None):
        # Windows and doors stand where the plain wall of that side does; an
        # open door's swing would throw the fit off, so use the plain wall.
        box, top, bot = geometry(kenney[geo_from or tile])
        if tall_from:   # a palisade stands as tall as a house wall, on the fence's line
            _, wtop, wbot = geometry(kenney[tall_from])
            h = (wbot[0] - wtop[0])
            top = (bot[0] - h, bot[1])
        img = fit(piece(sheet, src_name), box, top, bot)
        made[tile] = img
        return img

    # The geometry is read from the Kenney originals, so build everything
    # first and write at the end (corners are composed from the new walls).
    for side in "NESW":
        kind = slope_kind(kenney[f"wall_{side}"])
        make(f"wall_{side}", f"white_{kind}")
        make(f"wall_window_{side}", f"white_window_{kind}", geo_from=f"wall_{side}")
        make(f"wall_door_{side}", f"white_door_{kind}", geo_from=f"wall_{side}")
        make(f"wall_door_open_{side}", f"white_door_open_{kind}", geo_from=f"wall_{side}")
        make(f"fence_{side}", f"palisade_{slope_kind(kenney[f'fence_{side}'])}", tall_from=f"wall_{side}")

    def compose(a, b):
        out = Image.fromarray(made[a]); out.alpha_composite(Image.fromarray(made[b])); return np.array(out)
    # The two-wall corner pieces: the back pair and the front pair.
    made["wall_corner_back"] = compose("wall_E", "wall_S")
    made["wall_corner_front"] = compose("wall_N", "wall_W")
    made["fence_corner_back"] = compose("fence_E", "fence_S")
    made["fence_corner_front"] = compose("fence_N", "fence_W")

    for tile, img in made.items():
        Image.fromarray(img).save(kenney[tile])
    print(f"wrote {len(made)} tiles: {', '.join(sorted(made))}")

    if review:
        os.makedirs(sl.OUT, exist_ok=True)
        names = sorted(made)
        cw, ch, W = 140, 280, 8
        rows = (len(names) + W - 1) // W
        sheet_im = Image.new("RGBA", (W * cw, rows * ch), (90, 140, 90, 255))
        d = ImageDraw.Draw(sheet_im)
        for i, n in enumerate(names):
            x, y = (i % W) * cw, (i // W) * ch
            # the ground diamond the piece stands on
            d.polygon([(x + 6, y + 224), (x + 70, y + 192), (x + 134, y + 224), (x + 70, y + 256)], outline=(40, 60, 40))
            sheet_im.alpha_composite(Image.fromarray(made[n]), (x + 6, y))
            d.text((x + 4, y + 262), n, fill=(0, 0, 0))
        p = os.path.join(sl.OUT, "review_walls.png"); sheet_im.save(p); print(p)


if __name__ == "__main__":
    build(review="review" in sys.argv)
