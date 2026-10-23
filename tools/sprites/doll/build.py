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
SHAPE = {"spine_01": (1.15, 1.0, 1.1), "spine_02": (1.1, 1.0, 1.05), "neck_01": (1.3, 1.0, 1.3),
         "thigh_l": (1.25, 0.72, 1.25), "thigh_r": (1.25, 0.72, 1.25),
         "upperarm_l": (1.2, 0.85, 1.2), "upperarm_r": (1.2, 0.85, 1.2)}


def make_body():
    body = HumanService.create_human(macro_detail_dict={
        "gender": 1.0, "age": 0.5, "muscle": 0.8, "weight": 1.0, "proportions": 0.5,
        "height": 0.3, "cupsize": 0.5, "firmness": 0.5,
        "race": {"asian": 0.33, "caucasian": 0.34, "african": 0.33}})
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
    proportion(body, rig)
    # The skirt MakeHuman fits clothes to comes out before the helper
    # geometry is dropped.
    helpers = {g: split(body, g) for g in ("helper-skirt",)}
    only(body, "body")
    # The realistic head and hands come off: scaled up to the sprites'
    # size a real face turns into a caricature, and fingers into claws.
    # The head and fists are drawn the painted sheets' way instead (head()).
    dom = dominant_bone(body)
    gone = {"head"} | {b.name for b in rig.data.bones if b.name.startswith(("hand_", "thumb", "index", "middle", "ring", "pinky"))}
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if any(dom.get(v.index) in gone for v in f.verts)], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(body.data)
    bm.free()
    return body, rig, helpers


def proportion(body, rig):
    # The chest is broadened without the arms growing with it.
    for b in ("clavicle_l", "clavicle_r", "neck_01"):
        rig.data.bones[b].inherit_scale = 'NONE'
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
    for b in ("clavicle_l", "clavicle_r", "neck_01"):
        rig.data.bones[b].inherit_scale = 'FULL'
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


SKIN = "#cf9670"

# The head, as the painted sprites draw it: big and round, a plain face.
HEAD_R = Vector((0.235, 0.215, 0.24))
K = HEAD_R.x / 0.175     # the face below was laid out on a 0.175 m head


def ellipsoid(name, c, r, rig, bone, n=24):
    """An egg of radii r about c, a little narrower toward the chin."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=n, v_segments=n * 2 // 3, radius=1.0)
    verts = []
    for v in bm.verts:
        x, y, z = v.co
        if z < 0:
            x *= 1 - 0.12 * -z
            y *= 1 - 0.06 * -z
        verts.append(c + Vector((x * r.x, y * r.y, z * r.z)))
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    return weighted_mesh(name, verts, faces, bone, rig)


def on_face(c, dx, dz, r=HEAD_R):
    """The point on the front of the head dx across and dz up from its centre."""
    k = max(0.0, 1 - (dx / r.x) ** 2 - (dz / r.z) ** 2)
    return Vector((c.x + dx, c.y - r.y * math.sqrt(k), c.z + dz))


def dress(body, rig, helpers):
    waist = bone_z(rig, "spine_01") + 0.02
    knee = bone_z(rig, "calf_l")
    ankle = bone_z(rig, "foot_l")
    neck = rig.matrix_world @ rig.data.bones["head"].head_local
    c = neck + Vector((0, 0.012, HEAD_R.z * 0.82))
    head_top = c.z + HEAD_R.z
    print(f"waist {waist:.3f} knee {knee:.3f} ankle {ankle:.3f} head {c.z:.3f}")
    layers = {}

    skin = look.toon("skin", SKIN)
    hd = ellipsoid("head", c, HEAD_R, rig, "head")
    nose = blob("nose", on_face(c, 0, -0.03 * K) + Vector((0, 0.008, 0)), (0.026 * K, 0.028 * K, 0.032 * K), rig, "head")
    ears = [blob(f"ear_{s}", c + Vector((sg * HEAD_R.x * 0.97, 0.01, -0.015)), (0.022 * K, 0.03 * K, 0.042 * K), rig, "head")
            for s, sg in (("l", 1), ("r", -1))]
    fists = []
    for s in "lr":
        b = rig.data.bones[f"hand_{s}"]
        h = rig.matrix_world @ (b.head_local + (b.tail_local - b.head_local) * 0.55)
        fists.append(blob(f"fist_{s}", h, (0.042, 0.046, 0.05), rig, f"hand_{s}"))
    hd = join([hd, nose] + ears + fists, "head")
    hd.data.materials.append(skin)
    move_to(hd, collection("body"))

    # The face: small dark eyes and brows, laid on the front of the head.
    face = []
    for s, sg in (("l", 1), ("r", -1)):
        face.append(blob(f"eye_{s}", on_face(c, sg * 0.058 * K, 0.012 * K) + Vector((0, 0.004, 0)), (0.016 * K, 0.008 * K, 0.022 * K), rig, "head"))
        face.append(blob(f"brow_{s}", on_face(c, sg * 0.062 * K, 0.058 * K) + Vector((0, 0.002, 0)), (0.032 * K, 0.009 * K, 0.011 * K), rig, "head"))
    face = join(face, "eyes")
    face.data.materials.append(look.toon("eye", "#1c1410"))
    layers["face"] = [face]

    # Hair: back and sides under the cap, and the moustache.
    shell = ellipsoid("hair_shell", c, HEAD_R + Vector((0.008, 0.008, 0.008)), rig, "head")
    hair = cut(shell, "hair", 0.0, bones={"head"},
               keep=lambda co: co.z > c.z - 0.13 and (co.y > c.y + 0.05 or co.z > c.z + 0.12))
    bpy.data.objects.remove(shell)
    tache = [blob("moustache", on_face(c, 0, -0.068 * K) + Vector((0, 0.002, 0)), (0.07 * K, 0.022 * K, 0.022 * K), rig, "head")]
    for sg in (1, -1):
        tache.append(blob("tache_end", on_face(c, sg * 0.06 * K, -0.085 * K) + Vector((0, 0.006, 0)), (0.022 * K, 0.018 * K, 0.03 * K), rig, "head"))
    hair = join([hair] + tache, "hair")
    hair.data.materials.append(look.toon("hair", "#1e1612"))
    layers["hair"] = [hair]

    # Shirt: blue gingham, from the waist up and down the arms.
    shirt = cut(body, "shirt", 0.02, bones=SPINE | ARMS | {"pelvis", "neck_01"},
                keep=lambda co: co.z > waist - 0.06 and co.z < neck.z - 0.02)
    shirt.data.materials.append(look.toon("gingham", None, check=("#e8eef8", "#7d9ad6", "#2e4fa8", 55)))
    layers["shirt"] = [shirt]

    # Trousers: red wool, waist to mid-calf.
    trousers = cut(body, "trousers", 0.03, bones={"pelvis"} | THIGHS | CALVES,
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
    sash = cut(body, "sash", 0.04, bones=SPINE | {"pelvis"}, keep=lambda co: waist - 0.11 < co.z < waist + 0.05)
    # Its ends hang at his left hip.
    hip_x = max((body.matrix_world @ v.co).x for v in body.data.vertices
                if abs((body.matrix_world @ v.co).z - (waist - 0.05)) < 0.02)
    fy, _ = front_back(body, waist - 0.05, 0.25)
    ends = []
    for dx in (0.0, 0.05):
        x0 = hip_x - 0.1 + dx
        verts = [(x0, fy - 0.045, waist - 0.04), (x0 + 0.045, fy - 0.045, waist - 0.04),
                 (x0 + 0.05, fy - 0.03, waist - 0.3 + dx), (x0 + 0.005, fy - 0.03, waist - 0.3 + dx)]
        o = weighted_mesh("sash_end", verts, [(0, 1, 2, 3)], "pelvis", rig)
        o.modifiers.new("thick", 'SOLIDIFY').thickness = 0.008
        ends.append(o)
    sash = join([sash] + ends, "sash")
    heights(sash)
    sash.data.materials.append(look.toon("sash", None, bands=[(0.0, "#b3261e"), (0.62, "#e08a1e"),
                                                              (0.7, "#b3261e"), (0.8, "#e8c040"), (0.87, "#b3261e")]))
    layers["sash"] = [sash]

    # Tuque: the skull above the brow, and a drooping red tail with a pompom.
    shell = ellipsoid("cap_shell", c, HEAD_R + Vector((0.02, 0.02, 0.02)), rig, "head")
    cap = cut(shell, "tuque", 0.0, bones={"head"}, keep=lambda co: co.z > c.z + 0.075)
    bpy.data.objects.remove(shell)
    hb = Vector((c.x, c.y + 0.02, head_top - 0.03))
    tip, droop = hb + Vector((0.0, 0.03, 0.12)), Vector((-0.26, 0.06, -0.36))
    tail = cone("tuque_tail", hb, tip, 0.13, 0.05, rig, "head", droop=droop)
    pom = blob("pompom", tip + droop + Vector((-0.02, 0, -0.03)), (0.055, 0.055, 0.055), rig, "head")
    cap = join([cap, tail, pom], "tuque")
    cap.data.materials.append(look.toon("red_wool_cap", "#b8262a"))
    layers["tuque"] = [cap]

    # Capote: a white blanket coat with blue hem stripes, shoulders to knee.
    coat = cut(body, "capote", 0.03, bones=SPINE | ARMS | {"pelvis", "neck_01"},
               keep=lambda co: co.z > waist - 0.12 and co.z < neck.z - 0.02)
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


def cyl(bm, p0, p1, r, n=8):
    """A rod from p0 to p1."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    m = Matrix.Translation((p0 + p1) / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=n, radius1=r, radius2=r, depth=d.length, matrix=m)


def prop(name, parts):
    """An object from [(material, colour, build(bm))]."""
    objs = []
    for i, (mat, col, build) in enumerate(parts):
        bm = bmesh.new()
        build(bm)
        me = bpy.data.meshes.new(f"{name}_{i}")
        bm.to_mesh(me)
        bm.free()
        o = bpy.data.objects.new(f"{name}_{i}", me)
        bpy.context.scene.collection.objects.link(o)
        me.materials.append(look.toon(mat, col))
        objs.append(o)
    return join(objs, name)


def tools():
    """What the work animations hold, each by its handle at the origin.
    render.py shows each only in its own animation."""
    return [
        # The voyageur's paddle, red-bladed: grip at the top, shaft down -z.
        prop("paddle", [("paddle_wood", "#c79a5a", lambda bm: cyl(bm, (0, 0, 0.02), (0, 0, -0.62), 0.016)),
                        ("paddle_red", "#b02a24", lambda bm: box(bm, (-0.012, -0.055, -0.98), (0.012, 0.055, -0.6)))]),
        # Two pieces on the back, each 90 lb, and the tumpline: origin at
        # the small of the back, the load up and behind (+y, +z).
        prop("packs", [("canvas", "#cbb88f", lambda bm: (box(bm, (-0.2, 0.0, 0.0), (0.2, 0.26, 0.3)),
                                                         box(bm, (-0.18, 0.02, 0.3), (0.18, 0.24, 0.56)))),
                       ("rope", "#6b4a2a", lambda bm: (box(bm, (-0.21, -0.01, 0.13), (0.21, 0.27, 0.16)),
                                                       box(bm, (-0.19, 0.01, 0.42), (0.19, 0.25, 0.45))))]),
        # Pemmican, a dark cake.
        prop("food", [("pemmican", "#5a3a24", lambda bm: box(bm, (-0.04, -0.03, -0.03), (0.04, 0.03, 0.03)))]),
        # A tin cup, its mouth up +z.
        prop("cup", [("tin", "#a8acb4", lambda bm: cyl(bm, (0, 0, -0.045), (0, 0, 0.045), 0.035))]),
    ]


# Drawing order, back to front; what render.py composites in.
ORDER = ["body", "face", "hair", "moccasins", "trousers", "shirt", "breechcloth", "sash", "capote", "tuque", "tools", "gun"]


def main():
    clear()
    body, rig, helpers = make_body()
    body.data.materials.append(look.toon("skin", SKIN))
    move_to(rig, collection("rig"))
    move_to(body, collection("body"))
    layers = dress(body, rig, helpers)
    layers["gun"] = [gun()]
    layers["tools"] = tools()
    for name, objs in layers.items():
        c = collection(name)
        for o in objs:
            move_to(o, c)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            look.outline(o)
    # The painted men are broad: everything a fifth wider than it is tall.
    rig.scale = (1.2, 1.2, 1.0)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("built", OUT)


if __name__ == "__main__":
    main()
