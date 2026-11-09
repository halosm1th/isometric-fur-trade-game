"""Placeholder icons for the boat-building materials (Items: BirchBark ... CrookedKnife).

Drawn at 4x and scaled down to the 64x64 the other item icons use. Run from the
repo root:  python tools/sprites/boat_items.py
"""
import math
import random
from PIL import Image, ImageDraw, ImageFilter

OUT = "assets/sprites/items/"
S = 256  # drawn at 4x


def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def finish(im, name):
    im = im.filter(ImageFilter.SMOOTH)
    im.resize((64, 64), Image.LANCZOS).save(OUT + name)


def outline_poly(d, pts, fill, edge=(40, 26, 14, 255), w=8):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=edge, width=w, joint="curve")


def bark():
    im = canvas(); d = ImageDraw.Draw(im)
    # a roll of birch bark: white outside, tan inside, black lenticels
    d.ellipse((40, 70, 120, 190), fill=(205, 160, 110, 255), outline=(40, 26, 14, 255), width=8)
    d.rectangle((80, 70, 210, 190), fill=(236, 232, 220, 255))
    d.line((80, 70, 210, 70), fill=(40, 26, 14, 255), width=8)
    d.line((80, 190, 210, 190), fill=(40, 26, 14, 255), width=8)
    d.ellipse((170, 70, 250, 190), fill=(236, 232, 220, 255), outline=(40, 26, 14, 255), width=8)
    d.ellipse((190, 100, 230, 160), fill=(196, 150, 100, 255), outline=(90, 60, 30, 255), width=5)
    rnd = random.Random(3)
    for _ in range(14):
        x = rnd.randint(90, 200); y = rnd.randint(85, 175)
        d.line((x, y, x + rnd.randint(10, 24), y), fill=(30, 30, 30, 255), width=5)
    finish(im, "birch_bark.png")


def watap():
    im = canvas(); d = ImageDraw.Draw(im)
    # a coil of split spruce root
    for r, c in ((92, (150, 104, 60, 255)), (70, (170, 120, 70, 255)), (48, (150, 104, 60, 255)), (26, (170, 120, 70, 255))):
        d.ellipse((128 - r, 128 - r * 0.8, 128 + r, 128 + r * 0.8), outline=(40, 26, 14, 255), width=22)
        d.ellipse((128 - r, 128 - r * 0.8, 128 + r, 128 + r * 0.8), outline=c, width=14)
    d.line((200, 140, 245, 200), fill=(40, 26, 14, 255), width=20)
    d.line((200, 140, 245, 200), fill=(170, 120, 70, 255), width=11)
    finish(im, "watap.png")


def gum():
    im = canvas(); d = ImageDraw.Draw(im)
    # a cake of spruce gum on a chip of wood
    outline_poly(d, [(30, 180), (226, 160), (236, 200), (40, 220)], (170, 130, 80, 255))
    d.ellipse((60, 70, 200, 190), fill=(120, 70, 20, 255), outline=(40, 26, 14, 255), width=8)
    d.ellipse((90, 90, 140, 125), fill=(200, 140, 60, 255))
    d.ellipse((150, 120, 175, 140), fill=(180, 120, 50, 255))
    finish(im, "spruce_gum.png")


def cedar():
    im = canvas(); d = ImageDraw.Draw(im)
    # a bundle of split cedar ribs, bound
    for i, x in enumerate(range(50, 200, 30)):
        col = (214, 150, 110, 255) if i % 2 else (196, 128, 92, 255)
        outline_poly(d, [(x, 30), (x + 26, 30), (x + 30, 230), (x + 4, 230)], col, w=6)
    d.rectangle((40, 100, 220, 118), fill=(120, 80, 40, 255), outline=(40, 26, 14, 255), width=5)
    d.rectangle((40, 160, 220, 178), fill=(120, 80, 40, 255), outline=(40, 26, 14, 255), width=5)
    finish(im, "cedar.png")


def plank():
    im = canvas(); d = ImageDraw.Draw(im)
    for k, y in enumerate((70, 120, 170)):
        outline_poly(d, [(20, y + 10), (220, y - 20), (236, y + 10), (36, y + 40)], (200, 160, 100, 255) if k != 1 else (186, 146, 90, 255), w=6)
        d.line((50, y + 22, 200, y - 2), fill=(150, 110, 60, 255), width=3)
    finish(im, "plank.png")


def nails():
    im = canvas(); d = ImageDraw.Draw(im)
    rnd = random.Random(5)
    for _ in range(9):
        x = rnd.randint(50, 200); y = rnd.randint(60, 190); a = rnd.uniform(0, math.pi)
        dx, dy = math.cos(a) * 60, math.sin(a) * 60
        d.line((x - dx, y - dy, x + dx, y + dy), fill=(30, 30, 34, 255), width=16)
        d.line((x - dx, y - dy, x + dx, y + dy), fill=(120, 120, 128, 255), width=9)
        d.ellipse((x - dx - 13, y - dy - 13, x - dx + 13, y - dy + 13), fill=(90, 90, 96, 255), outline=(30, 30, 34, 255), width=4)
    finish(im, "nails.png")


def oakum():
    im = canvas(); d = ImageDraw.Draw(im)
    rnd = random.Random(7)
    d.ellipse((40, 70, 216, 210), fill=(150, 120, 70, 255), outline=(40, 26, 14, 255), width=8)
    for _ in range(40):
        x = rnd.randint(60, 196); y = rnd.randint(85, 195)
        d.arc((x - 30, y - 12, x + 30, y + 12), rnd.randint(0, 180), rnd.randint(180, 360), fill=(100, 78, 40, 255), width=4)
    finish(im, "oakum.png")


def pitch():
    im = canvas(); d = ImageDraw.Draw(im)
    # a small keg of pitch, black running over the rim
    outline_poly(d, [(70, 60), (186, 60), (200, 220), (56, 220)], (130, 90, 50, 255))
    for y in (90, 190):
        d.line((62, y, 194, y), fill=(60, 60, 60, 255), width=10)
    d.ellipse((70, 40, 186, 80), fill=(20, 18, 16, 255), outline=(40, 26, 14, 255), width=6)
    d.line((110, 70, 108, 150), fill=(20, 18, 16, 255), width=14)
    finish(im, "pitch.png")


def crooked_knife():
    im = canvas(); d = ImageDraw.Draw(im)
    # the mocotaugan: a bent blade on a thumb-rest handle
    outline_poly(d, [(40, 200), (120, 150), (140, 170), (70, 228)], (150, 96, 54, 255))
    d.ellipse((30, 196, 70, 236), fill=(150, 96, 54, 255), outline=(40, 26, 14, 255), width=7)
    blade = [(120, 150), (200, 90), (226, 50), (232, 70), (212, 112), (140, 170)]
    outline_poly(d, blade, (190, 196, 204, 255), w=7)
    d.line((132, 158, 210, 98), fill=(240, 244, 248, 255), width=5)
    d.rectangle((112, 146, 146, 176), fill=(70, 50, 30, 255))
    finish(im, "crooked_knife.png")


if __name__ == "__main__":
    for f in (bark, watap, gum, cedar, plank, nails, oakum, pitch, crooked_knife):
        f()
    print("ok")
