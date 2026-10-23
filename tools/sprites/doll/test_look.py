"""Render the built doll in its three facings, all layers together, to
out/test_<facing>.png.  blender -b out/doll.blend -P test_look.py"""
import math, os, sys
import bpy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import look, pose

cam = look.setup_scene()
ZOOM = float(os.environ.get("ZOOM", "1"))
if ZOOM != 1:
    cam.data.ortho_scale /= ZOOM
    cam.location.z += float(os.environ.get("ZUP", "0.2"))
    cam.location.y -= 0.0
rig = bpy.data.objects["rig"]
for o in bpy.data.objects:
    if o.type == 'MESH' and "outline" not in o.modifiers:
        look.outline(o)
for f, deg in look.FACINGS.items():
    rig.rotation_euler = (0, 0, math.radians(deg))
    bpy.context.scene.render.filepath = os.path.join(HERE, "out", f"test_{f}{'_zoom' if ZOOM != 1 else ''}.png")
    bpy.ops.render.render(write_still=True)
