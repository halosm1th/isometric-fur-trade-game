"""Render the bare head straight on, 512x512, as the guide Gemini paints a
face over; the same framing projects the painted face back onto the head.

  blender -b out/doll_real.blend -P face_guide.py -- out.png
"""
import math, os, sys
import bpy
from mathutils import Vector

out = sys.argv[sys.argv.index("--") + 1]
sc = bpy.context.scene
for c in bpy.data.collections:
    c.hide_render = c.name != "body"
rig = bpy.data.objects["rig"]
body = bpy.data.objects["body"]
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
e = body.evaluated_get(dg)
ps = [e.matrix_world @ v.co for v in e.data.vertices]
top = max(p.z for p in ps)
head = [p for p in ps if p.z > top - 0.27]
cx = sum(p.x for p in head) / len(head)
cz = top - 0.125
SIZE = 0.3     # metres across the frame: the whole head with a margin

cam = bpy.data.objects.new("facecam", bpy.data.cameras.new("facecam"))
sc.collection.objects.link(cam)
cam.data.type = 'ORTHO'
cam.data.ortho_scale = SIZE
cam.location = (cx, -5.0, cz)
cam.rotation_euler = (math.radians(90), 0, 0)
sc.camera = cam
sc.render.engine = 'BLENDER_EEVEE_NEXT'
sc.render.resolution_x = sc.render.resolution_y = 512
sc.render.film_transparent = False
sc.view_settings.view_transform = 'Standard'
if not sc.world:
    sc.world = bpy.data.worlds.new("w")
sc.world.use_nodes = True
bg = sc.world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (0.35, 0.35, 0.35, 1)
bg.inputs[1].default_value = 0.6
# Plain grey clay under a soft front light, so the features show as form.
clay = bpy.data.materials.new("clay")
clay.use_nodes = True
clay.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.6, 0.6, 0.6, 1)
for o in bpy.data.collections["body"].objects:
    o.data.materials.clear()
    o.data.materials.append(clay)
    for m in [m for m in o.modifiers if m.name == "outline"]:
        o.modifiers.remove(m)
sun = bpy.data.objects.get("sun") or bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN'))
if sun.name not in sc.collection.objects:
    sc.collection.objects.link(sun)
sun.rotation_euler = (math.radians(70), 0, math.radians(-15))
sun.data.energy = 3.0
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
print(f"frame centre x {cx:.4f} z {cz:.4f} size {SIZE}")
