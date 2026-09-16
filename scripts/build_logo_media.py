"""Turn the rendered logo frames into the README GIFs, WebP and videos.

    python scripts/build_logo_media.py            # after build_logo.py --frames [--square] [--stills]

Outputs
    branding/gif/opennpc-logo.gif               transparent, ink wordmark + red crowd (README, light theme)
    branding/gif/opennpc-logo-dark.gif          transparent, light wordmark + red crowd (README, dark theme)
    branding/gif/opennpc-logo-paper.gif         opaque on paper colour
    branding/gif/opennpc-logo-square.gif        transparent 1:1
    branding/gif/opennpc-logo.webp              transparent with smooth (8-bit) alpha edges
    branding/video/opennpc-logo.mp4             1920x1080 H.264 on paper
    branding/video/opennpc-logo.webm            1280x720 VP9 on paper (web page)
    branding/png/opennpc-logo-transparent.png   hero still, ink with real alpha
    branding/png/opennpc-logo-transparent-dark.png

How transparency is made
    The art is two inks (black wordmark, red crowd) on a light ground. Each
    pixel is unmixed into one ink plus an alpha (see unmix): paper, the white keyline and the white NPC rims all become
    transparent, and the cut-outs around the letters show whatever page the logo
    sits on. GIF only has 1-bit transparency, so GIFs threshold that alpha at
    50%; the WebP and PNGs keep the full anti-aliased alpha.
"""
import os
import pathlib
import shutil
import subprocess
import sys

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
RENDERS = ROOT / "branding" / "renders"
FRAMES = RENDERS / "frames"
FRAMES_SQUARE = RENDERS / "frames_square"
FRAMES_ALPHA = RENDERS / "frames_alpha"
GIF_DIR = ROOT / "branding" / "gif"
VIDEO_DIR = ROOT / "branding" / "video"
PNG_DIR = ROOT / "branding" / "png"
FPS_IN = 30

PAPER = np.array([243, 241, 236], dtype=np.float32)   # #F3F1EC
INK = (17, 17, 17)                                    # wordmark
RED = (217, 40, 30)                                   # NPC crowd, #D9281E
LIGHT_INK = (243, 241, 236)                           # wordmark on dark backgrounds
INKS = np.array([INK, RED], dtype=np.float32)
GIF_DELAYS = [70, 70, 60]   # centisecond-friendly pattern averaging 66.7 ms = 15 fps


def ffmpeg():
    exe = shutil.which("ffmpeg")
    if not exe:
        sys.exit("ffmpeg not found on PATH. Install it (e.g. `winget install Gyan.FFmpeg`).")
    return exe


def run(args):
    print("  $ ffmpeg", " ".join(args[1:])[:120], "...")
    subprocess.run(args, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)


def unmix(img):
    """RGB render on paper -> (alpha, ink index) per pixel.

    Every pixel is a blend of paper with exactly one ink (the keyline and white
    rims guarantee inks never touch). For each ink solve p = paper + a(ink - paper)
    by least squares and keep the ink that fits best. White (brighter than paper)
    solves to a < 0 and becomes fully transparent.
    """
    p = np.asarray(img.convert("RGB"), dtype=np.float32)
    best_a = np.zeros(p.shape[:2], np.float32)
    best_k = np.zeros(p.shape[:2], np.uint8)
    best_err = np.full(p.shape[:2], np.inf, np.float32)
    for k, ink in enumerate(INKS):
        d = ink - PAPER
        a = np.clip(((p - PAPER) @ d) / float(d @ d), 0.0, 1.0)
        err = np.sum((PAPER + a[..., None] * d - p) ** 2, axis=-1)
        better = err < best_err
        best_err[better], best_a[better], best_k[better] = err[better], a[better], k
    return best_a, best_k


def rgba(alpha, index, colours):
    h, w = alpha.shape
    out = np.empty((h, w, 4), dtype=np.uint8)
    out[..., :3] = np.asarray(colours, dtype=np.uint8)[index]
    out[..., 3] = np.round(alpha * 255.0).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def frames_of(directory, step=2):
    files = sorted(directory.glob("frame_*.png"))
    return files[::step]


def transparent_gif(files, dst, size, colours):
    frames = []
    for f in files:
        img = Image.open(f).convert("RGB").resize(size, Image.LANCZOS)
        alpha, index = unmix(img)
        pal_index = np.where(alpha >= 0.5, index + 1, 0).astype(np.uint8)
        frame = Image.fromarray(pal_index, "P")
        frame.putpalette([0, 0, 0] + [c for col in colours for c in col] + [0, 0, 0] * 253)
        frame.info["transparency"] = 0
        frames.append(frame)
    delays = [GIF_DELAYS[i % len(GIF_DELAYS)] for i in range(len(frames))]
    frames[0].save(dst, save_all=True, append_images=frames[1:], loop=0, duration=delays,
                   transparency=0, disposal=2, optimize=False)


def paper_gif(src, dst, width, height, fps=15, colours=32):
    graph = (f"fps={fps},scale={width}:{height}:flags=lanczos,split[a][b];"
             f"[a]palettegen=max_colors={colours}:stats_mode=full[p];"
             f"[b][p]paletteuse=dither=none:diff_mode=rectangle")
    run([ffmpeg(), "-y", "-framerate", str(FPS_IN), "-i", str(src / "frame_%04d.png"),
         "-filter_complex", graph, "-loop", "0", str(dst)])


def main():
    if not any(FRAMES.glob("frame_*.png")):
        sys.exit(f"No frames in {FRAMES}. Run: blender -b -P blender/scripts/build_logo.py -- --frames")
    for d in (GIF_DIR, VIDEO_DIR, PNG_DIR, FRAMES_ALPHA):
        d.mkdir(parents=True, exist_ok=True)

    print("  transparent GIFs")
    files = frames_of(FRAMES)
    transparent_gif(files, GIF_DIR / "opennpc-logo.gif", (800, 450), [INK, RED])
    transparent_gif(files, GIF_DIR / "opennpc-logo-dark.gif", (800, 450), [LIGHT_INK, RED])
    if any(FRAMES_SQUARE.glob("frame_*.png")):
        transparent_gif(frames_of(FRAMES_SQUARE), GIF_DIR / "opennpc-logo-square.gif", (480, 480), [INK, RED])
    paper_gif(FRAMES, GIF_DIR / "opennpc-logo-paper.gif", 800, 450)

    print("  smooth-alpha WebP")
    for f in FRAMES_ALPHA.glob("*.png"):
        f.unlink()
    for i, f in enumerate(files):
        img = Image.open(f).convert("RGB").resize((800, 450), Image.LANCZOS)
        rgba(*unmix(img), [INK, RED]).save(FRAMES_ALPHA / ("frame_%04d.png" % (i + 1)))
    run([ffmpeg(), "-y", "-framerate", "15", "-i", str(FRAMES_ALPHA / "frame_%04d.png"),
         "-c:v", "libwebp_anim", "-lossless", "0", "-quality", "80", "-pix_fmt", "yuva420p",
         "-loop", "0", str(GIF_DIR / "opennpc-logo.webp")])

    hero = PNG_DIR / "opennpc-logo.png"
    if hero.exists():
        alpha, index = unmix(Image.open(hero))
        rgba(alpha, index, [INK, RED]).save(PNG_DIR / "opennpc-logo-transparent.png", optimize=True)
        rgba(alpha, index, [LIGHT_INK, RED]).save(PNG_DIR / "opennpc-logo-transparent-dark.png", optimize=True)

    pattern = str(FRAMES / "frame_%04d.png")
    run([ffmpeg(), "-y", "-framerate", str(FPS_IN), "-i", pattern,
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p",
         "-movflags", "+faststart", str(VIDEO_DIR / "opennpc-logo.mp4")])
    run([ffmpeg(), "-y", "-framerate", str(FPS_IN), "-i", pattern,
         "-vf", "scale=1280:720:flags=lanczos", "-c:v", "libvpx-vp9", "-b:v", "0", "-crf", "36",
         "-pix_fmt", "yuv420p", "-an", str(VIDEO_DIR / "opennpc-logo.webm")])

    outputs = sorted(list(GIF_DIR.iterdir()) + list(VIDEO_DIR.iterdir()) + list(PNG_DIR.iterdir()))
    for f in outputs:
        print(f"  {f.relative_to(ROOT)}  {os.path.getsize(f) / 1024:.0f} KB")


if __name__ == "__main__":
    main()
