"""The sprites' look in Blender: flat toon shading in three tones, a dark
outline, a camera and light that match the painted Gemini characters
(seen nearly straight on, a little from above), rendered at the game's
180x170 frame with hard pixels."""
import math, os
import bpy
from mathutils import Vector

W, H = 180, 170
FEET_Y = 167            # pixel row the soles stand on, as in the cut sheets
PX = 0.0135             # metres per pixel: a ~1.7 m man stands ~125 px
ELEVATION = 18          # degrees the camera looks down
OUTLINE = 0.02          # metres; ~1.5 px
# DOLL_STYLE=soft: the Project Zomboid manner - light falling off smoothly,
# no ink line, soft edges.
SOFT = os.environ.get("DOLL_STYLE") == "soft"
# DOLL_STYLE=paint: a man of ordinary build drawn to sit among the painted
# buildings - four flat tones, a painted grain in every cloth, a thin
# brown line, hard pixels.
PAINT = os.environ.get("DOLL_STYLE") == "paint"
if PAINT:
    OUTLINE = 0.011


def _ramp(nodes, links, src):
    s2r = nodes.new("ShaderNodeShaderToRGB")
    links.new(src.outputs[0], s2r.inputs[0])
    ramp = nodes.new("ShaderNodeValToRGB")
    e = ramp.color_ramp.elements
    if SOFT:
        ramp.color_ramp.interpolation = 'LINEAR'
        e[0].position, e[0].color = 0.05, (0.30, 0.28, 0.34, 1)
        e[1].position, e[1].color = 0.85, (1.0, 1.0, 1.0, 1)
        links.new(s2r.outputs[0], ramp.inputs[0])
        return ramp
    ramp.color_ramp.interpolation = 'CONSTANT'
    if PAINT:
        e[0].position, e[0].color = 0.0, (0.40, 0.36, 0.40, 1)
        e[1].position, e[1].color = 0.12, (0.60, 0.57, 0.60, 1)
        e.new(0.35).color = (0.80, 0.79, 0.80, 1)
        e.new(0.62).color = (1.0, 1.0, 1.0, 1)
        links.new(s2r.outputs[0], ramp.inputs[0])
        return ramp
    e[0].position, e[0].color = 0.0, (0.36, 0.34, 0.40, 1)
    e[1].position, e[1].color = 0.22, (0.64, 0.63, 0.67, 1)
    e.new(0.6).color = (1.0, 1.0, 1.0, 1)
    links.new(s2r.outputs[0], ramp.inputs[0])
    return ramp


def lin(hexcol):
    """'#rrggbb' (as painted) -> linear rgb for Blender."""
    c = [int(hexcol[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def _steps(nodes, links, src, stops):
    """A constant ramp over `src`: stops [(position, '#rrggbb'), ...]."""
    r = nodes.new("ShaderNodeValToRGB")
    r.color_ramp.interpolation = 'CONSTANT'
    e = r.color_ramp.elements
    while len(e) < len(stops):
        e.new(0.99)
    for el, (p, c) in zip(e, stops):
        el.position, el.color = p, (*lin(c), 1)
    links.new(src, r.inputs[0])
    return r


def _math(nodes, links, op, a, b=None):
    n = nodes.new("ShaderNodeMath")
    n.operation = op
    for i, x in enumerate((a, b)):
        if x is None:
            continue
        if isinstance(x, (int, float)):
            n.inputs[i].default_value = x
        else:
            links.new(x, n.inputs[i])
    return n.outputs[0]


def _image(nodes, links, path, scale=1.0, uv=None, sx=None):
    """An image texture read through UV map `uv` (the active one if None),
    tiled `scale` times (`sx` across if it differs)."""
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path, check_existing=True)
    tex.interpolation = 'Linear'
    if uv:
        src = nodes.new("ShaderNodeUVMap")
        src.uv_map = uv
        src = src.outputs[0]
    else:
        src = nodes.new("ShaderNodeTexCoord").outputs["UV"]
    mp = nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (sx or scale, scale, 1.0)
    links.new(src, mp.inputs[0])
    links.new(mp.outputs[0], tex.inputs[0])
    return tex.outputs[0]


def toon(name, color, image=None, scale=1.0, check=None, bands=None, uv=None, sx=None, over=None):
    """A material lit in three flat tones, coloured by one of:
      color   '#rrggbb'
      image   a texture tiled `scale` times
      check   (light, mid, dark, n): gingham, n checks across the UV square
      bands   [(h, '#rrggbb'), ...]: stripes by the mesh's 'h' attribute
              (0 at the garment's hem, 1 at its top; see build.heights).
    An image is read through UV map `uv`; `over` = (image, uv, attribute)
    lays a second image over it where the mesh's attribute is 1 (the face
    over the skin, the stripe over the blanket)."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    diff = nodes.new("ShaderNodeBsdfDiffuse")
    diff.inputs[0].default_value = (1, 1, 1, 1)
    ramp = _ramp(nodes, links, diff)
    mul = nodes.new("ShaderNodeMix")
    mul.data_type, mul.blend_type = 'RGBA', 'MULTIPLY'
    mul.inputs[0].default_value = 1.0
    if check:
        light, mid, dark, n = check
        uv = nodes.new("ShaderNodeTexCoord").outputs["UV"]
        sep = nodes.new("ShaderNodeSeparateXYZ")
        links.new(uv, sep.inputs[0])
        on = [_math(nodes, links, 'LESS_THAN', _math(nodes, links, 'FRACT', _math(nodes, links, 'MULTIPLY', sep.outputs[i], n)), 0.5)
              for i in (0, 1)]
        s = _math(nodes, links, 'MULTIPLY', _math(nodes, links, 'ADD', on[0], on[1]), 0.5)
        links.new(_steps(nodes, links, s, [(0.0, light), (0.25, mid), (0.75, dark)]).outputs[0], mul.inputs[6])
    elif bands:
        attr = nodes.new("ShaderNodeAttribute")
        attr.attribute_name = "h"
        links.new(_steps(nodes, links, attr.outputs["Fac"], bands).outputs[0], mul.inputs[6])
    elif image:
        base = _image(nodes, links, image, scale, uv, sx)
        if over:
            img, ouv, attr = over
            a = nodes.new("ShaderNodeAttribute")
            a.attribute_name = attr
            mix = nodes.new("ShaderNodeMix")
            mix.data_type = 'RGBA'
            links.new(a.outputs["Fac"], mix.inputs[0])
            links.new(base, mix.inputs[6])
            links.new(_image(nodes, links, img, 1.0, ouv), mix.inputs[7])
            base = mix.outputs[2]
        links.new(base, mul.inputs[6])
    else:
        mul.inputs[6].default_value = (*lin(color), 1)
    links.new(ramp.outputs[0], mul.inputs[7])
    col = mul.outputs[2]
    if PAINT and not image:
        # The painted grain: the cloth a shade lighter and darker in soft
        # blotches and fine streaks, as the buildings' boards are.
        noise = nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 90.0
        noise.inputs["Detail"].default_value = 6.0
        uv = nodes.new("ShaderNodeTexCoord").outputs["UV"]
        links.new(uv, noise.inputs["Vector"])
        g = nodes.new("ShaderNodeMapRange")
        g.inputs[1].default_value, g.inputs[2].default_value = 0.3, 0.7
        g.inputs[3].default_value, g.inputs[4].default_value = 0.84, 1.1
        links.new(noise.outputs["Fac"], g.inputs[0])
        grain = nodes.new("ShaderNodeMix")
        grain.data_type, grain.blend_type = 'RGBA', 'MULTIPLY'
        grain.inputs[0].default_value = 1.0
        links.new(col, grain.inputs[6])
        gc = nodes.new("ShaderNodeCombineXYZ")
        for i in range(3):
            links.new(g.outputs[0], gc.inputs[i])
        links.new(gc.outputs[0], grain.inputs[7])
        col = grain.outputs[2]
    emit = nodes.new("ShaderNodeEmission")
    links.new(col, emit.inputs[0])
    links.new(emit.outputs[0], out.inputs[0])
    return m


def ink():
    m = bpy.data.materials.get("ink") or bpy.data.materials.new("ink")
    m.use_nodes = True
    nodes = m.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    e = nodes.new("ShaderNodeEmission")
    e.inputs[0].default_value = (0.05, 0.03, 0.02, 1) if PAINT else (0.04, 0.03, 0.03, 1)
    m.node_tree.links.new(e.outputs[0], out.inputs[0])
    m.use_backface_culling = True
    return m


def outline(obj, width=OUTLINE):
    """The inverted hull: a solidify shell, flipped, drawn only from behind."""
    if SOFT:
        return
    if "ink" not in [s.material.name for s in obj.material_slots if s.material]:
        obj.data.materials.append(ink())
    mod = obj.modifiers.get("outline") or obj.modifiers.new("outline", 'SOLIDIFY')
    mod.thickness = -width
    mod.offset = 1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.material_offset = len(obj.material_slots) - 1
    mod.use_quality_normals = True


def setup_scene():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE_NEXT'
    sc.render.resolution_x, sc.render.resolution_y = W, H
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = True
    sc.render.filter_size = 1.0 if SOFT else 0.0
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.view_settings.view_transform = 'Standard'
    sc.eevee.taa_render_samples = 16 if SOFT else 1
    if not sc.world:
        sc.world = bpy.data.worlds.new("world")
    sc.world.use_nodes = True
    sc.world.node_tree.nodes["Background"].inputs[1].default_value = 0.12

    sun = bpy.data.objects.get("sun")
    if not sun:
        sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN'))
        sc.collection.objects.link(sun)
    sun.data.energy = 4.0
    sun.data.use_shadow = False      # the outline shells would shade the body
    # From the upper left and in front, as the painted sheets are lit.
    sun.rotation_euler = (math.radians(55), 0, math.radians(-40))

    cam = bpy.data.objects.get("cam")
    if not cam:
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
        sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = W * PX
    # Put the feet (world origin) at pixel (W/2, FEET_Y): shift the camera so
    # the origin projects there.
    el = math.radians(ELEVATION)
    cam.rotation_euler = (math.radians(90) - el, 0, 0)
    up = (H / 2 - (H - FEET_Y)) * PX     # how far above the feet the frame centre is
    d = 20.0
    fwd = Vector((0, math.cos(el), -math.sin(el)))      # camera looks along +y, down
    upv = Vector((0, math.sin(el), math.cos(el)))
    cam.location = -fwd * d + upv * up
    cam.data.shift_y = 0.0
    cam.data.clip_end = 100
    return cam


# Which projected painting (projected.py) a render in each facing shows
# first; the three-quarter turn is nearest the front.
PAINTED_VIEW = {"front": "front", "side": "right", "back": "back", "three": "front"}


def painted_view(facing):
    m = bpy.data.materials.get("painted")
    if not m:
        return
    for n in m.node_tree.nodes:
        if n.name.startswith("cam_"):
            n.outputs[0].default_value = 1.0 if n.name == "cam_" + PAINTED_VIEW[facing] else 0.0


# Facing name -> rotation of the figure about z (the camera looks along +y,
# so a man facing -y faces the camera).
FACINGS = {"front": 0.0, "side": 90.0, "back": 180.0, "three": 35.0}
