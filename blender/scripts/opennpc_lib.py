"""Shared Blender helpers for every OpenNPC build script.

Kept deliberately small: paths, scene reset, flat materials, capsule geometry,
fast keyframe writing and the one FBX export preset.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

# --------------------------------------------------------------------------- #
# paths
# --------------------------------------------------------------------------- #
SCRIPTS = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(SCRIPTS, "..", ".."))
BLENDER_SOURCE = os.path.join(REPO, "blender", "source")
BLENDER_EXPORTS = os.path.join(REPO, "blender", "exports")
BRANDING = os.path.join(REPO, "branding")
FONTS = os.path.join(REPO, "assets", "fonts")

# The one palette. Web, Unity and renders all use these values.
PAPER = "#F3F1EC"   # off-white ground
INK = "#111111"     # near-black, never pure #000 (reads softer on screens)
WHITE = "#FFFFFF"


def hex_to_linear(hex_colour, alpha=1.0):
    h = hex_colour.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (*lin, alpha)


def args_after_dashes():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)
    return path


# --------------------------------------------------------------------------- #
# scene
# --------------------------------------------------------------------------- #
def reset_scene(fps=30):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = fps
    return scene


def select_only(*objs, active=None):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or (objs[0] if objs else None)


def flat_render_settings(scene, width, height, background=PAPER, transparent=False, aa="16"):
    """Workbench, flat lighting, material colour: pure 2D-looking ink on paper."""
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_backface_culling = True   # makes the inverted-hull outline work
    scene.display.shading.show_object_outline = False
    scene.display.shading.show_cavity = False
    scene.display.shading.show_shadows = False
    scene.display.shading.show_specular_highlight = False
    scene.display.render_aa = aa
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = transparent
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA" if transparent else "RGB"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Paper")
    scene.world.color = hex_to_linear(background)[:3]


def ortho_camera(scene, name, location, rotation, ortho_scale):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.ortho_scale = ortho_scale
    data.clip_start = 0.1
    data.clip_end = 500.0
    cam = bpy.data.objects.new(name, data)
    cam.location = location
    cam.rotation_euler = rotation
    scene.collection.objects.link(cam)
    return cam


# --------------------------------------------------------------------------- #
# materials
# --------------------------------------------------------------------------- #
def flat_material(name, hex_colour, backface_culling=False):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = hex_to_linear(hex_colour)
    mat.use_backface_culling = backface_culling
    mat.roughness = 1.0
    mat.metallic = 0.0
    mat.specular_intensity = 0.0
    return mat


# --------------------------------------------------------------------------- #
# geometry
# --------------------------------------------------------------------------- #
def add_capsule(bm, start, end, r_start, r_end, segments=16, rings=4, deform_layer=None,
                group_index=0, flip=False):
    """Append a (possibly tapered) capsule from `start` to `end` into `bm`.

    Built from explicit rings so the two hemispheres can have different radii.
    Every vertex is tagged with `group_index` in the deform layer (rigid skin).
    """
    start, end = Vector(start), Vector(end)
    axis = end - start
    length = axis.length
    if length < 1e-6:
        axis = Vector((0, 0, 1))
    align = axis.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
    xform = Matrix.Translation(start) @ align

    profile = []  # (z along axis, radius)
    for i in range(rings + 1):                       # bottom hemisphere, pole -> equator
        a = -math.pi / 2 + (math.pi / 2) * i / rings
        profile.append((math.sin(a) * r_start, math.cos(a) * r_start))
    for i in range(rings + 1):                       # top hemisphere, equator -> pole
        a = (math.pi / 2) * i / rings
        profile.append((length + math.sin(a) * r_end, math.cos(a) * r_end))

    ring_verts = []
    for z, r in profile:
        if r < 1e-5:
            v = bm.verts.new(xform @ Vector((0, 0, z)))
            ring_verts.append([v])
        else:
            ring = []
            for s in range(segments):
                t = 2 * math.pi * s / segments
                ring.append(bm.verts.new(xform @ Vector((math.cos(t) * r, math.sin(t) * r, z))))
            ring_verts.append(ring)

    faces = []
    for a, b in zip(ring_verts, ring_verts[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        if len(a) == 1:
            for s in range(segments):
                faces.append((a[0], b[(s + 1) % segments], b[s]))
        elif len(b) == 1:
            for s in range(segments):
                faces.append((a[s], a[(s + 1) % segments], b[0]))
        else:
            for s in range(segments):
                faces.append((a[s], a[(s + 1) % segments], b[(s + 1) % segments], b[s]))
    for f in faces:
        face = bm.faces.new(f[::-1] if flip else f)
        face.smooth = True

    if deform_layer is not None:
        for ring in ring_verts:
            for v in ring:
                v[deform_layer][group_index] = 1.0


# --------------------------------------------------------------------------- #
# animation
# --------------------------------------------------------------------------- #
def new_action(obj, name):
    if obj.animation_data is None:
        obj.animation_data_create()
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    obj.animation_data.action = action
    if hasattr(action, "slots") and len(action.slots) == 0:
        obj.animation_data.action_slot = action.slots.new(id_type="OBJECT", name=obj.name)
    return action


def assign_action(obj, action):
    if obj.animation_data is None:
        obj.animation_data_create()
    obj.animation_data.action = action
    if action is not None and hasattr(obj.animation_data, "action_slot") and len(action.slots):
        obj.animation_data.action_slot = action.slots[0]


def _fcurve_collection(obj, action):
    """F-curve container for the object's slot (Blender 4.4+ layered actions)."""
    try:
        from bpy_extras import anim_utils
        slot = obj.animation_data.action_slot
        bag = anim_utils.action_ensure_channelbag_for_slot(action, slot)
        return bag.fcurves
    except (ImportError, AttributeError):
        return action.fcurves


def write_curves(obj, action, curves, interpolation="LINEAR"):
    """Bulk-write keys. `curves` maps (data_path, index, group) -> [(frame, value), ...].

    Orders of magnitude faster than keyframe_insert for population animation.
    """
    fcurves = _fcurve_collection(obj, action)
    for (path, index, group), keys in curves.items():
        fc = fcurves.find(path, index=index) or fcurves.new(path, index=index, group_name=group)
        fc.keyframe_points.clear()
        fc.keyframe_points.add(len(keys))
        flat = [c for k in keys for c in k]
        fc.keyframe_points.foreach_set("co", flat)
        for kp in fc.keyframe_points:
            kp.interpolation = interpolation
        fc.update()


# --------------------------------------------------------------------------- #
# export
# --------------------------------------------------------------------------- #
def export_fbx(path, objects, animated=False):
    """The single FBX preset. If Unity ever sees the character rotated or 100x
    scaled, this is the only place that should change."""
    ensure_dir(os.path.dirname(path))
    select_only(*objects, active=objects[0])
    kwargs = dict(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_tspace=False,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=True,
        armature_nodetype="NULL",
        path_mode="STRIP",
        embed_textures=False,
        bake_anim=animated,
    )
    if animated:
        kwargs.update(
            bake_anim_use_all_bones=True,
            bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=True,
            bake_anim_force_startend_keying=True,
            bake_anim_step=1.0,
            bake_anim_simplify_factor=0.0,
        )
    bpy.ops.export_scene.fbx(**kwargs)
    print("  exported", path)
