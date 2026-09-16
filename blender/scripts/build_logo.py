"""OpenNPC logo animation: the wordmark with a crowd of NPCs walking behind it.

    blender --background --factory-startup --python blender/scripts/build_logo.py -- [options]

Options
    --preview        a 6-frame contact sheet at half resolution (fast look-dev)
    --frames         render the full 16:9 loop to branding/renders/frames/
    --square         also render the 1:1 loop to branding/renders/frames_square/
    --stills         hero PNG, transparent PNG and 1:1 still
    --seed N         crowd seed (default 11)

Always saves
    branding/blender/OpenNPC_Logo_Animation.blend
    branding/blender/OpenNPC_Logo.blend          (static hero frame)

How the loop stays seamless (LOOP_SECONDS = 8)
    Every NPC walks a wrap-around lane of width W. Its speed is W / LOOP, so it
    is back where it started after exactly one loop, and its number of stride
    cycles is an integer, so its legs are too. The stride angle is then solved
    from the speed, so feet do not slide. Nothing is shared between NPCs except
    the base mesh — speed, scale, proportions, gait, phase, lane and direction
    are all per-individual.
"""
import math
import os
import random
import sys
import time
from dataclasses import replace

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

import opennpc_lib as L  # noqa: E402
import stickman as S  # noqa: E402

FPS = 30
LOOP_SECONDS = 8.0                         # longer loop = slower walkers, still seamless
LOOP = int(FPS * LOOP_SECONDS)             # 240 frames; frame LOOP+1 == frame 1

FRAME_W, FRAME_H = 16.0, 9.0               # orthographic frame in scene units
LOGO_TEXT = "OpenNPC"
NPC_COLOUR = "#D9281E"                     # the crowd is red; the wordmark stays ink
LOGO_WIDTH = 10.6                          # wordmark width in scene units
LOGO_CENTER_Z = 0.35
KEYLINE = 0.075                            # white gap between letters and anything behind

HERO_FRAME = 61
CAMERA_Z = -0.45                           # frame sits a little low: crowd below, air above

# Depth layers. feet_z = vertical placement in frame, depth = distance behind the
# wordmark (negative = in front), scale = character scale, extra = additional
# off-screen lane length range (bigger = appears less often, walks faster).
LANES = [
    dict(name="far", feet_z=0.75, depth=14.0, scale=0.92, count=8, extra=(0.0, 3.0)),
    dict(name="mid", feet_z=-1.35, depth=9.0, scale=1.15, count=8, extra=(0.0, 4.0)),
    dict(name="near", feet_z=-3.2, depth=4.0, scale=1.45, count=5, extra=(1.0, 6.0)),
    dict(name="front", feet_z=-4.95, depth=-5.0, scale=2.15, count=2, extra=(4.0, 8.0)),
]

BLEND_DIR = os.path.join(L.BRANDING, "blender")
FRAMES = os.path.join(L.BRANDING, "renders", "frames")
FRAMES_SQUARE = os.path.join(L.BRANDING, "renders", "frames_square")
LOGO_RENDERS = os.path.join(L.BRANDING, "renders", "logo")
PNG = os.path.join(L.BRANDING, "png")
FONT = os.path.join(L.FONTS, "BebasNeue", "BebasNeue-Regular.ttf")


# --------------------------------------------------------------------------- #
# wordmark
# --------------------------------------------------------------------------- #
def build_wordmark():
    font = bpy.data.fonts.load(FONT)
    ink = L.flat_material("Logo_Ink", L.INK)
    gap = L.flat_material("Logo_Keyline", L.PAPER)

    def text_object(name, material, offset, depth):
        cu = bpy.data.curves.new(name, type="FONT")
        cu.body = LOGO_TEXT
        cu.font = font
        cu.align_x = "CENTER"
        cu.align_y = "CENTER"
        cu.resolution_u = 24
        cu.offset = offset
        cu.materials.append(material)
        ob = bpy.data.objects.new(name, cu)
        ob.rotation_euler = (math.pi / 2, 0.0, 0.0)   # stand up in XZ, face the camera
        ob.location = (0.0, depth, LOGO_CENTER_Z)
        bpy.context.scene.collection.objects.link(ob)
        return ob

    word = text_object("OpenNPC_Wordmark", ink, 0.0, 0.0)
    bpy.context.view_layer.update()
    size = LOGO_WIDTH / max(word.dimensions.x, 1e-3)
    word.data.size = size
    bpy.context.view_layer.update()

    # The keyline is the same text inflated and painted in paper colour, placed
    # just behind the ink. Anything further back is cut cleanly around the
    # letters, which is what makes the crowd read as "behind the sign".
    keyline = text_object("OpenNPC_Keyline", gap, KEYLINE, 0.6)   # offset is not scaled by font size
    keyline.data.size = size
    bpy.context.view_layer.update()
    print("  wordmark %.2f x %.2f units (font size %.2f)" % (word.dimensions.x, word.dimensions.y, size))
    return word, keyline


# --------------------------------------------------------------------------- #
# crowd
# --------------------------------------------------------------------------- #
def solve_gait(ind, speed_world, scale):
    """Pick an integer number of stride cycles per loop and the stride angle that
    makes the feet stick to the ground at this speed."""
    speed_char = speed_world / scale                      # metres/second in character space
    best = None
    for cycles in range(1, 40):
        cadence = cycles / LOOP_SECONDS
        ratio = speed_char / (cadence * 4.0 * S.LEG_LENGTH)
        if ratio >= 1.0:
            continue
        stride = math.degrees(math.asin(ratio))
        if not 14.0 <= stride <= 38.0:
            continue
        err = abs(stride - ind.gait.stride)
        if best is None or err < best[0]:
            best = (err, cycles, stride)
    if best is None:                                      # fall back to the nearest legal cadence
        cycles = max(1, round(speed_char * LOOP_SECONDS / (4.0 * S.LEG_LENGTH * math.sin(math.radians(30)))))
        return cycles, replace(ind.gait, stride=30.0)
    _, cycles, stride = best
    arm = ind.gait.arm * stride / max(ind.gait.stride, 1.0)
    return cycles, replace(ind.gait, stride=stride, arm=arm)


def build_crowd(base, seed):
    rng = random.Random(seed)
    frames = list(range(1, LOOP + 2))
    report = []
    n = 0
    for lane in LANES:
        for i in range(lane["count"]):
            ind = S.random_individual(rng)
            scale = lane["scale"] * rng.uniform(0.93, 1.07)
            height = scale * 1.785 * ind.height
            margin = 0.55 * height                         # just enough to exit fully
            width = FRAME_W + 2.0 * margin + rng.uniform(*lane["extra"])
            speed = width / LOOP_SECONDS
            direction = 1.0 if rng.random() < 0.55 else -1.0
            start = rng.uniform(-width / 2, width / 2)
            cycles, gait = solve_gait(ind, speed, scale)
            depth = lane["depth"] + i * 0.9 * lane["scale"] + rng.uniform(0.0, 0.3)
            feet_z = lane["feet_z"] + rng.uniform(-0.12, 0.12)

            name = "NPC%02d" % n
            rig, _body, rim = S.instance_character(*base, name=name)
            rig.rotation_euler = (0.0, 0.0, math.radians(90.0 * direction))
            S.apply_individual_shape(rig, ind, base_scale=scale)
            S.push_outline_back(rig, rim, depth=0.42)

            action = L.new_action(rig, name + "_Walk")
            phase0 = ind.phase

            def x_at(f):
                t = (f - 1) / FPS
                x = start + direction * speed * t
                return (x + width / 2) % width - width / 2

            curves = S.pose_curves(
                lambda f: S.walk_pose((phase0 + cycles * (f - 1) / LOOP) % 1.0, gait), frames)
            curves[("location", 0, "Object")] = [(f, x_at(f)) for f in frames]
            curves[("location", 1, "Object")] = [(1, depth)]
            curves[("location", 2, "Object")] = [(1, feet_z)]
            L.write_curves(rig, action, curves)

            report.append((name, lane["name"], scale, speed / height, cycles, gait.stride, direction))
            n += 1

    print("  %-6s %-6s %5s %8s %6s %6s %4s" % ("npc", "lane", "scale", "height/s", "cycles", "stride", "dir"))
    for r in report:
        print("  %-6s %-6s %5.2f %8.2f %6d %6.1f %4s" % (r[0], r[1], r[2], r[3], r[4], r[5], "->" if r[6] > 0 else "<-"))
    return n


# --------------------------------------------------------------------------- #
# scene / render
# --------------------------------------------------------------------------- #
def build_scene(seed):
    scene = L.reset_scene(fps=FPS)
    scene.frame_start, scene.frame_end = 1, LOOP

    base_coll = bpy.data.collections.new("Base_Character")
    scene.collection.children.link(base_coll)
    base = S.build_character("Base")
    # One shared mesh, so one material swap turns the whole crowd red.
    base[1].data.materials[0] = L.flat_material("Logo_NPC_Red", NPC_COLOUR)
    for o in base:
        scene.collection.objects.unlink(o)
        base_coll.objects.link(o)
        o.hide_render = True
    base_coll.hide_render = True
    base_coll.hide_viewport = True

    build_wordmark()
    count = build_crowd(base, seed)

    cam16 = L.ortho_camera(scene, "Camera_16x9", (0.0, -60.0, CAMERA_Z), (math.pi / 2, 0.0, 0.0), FRAME_W)
    L.ortho_camera(scene, "Camera_1x1", (0.0, -60.0, CAMERA_Z - 0.6), (math.pi / 2, 0.0, 0.0), FRAME_H + 0.6)
    scene.camera = cam16
    L.flat_render_settings(scene, 1920, 1080)
    print("  scene: %d NPCs, loop %d frames @ %d fps" % (count, LOOP, FPS))
    return scene


def use_camera(scene, name, width, height, transparent=False):
    scene.camera = bpy.data.objects[name]
    L.flat_render_settings(scene, width, height, transparent=transparent)


def render_still(scene, frame, path):
    scene.frame_set(frame)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("  rendered", path)


def render_sequence(scene, directory):
    L.ensure_dir(directory)
    for f in os.listdir(directory):
        if f.endswith(".png"):
            os.remove(os.path.join(directory, f))
    scene.frame_start, scene.frame_end = 1, LOOP
    scene.render.filepath = os.path.join(directory, "frame_")
    t0 = time.time()
    bpy.ops.render.render(animation=True)
    print("  rendered %d frames to %s in %.1fs" % (LOOP, directory, time.time() - t0))


def main():
    t0 = time.time()
    args = L.args_after_dashes()
    seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 11

    scene = build_scene(seed)

    L.ensure_dir(BLEND_DIR)
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, "OpenNPC_Logo_Animation.blend"))
    scene.frame_set(HERO_FRAME)
    scene.frame_start = scene.frame_end = HERO_FRAME
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, "OpenNPC_Logo.blend"))
    scene.frame_start, scene.frame_end = 1, LOOP

    if "--preview" in args:
        L.ensure_dir(LOGO_RENDERS)
        use_camera(scene, "Camera_16x9", 960, 540)
        for f in (1, 31, 61, 91, 121, 151):
            render_still(scene, f, os.path.join(LOGO_RENDERS, "preview_%03d.png" % f))
        render_still(scene, LOOP + 1, os.path.join(LOGO_RENDERS, "preview_loopcheck_%03d.png" % (LOOP + 1)))

    if "--stills" in args:
        L.ensure_dir(PNG)
        use_camera(scene, "Camera_16x9", 1920, 1080)
        render_still(scene, HERO_FRAME, os.path.join(PNG, "opennpc-logo.png"))
        use_camera(scene, "Camera_16x9", 1920, 1080, transparent=True)
        render_still(scene, HERO_FRAME, os.path.join(PNG, "opennpc-logo-transparent.png"))
        use_camera(scene, "Camera_1x1", 1080, 1080)
        render_still(scene, HERO_FRAME, os.path.join(PNG, "opennpc-logo-square.png"))
        use_camera(scene, "Camera_16x9", 1280, 640)          # GitHub social preview ratio (2:1)
        bpy.data.objects["Camera_16x9"].data.ortho_scale = FRAME_W
        render_still(scene, HERO_FRAME, os.path.join(PNG, "opennpc-social-preview.png"))

    if "--frames" in args:
        use_camera(scene, "Camera_16x9", 1920, 1080)
        render_sequence(scene, FRAMES)

    if "--square" in args:
        use_camera(scene, "Camera_1x1", 1080, 1080)
        render_sequence(scene, FRAMES_SQUARE)

    print("  done in %.1fs" % (time.time() - t0))


if __name__ == "__main__":
    main()
