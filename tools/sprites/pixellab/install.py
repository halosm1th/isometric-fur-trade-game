"""Put the PixelLab voyageur (voyageur_set.sh's frames in out/) where the game
can wear it: assets/sprites/voyageur_pl/, named as the painted frames are,
and Scenes/Npc/VoyageurPLFrames.tres, laid out as VoyageurFrames.tres with a
shoot animation added. The game uses it when run with `-- --pixellab`.

  python install.py

The idles are the painted frames themselves (they were PixelLab's
reference). Then run `godot --headless --path . --import`.
"""
import os, re, shutil
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SRC = os.path.join(ROOT, "assets", "sprites", "voyageur")
DST = os.path.join(ROOT, "assets", "sprites", "voyageur_pl")
TRES = os.path.join(ROOT, "Scenes", "Npc", "VoyageurFrames.tres")
OUT_TRES = os.path.join(ROOT, "Scenes", "Npc", "VoyageurPLFrames.tres")
# PixelLab job -> the painted set's frame name
NAMES = {"walk": "walk_front", "walk_back": "walk_back", "walk_side": "walk_side", "death": "death",
         "eat": "eat", "drink": "drink", "attack": "attack", "portage": "portage", "paddle": "paddle", "shoot": "shoot"}

os.makedirs(DST, exist_ok=True)
for f in ("idle_front.png", "idle_back.png", "idle_side.png"):
    shutil.copy(os.path.join(SRC, f), os.path.join(DST, f))
missing = []
for job, name in NAMES.items():
    for i in range(4):
        src = os.path.join(HERE, "out", f"{job}_{i}.png")
        if not os.path.exists(src):
            missing.append(f"{job}_{i}")
            # Until PixelLab has drawn it, the painted frame stands in.
            if os.path.exists(os.path.join(SRC, f"{name}_{i}.png")):
                shutil.copy(os.path.join(SRC, f"{name}_{i}.png"), os.path.join(DST, f"{name}_{i}.png"))
            continue
        a = np.array(Image.open(src).convert("RGBA"))
        # Hard edges, as the painted frames have.
        a[..., 3] = np.where(a[..., 3] > 127, 255, 0)
        Image.fromarray(a).save(os.path.join(DST, f"{name}_{i}.png"))

t = open(TRES, encoding="utf-8").read().replace("res://assets/sprites/voyageur/", "res://assets/sprites/voyageur_pl/")
t = re.sub(r' uid="[^"]*"', "", t)
if os.path.exists(os.path.join(DST, "shoot_0.png")):
    n = max(int(x) for x in re.findall(r'id="t(\d+)"', t))
    ids = [f"t{n + 1 + i}" for i in range(4)]
    ext = "".join(f'[ext_resource type="Texture2D" path="res://assets/sprites/voyageur_pl/shoot_{i}.png" id="{ids[i]}"]\n' for i in range(4))
    t = t.replace("\n[resource]", ext + "\n[resource]", 1)
    frames = ", ".join('{\n"duration": 1.0,\n"texture": ExtResource("%s")\n}' % i for i in ids)
    anim = ', {\n"name": &"shoot",\n"speed": 9.0,\n"loop": false,\n"frames": [%s]\n}]' % frames
    t = t[:t.rstrip().rfind("]")] + anim + t[t.rstrip().rfind("]") + 1:]
    t = re.sub(r"load_steps=(\d+)", lambda m: f"load_steps={int(m.group(1)) + 4}", t, count=1)
open(OUT_TRES, "w", encoding="utf-8").write(t)
print("installed;", f"painted stand-ins for {missing}" if missing else "all frames from PixelLab")
