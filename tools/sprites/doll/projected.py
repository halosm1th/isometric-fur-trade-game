"""Paint the doll with the painted sprites themselves, so every pose he is
put in keeps the Gemini look: the front, back and side idle frames are
projected onto him as he stands, each from its own direction, and he is
drawn unlit, the light being already in the paint.

  blender -b out/doll.blend -P projected.py -- SHEET_DIR [out.blend]

SHEET_DIR holds idle_front.png, idle_side.png and idle_back.png (180x170,
feet on row 167, side facing right). The doll's shape need not match the
painted man's: each row of his silhouette is stretched onto the matching
row of the painted one, opaque pixel by opaque pixel. Then render.py
renders him in every animation; outline.py draws the ink line afterwards.
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
SHEETS = os.path.abspath(args[0])
OUT = args[1] if len(args) > 1 else os.path.join(HERE, "out", "doll_proj.blend")
TMP = os.path.join(HERE, "out", "proj")
os.makedirs(TMP, exist_ok=True)
W, H = look.W, look.H
# Layers that make his shape; the rest stay hidden.
WEAR = ["body", "face", "moccasins", "trousers", "shirt", "breechcloth", "sash", "tuque"]
# view name -> (rig turn, painted frame, mirrored)
VIEWS = {"front": (0, "idle_front", False), "back": (180, "idle_back", False),
         "right": (90, "idle_side", False), "left": (270, "idle_side", True)}

sc = bpy.context.scene
cam = look.setup_scene()
rig = bpy.data.objects["rig"]
# The painted side frame's own turn, as fit.py found it.
SIDE = rig.get("side_turn", 90.0)
VIEWS["right"] = (SIDE, "idle_side", False)
VIEWS["left"] = (360 - SIDE, "idle_side", True)
# Hidden from the ray casts too, or a capote no one sees would shade him.
hidden = [c for c in bpy.data.collections if c.name not in WEAR and c.name != "rig"]
for c in hidden:
    c.hide_render = c.hide_viewport = True
props = [bpy.data.objects[th] for th in ("gun", "paddle", "packs", "food", "cup") if th in bpy.data.objects]
for o in props:
    o.hide_render = o.hide_viewport = True
meshes = [o for o in bpy.data.objects if o.type == 'MESH' and any(c.name in WEAR for c in o.users_collection)]
for o in bpy.data.objects:
    if o.type == 'MESH':
        for m in o.modifiers:
            if m.name == "outline":
                m.show_render = m.show_viewport = False


# ---------------------------------------------------------------- the paint

def read(path):
    """A PNG as a uint8 RGBA array, top row first (Blender has no PIL)."""
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]
    bpy.data.images.remove(im)
    return (a * 255 + 0.5).astype(np.uint8)


def write(a, path):
    h, w = a.shape[:2]
    im = bpy.data.images.new("tmp_write", w, h, alpha=True)
    im.pixels[:] = (a[::-1].astype(np.float32) / 255).ravel()
    im.filepath_raw = path
    im.file_format = 'PNG'
    im.save()
    bpy.data.images.remove(im)


def erode(m, r):
    out = m.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            out &= np.roll(np.roll(m, dy, 0), dx, 1)
    return out


def load(name, mirror):
    a = read(os.path.join(SHEETS, name + ".png"))
    return a[:, ::-1] if mirror else a


def unink(a):
    """The painted frame with its ink line taken off and its colours run
    outward, so paint that lands a pixel or two outside still has colour."""
    al = a[..., 3] > 127
    rgb = a[..., :3].astype(float)
    dark = rgb.sum(-1) < 170
    # The ink is the dark ring at the silhouette's edge.
    inner = erode(al, 2)
    keep = al & ~(dark & ~inner)
    col, have = rgb.copy(), keep.copy()
    for _ in range(12):
        acc = np.zeros_like(col); n = np.zeros(have.shape)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sh = np.roll(np.roll(have, dy, 0), dx, 1)
            acc += np.roll(np.roll(col * have[..., None], dy, 0), dx, 1); n += sh
        grow = ~have & (n > 0)
        col[grow] = acc[grow] / n[grow, None]
        have = have | grow
    out = np.dstack([col, np.full(have.shape, 255.0)]).astype(np.uint8)
    return out, al


def render_mask(view="mask"):
    sc.render.filepath = os.path.join(TMP, f"mask_{view}.png")
    bpy.ops.render.render(write_still=True)
    return read(sc.render.filepath)[..., 3] > 127


def rows(mask):
    return [np.nonzero(mask[y])[0] for y in range(mask.shape[0])]


def span(mask):
    ys = np.nonzero(mask.any(1))[0]
    return ys.min(), ys.max()


def remap(px, py, dmask, drows, pmask, prows):
    """A pixel of the doll's silhouette -> the matching pixel of the
    painted one: rows by height between crown and sole, and along the row
    by how far through its opaque pixels it is."""
    dt, db = span(dmask)
    pt, pb = span(pmask)
    f = (py - dt) / max(1, db - dt)
    qy = int(round(pt + f * (pb - pt)))
    qy = min(max(qy, pt), pb)
    y = int(round(min(max(py, dt), db)))
    dr = drows[y]
    pr = prows[qy]
    if len(dr) == 0 or len(pr) == 0:
        return px, qy
    k = np.searchsorted(dr, px)
    frac = k / max(1, len(dr) - 1)
    qx = pr[min(len(pr) - 1, int(round(frac * (len(pr) - 1))))]
    return qx, qy


# ---------------------------------------------------------------- projection

p, lift = pose.idle(rig, 0.0)
pose.apply(rig, p, lift)
bpy.context.view_layer.update()
# Stood on the ground as render.py stands him, or the paint slides.
dg = bpy.context.evaluated_depsgraph_get()
low = min(min((o.evaluated_get(dg).matrix_world @ v.co).z for v in o.evaluated_get(dg).data.vertices)
          for o in bpy.data.collections["body"].objects)
pel = rig.pose.bones["pelvis"]
pel.location += pel.bone.matrix_local.to_3x3().inverted() @ Vector((0, 0, -low))
bpy.context.view_layer.update()
fwd = (cam.matrix_world.to_3x3() @ Vector((0, 0, -1))).normalized()

uvs = {o.name: {} for o in meshes}        # view -> per-vertex uv
wts = {o.name: {} for o in meshes}        # view -> per-vertex weight
seen = {o.name: {} for o in meshes}       # view -> 1 where that painting shows the vertex
images, paints = {}, {}
for view, (turn, frame, mirror) in VIEWS.items():
    rig.rotation_euler = (0, 0, math.radians(turn))
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    dmask = render_mask(view)
    paint, pmask = unink(load(frame, mirror))
    path = os.path.join(TMP, f"paint_{view}.png")
    write(paint, path)
    images[view] = path
    paints[view] = paint
    drows, prows = rows(dmask), rows(pmask)
    for o in meshes:
        e = o.evaluated_get(dg)
        me = e.to_mesh()
        mw = e.matrix_world
        nm = mw.to_3x3().inverted().transposed()
        uv = np.zeros((len(me.vertices), 2)); wt = np.zeros(len(me.vertices)); sn = np.zeros(len(me.vertices))
        for v in me.vertices:
            co = mw @ v.co
            n = (nm @ v.normal).normalized()
            facing = -n.dot(fwd)
            c = world_to_camera_view(sc, cam, co)
            px, py = c.x * W, (1 - c.y) * H
            qx, qy = remap(px, py, dmask, drows, pmask, prows)
            uv[v.index] = ((qx + 0.5) / W, 1 - (qy + 0.5) / H)
            # Seen from this side and not hidden behind another part of him.
            hit = sc.ray_cast(dg, co - fwd * 0.004, -fwd)[0]
            wt[v.index] = 0.0 if hit else max(0.0, facing) ** 2
            sn[v.index] = 0.0 if hit or facing < 0.05 else 1.0
        uvs[o.name][view] = uv
        wts[o.name][view] = wt
        seen[o.name][view] = sn
        e.to_mesh_clear()

rig.rotation_euler = (0, 0, 0)
# Which way each arm bone pointed while it was painted: render.py turns an
# arm from its projected paint to its sleeve's cloth as it swings away.
ARM_BONES = ("upperarm_l", "lowerarm_l", "upperarm_r", "lowerarm_r")
for b in ARM_BONES:
    pb = rig.pose.bones[b]
    rig["painted_" + b] = list((pb.tail - pb.head).normalized())

# ---------------------------------------------------------------- the sleeves
#
# A hanging arm is a strip ten pixels wide in the painting; raised, that
# strip is stretched over twice the area and the cloth turns to mush. So an
# arm away from its painted pose wears its own cloth instead: a swatch cut
# from where the painting shows that sleeve, tiled round the arm in the
# mesh's undeformed coordinates (so it doesn't swim) and lit as he turns.

PX_PER_M = 1 / look.PX
ARM_GROUPS = ("upperarm_", "lowerarm_", "hand_", "thumb_", "index_", "middle_", "ring_", "pinky_")


def arm_weights(o):
    """Per vertex, how much the left and the right arm carry it."""
    n = len(o.data.vertices)
    side = {"l": np.zeros(n), "r": np.zeros(n)}
    idx = {g.index: g.name for g in o.vertex_groups}
    for v in o.data.vertices:
        for g in v.groups:
            name = idx[g.group]
            for sd in "lr":
                if name.startswith(ARM_GROUPS) and name.endswith("_" + sd):
                    side[sd][v.index] += g.weight
    return np.clip(side["l"], 0, 1), np.clip(side["r"], 0, 1)


def swatch(o, arm, uv_front, seen_front):
    """The painted front frame's pixels where it shows this piece's arms:
    the middle of their bounding box, as a small tile. None if it shows none."""
    pick = (arm > 0.5) & (seen_front > 0.5)
    if pick.sum() < 6:
        return None
    px = np.stack([uv_front[pick, 0] * W - 0.5, (1 - uv_front[pick, 1]) * H - 0.5], 1)
    # A patch from the middle of the one arm, not a box round both (that
    # takes in the chest and the sash between them). Small enough to stay
    # on the arm, big enough for a few checks of the cloth.
    cx, cy = np.median(px, 0)
    hw, hh = (3, 4) if len(px) > 40 else (1, 1)
    x0, y0 = int(round(cx)) - hw, int(round(cy)) - hh
    tile = paints["front"][y0:y0 + 2 * hh, x0:x0 + 2 * hw].copy()
    path = os.path.join(TMP, f"swatch_{o.name}.png")
    write(tile, path)
    return path, tile.shape[1], tile.shape[0]


# ---------------------------------------------------------------- material

def material(name, sw=None, dims=None):
    m = bpy.data.materials.new("painted_" + name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")

    def mix(kind, a, b):
        n = nodes.new("ShaderNodeMix"); n.data_type = 'RGBA'; n.blend_type = kind
        n.inputs[0].default_value = 1.0
        for i, x in ((6, a), (7, b)):
            if isinstance(x, float):
                n.inputs[i].default_value = (x, x, x, 1.0)
            else:
                links.new(x, n.inputs[i])
        return n.outputs[2]

    # EEVEE allows a material only so many attributes: the four views'
    # weights ride in one colour (r g b a = the views in order), their
    # seen-masks in another, the two arms in one vector.
    packed = {}

    def chan(name, j):
        if name not in packed:
            at = nodes.new("ShaderNodeAttribute"); at.attribute_name = name
            sp = nodes.new("ShaderNodeSeparateColor")
            links.new(at.outputs["Color"], sp.inputs[0])
            packed[name] = (at, sp)
        at, sp = packed[name]
        return at.outputs["Alpha"] if j == 3 else sp.outputs[j]

    acc = cam_acc = cam_sum = None
    for i, view in enumerate(VIEWS):
        uvn = nodes.new("ShaderNodeUVMap"); uvn.uv_map = "uv_" + view
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(images[view], check_existing=True)
        tex.interpolation = 'Closest'
        links.new(uvn.outputs[0], tex.inputs[0])
        mul = nodes.new("ShaderNodeMix"); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'
        mul.inputs[0].default_value = 1.0
        links.new(tex.outputs[0], mul.inputs[6]); links.new(chan("w", i), mul.inputs[7])
        if acc is None:
            acc = mul.outputs[2]
        else:
            add = nodes.new("ShaderNodeMix"); add.data_type = 'RGBA'; add.blend_type = 'ADD'
            add.inputs[0].default_value = 1.0
            links.new(acc, add.inputs[6]); links.new(mul.outputs[2], add.inputs[7])
            acc = add.outputs[2]
        # This painting, wherever it saw him, when the camera is on its side
        # (look.painted_view sets cam_<view> for each render).
        cv = nodes.new("ShaderNodeValue"); cv.name = cv.label = "cam_" + view
        cv.outputs[0].default_value = 0.0
        k = mix('MULTIPLY', cv.outputs[0], chan("s", i))
        part = mix('MULTIPLY', tex.outputs[0], k)
        cam_acc = part if cam_acc is None else mix('ADD', cam_acc, part)
        cam_sum = k if cam_sum is None else mix('ADD', cam_sum, k)
    rest = mix('SUBTRACT', 1.0, cam_sum)
    acc = mix('ADD', cam_acc, mix('MULTIPLY', acc, rest))
    if sw:
        path, tw, th = sw
        tc = nodes.new("ShaderNodeTexCoord")
        mp = nodes.new("ShaderNodeMapping")
        # Generated runs 0..1 over the piece; as many tiles as keep the
        # painting's own pixels per metre.
        mp.inputs["Scale"].default_value = (dims[0] * PX_PER_M / tw, dims[1] * PX_PER_M / tw, dims[2] * PX_PER_M / th)
        links.new(tc.outputs["Generated"], mp.inputs[0])
        st = nodes.new("ShaderNodeTexImage")
        st.image = bpy.data.images.load(path, check_existing=True)
        st.interpolation = 'Closest'
        st.projection = 'BOX'
        st.projection_blend = 0.2
        links.new(mp.outputs[0], st.inputs[0])
        # Lit in three tones about the painting's own average.
        dif = nodes.new("ShaderNodeBsdfDiffuse")
        s2r = nodes.new("ShaderNodeShaderToRGB")
        links.new(dif.outputs[0], s2r.inputs[0])
        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = 'CONSTANT'
        e = ramp.color_ramp.elements
        e[0].position, e[0].color = 0.0, (0.62, 0.62, 0.62, 1)
        e[1].position, e[1].color = 0.25, (0.85, 0.85, 0.85, 1)
        e.new(0.6).color = (1.08, 1.08, 1.08, 1)
        links.new(s2r.outputs[0], ramp.inputs[0])
        cloth = mix('MULTIPLY', st.outputs[0], ramp.outputs[0])
        # How far each arm has swung from where it was painted (render.py).
        fac = None
        aw = nodes.new("ShaderNodeAttribute"); aw.attribute_name = "arm"
        sp = nodes.new("ShaderNodeSeparateXYZ")
        links.new(aw.outputs["Vector"], sp.inputs[0])
        for j, sd in enumerate("lr"):
            dv = nodes.new("ShaderNodeValue"); dv.name = dv.label = "dev_" + sd
            dv.outputs[0].default_value = 0.0
            k = nodes.new("ShaderNodeMath"); k.operation = 'MULTIPLY'
            links.new(dv.outputs[0], k.inputs[0]); links.new(sp.outputs[j], k.inputs[1])
            if fac is None:
                fac = k.outputs[0]
            else:
                a = nodes.new("ShaderNodeMath"); a.operation = 'ADD'; a.use_clamp = True
                links.new(fac, a.inputs[0]); links.new(k.outputs[0], a.inputs[1])
                fac = a.outputs[0]
        blend = nodes.new("ShaderNodeMix"); blend.data_type = 'RGBA'
        links.new(fac, blend.inputs[0]); links.new(acc, blend.inputs[6]); links.new(cloth, blend.inputs[7])
        acc = blend.outputs[2]
    em = nodes.new("ShaderNodeEmission")
    links.new(acc, em.inputs[0])
    links.new(em.outputs[0], out.inputs[0])
    return m


def nearest_seen(me, seen_any):
    """For each vertex, the nearest vertex (along the mesh) that some
    painting saw: itself if one did."""
    from collections import deque
    nb = [[] for _ in me.vertices]
    for e in me.edges:
        a, b = e.vertices
        nb[a].append(b); nb[b].append(a)
    src = np.where(seen_any, np.arange(len(me.vertices)), -1)
    q = deque(np.nonzero(seen_any)[0])
    while q:
        i = q.popleft()
        for j in nb[i]:
            if src[j] < 0:
                src[j] = src[i]
                q.append(j)
    return src


# ---------------------------------------------------------------- clean mode
#
# DOLL_CLEAN=1: the clothes are not painted with the painting's pixels -
# resampled onto a moving body those go wavy - but drawn clean, in flat
# tones taken from it: each piece's shadow, middle and light colours (and
# a check woven in the shirt's own two blues, where the painting shows
# one), lit in hard steps. The head and the hat move rigidly and keep
# their projected paint, which holds up there.

CLEAN = os.environ.get("DOLL_CLEAN") == "1"
KEEP_PAINT = {"head", "tuque", "eyes"}


def lin(c):
    c = np.asarray(c, float) / 255
    return tuple(np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4))


def tones(o, n):
    """The painting's colours under this piece, as (dark, middle, light) and
    whether it is patterned (a check shows as a wide spread of lightness)."""
    cols = []
    for j, view in enumerate(VIEWS):
        sn = seen[o.name][view][:n] > 0.5
        uv = uvs[o.name][view][:n][sn]
        px = np.clip((uv[:, 0] * W).astype(int), 0, W - 1)
        py = np.clip(((1 - uv[:, 1]) * H).astype(int), 0, H - 1)
        cols.append(paints[view][py, px, :3])
    c = np.concatenate(cols).astype(float)
    lum = c @ (0.3, 0.59, 0.11)
    c, lum = c[lum > 35], lum[lum > 35]           # not the ink
    if len(c) < 10:
        return None
    # The piece's own colour is the commonest under it: much of a piece
    # lands on its neighbours' paint (the trousers on the boots), never the
    # most of it. Its shades are the colours near that one.
    q = (c // 24).astype(int)
    keys, counts = np.unique(q[:, 0] * 10000 + q[:, 1] * 100 + q[:, 2], return_counts=True)
    k = keys[counts.argmax()]
    seed = (np.array([k // 10000, k // 100 % 100, k % 100]) + 0.5) * 24
    near = np.linalg.norm(c - seed, axis=1) < 50
    # A check (the gingham) spreads its lightness wide: all of it is the
    # cloth, in its light, middle and dark.
    check = lum.std() > 38
    if check:
        near[:] = True
    c, lum = c[near], lum[near]
    lo, hi = np.percentile(lum, 30), np.percentile(lum, 70)
    band = lambda m: np.median(c[m], 0) if m.any() else np.median(c, 0)
    return band(lum <= lo), band((lum > lo) & (lum < hi)), band(lum >= hi), check


def cel(name, dims, dark, mid, light, check):
    m = bpy.data.materials.new("clean_" + name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    dif = nodes.new("ShaderNodeBsdfDiffuse")
    s2r = nodes.new("ShaderNodeShaderToRGB")
    links.new(dif.outputs[0], s2r.inputs[0])
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = 'CONSTANT'
    e = ramp.color_ramp.elements
    links.new(s2r.outputs[0], ramp.inputs[0])
    em = nodes.new("ShaderNodeEmission")
    if not check:
        # Three flat tones, as the painting has them.
        e[0].position, e[0].color = 0.0, (*lin(dark), 1)
        e[1].position, e[1].color = 0.22, (*lin(mid), 1)
        e.new(0.62).color = (*lin(light), 1)
        links.new(ramp.outputs[0], em.inputs[0])
    else:
        # A gingham: stripes across and down, light where neither falls,
        # middle where one does, dark where they cross; lit in two steps.
        tc = nodes.new("ShaderNodeTexCoord")
        sep = nodes.new("ShaderNodeSeparateXYZ")
        links.new(tc.outputs["Generated"], sep.inputs[0])
        period = 3.0 * look.PX            # metres: three pixels a check

        def stripe(*axes):
            acc = None
            for ax, size in axes:
                mul = nodes.new("ShaderNodeMath"); mul.operation = 'MULTIPLY'
                links.new(sep.outputs[ax], mul.inputs[0]); mul.inputs[1].default_value = size / period
                acc = mul.outputs[0] if acc is None else _add(acc, mul.outputs[0])
            fr = nodes.new("ShaderNodeMath"); fr.operation = 'FRACT'
            links.new(acc, fr.inputs[0])
            lt = nodes.new("ShaderNodeMath"); lt.operation = 'LESS_THAN'
            links.new(fr.outputs[0], lt.inputs[0]); lt.inputs[1].default_value = 0.5
            return lt.outputs[0]

        def _add(a, b):
            ad = nodes.new("ShaderNodeMath"); ad.operation = 'ADD'
            links.new(a, ad.inputs[0]); links.new(b, ad.inputs[1])
            return ad.outputs[0]

        g = _add(stripe((0, dims[0]), (1, dims[1])), stripe((2, dims[2])))
        sc_ = nodes.new("ShaderNodeMath"); sc_.operation = 'MULTIPLY'
        links.new(g, sc_.inputs[0]); sc_.inputs[1].default_value = 0.5
        weave = nodes.new("ShaderNodeValToRGB")
        weave.color_ramp.interpolation = 'CONSTANT'
        w = weave.color_ramp.elements
        w[0].position, w[0].color = 0.0, (*lin(light), 1)
        w[1].position, w[1].color = 0.4, (*lin(mid), 1)
        w.new(0.9).color = (*lin(dark), 1)
        links.new(sc_.outputs[0], weave.inputs[0])
        e[0].position, e[0].color = 0.0, (0.72, 0.72, 0.72, 1)
        e[1].position, e[1].color = 0.3, (1.0, 1.0, 1.0, 1)
        mul = nodes.new("ShaderNodeMix"); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'
        mul.inputs[0].default_value = 1.0
        links.new(weave.outputs[0], mul.inputs[6]); links.new(ramp.outputs[0], mul.inputs[7])
        links.new(mul.outputs[2], em.inputs[0])
    links.new(em.outputs[0], out.inputs[0])
    return m


for o in meshes:
    me = o.data
    n = len(me.vertices)
    al, ar = arm_weights(o)
    at = me.attributes.get("arm") or me.attributes.new("arm", 'FLOAT_VECTOR', 'POINT')
    at.data.foreach_set("vector", np.stack([al, ar, np.zeros(n)], 1).astype(np.float32).ravel())
    sw = swatch(o, al, uvs[o.name]["front"][:n], seen[o.name]["front"][:n]) if al.max() > 0 else None
    mat = material(o.name, sw, tuple(o.dimensions) if sw else None)
    # A modifier (the breechcloth's thickness) can add vertices after the
    # mesh's own, which come first; only those carry the paint.
    W4 = np.stack([wts[o.name][v][:n] for v in VIEWS], 1)
    for v in VIEWS:
        uvs[o.name][v] = uvs[o.name][v][:n]
    # Where the camera's own painting didn't see a spot, the one painting
    # that saw it most squarely - one, not a blend: the views disagree on
    # detail and a crossfade smears it. Seen by none (inside an armpit), the
    # nearest spot that was seen lends its paint.
    best = W4.argmax(1)
    src = nearest_seen(me, W4.max(1) > 0)
    for i in np.nonzero(src != np.arange(n))[0]:
        if src[i] < 0:
            continue
        best[i] = best[src[i]]
        uvs[o.name][list(VIEWS)[best[i]]][i] = uvs[o.name][list(VIEWS)[best[i]]][src[i]]
    W4 = np.zeros((n, len(VIEWS)))
    W4[np.arange(n), best] = 1.0
    for j, view in enumerate(VIEWS):
        uvl = me.uv_layers.get("uv_" + view) or me.uv_layers.new(name="uv_" + view)
        data = uvs[o.name][view]
        for loop in me.loops:
            uvl.data[loop.index].uv = data[loop.vertex_index]
    S4 = np.stack([seen[o.name][v][:n] for v in VIEWS], 1)
    for name, arr in (("w", W4), ("s", S4)):
        at = me.attributes.get(name) or me.attributes.new(name, 'FLOAT_COLOR', 'POINT')
        at.data.foreach_set("color", arr.astype(np.float32).ravel())
    if CLEAN and o.name not in KEEP_PAINT and (t := tones(o, n)):
        mat = cel(o.name, tuple(o.dimensions), *t)
        print(f"[clean] {o.name}: dark {t[0].astype(int)} mid {t[1].astype(int)} light {t[2].astype(int)}{' check' if t[3] else ''}")
    # Each piece its own number, so outline.py can ink where two meet.
    o.pass_index = 1 + meshes.index(o)
    me.materials.clear()
    me.materials.append(mat)
    for poly in me.polygons:
        poly.material_index = 0

for c in hidden:
    c.hide_viewport = False
for o in props:
    o.hide_viewport = False

# render.py shows these collections; nothing else of his.
for c in bpy.data.collections:
    if c.name not in WEAR and c.name not in ("rig", "gun", "tools"):
        for o in list(c.objects):
            o.hide_render = True
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("saved", OUT)
