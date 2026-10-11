"""Cut the Gemini animal sheets into assets/sprites/animals/<species>/ and
write Scenes/Animals/<Species>Frames.tres.

Each sheet has the animal heading down-right (se) and up-right (ne); the
left-hand directions (sw, nw) are those frames mirrored. Animal.cs picks
`<anim>_<dir>`. Sizes are set against a man about 98 px tall on screen, so
a caribou bull's antlers stand a little above his hat and a hare comes to
his shin.

  python animals.py [review]
"""
import json, os, sys, tempfile
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(ROOT, "assets", "external", "NEW HBC ASSETS")
sys.path.insert(0, HERE)
import slice as sl

R = lambda a, b: list(range(a, b + 1))

# species: (sheet, ref part, height px of ref, canvas, {anim: {"se": ids, "ne": ids}})
SPECIES = {
    "caribou": ("Barren_Ground_Caribou", 1, 100, [200, 150], {
        "idle": {"se": R(1, 4), "ne": R(5, 8)},
        "walk": {"se": R(9, 13), "ne": R(14, 18)},
        "run": {"se": R(19, 23), "ne": R(24, 28)},
        "death": {"se": R(29, 32), "ne": R(33, 36)},
        "cow_idle": {"se": [39], "ne": [41]},
        "cow_walk": {"se": [40], "ne": [42]},
    }),
    "wolf": ("Grey Wolf", 0, 58, [150, 100], {
        "idle": {"se": R(0, 3), "ne": R(4, 7)},
        "walk": {"se": R(8, 11), "ne": R(12, 15)},
        "run": {"se": R(16, 19), "ne": R(20, 23)},
        "attack": {"se": R(24, 27), "ne": R(28, 31)},
        "howl": {"se": R(32, 35), "ne": R(36, 39)},
        "death": {"se": R(40, 43), "ne": R(44, 47)},
    }),
    "goose": ("Goose", 0, 44, [150, 110], {
        "idle": {"se": R(0, 3), "ne": R(4, 7)},
        "walk": {"se": R(8, 11), "ne": R(12, 15)},
        "takeoff": {"se": R(16, 19), "ne": R(20, 23)},
        "fly": {"se": R(24, 27), "ne": R(28, 31)},
        "death": {"se": R(32, 36), "ne": R(32, 36)},
    }),
    "beaver": ("Beaver", 30, 34, [120, 80], {
        "idle": {"se": R(30, 35), "ne": R(36, 41)},
        "walk": {"se": R(67, 72), "ne": R(73, 78)},
        "gnaw": {"se": R(103, 106), "ne": R(107, 110)},
        "swim": {"se": R(133, 136), "ne": R(137, 140)},
        "slap": {"se": R(168, 171), "ne": R(172, 175)},
        "death": {"se": R(207, 210), "ne": R(211, 214)},
    }),
    "hare": ("snowhare", 59, 30, [90, 70], {
        "idle": {"se": R(59, 62), "ne": R(63, 66)},
        "walk": {"se": [89, 90, 91, 92], "ne": [93, 94, 93, 94]},
        "run": {"se": R(117, 120), "ne": [121, 122]},
        "death": {"se": [123, 124], "ne": [123, 124]},
        "summer_idle": {"se": R(196, 199), "ne": R(200, 203)},
        "summer_walk": {"se": R(244, 247), "ne": R(248, 251)},
        "summer_run": {"se": R(292, 295), "ne": [296, 297]},
        "summer_death": {"se": [298, 299], "ne": [298, 299]},
    }),
    "ptarmigan": ("willow ptarmigan", 0, 24, [70, 60], {
        "idle": {"se": R(0, 3), "ne": R(4, 7)},
        "walk": {"se": R(8, 11), "ne": R(12, 15)},
        "fly": {"se": R(16, 19), "ne": R(20, 23)},
        "death": {"se": [24], "ne": [25]},
        "summer_idle": {"se": R(26, 29), "ne": R(30, 33)},
        "summer_walk": {"se": R(34, 37), "ne": R(38, 41)},
        "summer_fly": {"se": R(42, 45), "ne": R(46, 49)},
        "summer_death": {"se": [50], "ne": [51]},
    }),
}

SPEED = {"idle": 4, "walk": 8, "run": 12, "attack": 10, "death": 6, "fly": 10, "takeoff": 8, "howl": 4, "gnaw": 6, "swim": 6, "slap": 8}
ONESHOT = {"attack", "death", "takeoff", "slap", "summer_death"}
MIRROR = {"se": "sw", "ne": "nw"}


def build(review=False):
    for who, (sheet, ref, height, canvas, anims) in SPECIES.items():
        folder = os.path.join(ROOT, "assets", "sprites", "animals", who)
        os.makedirs(folder, exist_ok=True)
        cut, flip, order = {}, [], []
        for anim, dirs in anims.items():
            for d, ids in dirs.items():
                cut[f"{anim}_{d}"] = ids
                cut[f"{anim}_{MIRROR[d]}"] = ids
                flip.append(f"{anim}_{MIRROR[d]}")
                order += [f"{anim}_{d}", f"{anim}_{MIRROR[d]}"]
        spec = {"sheet": os.path.join(SRC, sheet + ".jpg"), "out": folder, "canvas": canvas, "height": height,
                "ref": ref, "anims": cut, "flip": flip, "number_single": True, "merge": 4}
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as f:
            json.dump(spec, f)
        sl.export(f.name)
        os.unlink(f.name)
        # SpriteFrames
        res, blocks = [], []
        for name in order:
            n = len(cut[name])
            paths = [f"res://assets/sprites/animals/{who}/{name}_{i}.png" for i in range(n)]
            for p in paths:
                if p not in res:
                    res.append(p)
            base = name.rsplit("_", 1)[0]
            kind = base.split("_")[-1] if base.startswith(("summer_", "cow_")) else base
            speed = float(SPEED.get(kind, 6))
            loop = "false" if base in ONESHOT or kind in ONESHOT else "true"
            frames = ",\n".join('{\n"duration": 1.0,\n"texture": ExtResource("t%d")\n}' % (res.index(p) + 1) for p in paths)
            blocks.append('{\n"name": &"%s",\n"speed": %s,\n"loop": %s,\n"frames": [%s]\n}' % (name, speed, loop, frames))
        text = f'[gd_resource type="SpriteFrames" load_steps={len(res) + 1} format=3]\n\n'
        text += "".join(f'[ext_resource type="Texture2D" path="{p}" id="t{i + 1}"]\n' for i, p in enumerate(res))
        text += "\n[resource]\nanimations = [" + ", ".join(blocks) + "]\n"
        name = who.capitalize() + "Frames.tres"
        open(os.path.join(ROOT, "Scenes", "Animals", name), "w", newline="\n").write(text)
        if review:
            montage(who, folder, order, cut, canvas)


def montage(who, folder, order, cut, canvas):
    W, H = canvas
    rows = [n for n in order if n.endswith(("_se", "_ne"))]
    cols = max(len(cut[n]) for n in rows)
    img = Image.new("RGBA", (110 + cols * W, len(rows) * H), (90, 110, 90, 255))
    d = ImageDraw.Draw(img)
    for r, name in enumerate(rows):
        d.text((4, r * H + H // 2), name, fill=(255, 255, 255))
        for c in range(len(cut[name])):
            img.alpha_composite(Image.open(os.path.join(folder, f"{name}_{c}.png")).convert("RGBA"), (110 + c * W, r * H))
    img.save(os.path.join(HERE, "out", f"review_{who}.png"))


if __name__ == "__main__":
    build("review" in sys.argv)
