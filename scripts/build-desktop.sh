#!/bin/bash
# Windows player + the in-build autopilot playtest (screenshots + report).
#   scripts/build-desktop.sh [--no-test]
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
"$ROOT/scripts/unity.sh" OpenNPC.EditorTools.DemoPipeline.BuildWindows
[ "$1" == "--no-test" ] && exit 0
EXE="$ROOT/unity/OpenNPC-Demo/Builds/Windows/OpenNPC-Demo.exe"
"$EXE" -autopilot -screen-fullscreen 0 -screen-width 1600 -screen-height 900 || true
REPORT="$USERPROFILE/AppData/LocalLow/OpenNPC/OpenNPC Demo/autopilot/report.txt"
cat "$REPORT"
grep -q "RESULT: PASS" "$REPORT"
