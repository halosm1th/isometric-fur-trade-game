"""A scene with a MakeHuman (MPFB) woman: caucasian, brunette, lit for a
full-length portrait in Cycles.

  blender -b -P woman.py -- [out.blend]

Everything she wears is a MakeHuman asset from the library in
gemini_test/data, fitted and rigged by MPFB: skin, eyes, brows, lashes,
culturalibre_hair_13 and the medieval dress. The body keeps its targets
as live shape keys, so she can still be reshaped in MPFB's Model panel
and the assets refit with her.
"""
import math, os, sys
import bpy
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build as B
from bl_ext.user_default.mpfb.services.humanservice import HumanService

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 \
    else os.path.join(HERE, "out", "woman.blend")
DATA = os.path.join(HERE, "gemini_test", "data")

SKIN = "skins/darthfurby_caucasian_female/darthfurby_caucasian_female.mhmat"
PARTS = [("eyes", "eyes/high-poly/high-poly.mhclo"),          # brown eyes by default
         ("eyebrows", "eyebrows/eyebrow001/eyebrow001.mhclo"),
         ("eyelashes", "eyelashes/eyelashes01/eyelashes01.mhclo"),
         ("hair", "hair/culturalibre_hair_13/culturalibre_hair_13.mhclo"),
         ("clothes", "clothes/punkduck_medieval_dress/punkduck_medieval_dress.mhclo")]


def hex_rgba(h):
    h = h.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (*lin, 1.0)


def make_human():
    body = HumanService.create_human(macro_detail_dict={
        "gender": 0.0, "age": 0.5, "muscle": 0.5, "weight": 0.45, "proportions": 0.6,
        "height": 0.45, "cupsize": 0.5, "firmness": 0.5,
        "race": {"asian": 0.0, "caucasian": 1.0, "african": 0.0}})
    rig = HumanService.add_builtin_rig(body, "game_engine")
    body.name, rig.name = "body", "rig"
    HumanService.set_character_skin(os.path.join(DATA, SKIN), body, skin_type="ENHANCED_SSS")
    for kind, path in PARTS:
        HumanService.add_mhclo_asset(os.path.join(DATA, path), body,
                                     asset_type="Clothes" if kind == "clothes" else kind)
    return body, rig


def arms_down(rig, fwd, angle=math.radians(40)):
    """Lower the arms from MakeHuman's A-pose to hang at her sides."""
    bpy.context.view_layer.update()
    for s in "lr":
        pb = rig.pose.bones[f"upperarm_{s}"]
        best = None
        for sign in (1, -1):
            pivot = pb.head.copy()
            rot = Matrix.Translation(pivot) @ Matrix.Rotation(sign * angle, 4, fwd) @ Matrix.Translation(-pivot)
            tail_z = (rot @ pb.tail).z
            if best is None or tail_z < best[0]:
                best = (tail_z, rot)
        pb.matrix = best[1] @ pb.matrix
        bpy.context.view_layer.update()


def stage(height):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 128
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 1080, 1620
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Base Contrast'

    world = sc.world or bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.05, 0.055, 0.06, 1)

    bpy.ops.mesh.primitive_plane_add(size=20)
    floor = bpy.context.object
    floor.name = "floor"
    m = bpy.data.materials.new("floor")
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = hex_rgba("#6d6a64")
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85
    floor.data.materials.append(m)

    def light(name, loc, power, size, color=(1, 1, 1)):
        d = bpy.data.lights.new(name, 'AREA')
        d.energy, d.size, d.color = power, size, color
        o = bpy.data.objects.new(name, d)
        sc.collection.objects.link(o)
        o.location = loc
        o.rotation_euler = (Vector((0, 0, height * 0.6)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()

    light("key", (-1.6, -2.2, 2.6), 380, 1.5, (1.0, 0.95, 0.88))
    light("fill", (2.2, -1.8, 1.4), 110, 2.5, (0.85, 0.9, 1.0))
    light("rim", (0.8, 2.2, 2.4), 350, 1.0)

    cd = bpy.data.cameras.new("camera")
    cd.lens = 70
    cam = bpy.data.objects.new("camera", cd)
    sc.collection.objects.link(cam)
    cam.location = (0.0, -5.6, height * 0.62)
    cam.rotation_euler = (Vector((0, 0, height * 0.52)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam


def main():
    B.clear()
    body, rig = make_human()
    bpy.context.view_layer.update()
    lips, crown = B.centroid(body, "lips"), B.centroid(body, "scalp")
    fwd = Vector((0, 1 if lips.y > crown.y else -1, 0))
    arms_down(rig, fwd)
    real = B.members(body, "body")
    stage(max((body.matrix_world @ v.co).z for v in body.data.vertices if v.index in real))
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("built", OUT)


if __name__ == "__main__":
    main()
