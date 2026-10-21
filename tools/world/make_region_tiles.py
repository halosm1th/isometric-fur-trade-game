"""Regional ground tiles and two tree variants, recoloured from the existing
art so they keep its style (placeholders until painted versions exist).

    python tools/world/make_region_tiles.py

Writes assets/tiles/landscape/{prairie,tallgrass,parkland,tundra,shield,muskeg,mountain,scree,alpine,snowfield}.png
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

def patches(src, dst, colour, cover, blob=7, seed=1, base=None):
    """Soft clumps of `colour` over `src` (snow in the hollows of rock, rock
    through snow), the face's own light and shade kept: a clump on the dark
    side of the block is darker. `base` recolours the rest first."""
    im = Image.open(src).convert("RGBA")
    if base: im = base(im)
    px = im.load()
    rng = random.Random(seed)
    gw, gh = im.width // blob + 3, im.height // blob + 3
    grid = [[rng.random() for _ in range(gw)] for _ in range(gh)]
    top_v = max(colorsys.rgb_to_hsv(*(c / 255 for c in px[x, y][:3]))[2] for y in range(im.height) for x in range(im.width) if px[x, y][3] > 0) or 1
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0: continue
            fx, fy = x / blob, y / blob
            x0, y0 = int(fx), int(fy); tx, ty = fx - x0, fy - y0
            n = (grid[y0][x0] * (1 - tx) + grid[y0][x0 + 1] * tx) * (1 - ty) + (grid[y0 + 1][x0] * (1 - tx) + grid[y0 + 1][x0 + 1] * tx) * ty
            n += (rng.random() - 0.5) * 0.08   # a ragged edge, not a smooth one
            if n < cover:
                shade = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)[2] / top_v
                px[x, y] = (int(colour[0] * shade), int(colour[1] * shade), int(colour[2] * shade), a)
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
# The high country (World/TerrainPainter.Relief): talus above the trees, rock
# with snow lying in its hollows, and the snowfields round the peaks.
mountain = os.path.join(LAND, "mountain.png")
snow = os.path.join(LAND, "snow.png")
recolour(hill, os.path.join(LAND, "scree.png"), hue=0.09, sat=0.12, val=1.05, seed=11)
patches(os.path.join(LAND, "scree.png"), os.path.join(LAND, "scree.png"), (128, 124, 118), 0.30, blob=4, seed=14)
patches(mountain, os.path.join(LAND, "alpine.png"), (238, 242, 248), 0.42, blob=8, seed=12)
patches(snow, os.path.join(LAND, "snowfield.png"), (122, 118, 112), 0.24, blob=9, seed=13)
# Aspen: the tamarack's shape in summer green-gold; jack pine: a lighter, yellower spruce.
recolour(os.path.join(PROPS, "tamarack.png"), os.path.join(PROPS, "aspen.png"), hue=0.2, sat=0.9, val=1.1, seed=9)
recolour(os.path.join(PROPS, "spruce_1.png"), os.path.join(PROPS, "jackpine.png"), hue=0.24, sat=0.8, val=1.15, seed=10, only_green=True)
print("ok")
