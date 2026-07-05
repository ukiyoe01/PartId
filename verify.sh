#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
UNITY="${UNITY:-/Applications/Unity/Unity-6000.1.17f1/Unity.app/Contents}"
MONO="$UNITY/MonoBleedingEdge/bin/mono"
CSC_EXE="$UNITY/MonoBleedingEdge/lib/mono/4.5/csc.exe"
MONODIS="$UNITY/MonoBleedingEdge/bin/monodis"
SFS_GAME="${SFS_GAME:-$HOME/Library/Application Support/Steam/steamapps/common/Spaceflight Simulator/SpaceflightSimulatorGame.app}"
MANAGED="${SFS_MANAGED:-$SFS_GAME/Contents/Resources/Data/Managed}"
TEST_OUT="$ROOT/tools/bin/VerifyPartIdValues.exe"

"$ROOT/build.sh" >/dev/null
mkdir -p "$ROOT/tools/bin"

"$MONO" "$CSC_EXE" \
  -target:exe \
  -langversion:latest \
  -out:"$TEST_OUT" \
  -r:"$MANAGED/netstandard.dll" \
  -r:"$MANAGED/UnityEngine.dll" \
  -r:"$MANAGED/UnityEngine.CoreModule.dll" \
  -r:"$ROOT/dist/PartId.dll" \
  "$ROOT/tools/VerifyPartIdValues.cs" >/dev/null

MONO_PATH="$ROOT/dist:$MANAGED" "$MONO" "$TEST_OUT"

"$MONODIS" --assembly "$ROOT/dist/PartId.dll" | grep -q "Name:          PartId"

echo "PartId verification passed."
