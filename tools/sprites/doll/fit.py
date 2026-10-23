"""Fit the doll's build to a painted man's silhouettes before projected.py
paints him, so the paint of an arm lands on his arm and not his chest.

  blender -b out/doll.blend -P fit.py -- SHEET_DIR [out.blend] [--brim=ROW] [--chin=ROW]

--brim and --chin are the rows of the painted front frame where the hat's
brim crosses the brow and where the chin ends: the outline alone can't
tell where his face is. The painted side frame is often a three-quarter
view, not a profile; how far round it is turned is fitted too, and kept
on the rig for render.py.

The widths and depths of his body, arms, legs and head are scaled, and the
way his arms hang is turned, until his front, side and back silhouettes in
the idle pose overlap the painted frames' as well as they can; the build is
then baked into the rig's rest pose (as build.proportion does) and the arms'
hang kept on the rig for pose.arms_down.
"""
import math, os, sys
import bpy
import numpy as np
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import look, pose

args = sys.argv[sys.argv.index("--") + 1:]
opts = {a[2:].split("=")[0]: float(a.split("=")[1]) for a in args if a.startswith("--")}
args = [a for a in args if not a.startswith("--")]
SHEETS = os.path.abspath(args[0])
OUT = args[1] if len(args) > 1 else os.path.join(HERE, "out", "doll_fit.blend")
TMP = os.path.join(HERE, "out", "proj")
os.makedirs(TMP, exist_ok=True)
WEAR = ["body", "face", "moccasins", "trousers", "shirt", "breechcloth", "sash", "tuque"]
VIEWS = {"front": (0, "idle_front"), "right": (90, "idle_side"), "back": (180, "idle_back")}

sc = bpy.context.scene
look.setup_scene()
sc.render.engine = 'BLENDER_WORKBENCH'          # only the silhouette matters
rig = bpy.data.objects["rig"]
saved = {c.name: c.hide_render for c in bpy.data.collections}
for c in bpy.data.collections:
    c.hide_render = c.name not in WEAR and c.name != "rig"
for th in ("gun", "paddle", "packs", "food", "cup"):
    if th in bpy.data.objects:
        bpy.data.objects[th].hide_render = True


def read_alpha(path):
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, 3] > 0.5
    bpy.data.images.remove(im)
    return a


PAINT = {v: read_alpha(os.path.join(SHEETS, f + ".png")) for v, (_, f) in VIEWS.items()}

# Bones whose scale would otherwise carry on into a part fitted on its own.
for b in ("neck_01", "clavicle_l", "clavicle_r"):
    rig.data.bones[b].inherit_scale = 'NONE'

# name -> (start, step, apply(value))
SPINE = ("spine_01", "spine_02", "spine_03")


def scale(bones, axis):
    def f(v):
        for b in bones:
            s = list(rig.pose.bones[b].scale)
            s[axis] = v
            rig.pose.bones[b].scale = s
    return f


PARAMS = {
    "body_w": [1.0, 0.08, scale(SPINE, 0)],
    "body_d": [1.0, 0.08, scale(SPINE, 2)],
    "pelvis_w": [1.0, 0.08, scale(("pelvis",), 0)],
    "pelvis_d": [1.0, 0.08, scale(("pelvis",), 2)],
    "arm_w": [1.0, 0.1, scale(("upperarm_l", "upperarm_r"), 0)],
    "arm_d": [1.0, 0.1, scale(("upperarm_l", "upperarm_r"), 2)],
    "leg_w": [1.0, 0.08, scale(("thigh_l", "thigh_r"), 0)],
    "leg_d": [1.0, 0.08, scale(("thigh_l", "thigh_r"), 2)],
    "head": [1.0, 0.05, scale(("head",), 0)],
    "head_d": [1.0, 0.05, scale(("head",), 2)],
    "head_h": [1.0, 0.05, scale(("head",), 1)],
    "neck_h": [1.0, 0.1, scale(("neck_01",), 1)],
    "torso_h": [1.0, 0.05, scale(SPINE, 1)],
    "leg_h": [1.0, 0.05, scale(("thigh_l", "thigh_r", "calf_l", "calf_r"), 1)],
    "side_turn": [60.0, 10.0, None],
    "hang": [8.0, 4.0, None],
    "elbow": [12.0, 6.0, None],
    "swing": [0.0, 5.0, None],
}


# What a man can look like: the search stays inside these.
BOUNDS = {"hang": (0, 40), "elbow": (0, 40), "swing": (-30, 30), "side_turn": (20, 90)}  # the face shows: turned toward us, never away


def set_pose():
    p = pose.arms_down(rig, elbow=PARAMS["elbow"][0], hang=PARAMS["hang"][0])
    for s in "lr":
        p[f"upperarm_{s}"] = pose.fwd(PARAMS["swing"][0]) @ p[f"upperarm_{s}"]
    pose.apply(rig, p, 0.0)
    for k, (v, _, f) in PARAMS.items():
        if f:
            f(v)


def ground():
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    low = min(min((o.evaluated_get(dg).matrix_world @ v.co).z for v in o.evaluated_get(dg).data.vertices)
              for o in bpy.data.collections["body"].objects)
    pel = rig.pose.bones["pelvis"]
    pel.location += pel.bone.matrix_local.to_3x3().inverted() @ Vector((0, 0, -low))
    bpy.context.view_layer.update()


def rows_of(name, near_middle=None):
    """Projected pixel rows of an object's vertices (front view)."""
    dg = bpy.context.evaluated_depsgraph_get()
    e = bpy.data.objects[name].evaluated_get(dg)
    out = []
    for v in e.data.vertices:
        c = world_to_camera_view(sc, sc.camera, e.matrix_world @ v.co)
        if near_middle is None or abs(c.x * look.W - look.W / 2) < near_middle:
            out.append((1 - c.y) * look.H)
    return out


def score():
    set_pose()
    ground()
    tot = 0.0
    miss = 0.0
    for v, (turn, _) in VIEWS.items():
        if v == "right":
            turn = PARAMS["side_turn"][0]
        rig.rotation_euler = (0, 0, math.radians(turn))
        bpy.context.view_layer.update()
        if v == "front":
            # Where his face is, against the painted one.
            if "chin" in opts:
                miss += abs(max(rows_of("head", 6)) - opts["chin"])   # the head object holds the fists too
            if "brim" in opts:
                miss += abs(max(rows_of("tuque", 6)) - opts["brim"])
        sc.render.filepath = os.path.join(TMP, "fit.png")
        bpy.ops.render.render(write_still=True)
        d, p = read_alpha(sc.render.filepath), PAINT[v]
        tot += (d & p).sum() / max(1, (d | p).sum())
    rig.rotation_euler = (0, 0, 0)
    # A pixel off at the face counts like a point of overlap lost.
    return tot / len(VIEWS) - 0.01 * miss


best = score()
print(f"[fit] start score {best:.3f}")
for rnd in range(4):
    for k in PARAMS:
        for sign in (1, -1):
            while True:
                old = PARAMS[k][0]
                lo, hi = BOUNDS.get(k, (0.6, 2.0))
                PARAMS[k][0] = old + sign * PARAMS[k][1]
                if not lo <= PARAMS[k][0] <= hi:
                    PARAMS[k][0] = old
                    break
                s = score()
                if s > best + 1e-4:
                    best = s
                else:
                    PARAMS[k][0] = old
                    break
    for k in PARAMS:
        PARAMS[k][1] /= 2
    print(f"[fit] round {rnd} IoU {best:.3f} " + " ".join(f"{k}={v[0]:.2f}" for k, v in PARAMS.items()))


# ---------------------------------------------------------------- bake

# Only the build goes into the rest pose; the arms' hang is a pose.
pose.clear(rig)
for k, (v, _, f) in PARAMS.items():
    if f:
        f(v)
bpy.context.view_layer.update()


def select_only(o):
    for x in bpy.context.selected_objects:
        x.select_set(False)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)


for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    arm = next((m for m in o.modifiers if m.type == 'ARMATURE' and m.object == rig), None)
    if not arm:
        continue
    # Shape keys block modifier_apply; these meshes have none after build.py.
    select_only(o)
    keep = arm.name
    bpy.ops.object.modifier_copy(modifier=keep)
    bpy.ops.object.modifier_apply(modifier=keep)
select_only(rig)
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.select_all(action='SELECT')
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode='OBJECT')
for b in ("neck_01", "clavicle_l", "clavicle_r"):
    rig.data.bones[b].inherit_scale = 'FULL'
rig["hang"], rig["elbow"], rig["swing"] = PARAMS["hang"][0], PARAMS["elbow"][0], PARAMS["swing"][0]
rig["side_turn"] = PARAMS["side_turn"][0]

for c in bpy.data.collections:
    c.hide_render = saved[c.name]
for th in ("gun", "paddle", "packs", "food", "cup"):
    if th in bpy.data.objects:
        bpy.data.objects[th].hide_render = False
sc.render.engine = 'BLENDER_EEVEE_NEXT'
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("saved", OUT)
