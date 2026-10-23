"""PixelLab, driven from the doll: the painted voyageur posed by the 3D doll's
skeletons, and an outfit carried across the frames that come back.

  python pixellab.py balance
  python pixellab.py skeleton NAME REF.png REF_FRAME FRAME [FRAME ...]
        REF.png is a painted 180x170 frame; REF_FRAME and FRAMEs name
        skeletons in ../doll/out/skeletons.json (render.py skeleton mode),
        REF_FRAME the pose REF.png is in. Frames land in out/NAME_<i>.png.
  python pixellab.py outfit NAME OUTFIT.png FRAME.png [FRAME.png ...]
        OUTFIT.png: a sprite wearing the outfit; the FRAMEs are redrawn
        wearing it, to out/NAME_<i>.png.

The key is read from the PIXELLAB_API_KEY environment variable. Every
response is kept as out/NAME.json, so a run's cost and output can be read
back without paying for it again.
"""
import base64, io, json, os, sys, time, urllib.request, urllib.error
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
API = "https://api.pixellab.ai/v2"
W, H = 180, 170          # the game's frame
CANVAS = 192             # what PixelLab is sent: the frame on a square, feet kept low
PAD_X, PAD_Y = (CANVAS - W) // 2, CANVAS - H - 6
DESCRIPTION = ("a French-Canadian voyageur: red wool tuque with a tassel, black moustache, clay pipe, "
               "blue and white gingham shirt, red sash with an orange pattern, red trousers, "
               "blue breechcloth, tan leather boots")


def key():
    k = os.environ.get("PIXELLAB_API_KEY")
    if not k:
        sys.exit("PIXELLAB_API_KEY is not set")
    return k


def call(method, path, body=None):
    req = urllib.request.Request(API + path, method=method,
                                 data=json.dumps(body).encode() if body is not None else None,
                                 headers={"Authorization": "Bearer " + key(), "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return json.load(r)
    except urllib.error.HTTPError as e:
        sys.exit(f"{method} {path}: HTTP {e.code} {e.read().decode(errors='replace')[:800]}")


def b64(im):
    buf = io.BytesIO()
    im.save(buf, "PNG")
    return {"type": "base64", "base64": base64.b64encode(buf.getvalue()).decode(), "format": "png"}


def pad(path):
    """A 180x170 frame on the square canvas."""
    im = Image.open(path).convert("RGBA")
    c = Image.new("RGBA", (CANVAS, CANVAS))
    c.alpha_composite(im, (PAD_X, PAD_Y))
    return c


def unpad(im):
    return im.crop((PAD_X, PAD_Y, PAD_X + W, PAD_Y + H))


def joints(frame):
    sk = json.load(open(os.path.join(HERE, "..", "doll", "out", "skeletons.json")))[frame]
    return [{"label": j["label"], "x": (j["x"] * W + PAD_X) / CANVAS, "y": (j["y"] * H + PAD_Y) / CANVAS,
             "depth": j["depth"]} for j in sk]


def wait(name, job):
    jid = job.get("background_job_id") or job.get("id")
    print(f"[pixellab] {name}: job {jid}")
    while True:
        time.sleep(8)
        r = call("GET", f"/background-jobs/{jid}")
        if r["status"] != "processing":
            break
        print(f"[pixellab] {name}: {r['status']}...")
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name + ".json"), "w") as f:
        json.dump(r, f)
    if r["status"] != "completed":
        sys.exit(f"[pixellab] {name}: {r['status']} {json.dumps(r.get('last_response'))[:800]}")
    print(f"[pixellab] {name}: done, cost {r.get('usage')}")
    return r


def images(r):
    """Every image in a finished job's response, in order."""
    found = []

    def walk(x):
        if isinstance(x, dict):
            if "base64" in x:
                found.append(Image.open(io.BytesIO(base64.b64decode(x["base64"]))).convert("RGBA"))
                return
            for v in x.values():
                walk(v)
        elif isinstance(x, list):
            for v in x:
                walk(v)
    walk(r.get("last_response"))
    return found


def save(name, ims):
    for i, im in enumerate(ims):
        (unpad(im) if im.size == (CANVAS, CANVAS) else im).save(os.path.join(OUT, f"{name}_{i}.png"))
    print(f"[pixellab] {name}: {len(ims)} frames in {OUT}")


def skeleton(name, ref, ref_frame, frames):
    body = {"description": DESCRIPTION, "action": name, "direction": "south-east", "view": "low top-down",
            "first_frame": b64(pad(ref)), "first_frame_keypoints": joints(ref_frame),
            "keypoints": [joints(f) for f in frames], "no_background": True, "seed": 0}
    save(name, images(wait(name, call("POST", "/animate-with-skeleton-v3", body))))


def outfit(name, outfit_png, frames):
    ref = pad(outfit_png) if Image.open(outfit_png).size == (W, H) else Image.open(outfit_png).convert("RGBA")
    body = {"reference_image": {"image": b64(ref), "size": {"width": ref.width, "height": ref.height}},
            "frames": [{"image": b64(pad(f)), "size": {"width": CANVAS, "height": CANVAS}} for f in frames],
            "image_size": {"width": CANVAS, "height": CANVAS}, "no_background": True, "seed": 0,
            "additional_instructions": ("put the white wool blanket coat (capote) with blue stripes at the hem "
                                        "on the character, over his shirt; keep his red tuque, face, moustache, "
                                        "pipe, trousers and boots exactly as they are")}
    save(name, images(wait(name, call("POST", "/transfer-outfit-v2", body))))


if __name__ == "__main__":
    cmd, *a = sys.argv[1:]
    if cmd == "balance":
        print(call("GET", "/balance"))
    elif cmd == "skeleton":
        skeleton(a[0], a[1], a[2], a[3:])
    elif cmd == "outfit":
        outfit(a[0], a[1], a[2:])
