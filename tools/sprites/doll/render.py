"""Render the doll's layers for every animation frame and facing.

  blender -b out/doll.blend -P render.py -- [layers|preview] [anim ...]

layers  (default) each layer alone, the body held out so whatever is
        behind him is already cut away, to
        assets/sprites/doll/<layer>/<anim>_<i>.png
preview everything at once, to out/preview/<anim>_<i>.png

The names are the game's: idle, idle_side, idle_back, walk, walk_side,
walk_back (front has no suffix) and shoot (side only). Side frames face
right; the game flips them for left.
"""
import json, math, os, sys
import bpy
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)
import look, pose
from build import ORDER

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODE = args[0] if args and args[0] in ("layers", "preview") else "layers"
ONLY = [a for a in args if a not in ("layers", "preview")]
OUT = os.path.join(ROOT, "assets", "sprites", "doll") if MODE == "layers" else os.path.join(HERE, "out", "preview")

FACING_SUFFIX = {"front": "", "side": "_side", "back": "_back", "three": ""}

rig = bpy.data.objects["rig"]
sc = bpy.context.scene
look.setup_scene()
# A doll fitted to painted sheets (fit.py) turns as far as their side frame.
look.FACINGS["side"] = rig.get("side_turn", look.FACINGS["side"])


# ------------------------------------------------------------- the hands

def empty(name):
    e = bpy.data.objects.get(name)
    if not e:
        e = bpy.data.objects.new(name, None)
        sc.collection.objects.link(e)
    return e


IK = {}
for s in "lr":
    lower = rig.pose.bones[f"lowerarm_{s}"]
    c = lower.constraints.get("ik") or lower.constraints.new('IK')
    c.name = "ik"
    c.target = empty(f"ik_{s}")
    c.pole_target = empty(f"pole_{s}")
    c.pole_angle = math.radians(-90)
    c.chain_count = 2
    IK[s] = c


def place(obj, m):
    obj.matrix_world = rig.matrix_world @ m


def follow(bone, offset):
    """Where a point `offset` from the bone's head at rest is now (armature
    space), with the bone's turn: for things carried on the body."""
    pb = rig.pose.bones[bone]
    turn = pb.matrix.to_3x3() @ pb.bone.matrix_local.to_3x3().inverted()
    return Matrix.Translation(pb.head) @ turn.to_4x4() @ Matrix.Translation(Vector(offset))


def rest(bone):
    return rig.pose.bones[bone].bone.head_local


def grab(s, m, pole):
    """Put hand `s` at m (armature space), elbow toward `pole`."""
    place(empty(f"ik_{s}"), m)
    place(empty(f"pole_{s}"), Matrix.Translation(pole))
    IK[s].influence = 1.0


def mouth():
    return follow("head", (0, -0.24, 0.1))


def poles():
    return (rest("upperarm_r") + Vector((-0.4, 0.25, -0.4)), rest("upperarm_l") + Vector((0.4, 0.25, -0.4)))


def carry(t, i):
    # At the trail in the right hand, muzzle forward and down.
    pb = rig.pose.bones["hand_r"]
    h = (pb.head + pb.tail) / 2
    place(bpy.data.objects["gun"], Matrix.Translation(h + Vector((-0.02, 0, 0.0))) @ Matrix.Rotation(math.radians(28), 4, 'X'))


def aim(t, i):
    # Butt to the right shoulder, cheek down the barrel. The recoil throws
    # the muzzle up on the shot and it settles back.
    kick = (0.0, 0.0, 14.0, 6.0)[i]
    sh = rest("upperarm_r")
    grip = Vector((sh.x * 0.45, sh.y - 0.27 + kick * 0.003, sh.z - 0.06))
    m = Matrix.Translation(grip) @ Matrix.Rotation(math.radians(-kick), 4, 'X')
    place(bpy.data.objects["gun"], m)
    grab("r", m @ Matrix.Translation((0, 0.0, -0.03)), sh + Vector((-0.35, 0.25, -0.45)))
    grab("l", m @ Matrix.Translation((0, -0.34, -0.03)), rest("upperarm_l") + Vector((0.25, -0.1, -0.6)))


def paddle(t, i):
    # Top hand on the grip before his chest, the blade in the water at his
    # right: reach forward, pull, lift out at the hip, swing forward again.
    chest = rig.pose.bones["spine_03"].head
    reach = (-35, 0, 28, -12)[i]
    dy = (-0.12, 0.0, 0.12, -0.02)[i]
    up = (0.0, 0.0, 0.0, 0.08)[i]
    m = (Matrix.Translation(chest + Vector((-0.06, -0.24 + dy, 0.16 + up)))
         @ Matrix.Rotation(math.radians(-reach), 4, 'X') @ Matrix.Rotation(math.radians(24), 4, 'Y'))
    place(bpy.data.objects["paddle"], m)
    pr, pl = poles()
    grab("l", m @ Matrix.Translation((0, 0, -0.02)), pl)
    grab("r", m @ Matrix.Translation((0, 0, -0.4)), pr + Vector((0, -0.2, 0)))


def portage(t, i):
    # The load rides the small of his back; his hands steady the tumpline
    # at his brow.
    place(bpy.data.objects["packs"], follow("spine_01", (0, 0.13, 0.12)))
    pr, pl = poles()
    for s, sg, pole in (("r", -1, pr), ("l", 1, pl)):
        grab(s, follow("head", (sg * 0.17, -0.06, 0.2)), pole + Vector((0, 0, 0.5)))


def lerp(a, b, f):
    return Matrix.Translation(a.translation.lerp(b.translation, f))


def eat(t, i):
    m = mouth()
    low = Matrix.Translation(rest("spine_02") + Vector((-0.06, -0.3, 0)))
    at = m if i else lerp(low, m, 0.5)
    pr, pl = poles()
    grab("r", at @ Matrix.Translation((-0.04, 0.0, -0.02)), pr)
    grab("l", at @ Matrix.Translation((0.05, 0.01, -0.05)), pl)
    place(bpy.data.objects["food"], at @ Matrix.Translation((0, -0.03, 0)))


def drink(t, i):
    m = mouth()
    low = Matrix.Translation(rest("spine_02") + Vector((-0.1, -0.3, 0)))
    at = (lerp(low, m, 0.5), m, m, lerp(low, m, 0.6))[i]
    tip = (0, 30, 45, 10)[i]
    pr, pl = poles()
    grab("r", at @ Matrix.Translation((-0.03, 0.02, -0.04)), pr)
    place(bpy.data.objects["cup"], at @ Matrix.Translation((0, -0.01, 0)) @ Matrix.Rotation(math.radians(-tip), 4, 'X'))


def punch(t, i):
    sh = rest("upperarm_r")
    chest = rest("spine_03")
    at = (chest + Vector((-0.12, -0.25, 0.12)), chest + Vector((-0.2, -0.05, 0.08)),
          sh + Vector((0.05, -0.55, -0.02)), chest + Vector((-0.12, -0.25, 0.12)))[i]
    pr, pl = poles()
    grab("r", Matrix.Translation(at), pr)
    grab("l", Matrix.Translation(chest + Vector((0.1, -0.25, 0.22))), pl)


def stand(rig, t, i):
    return pose.idle(rig, t)


def kneel(rig, t, i):
    w = math.sin(2 * math.pi * t)
    p = pose.arms_down(rig)
    p.update({"thigh_l": pose.fwd(80), "calf_l": pose.fwd(-80),
              "thigh_r": pose.fwd(0), "calf_r": pose.fwd(-95), "foot_r": pose.fwd(50),
              "spine_01": pose.fwd(-8 - 6 * w) @ pose.twist(-15 + 10 * w),
              "head": pose.twist(15)})
    return p, 0.0


def stoop(rig, t, i):
    p, lift = pose.walk(rig, t)
    p["spine_01"] = pose.fwd(-16)
    p["spine_03"] = pose.fwd(0)
    p["head"] = pose.fwd(12)
    return p, lift


def fight(rig, t, i):
    p = pose.arms_down(rig)
    p.update({"thigh_l": pose.fwd(15), "thigh_r": pose.fwd(-12), "calf_r": pose.fwd(-6),
              "spine_01": pose.twist((0, -12, 22, 5)[i])})
    return p, 0.0


def aim_pose(rig, t, i):
    kick = (0.0, 0.0, 1.0, 0.4)[i]
    p = {"thigh_l": pose.fwd(14), "thigh_r": pose.fwd(-10), "calf_l": pose.fwd(-6),
         "foot_l": pose.fwd(-8), "foot_r": pose.fwd(10),
         "spine_01": pose.twist(-12), "spine_03": pose.fwd(6 * kick),
         "head": pose.fwd(-10) @ pose.twist(8)}
    return p, 0.0


def fall(rig, t, i):
    # Hit, the knees go, he goes over backward and lies on his back.
    p = pose.arms_down(rig)
    p.update({"pelvis": pose.fwd((8, 35, 65, 90)[i]),
              "thigh_l": pose.fwd((0, 35, 25, 6)[i]), "thigh_r": pose.fwd((0, 28, 15, 3)[i]),
              "calf_l": pose.fwd((0, -55, -35, -8)[i]), "calf_r": pose.fwd((0, -45, -25, -5)[i]),
              "spine_01": pose.fwd((14, 10, 5, 0)[i]), "head": pose.fwd((12, 8, 0, -12)[i])})
    for s in "lr":
        p[f"upperarm_{s}"] = pose.out(s, (25, 45, 60, 70)[i] - pose.rest_spread(rig, s)) @ pose.fwd((-20, -30, -10, 0)[i])
    return p, 0.0


THREE = ("three",)
# anim: (frames, facings, fps, loop, pose(rig, t, i), hands(t, i), things shown)
ANIMS = {
    "idle": (1, ("front", "side", "back"), 5, True, stand, carry, ["gun"]),
    "walk": (4, ("front", "side", "back"), 8, True, lambda r, t, i: pose.walk(r, t), carry, ["gun"]),
    "shoot": (4, ("side",), 9, False, aim_pose, aim, ["gun"]),
    "paddle": (4, THREE, 6, True, kneel, paddle, ["paddle"]),
    "portage": (4, THREE, 6, True, stoop, portage, ["packs"]),
    "eat": (4, THREE, 5, False, stand, eat, ["food"]),
    "drink": (4, THREE, 5, False, stand, drink, ["cup"]),
    "attack": (4, THREE, 9, False, fight, punch, []),
    "death": (4, ("side",), 5, False, fall, None, []),
}
THINGS = ["gun", "paddle", "packs", "food", "cup"]


def ground():
    """Lift or lower him so the lowest point of him touches the ground."""
    dg = bpy.context.evaluated_depsgraph_get()
    low = 1e9
    for o in bpy.data.collections["body"].objects:
        e = o.evaluated_get(dg)
        low = min(low, min((e.matrix_world @ v.co).z for v in e.data.vertices))
    pel = rig.pose.bones["pelvis"]
    pel.location += pel.bone.matrix_local.to_3x3().inverted() @ Vector((0, 0, -low))
    bpy.context.view_layer.update()


# ------------------------------------------------------------- rendering

def set_layers(show, holdout=()):
    for lc in bpy.context.view_layer.layer_collection.children:
        n = lc.name
        if n == "rig":
            continue
        lc.collection.hide_render = n not in show and n not in holdout
        lc.holdout = n in holdout


def render(path):
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def frames():
    for name, (n, facings, fps, loop, *_) in ANIMS.items():
        if ONLY and name not in ONLY:
            continue
        for facing in facings:
            for i in range(n):
                # An animation drawn one way only has no suffix.
                yield name, name + (FACING_SUFFIX[facing] if len(facings) > 1 else ""), facing, i, n


def main():
    present = [l for l in ORDER if l in bpy.data.collections]
    manifest = {"order": present, "anims": {}}
    for base, anim, facing, i, n in frames():
        t = i / n
        posefn, hands, things = ANIMS[base][4:]
        for c in IK.values():
            c.influence = 0.0
        p, lift = posefn(rig, t, i)
        pose.apply(rig, p, lift)
        rig.rotation_euler = (0, 0, math.radians(look.FACINGS[facing]))
        bpy.context.view_layer.update()
        look.painted_view(facing)
        ground()
        if hands:
            hands(t, i)
        for th in THINGS:
            if th in bpy.data.objects:
                bpy.data.objects[th].hide_render = th not in things
        bpy.context.view_layer.update()
        if MODE == "preview":
            set_layers(present)
            render(os.path.join(OUT, f"{anim}_{i}.png"))
            continue
        for layer in present:
            set_layers([layer], holdout=() if layer == "body" else ("body",))
            render(os.path.join(OUT, layer, f"{anim}_{i}.png"))
    if MODE == "layers":
        # Every animation is listed, rendered this time or not.
        for name, (n, facings, fps, loop, *_) in ANIMS.items():
            for facing in facings:
                anim = name + (FACING_SUFFIX[facing] if len(facings) > 1 else "")
                manifest["anims"][anim] = {"frames": n, "fps": fps, "loop": loop}
        with open(os.path.join(OUT, "doll.json"), "w") as f:
            json.dump(manifest, f, indent=1)


main()
