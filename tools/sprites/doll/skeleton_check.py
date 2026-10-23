"""Draw out/skeletons.json's joints over frames, to check them by eye.
  python skeleton_check.py OUT.png FRAME=IMAGE ...
"""
import json, sys
from PIL import Image, ImageDraw

BONES = [("NECK", "NOSE"), ("NECK", "RIGHT SHOULDER"), ("RIGHT SHOULDER", "RIGHT ELBOW"), ("RIGHT ELBOW", "RIGHT ARM"),
         ("NECK", "LEFT SHOULDER"), ("LEFT SHOULDER", "LEFT ELBOW"), ("LEFT ELBOW", "LEFT ARM"),
         ("NECK", "RIGHT HIP"), ("RIGHT HIP", "RIGHT KNEE"), ("RIGHT KNEE", "RIGHT LEG"),
         ("NECK", "LEFT HIP"), ("LEFT HIP", "LEFT KNEE"), ("LEFT KNEE", "LEFT LEG"),
         ("NOSE", "RIGHT EYE"), ("NOSE", "LEFT EYE"), ("RIGHT EYE", "RIGHT EAR"), ("LEFT EYE", "LEFT EAR")]
Z = 3


def draw(im, joints):
    im = im.convert("RGBA").resize((im.width * Z, im.height * Z), Image.NEAREST)
    d = ImageDraw.Draw(im)
    p = {j["label"]: (j["x"] * im.width, j["y"] * im.height) for j in joints}
    for a, b in BONES:
        d.line([p[a], p[b]], fill=(255, 80, 80, 255) if "RIGHT" in a + b else (80, 220, 255, 255), width=2)
    for xy in p.values():
        d.ellipse([xy[0] - 3, xy[1] - 3, xy[0] + 3, xy[1] + 3], fill=(255, 255, 0, 255))
    return im


if __name__ == "__main__":
    sk = json.load(open("out/skeletons.json"))
    ims = []
    for arg in sys.argv[2:]:
        frame, path = arg.split("=")
        bg = Image.new("RGBA", Image.open(path).size, (90, 110, 80, 255))
        bg.alpha_composite(Image.open(path).convert("RGBA"))
        ims.append(draw(bg, sk[frame]))
    out = Image.new("RGBA", (sum(i.width for i in ims), ims[0].height))
    x = 0
    for i in ims:
        out.paste(i, (x, 0)); x += i.width
    out.save(sys.argv[1])
