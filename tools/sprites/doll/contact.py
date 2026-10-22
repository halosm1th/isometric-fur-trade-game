"""Lay frames side by side on grass green, doubled, for looking at.
  python contact.py out.png a.png b.png ...   (a path of '-' leaves a gap)"""
import sys
from PIL import Image

out, paths = sys.argv[1], sys.argv[2:]
ims = [Image.open(p).convert("RGBA") if p != "-" else Image.new("RGBA", (40, 170)) for p in paths]
w, h = sum(i.width for i in ims), max(i.height for i in ims)
o = Image.new("RGBA", (w, h), (90, 110, 80, 255))
x = 0
for i in ims:
    o.alpha_composite(i, (x, 0))
    x += i.width
o.resize((w * 2, h * 2), Image.NEAREST).save(out)
