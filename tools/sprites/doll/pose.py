"""Poses on the game-engine rig, written as turns in the body's own axes
rather than each bone's: the man faces -y, x is to his left, z is up.

Each bone's turn is given as if its parent had not moved (the chain adds
them up), so `fwd(30)` on a thigh swings the leg 30 degrees forward
whatever the bone's roll. A pose is {bone: Quaternion}; `apply` sets it.
"""
import math
import bpy
from mathutils import Quaternion, Vector, Matrix

X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))


def q(axis, deg):
    return Quaternion(axis, math.radians(deg))


def fwd(deg):
    """Swing forward (toward -y, the way he faces); negative swings back."""
    return q(X, -deg)


def out(side, deg):
    """Swing away from the body sideways; side 'l' (+x) or 'r'."""
    return q(Y, -deg if side == "l" else deg)


def twist(deg):
    """Turn about the vertical, to his left."""
    return q(Z, deg)


def side_tilt(deg):
    """Lean toward his left."""
    return q(Y, -deg)


def rest_spread(rig, side):
    """How far the arm stands out from hanging straight down, at rest."""
    b = rig.data.bones[f"upperarm_{side}"]
    d = (b.tail_local - b.head_local).normalized()
    return math.degrees(math.atan2(abs(d.x), -d.z))


def clear(rig):
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = Quaternion()
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)


def apply(rig, pose, lift=0.0, shift=Vector()):
    """Set `pose` ({bone: armature-axes turn}) and move the pelvis by
    `lift` (m, up) and `shift` (armature axes)."""
    clear(rig)
    for name, d in pose.items():
        pb = rig.pose.bones[name]
        rb = pb.bone.matrix_local.to_quaternion()
        pb.rotation_quaternion = rb.inverted() @ d @ rb
    pel = rig.pose.bones["pelvis"]
    off = Vector((shift.x, shift.y, shift.z + lift))
    pel.location = pel.bone.matrix_local.to_3x3().inverted() @ off


def arms_down(rig, elbow=None, hang=None):
    """Arms hanging at the sides from the A-pose rest; a doll fitted to a
    painted man (fit.py) keeps how his arms hang on the rig."""
    elbow = rig.get("elbow", 12) if elbow is None else elbow
    hang = rig.get("hang", 8) if hang is None else hang
    p = {}
    for s in "lr":
        p[f"upperarm_{s}"] = fwd(rig.get("swing", 0)) @ out(s, hang - rest_spread(rig, s))
        p[f"lowerarm_{s}"] = fwd(elbow)
    return p


def idle(rig, t=0.0):
    p = arms_down(rig)
    return p, 0.0


def walk(rig, t):
    """The walk at phase t (0..1): one stride of each leg."""
    s, c = math.sin(2 * math.pi * t), math.cos(2 * math.pi * t)
    p = arms_down(rig, elbow=18)
    for side, sg in (("l", 1), ("r", -1)):
        th = 28 * s * sg
        # The knee folds as the leg comes through, most when it passes.
        kn = 8 + 28 * max(0.0, c * sg)
        p[f"thigh_{side}"] = fwd(th)
        p[f"calf_{side}"] = fwd(-kn)
        p[f"foot_{side}"] = fwd(kn - th - 4 * max(0.0, -c * sg))
        # The arms swing against the legs.
        p[f"upperarm_{side}"] = fwd(-22 * s * sg) @ p[f"upperarm_{side}"]
    p["spine_01"] = twist(-5 * s)
    p["spine_03"] = fwd(4)
    lift = -0.025 * abs(s) + 0.01 * abs(c)
    return p, lift
