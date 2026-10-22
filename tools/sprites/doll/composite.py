"""Stack doll layers for an outfit, as the game does, into one contact image.
  python composite.py out.png layer,layer,... anim_frame anim_frame ..."""
import json, os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
DIR = os.path.abspath(os.path.join(HERE, "..", "..", "..", "assets", "sprites", "doll"))
order = json.load(open(os.path.join(DIR, "doll.json")))["order"]

out, outfit, frames = sys.argv[1], sys.argv[2].split(","), sys.argv[3:]
ims = []
for f in frames:
    im = Image.new("RGBA", (180, 170))
    for layer in order:
        if layer in outfit:
            im.alpha_composite(Image.open(os.path.join(DIR, layer, f + ".png")).convert("RGBA"))
    ims.append(im)
o = Image.new("RGBA", (180 * len(ims), 170), (90, 110, 80, 255))
for i, im in enumerate(ims):
    o.alpha_composite(im, (180 * i, 0))
o.resize((o.width * 2, o.height * 2), Image.NEAREST).save(out)
