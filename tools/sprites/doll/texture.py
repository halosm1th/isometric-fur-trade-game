"""Dress the built man in Gemini's painted textures (gemini_textures/).

  blender -b out/doll_real.blend -P texture.py      (saves in place)

The face is projected straight on from the front, framed as face_guide.py
framed the clay head Gemini painted over, and fades to plain skin round
the sides; the cloths tile on the body's own UVs; the sash and the
capote's hem stripe are wrapped round him as bands.
"""
import math, os, sys
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import look

T = os.path.join(HERE, "gemini_textures")
tex = lambda name: os.path.join(T, name)
SIZE = 0.3          # face_guide.py's frame, metres across


def world(o):
    return [o.matrix_world @ v.co for v in o.data.vertices]


def uv_from(o, name, f):
    """A UV map from f(vertex world position, vertex index) -> (u, v)."""
    me = o.data
    layer = me.uv_layers.get(name) or me.uv_layers.new(name=name)
    ps = world(o)
    for loop in me.loops:
        layer.data[loop.index].uv = f(ps[loop.vertex_index], loop.vertex_index)


def attribute(o, name, values):
    a = o.data.attributes.get(name) or o.data.attributes.new(name, 'FLOAT', 'POINT')
    for i, x in enumerate(values):
        a.data[i].value = x


def keep_active_uv(o):
    # The cloths read the body's own UVs; the new maps must not displace them.
    o.data.uv_layers.active_index = 0
    for i, l in enumerate(o.data.uv_layers):
        l.active_render = i == 0


def main():
    bpy.context.view_layer.update()
    body = bpy.data.objects["body"]
    ps = world(body)
    top = max(p.z for p in ps)
    head = [p for p in ps if p.z > top - 0.27]
    cx, cz = sum(p.x for p in head) / len(head), top - 0.125
    neck = bpy.data.objects["rig"].matrix_world @ bpy.data.objects["rig"].data.bones["head"].head_local

    # The face: projected from the front, full on the face, gone by the
    # sides and the back of the head.
    uv_from(body, "face", lambda p, i: ((p.x - cx) / SIZE + 0.5, (p.z - cz) / SIZE + 0.5))
    rot = body.matrix_world.to_3x3().normalized()
    weights = []
    for v, p in zip(body.data.vertices, ps):
        n = rot @ v.normal
        front = min(1.0, max(0.0, (-n.y - 0.2) / 0.35))
        inside = abs(p.x - cx) < SIZE * 0.45 and p.z > neck.z - 0.06
        weights.append(front if inside else 0.0)
    attribute(body, "face_w", weights)
    keep_active_uv(body)
    body.data.materials.clear()
    body.data.materials.append(look.toon("skin", None, image=tex("skin.png"), over=(tex("FACE.jpg"), "face", "face_w")))

    cloth = {"shirt": ("gingham.jpg", 7), "trousers": ("red wool.jpg", 4), "breechcloth": ("blue wool stroud.jpg", 4),
             "moccasins": ("buckskin.jpg", 3), "tuque": ("red wool.jpg", 5), "hair": ("hair.jpg", 6)}
    for name, (img, sc) in cloth.items():
        o = bpy.data.objects.get(name)
        if not o:
            continue
        mats = [s.material for s in o.material_slots]
        o.data.materials[0] = look.toon(name + "_tex", None, image=tex(img), scale=sc)

    # The sash: the woven strip round his waist, twice round.
    sash = bpy.data.objects["sash"]
    uv_from(sash, "band", lambda p, i: (math.atan2(p.y, p.x) / (2 * math.pi) * 2, sash.data.attributes["h"].data[i].value))
    sash.data.materials[0] = look.toon("sash_tex", None, image=tex("sash.jpg"), uv="band")

    # The capote: blanket wool, the striped hem in its last 16 cm.
    coat = bpy.data.objects["capote"]
    cps = world(coat)
    lo = min(p.z for p in cps)
    uv_from(coat, "band", lambda p, i: (math.atan2(p.y, p.x) / (2 * math.pi) * 3, (p.z - lo) / 0.16))
    attribute(coat, "hem_w", [1.0 if p.z < lo + 0.16 else 0.0 for p in cps])
    keep_active_uv(coat)
    coat.data.materials[0] = look.toon("blanket_tex", None, image=tex("white wool.jpg"), scale=4,
                                       over=(tex("capot stripe.jpg"), "band", "hem_w"))
    bpy.ops.wm.save_mainfile()
    print("textured")


main()
