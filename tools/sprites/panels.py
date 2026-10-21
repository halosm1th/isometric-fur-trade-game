"""Cut the UI sheet into the furniture of the menus, in assets/ui/panels/.

  python panels.py [review]

These become nine-patch styleboxes (MenuTheme), so what matters is that the
corners and the border come out whole: the boxes below are the art's own
edges, read off tools/sprites/out/Panels.contact.png. The dark panel and
the divider are given by hand because the figure finder takes dark wood and
a thin iron rule for lettering.
"""
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(ROOT, "assets", "external", "NEW HBC ASSETS")
OUT = os.path.join(ROOT, "assets", "ui", "panels")
sys.path.insert(0, HERE)
import slice as sl

CUTS = {
    "panel_paper": (70, 38, 667, 352),      # parchment with brass corners
    "panel_wood": (727, 36, 1320, 357),     # dark boards with iron straps
    "button": (98, 442, 249, 590),          # tan leather
    "button_pressed": (298, 442, 450, 584),  # dark leather
    "button_disabled": (500, 442, 650, 589),
    "scrollbar": (845, 419, 890, 698),
    "tab_off": (1005, 511, 1134, 615),
    "tab_on": (1166, 502, 1299, 618),
    "divider": (45, 652, 712, 700),
}


def run(review=False):
    os.makedirs(OUT, exist_ok=True)
    im = np.array(Image.open(os.path.join(SRC, "Panels.jpg")).convert("RGB"))
    made = []
    for name, (x0, y0, x1, y1) in CUTS.items():
        sub = im[y0:y1, x0:x1]
        rgba = np.zeros(sub.shape[:2] + (4,), np.uint8)
        rgba[..., :3] = sub
        rgba[..., 3] = np.where(sl.key_magenta(sub), 255, 0)
        a = sl.despill(rgba)
        # The divider is a thin iron rule: nearly every pixel of it is an
        # edge, so the ordinary despill has little to bite on. Anything left
        # that is still pink is background.
        if name == "divider":
            r, g, b = [a[..., i].astype(int) for i in range(3)]
            a[(r - g > 28) & (b - g > 28), 3] = 0
        img = Image.fromarray(a)
        bb = img.getbbox()
        if bb:
            img = img.crop(bb)
        img.save(os.path.join(OUT, name + ".png"))
        made.append(f"{name} {img.width}x{img.height}")
    print("; ".join(made))
    if review:
        tiles = [Image.open(os.path.join(OUT, n + ".png")).convert("RGBA") for n in CUTS]
        W = max(t.width for t in tiles) + 10
        H = sum(t.height + 10 for t in tiles)
        out = Image.new("RGBA", (W, H), (30, 32, 40, 255))
        y = 0
        for t in tiles:
            out.alpha_composite(t, (5, y + 5)); y += t.height + 10
        out.save(os.path.join(HERE, "out", "review_panels.png"))


if __name__ == "__main__":
    run("review" in sys.argv)
