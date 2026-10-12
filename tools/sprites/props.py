"""Cut the Gemini prop sheets into 128x256 tiles like the existing props
(object centred, base on y=BASE), so a file can replace an old one in
place without touching terrain_tileset.tres.

  python props.py [review]
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(ROOT, "assets", "external", "NEW HBC ASSETS")
sys.path.insert(0, HERE)
import slice as sl

W, H, BASE = 128, 256, 236
PROPS = os.path.join(ROOT, "assets", "tiles", "props")
FURN = os.path.join(ROOT, "assets", "tiles", "furniture")

# (sheet, parts, out folder, file, target height, target width, flip)
CUTS = [
    # Replacing art the world already uses.
    ("Props 3", [1], PROPS, "spruce_0", 200, None, False),
    ("Props 3", [1], PROPS, "spruce_1", 186, None, True),
    ("Props 3", [1], PROPS, "spruce_2", 172, None, False),
    ("Props 1", [10], PROPS, "campfire", 92, None, False),
    ("Prop 2", [53], PROPS, "crate", None, 64, False),
    ("Prop 2", [3], PROPS, "hay_bales", 56, None, False),
    ("Prop 2", [52], FURN, "bed", 104, None, False),
    ("Prop 2", [24], FURN, "display_case", None, 110, False),
    # New, for when the world places them.
    ("Props 3", [2], PROPS, "tamarack", 190, None, False),
    ("Props 3", [3], PROPS, "dead_tree", 172, None, False),
    ("Props 3", [32], PROPS, "willow", 70, None, False),
    ("Props 3", [33], PROPS, "willow_winter", None, 112, False),
    ("Props 3", [34], PROPS, "muskeg_pool", None, 126, False),
    ("Props 3", [59], PROPS, "boulder", 70, None, False),
    ("Props 3", [60, 61, 63, 64], PROPS, "rocks", None, 90, False),
    ("Props 3", [62], PROPS, "reeds", 84, None, False),
    ("Props 3", [81], PROPS, "snowdrift", None, 126, False),
    ("Props 1", [0], PROPS, "beaver_lodge", None, 126, False),
    ("Props 1", [1], PROPS, "beaver_dam", None, 126, False),
    ("Props 1", [2], PROPS, "drying_rack", 100, None, False),
    ("Props 1", [3], PROPS, "pelt_frame", 92, None, False),
    ("Props 1", [4], PROPS, "hide_frame", 100, None, False),
    ("Props 1", [5], PROPS, "cache", 140, None, False),
    ("Props 1", [6], PROPS, "leg_trap", None, 44, False),
    ("Props 1", [7], PROPS, "fish_trap", None, 110, False),
    ("Props 1", [8], PROPS, "ice_hole", None, 120, False),
    ("Props 1", [9], PROPS, "canoe_beached", None, 126, False),
    ("Prop 2", [0, 1], PROPS, "flagstaff", 236, None, False),
    ("Prop 2", [2], PROPS, "cannon", None, 90, False),
    ("Prop 2", [23], PROPS, "fur_press", 120, None, False),
    ("Prop 2", [25], FURN, "writing_desk", 80, None, False),
    ("Prop 2", [51], FURN, "stove", 110, None, "box"),
    ("Prop 2", [67, 69], PROPS, "grave", None, 100, False),
    ("Prop 2", [66], PROPS, "woodpile_winter", None, 112, False),
    ("Prop 2", [68], PROPS, "sled", None, 118, False),
    # The berry season has something to see and to pick at last: the three
    # bushes that answer the scurvy, in fruit, picked and bare (design doc 13.5).
    ("bushes", [0], PROPS, "saskatoon_bush", 86, None, False),
    ("bushes", [1], PROPS, "saskatoon_picked", 86, None, False),
    ("bushes", [2], PROPS, "cranberry_patch", None, 112, False),
    ("bushes", [3], PROPS, "cranberry_picked", None, 112, False),
    ("bushes", [4], PROPS, "rosehip_bush", 86, None, False),
    ("bushes", [5], PROPS, "rosehip_bare", 80, None, False),
    ("bushes", [6], PROPS, "bushes_snow", None, 96, False),
    ("bushes", [7], PROPS, "berry_basket", 50, None, False),
    # The Missouri towns are farming towns, not hunting camps: earth lodges,
    # garden plots, corn scaffolds and bullboats (design doc 13.1).
    ("Mandan Huts", [0], PROPS, "earth_lodge", None, 126, False),
    ("Mandan Huts", [1], PROPS, "earth_lodge_winter", None, 126, False),
    ("Mandan Huts", [2], PROPS, "garden_plot", None, 126, False),
    ("Mandan Huts", [4], PROPS, "garden_harvested", None, 126, False),
    ("Mandan Huts", [3], PROPS, "corn_scaffold", 124, None, False),
    ("Mandan Huts", [5], PROPS, "cache_pit", None, 112, False),
    ("Mandan Huts", [6], PROPS, "village_palisade", None, 126, False),
    ("Mandan Huts", [7], PROPS, "bullboat", None, 92, False),
    ("Mandan Huts", [8], PROPS, "corn_mortar", 74, None, False),
]


def build(review=False):
    cache = {}
    done = []
    for sheet, ids, folder, name, th, tw, flip in CUTS:
        if sheet not in cache:
            cache[sheet] = sl.parts(os.path.join(SRC, sheet + ".jpg"), merge=25)
        im, fg, lab, ps = cache[sheet]
        byid = {p["id"]: p for p in ps}
        boxes = [byid[i]["box"] for i in ids]
        X0 = min(b[0] for b in boxes); Y0 = min(b[1] for b in boxes)
        X1 = max(b[2] for b in boxes); Y1 = max(b[3] for b in boxes)
        rgba = np.zeros((Y1 - Y0, X1 - X0, 4), np.uint8)
        for i, b in zip(ids, boxes):
            a = sl.cut(im, fg, lab, byid[i])
            if flip == "box":   # everything in the box: dark parts the text test dropped
                a[..., 3] = np.where(fg[b[1]:b[3], b[0]:b[2]], 255, 0)
                a[..., :3] = im[b[1]:b[3], b[0]:b[2]]
            sub = rgba[b[1] - Y0:b[3] - Y0, b[0] - X0:b[2] - X0]
            m = a[..., 3] > 0
            sub[m] = a[m]
        img = Image.fromarray(rgba)
        img = img.crop(img.getbbox())
        w, h = img.size
        f = min([v for v in [th and th / h, tw and tw / w, (W - 2) / w, (BASE - 2) / h] if v])
        img = img.resize((max(1, round(w * f)), max(1, round(h * f))), Image.LANCZOS)
        a = np.array(img)
        a[..., 3] = np.where(a[..., 3] > 110, 255, 0)
        img = Image.fromarray(a)
        if flip is True:
            img = img.transpose(Image.FLIP_LEFT_RIGHT)
        canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        canvas.alpha_composite(img, ((W - img.width) // 2, BASE - img.height))
        canvas.save(os.path.join(folder, name + ".png"))
        done.append((name, canvas))
    if review:
        cols = 12
        out = Image.new("RGBA", (cols * W, ((len(done) + cols - 1) // cols) * (H + 14)), (90, 110, 90, 255))
        d = ImageDraw.Draw(out)
        for k, (n, c) in enumerate(done):
            x, y = (k % cols) * W, (k // cols) * (H + 14)
            out.alpha_composite(c, (x, y))
            d.line([(x, y + BASE), (x + W, y + BASE)], fill=(60, 60, 60))
            d.text((x + 2, y + H), n, fill=(255, 255, 255))
        out.save(os.path.join(HERE, "out", "review_props.png"))
    print(f"{len(done)} props")


if __name__ == "__main__" and "vehicles" not in sys.argv:
    build("review" in sys.argv)


# Vehicles: same canvas and footprint as the images they replace, so the
# hand-set offsets in Ship.cs / Canoe.cs still hold.
VEHICLES = [
    # (parts, out path, canvas, fit box (x0, y0, x1, bottom))
    ([53], "assets/sprites/ship.png", (460, 340), (40, 5, 441, 301)),
    ([33], "assets/sprites/canoe.png", (110, 44), (4, 4, 107, 43)),
    ([37], "assets/sprites/vehicles/canoe_loaded.png", (330, 110), (0, 0, 330, 108)),
    ([41], "assets/sprites/vehicles/york_boat.png", (400, 130), (0, 0, 400, 128)),
    ([43, 44, 45, 46, 47], "assets/sprites/vehicles/dog_sled.png", (360, 90), (0, 0, 360, 88)),
]


def vehicles():
    im, fg, lab, ps = sl.parts(os.path.join(SRC, "York Factory Vehicles.jpg"), merge=25)
    byid = {p["id"]: p for p in ps}
    for ids, out, (cw, ch), (x0, y0, x1, bottom) in VEHICLES:
        boxes = [byid[i]["box"] for i in ids]
        X0 = min(b[0] for b in boxes); Y0 = min(b[1] for b in boxes)
        X1 = max(b[2] for b in boxes); Y1 = max(b[3] for b in boxes)
        rgba = np.zeros((Y1 - Y0, X1 - X0, 4), np.uint8)
        for i, b in zip(ids, boxes):
            a = sl.cut(im, fg, lab, byid[i])
            sub = rgba[b[1] - Y0:b[3] - Y0, b[0] - X0:b[2] - X0]
            m = a[..., 3] > 0
            sub[m] = a[m]
        if "canoe" in out:   # the hull's inside is solid, whatever the keying thought
            from scipy import ndimage
            solid = ndimage.binary_fill_holes(rgba[..., 3] > 0)
            hole = solid & (rgba[..., 3] == 0)
            rgba[hole, :3] = (150, 105, 60)
            rgba[hole, 3] = 255
        img = Image.fromarray(rgba)
        img = img.crop(img.getbbox())
        f = min((x1 - x0) / img.width, (bottom - y0) / img.height)
        img = img.resize((max(1, round(img.width * f)), max(1, round(img.height * f))), Image.LANCZOS)
        a = np.array(img)
        a[..., 3] = np.where(a[..., 3] > 110, 255, 0)
        img = Image.fromarray(a)
        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        canvas.alpha_composite(img, ((x0 + x1 - img.width) // 2, bottom - img.height))
        path = os.path.join(ROOT, out)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        canvas.save(path)
    print(f"{len(VEHICLES)} vehicles")


if __name__ == "__main__" and "vehicles" in sys.argv:
    vehicles()
