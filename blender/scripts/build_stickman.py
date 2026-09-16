"""Build the OpenNPC stickman: rig, mesh, clips, .blend, FBX and preview renders.

    blender --background --factory-startup --python blender/scripts/build_stickman.py
    blender ... -- --no-previews          # skip the preview renders

Outputs
    blender/source/OpenNPC_Stickman.blend
    blender/exports/OpenNPC_Stickman.fbx        (mesh + outline + armature + all clips)
    blender/exports/stickman_clips.json         (clip lengths, loop flags, native speeds)
    branding/renders/previews/stickman_*.png
    assets/reference/opennpc_stickman_silhouette.png
    web/assets/stickman-walk.png                (12-frame transparent sprite sheet)
"""
import json
import math
import os
import random
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

import opennpc_lib as L  # noqa: E402
import stickman as S  # noqa: E402

BLEND = os.path.join(L.BLENDER_SOURCE, "OpenNPC_Stickman.blend")
FBX = os.path.join(L.BLENDER_EXPORTS, "OpenNPC_Stickman.fbx")
CLIPS_JSON = os.path.join(L.BLENDER_EXPORTS, "stickman_clips.json")
PREVIEWS = os.path.join(L.BRANDING, "renders", "previews")
REFERENCE = os.path.join(L.REPO, "assets", "reference")
WEB_ASSETS = os.path.join(L.REPO, "web", "assets")
SPRITE_FRAMES = 12


def write_clip_manifest():
    data = {
        "fps": S.FPS,
        "height": S.SPEC["head_center"][2] + S.SPEC["head_radius"],
        "clips": [
            {"name": n, "frames": f + 1, "loop": loop,
             "nativeSpeed": round(speed, 3) if speed else 0.0}
            for n, f, loop, _s, speed in S.CLIPS
        ],
    }
    L.ensure_dir(os.path.dirname(CLIPS_JSON))
    with open(CLIPS_JSON, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=2)
    print("  wrote", CLIPS_JSON)


# --------------------------------------------------------------------------- #
# previews
# --------------------------------------------------------------------------- #
def front_camera(scene, center_x, center_z, ortho):
    cam = L.ortho_camera(scene, "PreviewCam", (center_x, -40.0, center_z),
                         (math.pi / 2, 0.0, 0.0), ortho)
    scene.camera = cam
    return cam


def render(scene, path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("  rendered", path)


def clear_instances(names):
    for o in list(bpy.data.objects):
        if o.name.split("_")[0] in names:
            bpy.data.objects.remove(o, do_unlink=True)


def render_previews(base):
    scene = bpy.context.scene
    for o in base:
        o.hide_render = True
    L.ensure_dir(PREVIEWS)

    # A: one walk cycle, eight phases, side view facing right
    L.flat_render_settings(scene, 2400, 620)
    for i in range(8):
        rig, _, rim = S.instance_character(*base, name="W%d" % i)
        rig.location = (i * 1.05, 0.0, 0.0)
        rig.rotation_euler = (0, 0, math.radians(90))
        S.push_outline_back(rig, rim)
        S.apply_pose(rig, S.walk_pose(i / 8.0, S.WALK))
    cam = front_camera(scene, 3.5 * 1.05, 0.92, 8.6)
    render(scene, os.path.join(PREVIEWS, "stickman_walk_cycle.png"))
    bpy.data.objects.remove(cam, do_unlink=True)
    clear_instances({"W0", "W1", "W2", "W3", "W4", "W5", "W6", "W7"})

    # B: turnaround + every clip's key pose
    L.flat_render_settings(scene, 2400, 620)
    poses = [
        (0, S.idle_pose(0.0)), (40, S.idle_pose(0.0)), (90, S.idle_pose(0.0)),
        (90, S.walk_pose(0.25, S.WALK_SLOW)), (90, S.walk_pose(0.25, S.WALK_FAST)),
        (90, S.stop_pose(0.35, S.WALK)), (60, S.turn_pose(0.3, 1.0)), (-30, S.talk_pose(0.25)),
    ]
    for i, (yaw, pose) in enumerate(poses):
        rig, _, rim = S.instance_character(*base, name="T%d" % i)
        rig.location = (i * 1.05, 0.0, 0.0)
        rig.rotation_euler = (0, 0, math.radians(yaw))
        if abs(yaw) == 90:          # the depth push is only exact for side-on figures
            S.push_outline_back(rig, rim)
        S.apply_pose(rig, pose)
    cam = front_camera(scene, 3.5 * 1.05, 0.92, 8.6)
    render(scene, os.path.join(PREVIEWS, "stickman_poses.png"))
    bpy.data.objects.remove(cam, do_unlink=True)
    clear_instances({"T%d" % i for i in range(8)})

    # C: ten individuals from one base — the variation system
    rng = random.Random(7)
    for i in range(10):
        ind = S.random_individual(rng)
        rig, _, rim = S.instance_character(*base, name="V%d" % i)
        rig.location = (i * 0.78 + rng.uniform(-0.1, 0.1), i % 2 * 0.6, 0.0)
        rig.rotation_euler = (0, 0, math.radians(90 if rng.random() < 0.6 else -90))
        S.apply_individual_shape(rig, ind)
        S.push_outline_back(rig, rim, depth=0.3)
        S.apply_pose(rig, S.walk_pose(ind.phase, ind.gait))
    cam = front_camera(scene, 4.5 * 0.78, 0.95, 8.6)
    render(scene, os.path.join(PREVIEWS, "stickman_variation.png"))
    bpy.data.objects.remove(cam, do_unlink=True)
    clear_instances({"V%d" % i for i in range(10)})

    # E: web sprite sheet — one row of walk phases, facing right, transparent
    frame_w, frame_h, n = 96, 128, SPRITE_FRAMES
    L.flat_render_settings(scene, frame_w * n, frame_h, transparent=True)
    spacing = 1.45                     # metres per cell; ortho_scale below is the frame width
    for i in range(n):
        rig, _, rim = S.instance_character(*base, name="S%d" % i)
        rig.location = (i * spacing, 0.0, 0.0)
        rig.rotation_euler = (0, 0, math.radians(90))
        S.push_outline_back(rig, rim)
        S.apply_pose(rig, S.walk_pose(i / n, S.WALK))
    cam = front_camera(scene, (n - 1) * spacing / 2.0, 0.93, spacing * n)
    L.ensure_dir(WEB_ASSETS)
    render(scene, os.path.join(WEB_ASSETS, "stickman-walk.png"))
    bpy.data.objects.remove(cam, do_unlink=True)
    clear_instances({"S%d" % i for i in range(n)})

    # D: single silhouette used as the in-repo reference image
    L.flat_render_settings(scene, 600, 760, transparent=False)
    rig, _, rim = S.instance_character(*base, name="R0")
    rig.rotation_euler = (0, 0, math.radians(90))
    S.push_outline_back(rig, rim)
    S.apply_pose(rig, S.walk_pose(0.22, S.WALK))
    cam = front_camera(scene, 0.0, 0.92, 2.2)
    L.ensure_dir(REFERENCE)
    render(scene, os.path.join(REFERENCE, "opennpc_stickman_silhouette.png"))
    bpy.data.objects.remove(cam, do_unlink=True)
    clear_instances({"R0"})


def main():
    t0 = time.time()
    args = L.args_after_dashes()
    L.reset_scene(fps=S.FPS)

    rig, body, outline = S.build_character("NPC")
    S.bake_clips(rig)

    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    print("  stickman: %d verts, %d tris (+%d outline), %d bones, %d clips" % (
        len(body.data.vertices), tris, tris, len(rig.data.bones), len(bpy.data.actions)))

    L.ensure_dir(L.BLENDER_SOURCE)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("  saved", BLEND)
    L.export_fbx(FBX, [rig, body, outline], animated=True)
    write_clip_manifest()

    if "--no-previews" not in args:
        render_previews((rig, body, outline))
    print("  done in %.1fs" % (time.time() - t0))


if __name__ == "__main__":
    main()
