"""Every role's common actions redrawn by PixelLab from its painted frames,
posed by the 3D doll, so that everyone can do everything:

  python roles.py make ROLE [ACTION ...]    (all of ROLES' actions if none)
  python roles.py install ROLE              -> assets/sprites/<dir>_pl, Scenes/.../<Role>PLFrames.tres
  python roles.py check ROLE                -> out/<role>/check.png, painted beside new

For each painted idle frame (front, three-quarter side, back) PixelLab
estimates the skeleton the figure stands in; the doll's motion for an
action (../doll/out/skeletons.json, render.py skeleton mode) is laid onto
it as each joint's movement from the doll's own standing pose, scaled to
the figure's height. The role's other animations (writing, smoking,
carrying wood...) stay painted. A job already done (out/<role>/<action>.json)
is skipped. The key is read from PIXELLAB_API_KEY.
"""
import json, os, re, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, ".."))
import pixellab as pl
from characters import parse_tres

CANVAS = 192            # what PixelLab is sent
EST = 256               # the skeleton estimator's canvas
DOLL_W, DOLL_H, DOLL_TALL = 180, 170, 122   # the doll's frames, and the voyageur's height in them

# role: (sheet, what they look like). The props come with each action.
ROLES = {
    "player": ("Scenes/Player/PlayerFrames.tres", "a Hudson's Bay Company man: black tricorn hat, dark beard, long navy blue coat with brass buttons, a yellow sash with a pouch, brown boots"),
    "labourer": ("Scenes/Npc/LabourerFrames.tres", "a stout Orkney labourer: black tricorn hat, thick black beard, dark green wool coat, leather belt, blue breeches, brown boots"),
    "clerk": ("Scenes/Npc/ClerkFrames.tres", "a young clerk: fair hair tied back, navy blue tailcoat, grey waistcoat, white cravat, black breeches, grey stockings, black shoes"),
    "factor": ("Scenes/Npc/FactorFrames.tres", "an elderly chief factor: black tricorn hat, grey hair, long grey greatcoat, dark waistcoat, white cravat, dark green breeches, grey stockings, buckled shoes"),
    "sailor": ("Scenes/Npc/SailorFrames.tres", "a sailor: round black hat, ruddy face with dark sideburns, short navy blue jacket, red waistcoat, black neckerchief, white canvas trousers, black shoes"),
    "rival": ("Scenes/Npc/RivalFrames.tres", "a North West Company man: black tricorn hat, dark beard, long dark red coat, brown boots"),
    "nwc_partner": ("Scenes/Npc/NwcPartnerFrames.tres", "a North West Company partner: black top hat, red hair and beard, long dark green coat, yellow waistcoat, red sash, grey trousers, black boots"),
    "country_wife": ("Scenes/Npc/CountryWifeFrames.tres", "a Metis woman: long black braids, red plaid shawl with a white collar, blue calico dress with white flowers, beaded pouch, moccasins"),
    "child_boy": ("Scenes/Npc/ChildBoyFrames.tres", "a Metis boy: black hair, white wool capote with blue stripes at the hem, red sash, blue leggings, moccasins"),
    "child_girl": ("Scenes/Npc/ChildGirlFrames.tres", "a Metis girl: black braids, grey shawl, red dress with white flowers, blue leggings, moccasins"),
    "cree": ("Scenes/Npc/CreeFrames.tres", "a Cree man: long black hair with an eagle feather, fringed buckskin shirt and leggings with quillwork, red breechcloth, moccasins"),
    "cree_woman": ("Scenes/Npc/CreeWomanFrames.tres", "a Cree woman: long black braids with a feather, long fringed brown hide dress with beadwork, moccasins"),
    "cree_captain": ("Scenes/Npc/CreeCaptainFrames.tres", "an elderly Cree trading captain: long grey hair with feathers, red officer's coat with gold trim, white shirt, blue trousers, bead necklace, moccasins"),
    "assiniboine": ("Scenes/Npc/AssiniboineFrames.tres", "an Assiniboine man: dark hair with a feather, fringed buckskin shirt with quilled strips, red breechcloth, moccasins"),
    "blackfoot": ("Scenes/Npc/BlackfootFrames.tres", "a Blackfoot man: long dark hair tied with red cloth, buckskin shirt, a buffalo robe over one shoulder, red-trimmed breechcloth, moccasins"),
    "coast": ("Scenes/Npc/CoastFrames.tres", "a Pacific coast man: woven cedar-bark conical hat, red face paint, cedar-bark cape, shell necklace, red and black painted apron"),
    "dakota": ("Scenes/Npc/DakotaFrames.tres", "a Dakota man: long hair with a feather and red wraps, a brown buffalo robe, buckskin leggings, moccasins"),
    "dene": ("Scenes/Npc/DeneFrames.tres", "a Dene man: long black hair, pale caribou-hide tunic pointed at front and back with red and quill trim, hide leggings and moccasins"),
    "mandan": ("Scenes/Npc/MandanFrames.tres", "a Mandan man: two upright eagle feathers in his hair, fringed buckskin shirt with quillwork, red breechcloth, moccasins"),
    "ojibwe": ("Scenes/Npc/OjibweFrames.tres", "an Ojibwe man: long hair with a feather and red wraps, buckskin shirt, a bandolier bag with floral beadwork, moccasins"),
    "plateau": ("Scenes/Npc/PlateauFrames.tres", "a Plateau man: woven brimmed basket hat, fringed buckskin shirt, dentalium shell necklace, bow and quiver on his back, moccasins"),
}

# action: (painted idle it starts from, doll frames, direction, motion label, in the hands, speed, loop)
ACTIONS = {
    "walk": ("idle", "walk", "south", "walk", "", 8, True),
    "walk_back": ("idle_back", "walk_back", "north", "walk", "", 8, True),
    "walk_side": ("idle_side", "walk_side", "south-east", "walk", "", 8, True),
    "shoot": ("idle_side", "shoot", "south-east", "shoot a musket", "a long flintlock trade musket, butt at the shoulder", 9, False),
    "attack": ("idle_side", "attack", "south-east", "punch", "nothing, fists bare", 9, False),
    "death": ("idle_side", "death", "south-east", "fall down dead", "nothing", 5, False),
    "eat": ("idle_side", "eat", "south-east", "eat bread", "a big round loaf of brown bannock bread in both hands, biting it", 5, False),
    "drink": ("idle_side", "drink", "south-east", "drink from a cup", "a large shiny tin cup in the right hand, tipped up to the lips", 5, False),
    "paddle": ("idle_side", "paddle", "south-east", "paddle", "a long wooden canoe paddle with a red blade, gripped in both hands, the blade down in the water at the side", 6, True),
    "portage": ("idle_side", "portage", "south-east", "carry a heavy load", "two bales of fur packs on the back, held by a leather tumpline across the forehead", 6, True),
}
DOLL_IDLE = {"idle": "idle_0", "idle_side": "idle_side_0", "idle_back": "idle_back_0"}
# Whose painted side frame is turned three-quarters toward us, like the
# voyageur's; the rest are drawn in profile, facing east.
THREE_QUARTER = {"player", "factor", "rival", "cree_woman", "cree_captain", "assiniboine", "blackfoot", "coast"}


def sheet(role):
    path = os.path.join(ROOT, ROLES[role][0])
    return path, parse_tres(path)


# A sheet without a side or back idle (the rival's) starts from its walk's
# first frame, or else from the front.
FALLBACK = {"idle_side": ("walk_side", "idle"), "idle_back": ("walk_back", "idle")}

# Side actions other than the walk that start from another painted frame:
# the NWC partner's painted side is seen from behind, while his painted
# actions face the front three-quarter, as his talk does; the Cree captain's
# side idle holds his calumet, which PixelLab then holds in every action,
# and his side walk does not; the Cree woman's side idle has her arms folded
# into her robe, so she shot and ate empty-handed.
SIDE_REF = {"nwc_partner": "talk", "cree_captain": "walk_side", "cree_woman": "walk_side"}


def ref_anim(role, action):
    idle = ACTIONS[action][0]
    if idle == "idle_side" and action != "walk_side" and role in SIDE_REF:
        return SIDE_REF[role]
    return idle


def painted(role, anim):
    anims = sheet(role)[1]
    for a in (anim,) + FALLBACK.get(anim, ()):
        if a in anims:
            return os.path.join(ROOT, anims[a][2][0].replace("res://", ""))


def out(role, *p):
    d = os.path.join(HERE, "out", role)
    os.makedirs(d, exist_ok=True)
    return os.path.join(d, *p)


def on_square(im, size):
    """The frame on a square canvas, centred, its bottom edge 6 px up."""
    sq = Image.new("RGBA", (size, size))
    ox, oy = (size - im.width) // 2, size - im.height - 6
    sq.alpha_composite(im, (ox, oy))
    return sq, ox, oy


def height(im):
    a = im.getchannel("A").point(lambda v: 255 if v > 127 else 0)
    bb = a.getbbox()
    return bb[3] - bb[1]


def reference(role, idle):
    """The painted idle frame, and the skeleton PixelLab finds in it, in the
    frame's own pixels (cached)."""
    im = Image.open(painted(role, idle)).convert("RGBA")
    cache = out(role, f"skeleton_{idle}.json")
    if not os.path.exists(cache):
        sq, ox, oy = on_square(im, EST)
        r = pl.call("POST", "/estimate-skeleton", {"image": pl.b64(sq)})
        kp = [{"label": k["label"], "x": k["x"] * (EST if k["x"] <= 1 else 1) - ox, "y": k["y"] * (EST if k["y"] <= 1 else 1) - oy}
              for k in r["keypoints"]]
        json.dump(kp, open(cache, "w"), indent=1)
    return im, {k["label"]: k for k in json.load(open(cache))}


def motion(role, action):
    """The reference skeleton and one per frame, in CANVAS fractions: the
    doll's joints' movement from its standing pose laid onto the figure."""
    idle, doll_anim, *_ = ACTIONS[action]
    idle_ref = ref_anim(role, action)
    im, ref = reference(role, idle_ref)
    doll = json.load(open(os.path.join(HERE, "..", "doll", "out", "skeletons.json")))
    stand = {j["label"]: j for j in doll[DOLL_IDLE[idle]]}
    s = height(im) / DOLL_TALL
    ox, oy = (CANVAS - im.width) // 2, CANVAS - im.height - 6
    # Joints the estimator missed stand where the doll's do, scaled about the feet.
    for label, j in stand.items():
        if label not in ref:
            ref[label] = {"label": label, "x": im.width / 2 + (j["x"] * DOLL_W - DOLL_W / 2) * s,
                          "y": im.height - (DOLL_H - j["y"] * DOLL_H) * s}

    def kp(frame):
        return [{"label": j["label"],
                 "x": (ref[j["label"]]["x"] + (j["x"] - stand[j["label"]]["x"]) * DOLL_W * s + ox) / CANVAS,
                 "y": (ref[j["label"]]["y"] + (j["y"] - stand[j["label"]]["y"]) * DOLL_H * s + oy) / CANVAS,
                 "depth": j["depth"]} for j in frame]
    start = [{"label": l, "x": (r["x"] + ox) / CANVAS, "y": (r["y"] + oy) / CANVAS, "depth": stand[l]["depth"]}
             for l, r in ref.items() if l in stand]
    frames = [kp(doll[f"{doll_anim}_{i}"]) for i in range(4)]
    return im, start, frames


def make(role, actions):
    desc = ROLES[role][1]
    for action in actions:
        if os.path.exists(out(role, action + ".json")):
            print(f"[roles] {role} {action}: already done")
            continue
        idle, _, direction, label, holding, *_ = ACTIONS[action]
        if direction == "south-east" and role not in THREE_QUARTER and role not in SIDE_REF:
            direction = "east"
        im, start, frames = motion(role, action)
        sq, ox, oy = on_square(im, CANVAS)
        body = {"description": desc + (", holding " + holding if holding else ""), "action": label,
                "direction": direction, "view": "low top-down", "first_frame": pl.b64(sq),
                "first_frame_keypoints": start, "keypoints": frames, "no_background": True, "seed": int(os.environ.get("PL_SEED", 0))}
        name = f"{role}/{action}"
        r = pl.wait(name.replace("/", "_"), pl.call("POST", "/animate-with-skeleton-v3", body))
        os.replace(os.path.join(pl.OUT, name.replace("/", "_") + ".json"), out(role, action + ".json"))
        for i, fr in enumerate(pl.images(r)[:4]):
            fr = fr.crop((ox, oy, ox + im.width, oy + im.height))
            a = fr.getchannel("A").point(lambda v: 255 if v > 127 else 0)
            fr.putalpha(a)
            fr.save(out(role, f"{action}_{i}.png"))
        print(f"[roles] {role} {action}: 4 frames")


def install(role):
    path, anims = sheet(role)
    folder = os.path.basename(os.path.dirname(anims["idle"][2][0].replace("res://", "")))
    dst = os.path.join(ROOT, "assets", "sprites", folder + "_pl")
    os.makedirs(dst, exist_ok=True)
    done = [a for a in ACTIONS if all(os.path.exists(out(role, f"{a}_{i}.png")) for i in range(4))]
    for a in done:
        for i in range(4):
            Image.open(out(role, f"{a}_{i}.png")).save(os.path.join(dst, f"{a}_{i}.png"))
        speed, loop = ACTIONS[a][5], ACTIONS[a][6]
        if a in anims:
            speed, loop = anims[a][0], anims[a][1] == "true"
        anims[a] = (float(speed), "true" if loop else "false",
                    [f"res://assets/sprites/{folder}_pl/{a}_{i}.png" for i in range(4)])
    res, blocks = [], []
    for name, (speed, loop, tex) in anims.items():
        for p in tex:
            if p not in res:
                res.append(p)
        frames = ",\n".join('{\n"duration": 1.0,\n"texture": ExtResource("t%d")\n}' % (res.index(p) + 1) for p in tex)
        blocks.append('{\n"name": &"%s",\n"speed": %s,\n"loop": %s,\n"frames": [%s]\n}' % (name, speed, loop, frames))
    head = f'[gd_resource type="SpriteFrames" load_steps={len(res) + 1} format=3]\n\n'
    head += "".join(f'[ext_resource type="Texture2D" path="{p}" id="t{i + 1}"]\n' for i, p in enumerate(res))
    with open(path.replace("Frames.tres", "PLFrames.tres"), "w", encoding="utf-8") as f:
        f.write(head + "\n[resource]\nanimations = [" + ", ".join(blocks) + "]\n")
    print(f"[roles] {role}: installed {done}")


def check(role):
    """Painted idles, then every action's frames, one row each."""
    rows = [[painted(role, a) for a in DOLL_IDLE]] + [[out(role, f"{a}_{i}.png") for i in range(4)] for a in ACTIONS]
    w, h = Image.open(painted(role, "idle")).size
    o = Image.new("RGBA", (w * 4, h * len(rows)), (90, 110, 80, 255))
    for r, row in enumerate(rows):
        for c, p in enumerate(row):
            if os.path.exists(p):
                o.alpha_composite(Image.open(p).convert("RGBA"), (c * w, r * h))
    o.save(out(role, "check.png"))


if __name__ == "__main__":
    cmd, role, *rest = sys.argv[1:]
    roles = list(ROLES) if role == "all" else [role]
    for r in roles:
        {"make": lambda: make(r, rest or list(ACTIONS)), "install": lambda: install(r), "check": lambda: check(r)}[cmd]()
