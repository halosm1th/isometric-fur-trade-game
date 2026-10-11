"""Cut the HUD icon, crest and game-icon sheets into assets/ui/.

  python ui.py
"""
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(ROOT, "assets", "external", "NEW HBC ASSETS")
sys.path.insert(0, HERE)
import slice as sl

# sheet: [(parts, out path, size)]
CUTS = {
    "Icons": [
        ([0], "needs/health", 48), ([1], "needs/warmth", 48), ([2, 3, 4, 6], "needs/cold", 48), ([5], "needs/food", 48),
        ([22], "needs/water", 48), ([23], "needs/fatigue", 48), ([24], "needs/drunk", 48), ([25], "needs/fever", 48),
        ([42], "needs/scurvy", 48), ([43], "needs/wounded", 48), ([44], "needs/load", 48), ([45], "needs/standing", 48),
    ],
    "Faction Symbols": [
        ([0], "crests/hbc", 128), ([1], "crests/nwc", 128), ([2], "crests/cree", 128), ([11], "crests/assiniboine", 128),
        ([12], "icon", 256),
    ],
}


def run():
    for sheet, cuts in CUTS.items():
        im, fg, lab, ps = sl.parts(os.path.join(SRC, sheet + ".jpg"), merge=25)
        byid = {p["id"]: p for p in ps}
        for ids, name, size in cuts:
            boxes = [byid[i]["box"] for i in ids]
            X0 = min(b[0] for b in boxes); Y0 = min(b[1] for b in boxes)
            X1 = max(b[2] for b in boxes); Y1 = max(b[3] for b in boxes)
            # Whole box: thin dark lines (a flagpole, outlines) the text test would drop.
            rgba = np.zeros((Y1 - Y0, X1 - X0, 4), np.uint8)
            rgba[..., :3] = im[Y0:Y1, X0:X1]
            rgba[..., 3] = np.where(fg[Y0:Y1, X0:X1], 255, 0)
            rgba = sl.despill(rgba)
            img = Image.fromarray(rgba)
            img = img.crop(img.getbbox())
            f = (size - 2) / max(img.size)
            img = img.resize((max(1, round(img.width * f)), max(1, round(img.height * f))), Image.LANCZOS)
            a = np.array(img)
            a[..., 3] = np.where(a[..., 3] > 110, 255, 0)
            img = Image.fromarray(a)
            canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
            canvas.alpha_composite(img, ((size - img.width) // 2, (size - img.height) // 2))
            path = os.path.join(ROOT, "assets", "ui", name + ".png")
            os.makedirs(os.path.dirname(path), exist_ok=True)
            canvas.save(path)
    print("done")


if __name__ == "__main__":
    run()
