"""The OpenNPC stickman: proportions, rig, mesh, poses and population instancing.

One base character. Everything else (clips, the logo crowd, the Unity NPC) is
derived from what is defined here, so a proportion change in SPEC propagates
everywhere after a re-run.

Conventions
-----------
* Metres. Feet on z = 0. The character faces -Y (Blender "front").
* Every bone's local Z is aligned to the character's forward axis, so a
  positive rotation about local X always means "swing forward" for limbs and
  "lean forward" for the spine. Knees bend with negative values, elbows with
  positive values, feet lift the toe with positive values.
"""
import math
import random
from dataclasses import dataclass, replace

import bmesh
import bpy
from mathutils import Matrix, Vector

import opennpc_lib as L

# --------------------------------------------------------------------------- #
# 1. proportions
# --------------------------------------------------------------------------- #
SPEC = dict(
    head_center=(0.0, 0.0, 1.585), head_radius=0.200,
    torso_bottom=(0.0, 0.0, 0.905), torso_top=(0.0, 0.0, 1.265),
    torso_r_bottom=0.074, torso_r_top=0.094,
    shoulder=(0.042, 0.0, 1.245), elbow=(0.058, 0.0, 0.955), wrist=(0.064, 0.0, 0.690),
    upper_arm_r=0.053, forearm_r=0.050,
    hip=(0.036, 0.0, 0.880), knee=(0.036, 0.0, 0.478), ankle=(0.036, 0.0, 0.080),
    toe=(0.036, -0.105, 0.052),
    thigh_r=0.064, shin_r=0.058, foot_r=0.050,
    outline=0.026,     # inverted-hull rim thickness (white sticker edge)
)

LEG_LENGTH = SPEC["hip"][2] - 0.02   # hip to ground with a straight leg

FPS = 30

# bone name, head, tail, parent, deform
BONES = [
    ("root", (0, 0, 0), (0, 0, 0.25), None),
    ("hips", (0, 0, 0.880), (0, 0, 0.980), "root"),
    ("spine", (0, 0, 0.900), (0, 0, 1.300), "hips"),
    ("head", (0, 0, 1.370), (0, 0, 1.790), "spine"),
]
for side, sx in (("L", 1.0), ("R", -1.0)):
    m = lambda p: (p[0] * sx, p[1], p[2])
    BONES += [
        ("upper_arm.%s" % side, m(SPEC["shoulder"]), m(SPEC["elbow"]), "spine"),
        ("forearm.%s" % side, m(SPEC["elbow"]), m(SPEC["wrist"]), "upper_arm.%s" % side),
        ("thigh.%s" % side, m(SPEC["hip"]), m(SPEC["knee"]), "hips"),
        ("shin.%s" % side, m(SPEC["knee"]), m(SPEC["ankle"]), "thigh.%s" % side),
        ("foot.%s" % side, m(SPEC["ankle"]), m(SPEC["toe"]), "shin.%s" % side),
    ]
BONE_NAMES = [b[0] for b in BONES]
ANIMATED_BONES = [n for n in BONE_NAMES if n != "root"]


# --------------------------------------------------------------------------- #
# 2. rig + mesh
# --------------------------------------------------------------------------- #
def build_armature(name="NPC_Rig"):
    arm = bpy.data.armatures.new(name)
    arm.display_type = "STICK"
    rig = bpy.data.objects.new(name, arm)
    bpy.context.scene.collection.objects.link(rig)
    L.select_only(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for bname, head, tail, parent in BONES:
        eb = arm.edit_bones.new(bname)
        eb.head, eb.tail = Vector(head), Vector(tail)
        if bname.startswith("foot"):
            eb.align_roll(Vector((0, 0, 1)))
        else:
            eb.align_roll(Vector((0, -1, 0)))
        if parent:
            eb.parent = arm.edit_bones[parent]
        eb.use_connect = False
        eb.use_deform = True
    # Proportion variation scales the spine; the head and arms should keep their
    # shape rather than stretching with it.
    for bname in ("head", "upper_arm.L", "upper_arm.R"):
        arm.edit_bones[bname].inherit_scale = "AVERAGE"
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"
    return rig


def _limb_parts(pad=0.0):
    """(bone, start, end, r_start, r_end, segments) for every capsule."""
    s = SPEC
    parts = [
        ("head", (0, 0, s["head_center"][2]), (0, 0, s["head_center"][2]),
         s["head_radius"] + pad, s["head_radius"] + pad, 28),
        ("spine", s["torso_bottom"], s["torso_top"],
         s["torso_r_bottom"] + pad, s["torso_r_top"] + pad, 18),
    ]
    for side, sx in (("L", 1.0), ("R", -1.0)):
        m = lambda p: (p[0] * sx, p[1], p[2])
        parts += [
            ("upper_arm.%s" % side, m(s["shoulder"]), m(s["elbow"]), s["upper_arm_r"] + pad, s["upper_arm_r"] + pad, 14),
            ("forearm.%s" % side, m(s["elbow"]), m(s["wrist"]), s["forearm_r"] + pad, s["forearm_r"] + pad, 14),
            ("thigh.%s" % side, m(s["hip"]), m(s["knee"]), s["thigh_r"] + pad, s["thigh_r"] + pad, 14),
            ("shin.%s" % side, m(s["knee"]), m(s["ankle"]), s["shin_r"] + pad, s["shin_r"] + pad, 14),
            ("foot.%s" % side, m(s["ankle"]), m(s["toe"]), s["foot_r"] + pad, s["foot_r"] * 0.9 + pad, 12),
        ]
    return parts


def build_skinned_mesh(rig, name, material, pad=0.0, flip=False):
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for bname in BONE_NAMES:
        obj.vertex_groups.new(name=bname)

    bm = bmesh.new()
    deform = bm.verts.layers.deform.verify()
    for bname, start, end, r0, r1, seg in _limb_parts(pad):
        gi = obj.vertex_groups[bname].index
        add_capsule_rings = 8 if bname == "head" else 4
        L.add_capsule(bm, start, end, r0, r1, segments=seg, rings=add_capsule_rings,
                      deform_layer=deform, group_index=gi, flip=flip)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(material)

    obj.parent = rig
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    return obj


def build_character(prefix="NPC"):
    """Returns (rig, body, outline). Body is ink, outline is an inverted hull."""
    ink = L.flat_material("NPC_Ink", L.INK)
    rim = L.flat_material("NPC_Outline", L.WHITE, backface_culling=True)
    rig = build_armature(prefix + "_Rig")
    body = build_skinned_mesh(rig, prefix + "_Body", ink)
    outline = build_skinned_mesh(rig, prefix + "_Outline", rim, pad=SPEC["outline"], flip=True)
    return rig, body, outline


def instance_character(base_rig, base_body, base_outline, name, collection=None):
    """A new NPC sharing the base meshes. Only the armature object is unique,
    so a crowd of 40 costs 2 meshes, not 80."""
    coll = collection or bpy.context.scene.collection
    rig = base_rig.copy()
    rig.data = base_rig.data            # shared armature data too
    rig.name = name
    rig.animation_data_clear()
    rig.hide_render = False
    coll.objects.link(rig)
    parts = []
    for base, suffix in ((base_body, "Body"), (base_outline, "Outline")):
        o = base.copy()
        o.name = "%s_%s" % (name, suffix)
        o.parent = rig
        o.modifiers["Armature"].object = rig
        o.hide_render = False
        coll.objects.link(o)
        parts.append(o)
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"
    return rig, parts[0], parts[1]


# --------------------------------------------------------------------------- #
# 3. poses
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class Gait:
    stride: float = 28.0      # thigh swing amplitude, degrees
    arm: float = 24.0         # upper-arm swing amplitude
    knee: float = 40.0        # extra knee bend during swing
    elbow: float = 16.0       # resting elbow bend
    elbow_swing: float = 26.0 # extra elbow bend on the forward swing
    bounce: float = 0.028     # hip bob, metres
    lean: float = 5.0         # forward spine lean
    twist: float = 6.0        # shoulder counter-rotation
    head_bob: float = 2.5


def stride_length(gait):
    """Ground distance covered per full walk cycle with no foot sliding."""
    return 4.0 * LEG_LENGTH * math.sin(math.radians(gait.stride))


def zero_pose():
    return {n: [0.0, 0.0, 0.0] for n in ANIMATED_BONES}, 0.0


def walk_pose(phase, g):
    """phase in [0, 1). At phase 0 the left leg passes under the body moving forward."""
    rot, _ = zero_pose()
    a = 2.0 * math.pi * phase
    s, c = math.sin(a), math.cos(a)
    for side, sign in (("L", 1.0), ("R", -1.0)):
        ss, cc = s * sign, c * sign
        # knee flexion peaks a little after the thigh passes vertical, so the
        # swinging foot travels under the hip instead of flicking up behind
        lag = math.cos(a - 0.55) * sign
        thigh = g.stride * ss
        knee = -(3.0 + g.knee * max(0.0, lag) ** 2.0 + 4.0 * max(0.0, -ss) ** 2)
        foot = -(thigh + knee) * 0.75 + 12.0 * max(0.0, ss) ** 3 - 10.0 * max(0.0, -cc * ss)
        rot["thigh.%s" % side][0] = thigh
        rot["shin.%s" % side][0] = knee
        rot["foot.%s" % side][0] = foot
        arm = -g.arm * ss
        rot["upper_arm.%s" % side][0] = arm
        rot["forearm.%s" % side][0] = g.elbow + g.elbow_swing * max(0.0, arm / max(g.arm, 1e-3))
    rot["spine"][0] = g.lean + 1.5 * abs(c)
    rot["spine"][1] = g.twist * s
    rot["head"][0] = -g.lean * 0.6 + g.head_bob * math.cos(2.0 * a)
    rot["head"][1] = -g.twist * s * 0.7
    hips_y = g.bounce * math.cos(2.0 * a)
    return rot, hips_y


def idle_pose(t):
    """t in [0, 1): a 3-second breathing / weight-shift / glance loop."""
    rot, _ = zero_pose()
    a = 2.0 * math.pi * t
    breathe = math.sin(a * 2.0)
    rot["spine"][0] = 2.0 + 1.2 * breathe
    rot["head"][0] = -1.0 + 1.5 * math.sin(a * 2.0 + 0.6)
    rot["head"][1] = 14.0 * math.sin(a) ** 3                   # glance left / right
    for side, sign in (("L", 1.0), ("R", -1.0)):
        rot["upper_arm.%s" % side][0] = 2.0 + 1.5 * breathe
        rot["upper_arm.%s" % side][2] = -3.0 * sign
        rot["forearm.%s" % side][0] = 10.0 + 2.0 * breathe
        rot["thigh.%s" % side][0] = 1.5 * sign * math.sin(a)   # weight shift
        rot["shin.%s" % side][0] = -3.0 - 2.0 * max(0.0, sign * math.sin(a))
        rot["foot.%s" % side][0] = 1.0
    return rot, -0.006 * (1.0 - breathe) * 0.5


def talk_pose(t):
    """t in [0, 1): idle base plus explanatory hand gestures."""
    rot, hy = idle_pose(t * 0.5)
    a = 2.0 * math.pi * t
    g1 = max(0.0, math.sin(a)) ** 2
    g2 = max(0.0, math.sin(a * 2.0 + 1.3)) ** 2
    rot["upper_arm.R"][0] = 18.0 + 32.0 * g1
    rot["forearm.R"][0] = 35.0 + 45.0 * g1
    rot["upper_arm.L"][0] = 6.0 + 18.0 * g2
    rot["forearm.L"][0] = 18.0 + 40.0 * g2
    rot["head"][0] = -3.0 + 4.0 * math.sin(a * 3.0)
    rot["head"][1] = 6.0 * math.sin(a)
    rot["spine"][0] = 3.0 + 2.0 * g1
    return rot, hy


def blend(pose_a, pose_b, w):
    ra, ha = pose_a
    rb, hb = pose_b
    return {n: [ra[n][i] + (rb[n][i] - ra[n][i]) * w for i in range(3)] for n in ra}, ha + (hb - ha) * w


def smoothstep(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def stop_pose(u, g):
    """u in [0, 1]: plant the front foot, settle, small overshoot, then idle."""
    w = smoothstep(u * 1.15)
    base = blend(walk_pose(0.25 + 0.2 * u, g), idle_pose(0.0), w)
    rot, hy = base
    overshoot = math.sin(math.pi * min(1.0, u * 1.4)) * (1.0 - u)
    rot["spine"][0] += 7.0 * overshoot
    rot["head"][0] -= 4.0 * overshoot
    return rot, hy - 0.02 * overshoot


def turn_pose(u, direction):
    """u in [0, 1]: in-place stepping turn. The engine rotates the object while
    this plays; the clip supplies the lead-with-the-head body language.
    direction: +1 = turn left, -1 = turn right."""
    rot, hy = idle_pose(0.0)
    head_lead = math.sin(math.pi * min(1.0, u * 1.6))
    torso = math.sin(math.pi * u)
    rot["head"][1] = 32.0 * direction * head_lead
    rot["spine"][1] = 16.0 * direction * torso
    step1 = math.sin(math.pi * min(1.0, u * 2.0)) if u < 0.5 else 0.0
    step2 = math.sin(math.pi * (u * 2.0 - 1.0)) if u >= 0.5 else 0.0
    first, second = ("L", "R") if direction > 0 else ("R", "L")
    for side, st in ((first, step1), (second, step2)):
        rot["thigh.%s" % side][0] = 22.0 * st
        rot["shin.%s" % side][0] = -44.0 * st
        rot["foot.%s" % side][0] = 12.0 * st
    rot["upper_arm.L"][0] += 10.0 * torso * direction
    rot["upper_arm.R"][0] -= 10.0 * torso * direction
    return rot, hy - 0.025 * torso


def apply_pose(rig, pose, key_frame=None):
    rot, hips_y = pose
    for name, (x, y, z) in rot.items():
        pb = rig.pose.bones[name]
        pb.rotation_euler = (math.radians(x), math.radians(y), math.radians(z))
        if key_frame is not None:
            pb.keyframe_insert("rotation_euler", frame=key_frame, group=name)
    hips = rig.pose.bones["hips"]
    hips.location = (0.0, hips_y, 0.0)
    if key_frame is not None:
        hips.keyframe_insert("location", frame=key_frame, group="hips")


def pose_curves(sample, frames):
    """Sample pose(frame) for each frame into write_curves() format."""
    curves = {}
    for f in frames:
        rot, hips_y = sample(f)
        for name, xyz in rot.items():
            for i in range(3):
                curves.setdefault(('pose.bones["%s"].rotation_euler' % name, i, name), []).append(
                    (f, math.radians(xyz[i])))
        curves.setdefault(('pose.bones["hips"].location', 1, "hips"), []).append((f, hips_y))
    return curves


# --------------------------------------------------------------------------- #
# 4. clip catalogue (shared with Unity through stickman_clips.json)
# --------------------------------------------------------------------------- #
WALK = Gait()
WALK_SLOW = replace(WALK, stride=20.0, arm=14.0, knee=30.0, bounce=0.016, lean=3.0, twist=4.0)
WALK_FAST = replace(WALK, stride=33.0, arm=38.0, knee=52.0, elbow=30.0, elbow_swing=40.0,
                    bounce=0.036, lean=10.0, twist=9.0, head_bob=3.5)

CLIPS = [
    # name, frames, loop, sampler(u in [0,1)), native speed m/s (None = in place)
    ("Idle", 90, True, lambda u: idle_pose(u), None),
    ("Walk_Slow", 40, True, lambda u: walk_pose(u, WALK_SLOW), stride_length(WALK_SLOW) / (40 / FPS)),
    ("Walk", 30, True, lambda u: walk_pose(u, WALK), stride_length(WALK) / (30 / FPS)),
    ("Walk_Fast", 22, True, lambda u: walk_pose(u, WALK_FAST), stride_length(WALK_FAST) / (22 / FPS)),
    ("Stop", 16, False, lambda u: stop_pose(u, WALK), None),
    ("Turn_Left", 15, False, lambda u: turn_pose(u, +1.0), None),
    ("Turn_Right", 15, False, lambda u: turn_pose(u, -1.0), None),
    ("Talk", 60, True, lambda u: talk_pose(u), None),
]


def bake_clips(rig):
    for name, frames, loop, sampler, _speed in CLIPS:
        action = L.new_action(rig, name)
        # Looping clips get frames+1 keys so the last key equals the first and
        # Unity's loop has no hitch; one-shots sample u in [0, 1] inclusive.
        count = frames + 1
        for i in range(count):
            u = (i / frames) % 1.0 if loop else i / frames
            if loop and i == frames:
                u = 0.0
            apply_pose(rig, sampler(u), key_frame=i + 1)
        action.frame_range = (1, count)
        print("  baked %-10s %3d frames%s" % (name, count, "  (loop)" if loop else ""))
    L.assign_action(rig, None)
    apply_pose(rig, zero_pose())


# --------------------------------------------------------------------------- #
# 5. procedural individuality
# --------------------------------------------------------------------------- #
@dataclass
class Individual:
    height: float        # non-uniform Z scale on the rig object
    girth: float         # X/Y scale
    head: float          # head bone scale
    torso: float         # spine bone Y scale
    gait: Gait
    phase: float


def random_individual(rng: random.Random):
    g = Gait(
        stride=rng.uniform(22.0, 34.0),
        arm=rng.uniform(14.0, 34.0),
        knee=rng.uniform(32.0, 48.0),
        elbow=rng.uniform(8.0, 28.0),
        elbow_swing=rng.uniform(14.0, 36.0),
        bounce=rng.uniform(0.015, 0.04),
        lean=rng.uniform(1.0, 11.0),
        twist=rng.uniform(3.0, 9.0),
        head_bob=rng.uniform(1.0, 4.0),
    )
    return Individual(
        height=rng.uniform(0.9, 1.1),
        girth=rng.uniform(0.9, 1.12),
        head=rng.uniform(0.86, 1.16),
        torso=rng.uniform(0.9, 1.12),
        gait=g,
        phase=rng.random(),
    )


def apply_individual_shape(rig, ind, base_scale=1.0):
    rig.scale = (base_scale * ind.girth, base_scale * ind.girth, base_scale * ind.height)
    rig.pose.bones["head"].scale = (ind.head, ind.head, ind.head)
    rig.pose.bones["spine"].scale = (1.0, ind.torso, 1.0)


def push_outline_back(rig, outline, depth=0.45, view_dir=(0.0, 1.0, 0.0)):
    """Slide the outline hull away from an orthographic camera.

    In orthographic projection a shift along the view axis does not change the
    hull's silhouette on screen, but it puts the hull behind every body part of
    the same character, so overlapping limbs no longer show white slivers.
    Call after the rig's rotation and scale are final.
    """
    to_local = Matrix.LocRotScale(None, rig.rotation_euler, rig.scale).inverted().to_3x3()
    outline.location = to_local @ (Vector(view_dir) * depth * max(rig.scale))
