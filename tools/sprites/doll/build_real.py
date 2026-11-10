"""The same voyageur in the Project Zomboid manner: a man of ordinary
proportions with his own face, soft shading and no ink line.

  set DOLL_STYLE=soft, then: blender -b -P build_real.py -- [out.blend]

Uses build.py's garment cutting, props and layer collections; only the
body, the face and the sizes of things differ.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import look
import build as B
from bl_ext.user_default.mpfb.services.humanservice import HumanService

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 \
    else os.path.join(HERE, "out", "doll_real.blend")
HEIGHT = 1.66          # as tall on screen as the painted men (~123 px)
# A working man's build, a touch stylised so he reads among the painted
# buildings: the head a little large, chest, arms and thighs a little thick.
SHAPE = {"head": (1.15, 1.15, 1.15), "neck_01": (1.1, 1.0, 1.1), "spine_01": (1.08, 1.0, 1.05),
         "thigh_l": (1.1, 1.0, 1.1), "thigh_r": (1.1, 1.0, 1.1),
         "upperarm_l": (1.1, 1.0, 1.1), "upperarm_r": (1.1, 1.0, 1.1)}


def make_body():
    body = HumanService.create_human(macro_detail_dict={
        "gender": 1.0, "age": 0.5, "muscle": 0.75, "weight": 0.8, "proportions": 0.5,
        "height": 0.45, "cupsize": 0.5, "firmness": 0.5,
        "race": {"asian": 0.33, "caucasian": 0.34, "african": 0.33}})
    rig = HumanService.add_builtin_rig(body, "game_engine")
    body.name, rig.name = "body", "rig"
    bpy.context.view_layer.objects.active = body
    if body.data.shape_keys:
        body.shape_key_add(name="baked", from_mix=True)
        for kb in list(body.data.shape_keys.key_blocks)[:-1]:
            body.shape_key_remove(kb)
        body.shape_key_remove(body.data.shape_keys.key_blocks[0])
    for m in list(body.modifiers):
        if m.type == 'MASK':
            body.modifiers.remove(m)
    bpy.context.view_layer.update()
    # The eye markers are not skinned: the eyes are found again after the
    # head grows from the skin round each marker.
    real = B.members(body, "body")
    rings = {}
    for s in "lr":
        e = B.centroid(body, f"joint-{s}-eye")
        rings[s] = [v.index for v in body.data.vertices if v.index in real and ((body.matrix_world @ v.co) - e).length < 0.02]
    B.SHAPE = SHAPE
    B.proportion(body, rig)
    helpers = {"helper-skirt": B.split(body, "helper-skirt")}
    joints = {f"joint-{s}-eye": sum((body.matrix_world @ body.data.vertices[i].co for i in ring), Vector()) / len(ring)
              for s, ring in rings.items()}
    joints["mouth"] = B.centroid(body, "lips")
    B.only(body, "body")
    return body, rig, helpers, joints


def dress(body, rig, helpers, joints):
    waist = B.bone_z(rig, "spine_01") + 0.02
    knee = B.bone_z(rig, "calf_l")
    ankle = B.bone_z(rig, "foot_l")
    neck = B.bone_z(rig, "head")
    eye = joints["joint-l-eye"]
    mouth = joints["mouth"]
    top = max((body.matrix_world @ v.co).z for v in body.data.vertices)
    layers = {}

    # The face, brows and moustache are painted on (texture.py).
    hair = B.cut(body, "hair", 0.005, group="scalp", keep=lambda co: co.z > eye.z + 0.02)
    hair.data.materials.append(look.toon("hair", "#2a1e16"))
    layers["hair"] = [hair]

    shirt = B.cut(body, "shirt", 0.016, bones=B.SPINE | B.ARMS | {"pelvis", "neck_01"},
                  keep=lambda co: waist - 0.06 < co.z < neck - 0.02)
    shirt.data.materials.append(look.toon("gingham", None, check=("#e8eef8", "#7d9ad6", "#2e4fa8", 70)))
    layers["shirt"] = [shirt]

    trousers = B.cut(body, "trousers", 0.028, bones={"pelvis"} | B.THIGHS | B.CALVES,
                     keep=lambda co: waist + 0.02 > co.z > ankle + 0.12)
    trousers.data.materials.append(look.toon("red_wool", "#9c2a2c"))
    layers["trousers"] = [trousers]

    moc = B.cut(body, "moccasins", 0.01, bones=B.FEET | B.CALVES, keep=lambda co: co.z < ankle + 0.14)
    moc.data.materials.append(look.toon("buckskin", "#9a6a3a"))
    layers["moccasins"] = [moc]

    fy, by = B.front_back(body, waist - 0.05)
    flaps = []
    for nm, y, d in (("flap_front", fy - 0.02, -1), ("flap_back", by + 0.02, 1)):
        verts = [(-0.06, y, waist - 0.02), (0.06, y, waist - 0.02), (0.065, y + d * 0.01, waist - 0.24), (-0.065, y + d * 0.01, waist - 0.24)]
        o = B.weighted_mesh(nm, verts, [(0, 1, 2, 3)], "pelvis", rig)
        o.modifiers.new("thick", 'SOLIDIFY').thickness = 0.005
        flaps.append(o)
    flaps = B.join(flaps, "breechcloth")
    flaps.data.materials.append(look.toon("blue_stroud", "#36508c"))
    layers["breechcloth"] = [flaps]

    sash = B.cut(body, "sash", 0.034, bones=B.SPINE | {"pelvis"}, keep=lambda co: waist - 0.08 < co.z < waist + 0.04)
    B.heights(sash)
    sash.data.materials.append(look.toon("sash", None, bands=[(0.0, "#b3261e"), (0.3, "#e08a1e"), (0.42, "#b3261e"),
                                                                (0.62, "#e8c040"), (0.74, "#b3261e")]))
    layers["sash"] = [sash]

    cap = B.cut(body, "tuque", 0.016, bones={"head"}, keep=lambda co: co.z > eye.z + 0.022)
    hb = Vector((0.0, 0.01, top - 0.015))
    tip, droop = hb + Vector((0.0, 0.02, 0.09)), Vector((-0.13, 0.03, -0.16))
    tail = B.cone("tuque_tail", hb, tip, 0.085, 0.028, rig, "head", droop=droop)
    pom = B.blob("pompom", tip + droop + Vector((-0.01, 0, -0.015)), (0.025, 0.025, 0.025), rig, "head")
    cap = B.join([cap, tail, pom], "tuque")
    cap.data.materials.append(look.toon("red_wool_cap", "#b8262a"))
    layers["tuque"] = [cap]

    coat = B.cut(body, "capote", 0.034, bones=B.SPINE | B.ARMS | {"pelvis", "neck_01"},
                 keep=lambda co: waist - 0.12 < co.z < neck - 0.02)
    skirt = helpers.pop("helper-skirt")
    for v in skirt.data.vertices:
        v.co += v.normal * 0.034
    sk = B.cut(skirt, "capote_skirt", 0.0, keep=lambda co: knee - 0.02 < co.z < waist)
    bpy.data.objects.remove(skirt)
    coat = B.join([coat, sk], "capote")
    lo, hi = B.heights(coat)
    f = lambda z: (z - lo) / (hi - lo)
    coat.data.materials.append(look.toon("blanket", None, bands=[(0.0, "#e9e4d6"), (f(lo + 0.04), "#2a3f8c"),
                                                                 (f(lo + 0.07), "#e9e4d6"), (f(lo + 0.085), "#2a3f8c"),
                                                                 (f(lo + 0.105), "#e9e4d6")]))
    layers["capote"] = [coat]
    return layers


def main():
    B.clear()
    body, rig, helpers, joints = make_body()
    body.data.materials.append(look.toon("skin", "#c99572"))
    B.move_to(rig, B.collection("rig"))
    B.move_to(body, B.collection("body"))
    layers = dress(body, rig, helpers, joints)
    layers["gun"] = [B.gun()]
    layers["tools"] = B.tools()
    for name, objs in layers.items():
        c = B.collection(name)
        for o in objs:
            B.move_to(o, c)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            look.outline(o)
    tall = max((body.matrix_world @ v.co).z for v in body.data.vertices)
    k = HEIGHT / tall
    rig.scale = (k, k, k)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("built", OUT, f"height {tall:.2f} m, scaled {k:.2f}")


if __name__ == "__main__":
    main()
