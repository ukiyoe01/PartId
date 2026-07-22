#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
UNITY="${UNITY:-/Applications/Unity/Unity-6000.1.17f1/Unity.app/Contents}"
MONO="$UNITY/MonoBleedingEdge/bin/mono"
CSC_EXE="$UNITY/MonoBleedingEdge/lib/mono/4.5/csc.exe"
SFS_GAME="${SFS_GAME:-$HOME/Library/Application Support/Steam/steamapps/common/Spaceflight Simulator/SpaceflightSimulatorGame.app}"
MANAGED="${SFS_MANAGED:-$SFS_GAME/Contents/Resources/Data/Managed}"
OUT="$ROOT/dist/PartId.dll"

mkdir -p "$ROOT/dist"

"$MONO" "$CSC_EXE" \
  -target:library \
  -langversion:latest \
  -out:"$OUT" \
  -r:"$MANAGED/netstandard.dll" \
  -r:"$MANAGED/Assembly-CSharp.dll" \
  -r:"$MANAGED/0Harmony.dll" \
  -r:"$MANAGED/UnityEngine.dll" \
  -r:"$MANAGED/UnityEngine.CoreModule.dll" \
  "$ROOT/Main.cs" \
  "$ROOT/PartIdApi.cs" \
  "$ROOT/PartIdCommonKeySchema.cs" \
  "$ROOT/PartIdKeyDefinition.cs" \
  "$ROOT/PartIdKeys.cs" \
  "$ROOT/PartIdRecord.cs" \
  "$ROOT/PartIdValue.cs" \
  "$ROOT/PartIdRecordStore.cs" \
  "$ROOT/PartIdIdentityPatch.cs" \
  "$ROOT/PartIdRuntime.cs"

echo "$OUT"
