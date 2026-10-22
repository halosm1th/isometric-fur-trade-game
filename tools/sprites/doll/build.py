"""Build the paper-doll man in Blender, from nothing but scripts.

  blender -b -P build.py -- [out.blend]

A MakeHuman body (MPFB2) on the game-engine rig, given the sprites'
proportions (big head, short stocky legs). Every garment is cut from the
body itself: the vertices a set of bones carry are copied and pushed out
along their normals, so each piece fits, keeps the body's skin weights and
moves with the rig without any rigging of its own. Props are built from
primitives and parented to a hand bone. Each layer is its own collection,
which is what render.py renders one at a time.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import look

from bl_ext.user_default.mpfb.services.humanservice import HumanService

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 \
    else os.path.join(HERE, "out", "doll.blend")


def clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)


def collection(name):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(c)
    return c


def move_to(obj, coll):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)


def select_only(obj):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


# ---------------------------------------------------------------- the body

# The painted sprites' proportions, baked into the rig's rest pose: a big
# head, short thick legs and arms. Bones keep scale 1 afterwards, so the
# animations never inherit a squash.
SHAPE = {"head": (1.85, 1.85, 1.85), "neck_01": (1.5, 1.1, 1.5),
         "thigh_l": (1.25, 0.72, 1.25), "thigh_r": (1.25, 0.72, 1.25),
         "upperarm_l": (1.2, 0.85, 1.2), "upperarm_r": (1.2, 0.85, 1.2)}


def make_body():
    macro = {"gender": 1.0, "age": 0.5, "muscle": 0.8, "weight": 0.9, "proportions": 0.3,
             "height": 0.3, "cupsize": 0.5, "firmness": 0.5,
             "race": {"asian": 0.2, "caucasian": 0.6, "african": 0.2}}
    body = HumanService.create_human(macro_detail_dict=macro)
    rig = HumanService.add_builtin_rig(body, "game_engine")
    body.name, rig.name = "body", "rig"
    # Bake the shape keys (the macro targets) into the mesh.
    bpy.context.view_layer.objects.active = body
    if body.data.shape_keys:
        body.shape_key_add(name="baked", from_mix=True)
        for kb in list(body.data.shape_keys.key_blocks)[:-1]:
            body.shape_key_remove(kb)
        body.shape_key_remove(body.data.shape_keys.key_blocks[0])
    for m in list(body.modifiers):
        if m.type == 'MASK':
            body.modifiers.remove(m)
    # The eyes are where the skin round each eye marker is: noted now, read
    # again once the head has grown (the markers themselves are not skinned).
    real = members(body, "body")
    rings = {}
    for side in "lr":
        e = centroid(body, f"joint-{side}-eye")
        rings[side] = [v.index for v in body.data.vertices
                       if v.index in real and ((body.matrix_world @ v.co) - e).length < 0.02]
    proportion(body, rig)
    # The helper geometry (the skirt MakeHuman fits clothes to, the joint
    # markers) comes out before it is dropped.
    helpers = {g: split(body, g) for g in ("helper-skirt",)}
    joints = {g.name: centroid(body, g.name) for g in body.vertex_groups if g.name.startswith("joint-")}
    for side, ring in rings.items():
        joints[f"joint-{side}-eye"] = sum((body.matrix_world @ body.data.vertices[i].co for i in ring), Vector()) / len(ring)
    joints["joint-mouth"] = centroid(body, "lips")
    only(body, "body")
    return body, rig, helpers, joints


def proportion(body, rig):
    for name, sc in SHAPE.items():
        rig.pose.bones[name].scale = sc
    bpy.context.view_layer.update()
    arm = next(m for m in body.modifiers if m.type == 'ARMATURE')
    select_only(body)
    keep = arm.name
    bpy.ops.object.modifier_copy(modifier=keep)
    bpy.ops.object.modifier_apply(modifier=keep)
    body.modifiers[-1].name = keep
    select_only(rig)
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    # Stand him back on the ground.
    real = members(body, "body")
    low = min((body.matrix_world @ v.co).z for v in body.data.vertices if v.index in real)
    rig.location.z -= low
    bpy.context.view_layer.update()


def members(obj, group):
    gi = obj.vertex_groups[group].index
    return {v.index for v in obj.data.vertices if any(g.group == gi and g.weight > 0 for g in v.groups)}


def centroid(obj, group):
    vs = members(obj, group)
    if not vs:
        return None
    s = sum((obj.matrix_world @ obj.data.vertices[i].co for i in vs), Vector())
    return s / len(vs)


def only(obj, group):
    """Keep only the vertices in `group`."""
    keep = members(obj, group)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()


def duplicate(obj, name):
    g = obj.copy()
    g.data = obj.data.copy()
    g.name = g.data.name = name
    for c in obj.users_collection:
        c.objects.link(g)
    return g


def split(obj, group):
    g = duplicate(obj, group)
    only(g, group)
    return g


def bone_z(rig, name, tail=False):
    b = rig.data.bones[name]
    return (rig.matrix_world @ (b.tail_local if tail else b.head_local)).z


# ---------------------------------------------------------------- garments

SPINE = {"spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r"}
ARMS = {"upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r"}
THIGHS = {"thigh_l", "thigh_r"}
CALVES = {"calf_l", "calf_r"}
FEET = {"foot_l", "foot_r", "ball_l", "ball_r"}


def dominant_bone(obj):
    """Vertex index -> name of the bone that carries it most."""
    names = {g.index: g.name for g in obj.vertex_groups}
    bones = set(b.name for b in bpy.data.objects["rig"].data.bones)
    dom = {}
    for v in obj.data.vertices:
        best, w = None, 0.0
        for g in v.groups:
            n = names[g.group]
            if n in bones and g.weight > w:
                best, w = n, g.weight
        dom[v.index] = best
    return dom


def cut(src, name, push, bones=None, group=None, keep=None):
    """A garment from `src`: the faces whose every vertex is carried by one
    of `bones` (or is in vertex group `group`) and passes keep(world co),
    copied and pushed out along the normals by `push` metres."""
    dom = dominant_bone(src) if bones else None
    grp = members(src, group) if group else None
    g = duplicate(src, name)
    mw = src.matrix_world

    def ok(v):
        if bones and dom.get(v.index) not in bones:
            return False
        if grp is not None and v.index not in grp:
            return False
        return keep is None or keep(mw @ v.co)

    bm = bmesh.new()
    bm.from_mesh(g.data)
    flags = [ok(v) for v in bm.verts]
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(flags[v.index] for v in f.verts)], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * push
    bm.to_mesh(g.data)
    bm.free()
    g.data.materials.clear()
    return g


def heights(obj):
    """Store each vertex's rest height as attribute 'h': 0 at the garment's
    lowest point, 1 at its highest (what band materials stripe by)."""
    zs = [(obj.matrix_world @ v.co).z for v in obj.data.vertices]
    lo, hi = min(zs), max(zs)
    a = obj.data.attributes.get("h") or obj.data.attributes.new("h", 'FLOAT', 'POINT')
    for i, z in enumerate(zs):
        a.data[i].value = (z - lo) / max(hi - lo, 1e-6)
    return lo, hi


def join(objs, name):
    select_only(objs[0])
    for o in objs[1:]:
        o.select_set(True)
    bpy.ops.object.join()
    objs[0].name = objs[0].data.name = name
    return objs[0]


def weighted_mesh(name, verts, faces, bone, rig):
    """A mesh carried wholly by one bone, deformed by the rig."""
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.parent = rig
    o.matrix_parent_inverse = rig.matrix_world.inverted()
    vg = o.vertex_groups.new(name=bone)
    vg.add(list(range(len(verts))), 1.0, 'REPLACE')
    m = o.modifiers.new("rig", 'ARMATURE')
    m.object = rig
    return o


def surface_y(body, x, z, r=0.015):
    """The front of the body (least y) near (x, z)."""
    return min((body.matrix_world @ v.co).y for v in body.data.vertices
               if abs((body.matrix_world @ v.co).x - x) < r and abs((body.matrix_world @ v.co).z - z) < r)


def front_back(body, z, half=0.05):
    """The body's front and back (y) at height z, near the middle."""
    ys = [(body.matrix_world @ v.co).y for v in body.data.vertices
          if abs((body.matrix_world @ v.co).z - z) < 0.02 and abs((body.matrix_world @ v.co).x) < half]
    return min(ys), max(ys)


def cone(name, base, tip, r0, r1, rig, bone, n=10, rings=6, droop=Vector()):
    """A tapering tube from base to tip, bent by `droop` (the tuque's tail)."""
    verts, faces = [], []
    axis = (tip - base).normalized()
    side = axis.cross(Vector((0, 1, 0))).normalized()
    up = axis.cross(side).normalized()
    for r in range(rings + 1):
        t = r / rings
        c = base.lerp(tip, t) + droop * (t * t)
        rad = r0 + (r1 - r0) * t
        for i in range(n):
            a = 2 * math.pi * i / n
            verts.append(c + (side * math.cos(a) + up * math.sin(a)) * rad)
    for r in range(rings):
        for i in range(n):
            a, b = r * n + i, r * n + (i + 1) % n
            faces.append((a, b, b + n, a + n))
    faces.append(tuple(rings * n + i for i in range(n)))
    return weighted_mesh(name, verts, faces, bone, rig)


def blob(name, centre, size, rig, bone, n=8):
    """A squashed sphere (pompom, moustache)."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=n, v_segments=max(4, n // 2), radius=1.0)
    verts = [Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + centre for v in bm.verts]
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    return weighted_mesh(name, verts, faces, bone, rig)


def dress(body, rig, helpers, joints):
    waist = bone_z(rig, "spine_01") + 0.02
    knee = bone_z(rig, "calf_l")
    ankle = bone_z(rig, "foot_l")
    eye = joints["joint-l-eye"]
    mouth = joints["joint-mouth"]
    head_top = max((body.matrix_world @ v.co).z for v in body.data.vertices)
    print(f"waist {waist:.3f} knee {knee:.3f} ankle {ankle:.3f} eye {eye.z:.3f} top {head_top:.3f}")
    layers = {}

    # The face: dark eyes, brows and a moustache laid on the face, big
    # enough to read at 2 px.
    face = []
    for side in "lr":
        e = joints[f"joint-{side}-eye"]
        y = surface_y(body, e.x, e.z)
        face.append(blob(f"eye_{side}", Vector((e.x, y + 0.006, e.z)), (0.024, 0.008, 0.028), rig, "head"))
        face.append(blob(f"brow_{side}", Vector((e.x, surface_y(body, e.x, e.z + 0.05) + 0.002, e.z + 0.05)),
                         (0.036, 0.008, 0.011), rig, "head"))
    face = join(face, "eyes")
    face.data.materials.append(look.toon("eye", "#1c1410"))
    layers["face"] = [face]

    hair = cut(body, "hair", 0.006, group="scalp", keep=lambda co: co.z > eye.z + 0.03)
    hair.data.materials.append(look.toon("hair", "#2a1d16"))
    mz = mouth.z + 0.02
    tache = blob("moustache", Vector((0, surface_y(body, 0, mz) + 0.004, mz)), (0.075, 0.016, 0.022), rig, "head")
    tache.data.materials.append(look.toon("hair", "#2a1d16"))
    layers["hair"] = [hair, tache]

    # Shirt: blue gingham, from the waist up and down the arms.
    shirt = cut(body, "shirt", 0.009, bones=SPINE | ARMS | {"pelvis", "neck_01"},
                keep=lambda co: co.z > waist - 0.06 and co.z < eye.z - 0.11)
    shirt.data.materials.append(look.toon("gingham", None, check=("#e8eef8", "#7d9ad6", "#2e4fa8", 160)))
    layers["shirt"] = [shirt]

    # Trousers: red wool, waist to mid-calf.
    trousers = cut(body, "trousers", 0.012, bones={"pelvis"} | THIGHS | CALVES,
                   keep=lambda co: waist + 0.02 > co.z > ankle + 0.1)
    trousers.data.materials.append(look.toon("red_wool", "#a8282c"))
    layers["trousers"] = [trousers]

    # Moccasins: the feet and the ankles.
    moc = cut(body, "moccasins", 0.022, bones=FEET | CALVES, keep=lambda co: co.z < ankle + 0.12)
    moc.data.materials.append(look.toon("buckskin", "#9a6a3a"))
    layers["moccasins"] = [moc]

    # Breechcloth: a blue flap before and behind, hanging from the sash.
    fy, by = front_back(body, waist - 0.05)
    w, top, bot = 0.075, waist - 0.02, waist - 0.24
    flaps = []
    for nm, y, d in (("flap_front", fy - 0.03, -1), ("flap_back", by + 0.03, 1)):
        verts = [(-w, y, top), (w, y, top), (w * 1.1, y + d * 0.012, bot), (-w * 1.1, y + d * 0.012, bot)]
        o = weighted_mesh(nm, verts, [(0, 1, 2, 3)], "pelvis", rig)
        sol = o.modifiers.new("thick", 'SOLIDIFY')
        sol.thickness = 0.006
        bpy.context.view_layer.objects.active = o
        flaps.append(o)
    flaps = join(flaps, "breechcloth")
    flaps.data.materials.append(look.toon("blue_stroud", "#36508c"))
    layers["breechcloth"] = [flaps]

    # Sash: the arrow sash, red with orange and yellow bands, round the waist.
    sash = cut(body, "sash", 0.03, bones=SPINE | {"pelvis"}, keep=lambda co: waist - 0.08 < co.z < waist + 0.04)
    heights(sash)
    sash.data.materials.append(look.toon("sash", None, bands=[(0.0, "#b3261e"), (0.2, "#e08a1e"),
                                                              (0.38, "#b3261e"), (0.62, "#e8c040"), (0.8, "#b3261e")]))
    layers["sash"] = [sash]

    # Tuque: the skull above the brow, and a drooping red tail with a pompom.
    cap = cut(body, "tuque", 0.02, bones={"head"}, keep=lambda co: co.z > eye.z + 0.05)
    hb = Vector((0.0, 0.02, head_top - 0.03))
    tip, droop = hb + Vector((0.0, 0.03, 0.17)), Vector((-0.32, 0.05, -0.25))
    tail = cone("tuque_tail", hb, tip, 0.13, 0.05, rig, "head", droop=droop)
    pom = blob("pompom", tip + droop + Vector((-0.02, 0, -0.03)), (0.055, 0.055, 0.055), rig, "head")
    cap = join([cap, tail, pom], "tuque")
    cap.data.materials.append(look.toon("red_wool_cap", "#b8262a"))
    layers["tuque"] = [cap]

    # Capote: a white blanket coat with blue hem stripes, shoulders to knee.
    coat = cut(body, "capote", 0.03, bones=SPINE | ARMS | {"pelvis", "neck_01"},
               keep=lambda co: co.z > waist - 0.12 and co.z < eye.z - 0.12)
    skirt = helpers.pop("helper-skirt")
    for v in skirt.data.vertices:
        v.co += v.normal * 0.03
    sk = cut(skirt, "capote_skirt", 0.0, keep=lambda co: co.z > knee - 0.02 and co.z < waist)
    bpy.data.objects.remove(skirt)
    coat = join([coat, sk], "capote")
    lo, hi = heights(coat)
    f = lambda z: (z - lo) / (hi - lo)
    coat.data.materials.append(look.toon("blanket", None, bands=[(0.0, "#e9e4d6"), (f(lo + 0.04), "#2a3f8c"),
                                                                 (f(lo + 0.075), "#e9e4d6"), (f(lo + 0.09), "#2a3f8c"),
                                                                 (f(lo + 0.11), "#e9e4d6")]))
    layers["capote"] = [coat]

    for o in helpers.values():
        bpy.data.objects.remove(o)
    return layers


# ---------------------------------------------------------------- props

def box(bm, lo, hi):
    r = bmesh.ops.create_cube(bm, size=1.0)
    for v in r["verts"]:
        v.co = Vector(((lo[i] + hi[i]) / 2 + v.co[i] * (hi[i] - lo[i]) for i in range(3)))
    return r["verts"]


def tube(bm, y0, y1, r, z=0.0, n=8):
    m = Matrix.Translation(((0, (y0 + y1) / 2, z))) @ Matrix.Rotation(math.radians(90), 4, 'X')
    bmesh.ops.create_cone(bm, cap_ends=True, segments=n, radius1=r, radius2=r, depth=abs(y1 - y0), matrix=m)


def gun():
    """The trade gun, by the grip: barrel along -y, stock behind (+y)."""
    parts = []
    for name, col, build in (
            ("gun_wood", "#7a4a26", lambda bm: (box(bm, (-0.017, -0.45, -0.035), (0.017, 0.0, 0.0)),
                                                 # the stock: wrist to butt, dropping
                                                 box(bm, (-0.02, 0.0, -0.05), (0.02, 0.12, 0.0)),
                                                 box(bm, (-0.024, 0.12, -0.11), (0.024, 0.30, 0.0)))),
            ("gun_iron", "#3b3b42", lambda bm: (tube(bm, -0.72, 0.0, 0.013, z=0.012),
                                                 box(bm, (-0.022, -0.06, -0.03), (0.022, 0.03, 0.01)))),
            ("gun_brass", "#c9a646", lambda bm: (box(bm, (-0.026, 0.30, -0.115), (0.026, 0.315, 0.004)),
                                                  box(bm, (-0.004, -0.04, -0.07), (0.004, 0.06, -0.035))))):
        bm = bmesh.new()
        build(bm)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        o = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(o)
        me.materials.append(look.toon(name, col))
        parts.append(o)
    g = join(parts, "gun")
    return g


# Drawing order, back to front; what render.py composites in.
ORDER = ["body", "face", "hair", "moccasins", "trousers", "shirt", "breechcloth", "sash", "capote", "tuque", "gun"]


def main():
    clear()
    body, rig, helpers, joints = make_body()
    body.data.materials.append(look.toon("skin", "#d39c74"))
    move_to(rig, collection("rig"))
    move_to(body, collection("body"))
    layers = dress(body, rig, helpers, joints)
    layers["gun"] = [gun()]
    for name, objs in layers.items():
        c = collection(name)
        for o in objs:
            move_to(o, c)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            look.outline(o)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("built", OUT)


if __name__ == "__main__":
    main()
