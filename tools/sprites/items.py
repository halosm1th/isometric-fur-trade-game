"""Cut the Gemini item sheets (4 x 3 grids on magenta, a label under each
object) into 64x64 icons in assets/sprites/items/.

  python items.py [review]
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(ROOT, "assets", "external", "NEW HBC ASSETS")
OUT = os.path.join(ROOT, "assets", "sprites", "items")
sys.path.insert(0, HERE)
import slice as sl

SHEETS = {
    "Food Items": ["pemmican", "dried_goose", "salt_geese", "bread_loaf", "ale_tankard", "rum",
                   "water", "venison", "dried_meat", "goose_item", "fish", "hare"],
    "Trade Items": ["steel_trap", "kettle", "flint", "fishing_line", "pack_basket", "axe",
                    "musket", "bow", "knife", "tomahawk", "powder_horn", "fishing_net"],
    "Trade Items 2": ["beaver_pelt", "thin_pelt", "shot_pelt", "caribou_hide", "shot_hide", "wolf_pelt",
                      "capote", "blanket_pelt", "hide_coat", "trade_goods", "snowshoes", "tumpline"],
    "Trade Items 3": ["letter", "dispatch", "journal", "ledger", "mb_token", "tobacco",
                      "beads", "awl", "stroud", "ice_chisel", "pemmican_bag", "fur_pack"],
    # The scurvy sheet: what the country eats besides meat (design doc 13.5).
    "bery food items": ["berries", "corn", "spruce_beer"],
}

# Sheets that are not the usual four across and three down.
GRID = {"bery food items": (3, 1)}
SIZE, MARGIN = 64, 2


def cut_sheet(sheet, names):
    ncols, nrows = GRID.get(sheet, (4, 3))
    im = np.array(Image.open(os.path.join(SRC, sheet + ".jpg")).convert("RGB"))
    fg = sl.key_magenta(im)
    H, W = fg.shape
    icons = {}
    for i, name in enumerate(names):
        cx0, cy0 = int((i % ncols) * W / ncols), int((i // ncols) * H / nrows)
        cx1, cy1 = int((i % ncols + 1) * W / ncols), int((i // ncols + 1) * H / nrows)
        m = fg[cy0:cy1, cx0:cx1].copy()
        ch = cy1 - cy0
        # The label is the band at the bottom: cut at the highest empty gap
        # that still lies in the lower part of the cell.
        rows = m.sum(axis=1) > 2
        cut = ch
        y = ch - 1
        while y > 0:
            if not rows[y]:
                top = y
                while top > 0 and not rows[top - 1]:
                    top -= 1
                if y - top >= 3 and top > 0.55 * ch and rows[:top].any():
                    cut = top
                y = top - 1
            else:
                y -= 1
        m[cut:] = False
        from scipy import ndimage
        m = ndimage.binary_opening(m, iterations=1)
        lab, n = ndimage.label(m)
        if n == 0:
            print(f"  ! nothing found for {name}"); continue
        sizes = ndimage.sum(m, lab, range(1, n + 1))
        keep = np.isin(lab, [k + 1 for k, z in enumerate(sizes) if z >= max(60, sizes.max() * 0.01)])
        rgba = np.zeros(m.shape + (4,), np.uint8)
        rgba[..., :3] = im[cy0:cy1, cx0:cx1]
        rgba[..., 3] = np.where(keep, 255, 0)
        rgba = sl.despill(rgba)
        img = Image.fromarray(rgba)
        img = img.crop(img.getbbox())
        f = (SIZE - 2 * MARGIN) / max(img.size)
        img = img.resize((max(1, round(img.width * f)), max(1, round(img.height * f))), Image.LANCZOS)
        a = np.array(img)
        a[..., 3] = np.where(a[..., 3] > 110, 255, 0)
        img = Image.fromarray(a)
        canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        canvas.alpha_composite(img, ((SIZE - img.width) // 2, (SIZE - img.height) // 2))
        canvas.save(os.path.join(OUT, name + ".png"))
        icons[name] = canvas
    return icons


def main(review):
    allicons = {}
    for sheet, names in SHEETS.items():
        allicons.update(cut_sheet(sheet, names))
    if review:
        names = list(allicons)
        cols = 12
        img = Image.new("RGBA", (cols * 140, ((len(names) + cols - 1) // cols) * 110), (60, 60, 60, 255))
        d = ImageDraw.Draw(img)
        for k, n in enumerate(names):
            x, y = (k % cols) * 140, (k // cols) * 110
            img.alpha_composite(allicons[n].resize((96, 96), Image.NEAREST), (x + 22, y))
            d.text((x + 4, y + 96), n, fill=(255, 255, 255))
        img.save(os.path.join(HERE, "out", "review_items.png"))
    print(f"{len(allicons)} icons")


if __name__ == "__main__":
    main("review" in sys.argv)
