#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
PROGRAM="$PROJECT_ROOT/Source/Core/Oryn.Generator/Program.cs"
ORYN_SH="$PROJECT_ROOT/Oryn.sh"
RUNQEMU_SH="$PROJECT_ROOT/Runqemu.sh"

grep -q 'new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)' "$PROGRAM"
grep -q "utf-8-sig" "$ORYN_SH"
grep -q "utf-8-sig" "$RUNQEMU_SH"

echo "[ OK ] 1.0.5 generator writes no-BOM JSON and script readers tolerate BOM JSON."
