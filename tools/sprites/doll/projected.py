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
images = {}
for view, (turn, frame, mirror) in VIEWS.items():
    rig.rotation_euler = (0, 0, math.radians(turn))
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    dmask = render_mask(view)
    paint, pmask = unink(load(frame, mirror))
    path = os.path.join(TMP, f"paint_{view}.png")
    write(paint, path)
    images[view] = path
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


# ---------------------------------------------------------------- material

def material():
    m = bpy.data.materials.new("painted")
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

    acc = cam_acc = cam_sum = None
    for i, view in enumerate(VIEWS):
        uvn = nodes.new("ShaderNodeUVMap"); uvn.uv_map = "uv_" + view
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(images[view], check_existing=True)
        tex.interpolation = 'Closest'
        links.new(uvn.outputs[0], tex.inputs[0])
        at = nodes.new("ShaderNodeAttribute"); at.attribute_name = "w_" + view
        mul = nodes.new("ShaderNodeMix"); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'
        mul.inputs[0].default_value = 1.0
        links.new(tex.outputs[0], mul.inputs[6]); links.new(at.outputs["Fac"], mul.inputs[7])
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
        sa = nodes.new("ShaderNodeAttribute"); sa.attribute_name = "s_" + view
        k = mix('MULTIPLY', cv.outputs[0], sa.outputs["Fac"])
        part = mix('MULTIPLY', tex.outputs[0], k)
        cam_acc = part if cam_acc is None else mix('ADD', cam_acc, part)
        cam_sum = k if cam_sum is None else mix('ADD', cam_sum, k)
    rest = mix('SUBTRACT', 1.0, cam_sum)
    acc = mix('ADD', cam_acc, mix('MULTIPLY', acc, rest))
    em = nodes.new("ShaderNodeEmission")
    links.new(acc, em.inputs[0])
    links.new(em.outputs[0], out.inputs[0])
    return m


mat = material()
for o in meshes:
    me = o.data
    n = len(me.vertices)
    W4 = np.stack([wts[o.name][v] for v in VIEWS], 1)
    tot = W4.sum(1)
    # Nowhere seen (an armpit, the inside of a sleeve): the nearest view's paint.
    for i in np.nonzero(tot < 1e-4)[0]:
        W4[i, :] = 0
        W4[i, int(np.argmax([uvs[o.name][v][i][0] * 0 + 1 for v in VIEWS]))] = 1
    W4 = W4 / np.maximum(W4.sum(1, keepdims=True), 1e-6)
    # Sharpen the blend: the painted views don't agree on detail, and a
    # crossfade between them smears it.
    W4 = W4 ** 3
    W4 = W4 / W4.sum(1, keepdims=True)
    for j, view in enumerate(VIEWS):
        uvl = me.uv_layers.get("uv_" + view) or me.uv_layers.new(name="uv_" + view)
        data = uvs[o.name][view]
        for loop in me.loops:
            uvl.data[loop.index].uv = data[loop.vertex_index]
        at = me.attributes.get("w_" + view) or me.attributes.new("w_" + view, 'FLOAT', 'POINT')
        for i in range(n):
            at.data[i].value = float(W4[i, j])
        sa = me.attributes.get("s_" + view) or me.attributes.new("s_" + view, 'FLOAT', 'POINT')
        for i in range(n):
            sa.data[i].value = float(seen[o.name][view][i])
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
