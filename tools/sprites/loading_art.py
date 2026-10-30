"""The loading screen's art, cut from the menu painting: the ship lifted out
(so she can sail in across the Bay while the world loads) and the sky and
water painted back behind her.

  python tools/sprites/loading_art.py

Writes assets/ui/loading_background.jpg (the painting without her) and
assets/ui/loading_ship.png (her alone, with her reflection). UI/LoadingScreen
puts her back at SHIP_BOX's corner when the bar is full.
"""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
SRC = os.path.join(ROOT, "assets", "ui", "menu_background.jpg")
SHIP_BOX = (2195, 650, 2532, 992)   # x0, y0, x1, y1 in the painting; the jolly boat lies just right of it
# Painted back row by row; below her hull only her reflection, kept clear of the chimney smoke.
PATCHES = [(2195, 650, 2532, 960), (2270, 960, 2532, 992)]
THRESHOLD = 22                       # summed RGB difference from the painted-back sky that counts as ship


def main():
    im = Image.open(SRC).convert("RGB")
    px = im.load()
    bg = im.copy()
    bpx = bg.load()
    # Sky and water run in level bands: paint each row across from the
    # columns either side of her.
    for x0, y0, x1, y1 in PATCHES:
        for y in range(y0, y1):
            lc, rc = px[x0 - 1, y], px[x1, y]
            # The jolly boat sits on the right column low down: use the left alone there.
            if y > 925 and abs(sum(rc) - sum(lc)) > 40:
                rc = lc
            for x in range(x0, x1):
                t = (x - x0 + 1) / (x1 - x0 + 1)
                bpx[x, y] = tuple(round(lc[i] * (1 - t) + rc[i] * t) for i in range(3))
    x0, y0, x1, y1 = SHIP_BOX
    ship = Image.new("RGBA", (x1 - x0, y1 - y0), (0, 0, 0, 0))
    spx = ship.load()
    for y in range(y0, y1):
        for x in range(x0, x1):
            o, b = px[x, y], bpx[x, y]
            d = sum(abs(o[i] - b[i]) for i in range(3))
            if d > THRESHOLD:
                a = min(255, int((d - THRESHOLD) * 12))
                spx[x - x0, y - y0] = (*o, a)
    bg.save(os.path.join(ROOT, "assets", "ui", "loading_background.jpg"), quality=93)
    ship.save(os.path.join(ROOT, "assets", "ui", "loading_ship.png"))
    print("ship box", SHIP_BOX)


if __name__ == "__main__":
    main()
