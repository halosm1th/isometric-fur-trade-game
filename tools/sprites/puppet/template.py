"""The parts template Gemini paints a cut-out man into.

  python template.py      -> template.png (for Gemini) + template.json (for us)

Three rows (front, side, back). Each starts with the painted voyageur in
that facing, for style and size, then one labelled box per part. The
pieces are drawn at 3x the game's size. Each box carries a small cross at
the pivot the part turns about (the neck, the shoulder, the hip...);
template.json records the boxes and pivots, so whatever Gemini paints
in a box can be cut out and pinned to the skeleton at that point.
"""
import json, os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SCALE = 3
MAGENTA = (255, 0, 255)

# part: (box w, h at 3x, pivot as fractions of the box, what it is)
PARTS = [
    ("head", 150, 150, (0.5, 0.92), "head and face, down to the jaw"),
    ("torso", 165, 150, (0.5, 0.08), "chest and belly, neck to waist"),
    ("hips", 150, 90, (0.5, 0.15), "hips and seat, waist to top of thighs"),
    ("upper_arm", 66, 105, (0.5, 0.12), "shoulder to elbow"),
    ("forearm", 66, 120, (0.5, 0.1), "elbow to fingers, hand included"),
    ("thigh", 78, 105, (0.5, 0.1), "hip to knee"),
    ("shin", 90, 120, (0.5, 0.08), "knee to sole, foot included"),
]
FACINGS = [("front", "idle_front"), ("side", "idle_side"), ("back", "idle_back")]
GAP, LABEL, PAD = 24, 26, 20


def main():
    try:
        font = ImageFont.truetype("arial.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
    ref_w = 180 * SCALE // 2 + 40
    row_h = LABEL + max(h for _, _, h, _, _ in PARTS) + 2 * GAP
    row_h = max(row_h, 170 * SCALE // 2 + LABEL + GAP)
    col = lambda w: max(w, 112)
    width = PAD + ref_w + sum(col(w) + GAP for _, w, _, _, _ in PARTS) + PAD
    height = PAD + row_h * len(FACINGS) + PAD
    img = Image.new("RGB", (width, height), MAGENTA)
    d = ImageDraw.Draw(img)
    spec = {"scale": SCALE, "parts": {}}
    for r, (facing, ref) in enumerate(FACINGS):
        y0 = PAD + r * row_h
        sprite = Image.open(os.path.join(ROOT, "assets", "sprites", "voyageur", ref + ".png")).convert("RGBA")
        sprite = sprite.resize((180 * SCALE // 2, 170 * SCALE // 2), Image.NEAREST)
        img.paste(sprite, (PAD, y0 + LABEL), sprite)
        d.text((PAD, y0), f"{facing.upper()} - reference", fill=(0, 0, 0), font=font)
        x = PAD + ref_w
        for name, w, h, (px, py), what in PARTS:
            by = y0 + LABEL
            d.text((x, y0), name.replace("_", " "), fill=(0, 0, 0), font=font)
            d.rectangle([x, by, x + w, by + h], outline=(0, 0, 0), width=2)
            cx, cy = x + px * w, by + py * h
            d.line([cx - 6, cy, cx + 6, cy], fill=(0, 0, 0), width=2)
            d.line([cx, cy - 6, cx, cy + 6], fill=(0, 0, 0), width=2)
            spec["parts"][f"{facing}_{name}"] = {"box": [x, by, x + w, by + h], "pivot": [cx, cy], "what": what}
            x += col(w) + GAP
    img.save(os.path.join(HERE, "template.png"))
    with open(os.path.join(HERE, "template.json"), "w") as f:
        json.dump(spec, f, indent=1)
    print("template", img.size)


if __name__ == "__main__":
    main()
