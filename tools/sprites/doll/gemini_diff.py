"""Lift a garment out of a Gemini sheet as the pixels that changed.

  python gemini_diff.py base.jpg dressed.jpg out_prefix

Both sheets are the same 4x2 grid of frames on magenta: base is the man
without the garment, dressed the same man with it. Each frame's garment
layer is what differs from the base (beyond JPEG noise), cleaned of specks,
keeping the dressed sheet's colours. Writes out_prefix_<i>.png per frame at
the sheet's own size, and out_prefix_check.png: the layer laid back on the
base next to the dressed frame.
"""
import sys
import numpy as np
from PIL import Image, ImageFilter

THRESH = 40          # summed RGB difference that counts as changed


def cells(path):
    im = Image.open(path).convert("RGB")
    W, H = im.size
    w, h = W / 4, H / 2
    return [np.array(im.crop((int(c * w) + 4, int(r * h) + 4, int((c + 1) * w) - 4, int((r + 1) * h) - 4)), dtype=int)
            for r in range(2) for c in range(4)]


def magenta(a):
    return (a[..., 0] > 170) & (a[..., 1] < 100) & (a[..., 2] > 170)


def layer(base, dressed):
    h = min(base.shape[0], dressed.shape[0]); w = min(base.shape[1], dressed.shape[1])
    base, dressed = base[:h, :w], dressed[:h, :w]
    diff = np.abs(base - dressed).sum(axis=2)
    mask = (diff > THRESH) & ~magenta(dressed)
    m = Image.fromarray((mask * 255).astype("uint8"))
    # Specks out, small holes in.
    m = m.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(3))
    mask = np.array(m) > 127
    rgba = np.dstack([dressed, np.where(mask, 255, 0)]).astype("uint8")
    return Image.fromarray(rgba, "RGBA"), base, dressed


def keyed(a):
    return Image.fromarray(np.dstack([a, np.where(magenta(a), 0, 255)]).astype("uint8"), "RGBA")


def main(base_path, dressed_path, prefix):
    bs, ds = cells(base_path), cells(dressed_path)
    rows = []
    for i, (b, d) in enumerate(zip(bs, ds)):
        lay, b, d = layer(b, d)
        lay.save(f"{prefix}_{i}.png")
        on = keyed(b)
        on.alpha_composite(lay)
        pair = Image.new("RGBA", (on.width * 3, on.height), (90, 110, 80, 255))
        pair.alpha_composite(on, (0, 0))
        pair.alpha_composite(keyed(d), (on.width, 0))
        pair.alpha_composite(lay, (on.width * 2, 0))
        rows.append(pair)
    sheet = Image.new("RGBA", (rows[0].width * 2, rows[0].height * 4), (90, 110, 80, 255))
    for i, r in enumerate(rows):
        sheet.alpha_composite(r, ((i % 2) * r.width, (i // 2) * r.height))
    sheet.thumbnail((1800, 1800))
    sheet.save(f"{prefix}_check.png")


if __name__ == "__main__":
    main(*sys.argv[1:4])
