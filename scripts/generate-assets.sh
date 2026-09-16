#!/bin/bash
# Blender -> stickman rig, clips, FBX, previews and web sprite; then sync into Unity.
#   scripts/generate-assets.sh            (BLENDER=/path/to/blender to override)
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BLENDER="${BLENDER:-D:/blender/blender.exe}"
"$BLENDER" --background --factory-startup --python "$ROOT/blender/scripts/build_stickman.py"
python "$ROOT/scripts/validate_personas.py"
"$ROOT/scripts/unity.sh" OpenNPC.EditorTools.DemoPipeline.All
grep -a "VERIFY:" "$ROOT/unity/OpenNPC-Demo/Logs/batch.log" | tail -1
