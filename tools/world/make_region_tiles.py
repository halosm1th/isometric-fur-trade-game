"""Regional ground tiles and two tree variants, recoloured from the existing
art so they keep its style (placeholders until painted versions exist).

    python tools/world/make_region_tiles.py

Writes assets/tiles/landscape/{prairie,tallgrass,parkland,tundra,shield,muskeg,mountain}.png
and assets/tiles/props/{aspen,jackpine}.png.
"""
import colorsys, os, random
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
LAND = os.path.join(ROOT, "assets", "tiles", "landscape")
PROPS = os.path.join(ROOT, "assets", "tiles", "props")

def recolour(src, dst, hue=None, sat=1.0, val=1.0, speckle=None, seed=1, only_green=False):
    im = Image.open(src).convert("RGBA")
    px = im.load()
    rng = random.Random(seed)
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0: continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            green = 0.16 < h < 0.45 and s > 0.15
            if only_green and not green: continue
            if hue is not None and (green or not only_green): h = hue
            s = min(1, s * sat); v = min(1, v * val)
            if speckle and rng.random() < speckle[0]:
                sr, sg, sb = speckle[1]
                r2, g2, b2 = sr / 255, sg / 255, sb / 255
            else:
                r2, g2, b2 = colorsys.hsv_to_rgb(h, s, v)
            px[x, y] = (int(r2 * 255), int(g2 * 255), int(b2 * 255), a)
    im.save(dst)

g = os.path.join(LAND, "grass.png")
hill = os.path.join(LAND, "hill.png")
recolour(g, os.path.join(LAND, "prairie.png"), hue=0.12, sat=0.75, val=1.05, speckle=(0.04, (196, 170, 104)), seed=2)
recolour(g, os.path.join(LAND, "tallgrass.png"), hue=0.15, sat=0.85, val=1.0, speckle=(0.05, (150, 140, 70)), seed=3)
recolour(g, os.path.join(LAND, "parkland.png"), hue=0.21, sat=0.85, val=1.08, seed=4)
recolour(g, os.path.join(LAND, "tundra.png"), hue=0.2, sat=0.35, val=0.95, speckle=(0.06, (176, 168, 150)), seed=5)
recolour(g, os.path.join(LAND, "shield.png"), hue=0.25, sat=0.55, val=0.85, speckle=(0.12, (140, 138, 132)), seed=6)
recolour(g, os.path.join(LAND, "muskeg.png"), hue=0.19, sat=0.7, val=0.7, speckle=(0.05, (92, 110, 72)), seed=7)
recolour(hill, os.path.join(LAND, "mountain.png"), hue=0.08, sat=0.25, val=0.85, seed=8)
# Aspen: the tamarack's shape in summer green-gold; jack pine: a lighter, yellower spruce.
recolour(os.path.join(PROPS, "tamarack.png"), os.path.join(PROPS, "aspen.png"), hue=0.2, sat=0.9, val=1.1, seed=9)
recolour(os.path.join(PROPS, "spruce_1.png"), os.path.join(PROPS, "jackpine.png"), hue=0.24, sat=0.8, val=1.15, seed=10, only_green=True)
print("ok")
