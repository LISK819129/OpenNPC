#!/bin/bash
# Unity WebGL build -> web/demo/Build + web/demo/build.json. Needs the WebGL Build Support module.
#   scripts/build-web.sh           build
#   scripts/build-web.sh --serve   build, then serve web/ at http://localhost:8000
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
"$ROOT/scripts/unity.sh" OpenNPC.EditorTools.DemoPipeline.BuildWebGL
ls -la "$ROOT/web/demo/Build"
if [ "$1" == "--serve" ]; then
  echo "Serving $ROOT/web at http://localhost:8000"
  python -m http.server 8000 -d "$ROOT/web"
fi
