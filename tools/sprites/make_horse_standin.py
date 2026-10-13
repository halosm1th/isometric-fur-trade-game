"""A stand-in horse until the real sheet is drawn (docs/asset-prompts.md,
Priority 4b): a plain side-view pony in pale greys, which Agents/Mount.cs
tints to each horse's coat. Writes assets/sprites/animals/horse_standin/ and
Scenes/Animals/HorseStandinFrames.tres. Run from the repo root, then
`godot --headless --path . --import`.

Drawn at 4x and reduced, with a dark outline, so it sits beside the painted
sprites without looking like a diagram. Facing right ("e"); the "w" frames
are mirrors. The "n" frames are the same horse - a stand-in does not need a
back view."""
import math, os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "sprites", "animals", "horse_standin")
TRES = os.path.join(ROOT, "Scenes", "Animals", "HorseStandinFrames.tres")
W, H, S = 200, 150, 4          # frame size as the caribou's; drawn at S times
GROUND = 146

COAT = (236, 230, 220, 255)
SHADE = (196, 188, 176, 255)
DARK = (92, 84, 76, 255)       # mane, tail, hooves: darker under any tint
OUTLINE = (40, 30, 24, 255)

def P(x, y): return (x * S, y * S)

def leg(d, top, angle, length=44, knee=0.0):
    """One leg from its top (x, y), swung by angle (radians, + = forward)."""
    x, y = top
    kx = x + math.sin(angle) * length * 0.5
    ky = y + math.cos(angle) * length * 0.5
    a2 = angle + knee
    fx = kx + math.sin(a2) * length * 0.5
    fy = min(GROUND, ky + math.cos(a2) * length * 0.5)
    d.line([P(x, y), P(kx, ky)], fill=SHADE, width=7 * S)
    d.line([P(kx, ky), P(fx, fy)], fill=SHADE, width=5 * S)
    d.ellipse([P(fx - 4, fy - 4), P(fx + 4, fy + 1)], fill=DARK)

def horse(phase, gait):
    img = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    gallop = gait == "run"
    graze = gait == "graze"
    swing = 0.0 if gait in ("idle", "graze") else math.sin(phase * math.pi / 2) * (0.55 if gallop else 0.32)
    bob = 0 if gait in ("idle", "graze") else (abs(math.sin(phase * math.pi / 2)) * (5 if gallop else 2))
    by = 92 - bob                     # body centre
    # far legs first (shaded), then the body, then near legs
    leg(d, (60, by + 8), -swing * 0.8, knee=0.25 if gallop else 0.1)
    leg(d, (122, by + 6), swing * 0.8, knee=-0.3 if gallop else -0.1)
    # tail
    tail_lift = 10 if gallop else 0
    d.polygon([P(52, by - 12), P(44, by - 4 - tail_lift), P(34, by + 32 - tail_lift * 2), P(42, by + 36 - tail_lift * 2), P(56, by - 2)], fill=DARK)
    # body and chest
    d.ellipse([P(48, by - 20), P(138, by + 18)], fill=COAT)
    d.ellipse([P(52, by - 2), P(134, by + 18)], fill=SHADE)          # the belly in shade
    d.ellipse([P(48, by - 20), P(138, by + 8)], fill=COAT)
    # neck and head: up and forward, or down to the grass
    if graze:
        neck = [P(118, by - 16), P(134, by - 10), P(158, by + 30), P(146, by + 36)]
        head = [P(146, by + 26), P(162, by + 26), P(170, by + 50), P(156, by + 52)]
        ear = [P(150, by + 24), P(146, by + 16), P(154, by + 22)]
        mane = [P(118, by - 18), P(124, by - 20), P(152, by + 26), P(146, by + 28)]
    else:
        lift = -6 if gallop else 0
        neck = [P(116, by - 14), P(134, by - 6), P(152, by - 44 - lift), P(138, by - 50 - lift)]
        head = [P(138, by - 58 - lift), P(152, by - 54 - lift), P(170, by - 30 - lift), P(160, by - 26 - lift)]
        ear = [P(140, by - 58 - lift), P(138, by - 68 - lift), P(146, by - 60 - lift)]
        mane = [P(114, by - 16), P(120, by - 20), P(140, by - 56 - lift), P(134, by - 52 - lift)]
    d.polygon(neck, fill=COAT)
    d.polygon(head, fill=COAT)
    d.polygon(ear, fill=DARK)
    d.polygon(mane, fill=DARK)
    # near legs
    leg(d, (66, by + 6), swing, knee=0.3 if gallop else 0.12)
    leg(d, (128, by + 4), -swing, knee=-0.35 if gallop else -0.12)
    # eye and nostril
    if graze:
        d.ellipse([P(155, by + 32), P(158, by + 35)], fill=OUTLINE)
    else:
        d.ellipse([P(149, by - 50 - (-6 if gallop else 0)), P(152, by - 47 - (-6 if gallop else 0))], fill=OUTLINE)
    small = img.resize((W, H), Image.LANCZOS)
    # a dark 1-pixel outline, as the painted sprites have
    alpha = small.split()[3].point(lambda a: 255 if a > 90 else 0)
    ring = alpha.filter(ImageFilter.MaxFilter(3))
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.paste(Image.new("RGBA", (W, H), OUTLINE), (0, 0), ring)
    body = small.copy(); body.putalpha(alpha)
    out.alpha_composite(body)
    return out

def main():
    os.makedirs(OUT, exist_ok=True)
    frames = {}
    for gait, n in (("idle", 1), ("graze", 1), ("walk", 4), ("run", 4)):
        for i in range(n):
            im = horse(i, gait)
            for facing in ("se", "ne", "sw", "nw"):
                f = im if facing.endswith("e") else im.transpose(Image.FLIP_LEFT_RIGHT)
                name = f"{gait}_{facing}_{i}.png"
                f.save(os.path.join(OUT, name))
                frames.setdefault(f"{gait}_{facing}", []).append(name)
    # the SpriteFrames resource
    ext, anims, k = [], [], 0
    for anim, names in frames.items():
        ids = []
        for nm in names:
            k += 1
            ext.append(f'[ext_resource type="Texture2D" path="res://assets/sprites/animals/horse_standin/{nm}" id="t{k}"]')
            ids.append(f't{k}')
        speed = {"walk": 6.0, "run": 10.0}.get(anim.split("_")[0], 1.0)
        fr = ", ".join('{\n"duration": 1.0,\n"texture": ExtResource("%s")\n}' % i for i in ids)
        anims.append('{\n"frames": [%s],\n"loop": true,\n"name": &"%s",\n"speed": %.1f\n}' % (fr, anim, speed))
    with open(TRES, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f'[gd_resource type="SpriteFrames" load_steps={k + 1} format=3]\n\n')
        fh.write("\n".join(ext) + "\n\n[resource]\nanimations = [" + ", ".join(anims) + "]\n")
    print(f"{k} frames, {len(frames)} animations -> {TRES}")

if __name__ == "__main__":
    main()
