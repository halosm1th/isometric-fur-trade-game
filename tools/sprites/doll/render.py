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

FACING_SUFFIX = {"front": "", "side": "_side", "back": "_back"}
# anim: (frames, facings, gun hold, fps, loop)
ANIMS = {
    "idle": (1, ("front", "side", "back"), "carry", 5, True),
    "walk": (4, ("front", "side", "back"), "carry", 8, True),
    "shoot": (4, ("side",), "aim", 9, False),
}

rig = bpy.data.objects["rig"]
gun = bpy.data.objects["gun"]
sc = bpy.context.scene
look.setup_scene()


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


def arm_space(p):
    return rig.matrix_world @ p


def place(obj, m):
    obj.matrix_world = rig.matrix_world @ m


def hand_point(s):
    pb = rig.pose.bones[f"hand_{s}"]
    return (pb.head + pb.tail) / 2


def hold(mode, t):
    """Put the gun where `mode` holds it; returns the extra pose (aim)."""
    for c in IK.values():
        c.influence = 0.0
    if mode == "carry":
        # At the trail in the right hand, muzzle forward and down.
        bpy.context.view_layer.update()
        h = hand_point("r")
        place(gun, Matrix.Translation(h + Vector((-0.02, 0, 0.0))) @ Matrix.Rotation(math.radians(28), 4, 'X'))
        return
    # Aim: butt to the right shoulder, cheek down the barrel. The recoil
    # throws the muzzle up on the shot (frame 1) and it settles back.
    kick = (0.0, 0.0, 14.0, 6.0)[int(t * 4) % 4]
    sh = rig.pose.bones["upperarm_r"].bone.head_local
    grip = Vector((sh.x * 0.45, sh.y - 0.27 + kick * 0.003, sh.z - 0.06))
    m = Matrix.Translation(grip) @ Matrix.Rotation(math.radians(-kick), 4, 'X')
    place(gun, m)
    place(empty("ik_r"), m @ Matrix.Translation((0, 0.0, -0.03)))
    place(empty("ik_l"), m @ Matrix.Translation((0, -0.34, -0.03)))
    place(empty("pole_r"), Matrix.Translation(sh + Vector((-0.35, 0.25, -0.45))))
    shl = rig.pose.bones["upperarm_l"].bone.head_local
    place(empty("pole_l"), Matrix.Translation(shl + Vector((0.25, -0.1, -0.6))))
    for c in IK.values():
        c.influence = 1.0


def aim_pose(t):
    kick = (0.0, 0.0, 1.0, 0.4)[int(t * 4) % 4]
    p = {"thigh_l": pose.fwd(14), "thigh_r": pose.fwd(-10), "calf_l": pose.fwd(-6),
         "foot_l": pose.fwd(-8), "foot_r": pose.fwd(10),
         "spine_01": pose.twist(-12), "spine_03": pose.fwd(-6 * kick) @ pose.fwd(4),
         "head": pose.fwd(10) @ pose.twist(8)}
    return p, -0.01


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
    for name, (n, facings, mode, fps, loop) in ANIMS.items():
        if ONLY and name not in ONLY:
            continue
        for facing in facings:
            for i in range(n):
                # An animation drawn one way only (the shot) has no suffix.
                yield name + (FACING_SUFFIX[facing] if len(facings) > 1 else ""), facing, i, n, mode


def main():
    present = [l for l in ORDER if l in bpy.data.collections]
    manifest = {"order": present, "anims": {}}
    for anim, facing, i, n, mode in frames():
        t = i / n
        base = anim.split("_")[0]
        p, lift = pose.idle(rig, t) if base == "idle" else pose.walk(rig, t) if base == "walk" else aim_pose(t)
        pose.apply(rig, p, lift)
        rig.rotation_euler = (0, 0, math.radians(look.FACINGS[facing]))
        bpy.context.view_layer.update()
        hold(mode, t)
        bpy.context.view_layer.update()
        frames_, fps, loop = ANIMS[base][0], ANIMS[base][3], ANIMS[base][4]
        manifest["anims"][anim] = {"frames": frames_, "fps": fps, "loop": loop}
        if MODE == "preview":
            set_layers(present)
            render(os.path.join(OUT, f"{anim}_{i}.png"))
            continue
        for layer in present:
            set_layers([layer], holdout=() if layer == "body" else ("body",))
            render(os.path.join(OUT, layer, f"{anim}_{i}.png"))
    if MODE == "layers" and not ONLY:
        with open(os.path.join(OUT, "doll.json"), "w") as f:
            json.dump(manifest, f, indent=1)


main()
