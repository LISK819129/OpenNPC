#!/bin/bash
# Blender logo scene -> frames + stills -> GIF / WebP / MP4 / WebM / transparent PNGs.
#   scripts/build-logo.sh              full render (~1-2 min)
#   scripts/build-logo.sh --preview    six look-dev frames only
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BLENDER="${BLENDER:-D:/blender/blender.exe}"
if [ "$1" == "--preview" ]; then
  "$BLENDER" --background --factory-startup --python "$ROOT/blender/scripts/build_logo.py" -- --preview
  exit 0
fi
"$BLENDER" --background --factory-startup --python "$ROOT/blender/scripts/build_logo.py" -- --stills --frames --square
python "$ROOT/scripts/build_logo_media.py"
