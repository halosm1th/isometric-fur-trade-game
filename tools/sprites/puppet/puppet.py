"""Pin a set of cut pieces into a man and pose him: a quick look at the
cut-out idea before any of it goes into Godot.

  python puppet.py bare [out_dir]   -> idle and walk frames, front/side/back

Each piece hangs from its pivot. The skeleton puts each child's pivot at a
joint found on its parent (the bottom of the thigh is the knee, the top
corners of the torso the shoulders...), and a pose is an angle per joint.
"""
import json, math, os, sys
from PIL import Image, ImageEnhance

HERE = os.path.dirname(os.path.abspath(__file__))
W, H, FEET_Y, TALL = 180, 170, 167, 123
# Gemini drew the parts at a real man's build; the painted men are chunky,
# with big heads and short thick limbs. (across, down) per part.
SQUASH = {"head": (1.7, 1.6), "torso": (1.6, 1.0), "hips": (1.6, 0.9), "upper_arm": (1.45, 0.85),
          "forearm": (1.4, 0.62), "thigh": (1.5, 0.72), "shin": (1.45, 0.64)}


class Piece:
    def __init__(self, folder, key, part):
        spec = json.load(open(os.path.join(folder, "pieces.json")))[key]
        self.img = Image.open(os.path.join(folder, key + ".png")).convert("RGBA")
        self.pivot = list(spec["pivot"])
        kx, ky = SQUASH.get(part, (1.0, 1.0))
        self.img = self.img.resize((int(self.img.width * kx), int(self.img.height * ky)), Image.LANCZOS)
        self.pivot = [self.pivot[0] * kx, self.pivot[1] * ky]
        self.w, self.h = self.img.size

    def darker(self, f=0.7):
        p = Piece.__new__(Piece)
        p.__dict__ = dict(self.__dict__)
        p.img = ImageEnhance.Brightness(self.img).enhance(f)
        return p

    def mirrored(self):
        p = Piece.__new__(Piece)
        p.__dict__ = dict(self.__dict__)
        p.img = self.img.transpose(Image.FLIP_LEFT_RIGHT)
        p.pivot = [self.w - self.pivot[0], self.pivot[1]]
        return p


def rot(v, deg):
    a = math.radians(deg)
    # Image space, y down: a positive angle turns the piece anticlockwise
    # on screen, as PIL's rotate does.
    return (v[0] * math.cos(a) + v[1] * math.sin(a), -v[0] * math.sin(a) + v[1] * math.cos(a))


def add(a, b):
    return (a[0] + b[0], a[1] + b[1])


def stamp(canvas, piece, at, deg):
    """Draw `piece` with its pivot at `at`, turned by deg."""
    big = max(piece.w, piece.h) * 2 + 4
    tmp = Image.new("RGBA", (big, big))
    c = big // 2
    tmp.alpha_composite(piece.img, (int(c - piece.pivot[0]), int(c - piece.pivot[1])))
    tmp = tmp.rotate(deg, resample=Image.BICUBIC, center=(c, c))
    canvas.alpha_composite(tmp, (int(at[0] - c), int(at[1] - c)))


def load(folder, facing):
    parts = {}
    for part in ("head", "torso", "hips", "upper_arm", "forearm", "thigh", "shin"):
        parts[part] = Piece(folder, f"{facing}_{part}", part)
    return parts


def man(folder, facing, pose):
    """One frame: the pieces of `facing` in `pose` ({joint: degrees}, plus
    'lift' in sheet pixels), as a canvas with the waist at the centre."""
    P = load(folder, facing)
    size = 1400
    cv = Image.new("RGBA", (size, size))
    waist = (size / 2, size / 2 + pose.get("lift", 0))
    hips, torso, head = P["hips"], P["torso"], P["head"]
    ua, fa, th, sh = P["upper_arm"], P["forearm"], P["thigh"], P["shin"]
    side = facing == "side"
    lean = pose.get("lean", 0)
    # The torso hangs from the neck; its waist sits a little into the hips.
    neck = add(waist, rot((0, -(torso.h - torso.pivot[1]) + hips.h * 0.12 - torso.pivot[1] * 0), lean))
    neck = add(waist, rot((0, -(torso.h - torso.pivot[1] - hips.h * 0.12)), lean))
    hip_y = hips.h * 0.4 - hips.pivot[1]
    spread = 0 if side else hips.w * 0.24
    shoulder_y = torso.h * 0.14
    shoulder_x = 0 if side else torso.w / 2 - ua.w * 0.22

    def leg(sign, key):
        a_th = pose.get(f"thigh_{key}", 0)
        a_sh = a_th + pose.get(f"shin_{key}", 0)
        hip = add(waist, (sign * spread, hip_y))
        knee = add(hip, rot((0, th.h - th.pivot[1] - th.h * 0.12), a_th))
        t, s = (th, sh) if not (side and key == "far") else (th.darker(), sh.darker())
        if not side and sign < 0:
            t, s = t.mirrored(), s.mirrored()
        return [(s, knee, a_sh), (t, hip, a_th)]

    def arm(sign, key):
        a_ua = lean + pose.get(f"arm_{key}", 0)
        a_fa = a_ua + pose.get(f"elbow_{key}", 0)
        sh_pt = add(neck, rot((sign * shoulder_x, shoulder_y), lean))
        elbow = add(sh_pt, rot((0, ua.h - ua.pivot[1] - ua.h * 0.14), a_ua))
        u, f = (ua, fa) if not (side and key == "far") else (ua.darker(), fa.darker())
        if not side and sign < 0:
            u, f = u.mirrored(), f.mirrored()
        return [(u, sh_pt, a_ua), (f, elbow, a_fa)]

    order = []
    if side:
        order += arm(1, "far") + leg(1, "far") + leg(1, "near")
        order += [(hips, waist, 0), (torso, neck, lean), (head, neck, lean + pose.get("head", 0))]
        order += arm(1, "near")
    else:
        # Front and back: the right limbs are the mirror of the left.
        order += leg(1, "near") + leg(-1, "far") + [(hips, waist, 0), (torso, neck, lean)]
        order += arm(1, "near") + arm(-1, "far")
        order += [(head, neck, pose.get("head", 0))]
    for piece, at, deg in order:
        stamp(cv, piece, at, deg)
    return cv


def frame(folder, facing, pose, scale):
    cv = man(folder, facing, pose)
    box = cv.getbbox()
    small = cv.resize((int(cv.width * scale), int(cv.height * scale)), Image.LANCZOS)
    # Stand him at the frame's foot row: the lowest painted pixel of the
    # unlifted man is the sole.
    out = Image.new("RGBA", (W, H))
    feet = pose.get("_feet")
    ox = W / 2 - cv.width / 2 * scale
    oy = FEET_Y - feet * scale
    out.alpha_composite(small, (int(ox), int(oy)))
    return out


def walk(t, facing):
    s, c = math.sin(2 * math.pi * t), math.cos(2 * math.pi * t)
    if facing == "side":
        return {"thigh_near": 26 * s, "shin_near": -30 * max(0, -c), "thigh_far": -26 * s, "shin_far": -30 * max(0, c),
                "arm_near": -20 * s, "elbow_near": 12, "arm_far": 20 * s, "elbow_far": 12, "lift": -6 * abs(c) + 6}
    # Seen from front or back the legs step up and down and the arms swing
    # a little out.
    return {"thigh_near": 4 * s, "thigh_far": 4 * s, "arm_near": 4 + 4 * s, "arm_far": -4 + 4 * s, "lift": -8 * abs(s),
            "shin_near": 0, "shin_far": 0}


def main(name, out):
    folder = os.path.join(HERE, "parts", name)
    os.makedirs(out, exist_ok=True)
    # Size: the standing man, front on, made TALL pixels high.
    stand = man(folder, "front", {})
    x0, y0, x1, y1 = stand.getbbox()
    scale = TALL / (y1 - y0)
    for facing, suffix in (("front", ""), ("side", "_side"), ("back", "_back")):
        base = man(folder, facing, {})
        feet = base.getbbox()[3]
        p = {"_feet": feet, "arm_near": 3, "arm_far": -3} if facing != "side" else {"_feet": feet}
        frame(folder, facing, p, scale).save(os.path.join(out, f"idle{suffix}_0.png"))
        for i in range(4):
            p = walk(i / 4, facing)
            p["_feet"] = feet
            frame(folder, facing, p, scale).save(os.path.join(out, f"walk{suffix}_{i}.png"))
    print("frames ->", out, "scale", round(scale, 3))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "out", sys.argv[1]))
