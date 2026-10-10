"""Cut Gemini sprite sheets drawn on flat magenta into game frames.

  python slice.py detect SHEET [--merge N]
      Finds every figure, writes SHEET.contact.png (each numbered) and
      SHEET.parts.json beside it in tools/sprites/out/.

  python slice.py export SPEC.json
      SPEC: {"sheet": path, "out": dir, "canvas": [w, h], "height": px,
             "ref": id, "anims": {"walk": [ids...], ...}, "merge": N,
             "prefix": {"walk": "walk_front"} (optional file stem per anim),
             "flip": ["walk_side"] (optional: mirror these)}
      Scales the whole sheet so part `ref` (a standing idle) is `height`
      px tall, then puts each part on a `canvas` with its feet on the
      bottom edge and its centre of mass on the middle column, and saves
      out/<stem>_<n>.png.

Text labels (near-black, no colour) are dropped; pieces closer than
--merge px (dilation) count as one figure, so a dropped cup stays with
its drinker.
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")


def key_magenta(rgb):
    r, g, b = [rgb[..., i].astype(int) for i in range(3)]
    # Magenta and the JPEG mush round it: red and blue both well above green.
    bg = (r - g > 70) & (b - g > 70) & (r > 120) & (b > 120)
    return ~bg


def despill(rgba):
    """Edge pixels still tinted by the magenta they were blended with."""
    a = rgba[..., 3] > 0
    r, g, b = [rgba[..., i].astype(int) for i in range(3)]
    pink = (r - g > 35) & (b - g > 35)
    edge = a & ~ndimage.binary_erosion(a, iterations=2)
    kill = edge & pink
    rgba[kill, 3] = 0
    return rgba


def parts(path, merge=40, min_frac=0.2):
    """Figures are the big connected blobs. Small blobs are either letters
    of a label (dark, dropped) or bits of a figure that came loose - a cup,
    a dropped pipe - which join the nearest figure within `merge` px."""
    im = np.array(Image.open(path).convert("RGB"))
    fg = key_magenta(im)
    fg = ndimage.binary_opening(fg, iterations=1)
    lab, n = ndimage.label(fg)
    objs = ndimage.find_objects(lab)
    sizes = np.array([(lab[sl] == i).sum() if sl else 0 for i, sl in enumerate(objs, 1)])
    big_cut = max(400, np.percentile(sizes[sizes > 300], 75) * min_frac) if (sizes > 300).any() else 400
    figs, small = [], []
    for i, sl in enumerate(objs, 1):
        if sl is None or sizes[i - 1] < 40:
            continue
        box = [sl[1].start, sl[0].start, sl[1].stop, sl[0].stop]
        px = im[sl][lab[sl] == i]
        # Label letters are dark and colourless; sprites, even black-clad
        # ones, carry skin, cloth and highlights.
        dark = (px.max(axis=1) < 80).mean()
        sat = (px.max(axis=1).astype(int) - px.min(axis=1)).mean()
        mean = px.mean(axis=0)
        # ...or, anti-aliased into the magenta, a dark purple with no green.
        purple = mean[1] < 15 and abs(mean[0] - mean[2]) < 25 and mean[0] < 120
        text = (dark > 0.55 and sat < 30 or purple) and sizes[i - 1] < big_cut
        if text:
            continue
        if sizes[i - 1] >= big_cut:
            figs.append({"box": box, "labels": [i]})
        elif dark < 0.45:
            small.append((i, box))
    for i, b in small:
        best, bd = None, merge
        for f in figs:
            fb = f["box"]
            dx = max(fb[0] - b[2], b[0] - fb[2], 0); dy = max(fb[1] - b[3], b[1] - fb[3], 0)
            d = (dx * dx + dy * dy) ** 0.5
            if d < bd:
                best, bd = f, d
        if best:
            best["labels"].append(i)
            fb = best["box"]
            best["box"] = [min(fb[0], b[0]), min(fb[1], b[1]), max(fb[2], b[2]), max(fb[3], b[3])]
    out = [{"box": [int(v) for v in f["box"]], "labels": f["labels"]} for f in figs]
    # Reading order: rows by vertical centre, then left to right.
    out.sort(key=lambda p: ((p["box"][1] + p["box"][3]) / 2, p["box"][0]))
    rows, cur = [], []
    for p in out:
        cy = (p["box"][1] + p["box"][3]) / 2
        if cur and abs(cy - np.mean([(q["box"][1] + q["box"][3]) / 2 for q in cur])) > 45:
            rows.append(cur); cur = []
        cur.append(p)
    if cur:
        rows.append(cur)
    ordered = [p for r in rows for p in sorted(r, key=lambda p: p["box"][0])]
    for k, p in enumerate(ordered):
        p["id"] = k
    return im, fg, lab, ordered


def detect(path, merge=40):
    im, fg, lab, ps = parts(path, merge)
    os.makedirs(OUT, exist_ok=True)
    stem = os.path.splitext(os.path.basename(path))[0]
    img = Image.fromarray(im)
    d = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arialbd.ttf", 22)
    except OSError:
        font = ImageFont.load_default()
    for p in ps:
        x0, y0, x1, y1 = p["box"]
        d.rectangle([x0, y0, x1, y1], outline=(0, 255, 0), width=2)
        d.rectangle([x0, y0, x0 + 34, y0 + 24], fill=(255, 255, 0))
        d.text((x0 + 3, y0 + 1), str(p["id"]), fill=(0, 0, 0), font=font)
    img.save(os.path.join(OUT, stem + ".contact.png"))
    json.dump(ps, open(os.path.join(OUT, stem + ".parts.json"), "w"), indent=1)
    print(f"{len(ps)} parts -> {os.path.join(OUT, stem + '.contact.png')}")


def cut(im, fg, lab, p):
    x0, y0, x1, y1 = p["box"]
    mask = np.isin(lab[y0:y1, x0:x1], p["labels"]) & fg[y0:y1, x0:x1]
    rgba = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
    rgba[..., :3] = im[y0:y1, x0:x1]
    rgba[..., 3] = np.where(mask, 255, 0)
    return despill(rgba)


def export(spec_path):
    spec = json.load(open(spec_path))
    sheet = spec["sheet"]
    if not os.path.isabs(sheet):
        sheet = os.path.join(os.path.dirname(os.path.abspath(spec_path)), sheet)
    im, fg, lab, ps = parts(sheet, spec.get("merge", 40), spec.get("min_frac", 0.2))
    byid = {p["id"]: p for p in ps}
    W, H = spec.get("canvas", [150, 130])
    ref = byid[spec["ref"]]
    rh = cut(im, fg, lab, ref)
    ys = np.where(rh[..., 3].max(axis=1) > 0)[0]
    scale = spec.get("height", 123) / (ys.max() - ys.min() + 1)
    out = spec["out"]
    if not os.path.isabs(out):
        out = os.path.join(os.path.dirname(os.path.abspath(spec_path)), out)
    os.makedirs(out, exist_ok=True)
    flip = set(spec.get("flip", []))
    stems = spec.get("prefix", {})
    for anim, ids in spec["anims"].items():
        stem = stems.get(anim, anim)
        for n, i in enumerate(ids):
            if isinstance(i, list):   # several parts make one frame
                ims = [cut(im, fg, lab, byid[j]) for j in i]
                boxes = [byid[j]["box"] for j in i]
                X0 = min(b[0] for b in boxes); Y0 = min(b[1] for b in boxes)
                X1 = max(b[2] for b in boxes); Y1 = max(b[3] for b in boxes)
                rgba = np.zeros((Y1 - Y0, X1 - X0, 4), np.uint8)
                for a, b in zip(ims, boxes):
                    sub = rgba[b[1] - Y0:b[3] - Y0, b[0] - X0:b[2] - X0]
                    m = a[..., 3] > 0
                    sub[m] = a[m]
            else:
                rgba = cut(im, fg, lab, byid[i])
            frame = Image.fromarray(rgba)
            frame = frame.crop(frame.getbbox())
            w, h = frame.size
            frame = frame.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
            a = np.array(frame)
            a[..., 3] = np.where(a[..., 3] > 110, 255, 0)
            frame = Image.fromarray(a)
            if anim in flip:
                frame = frame.transpose(Image.FLIP_LEFT_RIGHT)
            w, h = frame.size
            if w > W or h > H:
                print(f"  ! {anim} {n}: {w}x{h} exceeds canvas {W}x{H}, shrinking to fit")
                f = min(W / w, H / h)
                frame = frame.resize((int(w * f), int(h * f)), Image.LANCZOS)
                w, h = frame.size
            alpha = np.array(frame)[..., 3] > 0
            cx = np.where(alpha.any(axis=0))[0]
            cols = alpha.sum(axis=0)
            mx = (cols * np.arange(w)).sum() / max(1, cols.sum())
            canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            x = int(round(W / 2 - mx)); x = min(max(x, 0), W - w)
            canvas.alpha_composite(frame, (x, H - h))
            name = f"{stem}_{n}.png" if len(ids) > 1 or spec.get("number_single") else f"{stem}.png"
            canvas.save(os.path.join(out, name))
    print(f"exported to {out} (scale {scale:.3f})")


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "detect":
        merge = int(sys.argv[sys.argv.index("--merge") + 1]) if "--merge" in sys.argv else 40
        detect(sys.argv[2], merge)
    elif cmd == "export":
        export(sys.argv[2])
