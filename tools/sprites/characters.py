"""Cut the Gemini character sheets in assets/external/NEW HBC ASSETS into
frames under assets/sprites/<who>/ and write Scenes/Npc/<Who>Frames.tres.

  python characters.py            cut everything, write the .tres files
  python characters.py review     also write tools/sprites/out/review_<who>.png

The part numbers are the ones `slice.py detect` prints on each sheet's
contact image. Sheets for characters that already exist (player, labourer,
factor, Cree, Assiniboine) are cut onto the old 150x130 canvas so new and
old frames line up; new characters get a roomier 180x170 one (the Npc
works its offset out from the frame size).
"""
import glob, json, os, sys, tempfile
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(ROOT, "assets", "external", "NEW HBC ASSETS")
sys.path.insert(0, HERE)
import slice as sl

OLD = [150, 130]
NEW = [180, 170]

# who: (canvas, [(sheet, ref part, {anim: [parts]})], {anim: (fps, loop)} overrides, flip list)
CUTS = {
    "voyageur": (NEW, [
        ("NWC WAlking", 0, {"idle_front": [0], "idle_back": [1], "idle_side": [2],
                            "walk_front": [3, 4, 5, 6], "walk_back": [7, 8, 9, 10],
                            "walk_side": [11, 12, 13, 14], "death": [15, 16, 17, 18]}),
        ("NWC Working", 0, {"portage": [7, 8, 9, 8], "paddle": [10, 11, 12, 13],
                            "eat": [14, 15, 16, 17], "drink": [18, 19, 20, 21], "attack": [22, 23, 24, 25]}),
    ]),
    "nwc_partner": (NEW, [
        ("NWC BourGoise", 0, {"idle_front": [0], "idle_back": [1], "idle_side": [2],
                              "walk_front": [3, 4, 5, 8], "walk_back": [9, 1, 10, 1],
                              "walk_side": [11, 12, 13, 14], "talk": [15, 16, 17, 18],
                              "eat": [19, 20, 21, 22], "drink": [23, 24, 25, 26],
                              "shoot": [27, 28, 29, 30], "death": [31, 32, 33, 34, 35]}),
    ]),
    "country_wife": (NEW, [
        ("COuntry WIfe", 0, {"idle_front": [0], "idle_back": [1], "idle_side": [2],
                             "walk_front": [3, 4, 5, 4], "walk_back": [7, 8, 9, 8],
                             "walk_side": [11, 12, 13, 14], "death": [15, 16, 17, 18]}),
        ("Country wife 2", 4, {"carry_wood": [0, 1, 2, 3], "carry_water": [4, 5, 6, 7],
                               "scrape": [8, 9, 10, 11], "eat": [12, 13, 14, 15, 16, 17],
                               "drink": [18, 19], "carry_baby": [21, 22, 23, 22]}),
    ]),
    "sailor": (NEW, [
        ("Boatman", 0, {"idle_front": [0], "idle_back": [1], "idle_side": [2],
                        "walk_front": [31, 32, 33, 32], "walk_back": [8, 9, 10, 9],
                        "walk_side": [15, 16, 17, 18], "haul": [11, 12, 13, 14],
                        "drink": [19, 20, 21, 22], "attack": [27, 28, 29, 30],
                        "death": [35, 36, 37, 38]}),
    ]),
    "clerk": (NEW, [
        ("HBC Clerk", 7, {"idle_front": [7], "idle_back": [9], "idle_side": [13],
                          "walk_front": [3, 4, 5, 4], "walk_back": [9, 10, 15, 16],
                          "walk_side": [13, 14, 17, 18], "write": [19, 20, 21, 22],
                          "eat": [23, 24, 25, 26], "drink": [27, 28, 29, 30],
                          "death": [31, 32, 33, 34]}),
    ]),
    "cree_captain": (NEW, [
        ("Cree Trading Capitain", 0, {"idle_front": [0], "idle_back": [1], "idle_side": [2],
                                      "walk_front": [3, 4, 5, 4], "walk_back": [8, 9, 10, 9],
                                      "walk_side": [11, 12, 13, 14], "talk": [15, 16, 17, 18],
                                      "smoke": [19, 20, 21, 22], "eat": [27, 28, 29, 30],
                                      "death": [31, 32, 33, 34]}),
    ]),
    "child_boy": (NEW, [
        ("HBC Child", 0, {"idle_front": [0], "idle_back": [1], "idle_side": [2],
                          "walk_front": [3, 4, 5, 6], "walk_back": [10, 11, 10, 11],
                          "walk_side": [12, 13, 14, 13], "run_side": [15, 16]}),
    ]),
    "child_girl": (NEW, [
        ("HBC Child", 17, {"idle_front": [17], "idle_back": [18], "idle_side": [19],
                           "walk_front": [20, 21, 22, 21], "walk_back": [25, 26, 27, 26],
                           "walk_side": [28, 29, 30, 29], "run_side": [31, 32],
                           "eat": [36, 37, 38, 39]}),
    ]),
    # Additions to existing characters, on their old canvas.
    "player": (OLD, [
        ("Player other animations", 0, {"idle_side": [0], "walk_side": [1, 2, 3, 4],
                                        "run_side": [7, 8, 9, 10], "attack": [11, 12, 13, 14],
                                        "chop": [15, 16, 17, [18, 19]], "carry_side": [20, 21, 22, 23],
                                        "carry": [24, 25, 26, 27], "paddle": [28, 29, 30, 31],
                                        "sleep": [32, 33], "shiver": [34, 35, 36, 37],
                                        "drunk": [38, 39, 40, 41]}),
    ]),
    "labourer": (OLD, [
        ("Orkney Labourer", 0, {"idle_back": [4], "idle_side": [1], "walk_back": [5, 6, 7, 4],
                                "eat": [8, 9, 10, 11], "drink": [12, 13, 14, 15],
                                "chop": [16, [17, 18], [19, 20], 21], "haul": [22, 23, 24, 25],
                                "attack": [30, 31, 32, 33], "death": [34, 35, 36, 37, 38, 39, 40]}),
    ]),
    "cree": (OLD, [
        ("Cree Hunter", 1, {"idle_back": [0], "idle_side": [1], "walk_side": [6, 7, 8, 9],
                            "run_side": [14, 15, 16, 17], "eat": [22, 23, 24, 25],
                            "drink": [30, 31, 32, 33], "attack": [18, 19, 20, 21],
                            "butcher": [26, 27, 28, 29], "death": [34, 35, 36, 37]}),
    ]),
    "cree_woman": (OLD, [
        ("Cree Woman", 0, {"idle_back": [0], "idle_side": [1], "walk_side": [2, 3, 4, 5, 6, 7],
                           "eat": [8, 9, 10, 11], "drink": [12, 13, 14, 15],
                           "scrape": [20, 21, 22, 23], "carry_wood": [28, 29, 30, 31],
                           "death": [32, 33, 34, 35]}),
    ]),
    "assiniboine": (OLD, [
        ("Assiniboine Man Attacking", 0, {"idle_back": [0], "idle_side": [1], "walk_side": [2, 3, 4, 5, 6, 7],
                                          "eat": [8, 9, 10, 11], "drink": [12, 13, 14, 15],
                                          "attack": [18, 19, 20, 21], "death": [22, 23, 24, 25]}),
    ]),
}

# Facing fixes found on review: anims whose side frames face left.
FLIP = {}

# Existing characters keep their own height (feet to hat in their idle
# frame), so new frames match the old ones beside them.
HEIGHT = {"labourer": 108, "cree": 124, "cree_woman": 128}

# Where each character's SpriteFrames live; the existing ones are merged into.
TRES = {"player": "Scenes/Player/PlayerFrames.tres", "labourer": "Scenes/Npc/LabourerFrames.tres",
        "cree": "Scenes/Npc/CreeFrames.tres", "cree_woman": "Scenes/Npc/CreeWomanFrames.tres",
        "assiniboine": "Scenes/Npc/AssiniboineFrames.tres"}
# Old animations to keep under another name before the new ones land.
RENAME = {"cree": {"attack": "shoot"}}
# Files on disk that should not become animations.
SKIP = {"player": {"idle_back"}}

SPEED = {"idle": 5, "walk": 8, "walk_back": 8, "walk_side": 8, "run_side": 11, "death": 5,
         "eat": 5, "drink": 5, "attack": 9, "shoot": 7, "sleep": 1.5, "shiver": 8, "drunk": 5}
ONESHOT = {"death", "eat", "drink", "attack", "shoot", "chop", "write", "talk", "smoke", "butcher"}

# Which frame files make each animation the game knows by name.
def animations(folder):
    files = sorted(os.path.basename(f)[:-4] for f in glob.glob(os.path.join(folder, "*.png")))
    def seq(stem):
        if stem in files:
            return [stem]
        return sorted([f for f in files if f.startswith(stem + "_") and f[len(stem) + 1:].isdigit()],
                      key=lambda f: int(f.rsplit("_", 1)[1]))
    anims = {}
    def add(name, stem):
        s = seq(stem)
        if s:
            anims[name] = s
    add("idle", "idle_front")
    add("idle_back", "idle_back")
    add("idle_side", "idle_side")
    add("walk", "walk_front")
    add("walk_back", "walk_back")
    add("walk_side", "walk_side")
    for stem in ["run_side", "death", "eat", "drink", "attack", "shoot", "portage", "paddle", "talk", "carry",
                 "carry_side", "carry_wood", "carry_water", "carry_baby", "scrape", "write", "smoke", "haul",
                 "chop", "butcher", "sleep", "shiver", "drunk", "sneak", "sneak_back", "sneak_side", "musket"]:
        add(stem, stem)
    return anims


ANIM_OF = {"idle_front": "idle", "walk_front": "walk"}


def parse_tres(path):
    """{anim: (speed, loop, [res paths])} from an existing SpriteFrames."""
    import re
    if not os.path.exists(path):
        return {}
    t = open(path, encoding="utf-8").read()
    ids = dict((m.group(2), m.group(1)) for m in re.finditer(r'path="([^"]+)" id="([^"]+)"', t))
    out = {}
    for m in re.finditer(r'"name": &"(\w+)",\s*"speed": ([\d.]+),\s*"loop": (\w+),\s*"frames": \[(.*?)\]\s*\}', t, re.S):
        tex = [ids[i] for i in re.findall(r'ExtResource\("([^"]+)"\)', m.group(4))]
        out[m.group(1)] = (float(m.group(2)), m.group(3), tex)
    return out


def merge_tres(who, path, produced):
    """Old animations stay unless this run made a new one of that name."""
    folder = os.path.join(ROOT, "assets", "sprites", who)
    anims = {}
    for name, v in parse_tres(path).items():
        anims[RENAME.get(who, {}).get(name, name)] = v
    for name, stems in animations(folder).items():
        if name in SKIP.get(who, set()) or (name in anims and name not in produced):
            continue
        anims[name] = (float(SPEED.get(name, 6)), "false" if name in ONESHOT else "true",
                       [f"res://assets/sprites/{who}/{s}.png" for s in stems])
    res = []
    blocks = []
    for name, (speed, loop, tex) in anims.items():
        for p in tex:
            if p not in res:
                res.append(p)
        frames = ",\n".join('{\n"duration": 1.0,\n"texture": ExtResource("t%d")\n}' % (res.index(p) + 1) for p in tex)
        blocks.append('{\n"name": &"%s",\n"speed": %s,\n"loop": %s,\n"frames": [%s]\n}' % (name, speed, loop, frames))
    head = f'[gd_resource type="SpriteFrames" load_steps={len(res) + 1} format=3]\n\n'
    head += "".join(f'[ext_resource type="Texture2D" path="{p}" id="t{i + 1}"]\n' for i, p in enumerate(res))
    return head + "\n[resource]\nanimations = [" + ", ".join(blocks) + "]\n"


def write_tres(who, folder, keep=None):
    """Writes SpriteFrames for the folder. `keep` is the existing .tres, whose
    hand-made animations (sneak, shoot, ...) are carried over when this sheet
    has nothing new for them."""
    anims = animations(folder)
    res = []
    def rid(stem):
        path = f"res://assets/sprites/{who}/{stem}.png"
        if path not in res:
            res.append(path)
        return f"t{res.index(path) + 1}"
    blocks = []
    for name, stems in anims.items():
        speed = SPEED.get(name, 6)
        loop = "false" if name in ONESHOT else "true"
        frames = ",\n".join('{\n"duration": 1.0,\n"texture": ExtResource("%s")\n}' % rid(s) for s in stems)
        blocks.append('{\n"name": &"%s",\n"speed": %s,\n"loop": %s,\n"frames": [%s]\n}' % (name, float(speed), loop, frames))
    head = f'[gd_resource type="SpriteFrames" load_steps={len(res) + 1} format=3]\n\n'
    head += "".join(f'[ext_resource type="Texture2D" path="{p}" id="t{i + 1}"]\n' for i, p in enumerate(res))
    return head + "\n[resource]\nanimations = [" + ", ".join(blocks) + "]\n"


def cut_all(review=False):
    for who, (canvas, sheets) in CUTS.items():
        folder = os.path.join(ROOT, "assets", "sprites", who)
        os.makedirs(folder, exist_ok=True)
        for sheet, ref, anims in sheets:
            spec = {"sheet": os.path.join(SRC, sheet + ".jpg"), "out": folder, "canvas": canvas,
                    "height": HEIGHT.get(who, 123), "ref": ref, "anims": anims, "flip": FLIP.get(who, [])}
            with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as f:
                json.dump(spec, f)
            sl.export(f.name)
            os.unlink(f.name)
        name = "".join(w.capitalize() for w in who.split("_")) + "Frames.tres"
        path = os.path.join(ROOT, TRES.get(who, "Scenes/Npc/" + name))
        produced = set()
        for _, _, anims in sheets:
            produced |= {ANIM_OF.get(a, a) for a in anims}
        text = merge_tres(who, path, produced)
        open(path, "w", newline="\n").write(text)
        if review:
            montage(who, folder)


def montage(who, folder):
    anims = animations(folder)
    rows = list(anims.items())
    sample = Image.open(os.path.join(folder, rows[0][1][0] + ".png"))
    W, H = sample.size
    cols = max(len(s) for _, s in rows)
    img = Image.new("RGBA", (130 + cols * W, len(rows) * H), (90, 110, 90, 255))
    d = ImageDraw.Draw(img)
    for r, (name, stems) in enumerate(rows):
        d.text((4, r * H + H // 2), name, fill=(255, 255, 255))
        for c, s in enumerate(stems):
            fr = Image.open(os.path.join(folder, s + ".png")).convert("RGBA")
            if fr.size != (W, H):
                d.text((130 + c * W + 4, r * H + 4), f"{fr.size}", fill=(255, 0, 0))
            img.alpha_composite(fr, (130 + c * W, r * H))
            d.line([(130 + c * W, r * H + H - 1), (130 + (c + 1) * W, r * H + H - 1)], fill=(60, 60, 60))
    img.save(os.path.join(HERE, "out", f"review_{who}.png"))


if __name__ == "__main__":
    cut_all("review" in sys.argv)
