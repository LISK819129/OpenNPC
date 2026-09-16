#!/bin/bash
# Headless Unity runner for the OpenNPC demo.
#   scripts/unity.sh <Namespace.Class.Method> [extra Unity args]   (no graphics: setup, compile, verify, builds)
#   GFX=1 scripts/unity.sh <Method>                                (with a graphics device: captures)
# Override the editor with UNITY=/path/to/Unity.exe
UNITY="${UNITY:-D:/unity/6000.6.0f1/Editor/Unity.exe}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="$ROOT/unity/OpenNPC-Demo"
METHOD="$1"; shift
mkdir -p "$PROJ/Logs"
LOG="$PROJ/Logs/batch.log"
ARGS=(-batchmode -quit -projectPath "$PROJ" -logFile "$LOG")
[ -z "$GFX" ] && ARGS+=(-nographics)
[ -n "$METHOD" ] && ARGS+=(-executeMethod "$METHOD")
"$UNITY" "${ARGS[@]}" "$@"
CODE=$?
echo "=== unity exit: $CODE ==="
grep -aE "error CS|Shader error|Exception|\[OPENNPC\]|Compilation failed|error:" "$LOG" | grep -avE "Licensing|Access token|UnityConnect" | head -80
exit $CODE
