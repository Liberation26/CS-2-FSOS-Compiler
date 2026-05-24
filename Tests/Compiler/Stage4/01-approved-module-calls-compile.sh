#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage4"
LOG_FILE="$OUTPUT_DIR/01-approved-module-calls-compile.log"
COMPILER_DLL="$ROOT_DIR/Source/Core/Oryn.Compiler/bin/Debug/net8.0/Oryn.Compiler.dll"
SOURCE_FILE="$SCRIPT_DIR/Sources/AllowedKernel.cs"
OBJECT_FILE="$OUTPUT_DIR/AllowedKernel.stage4.o"

info() { printf '[ OK ] [ STAGE4T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE4T ] %s\n' "$1"; exit 1; }

mkdir -p "$OUTPUT_DIR"
command -v dotnet >/dev/null 2>&1 || fail "Required tool not found: dotnet"

dotnet build "$ROOT_DIR/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj" -c Debug --nologo --disable-build-servers -v:minimal >"$OUTPUT_DIR/compiler-build.log" 2>&1 || fail "Compiler build failed. See: $OUTPUT_DIR/compiler-build.log"

set +e
(cd "$ROOT_DIR" && dotnet "$COMPILER_DLL" compile "$SOURCE_FILE" --target x64-elf --output "$OBJECT_FILE") 2>&1 | tee "$LOG_FILE"
STATUS=${PIPESTATUS[0]}
set -e

[ "$STATUS" -eq 0 ] || fail "Approved module kernel failed to compile. See: $LOG_FILE"
[ -f "$OBJECT_FILE" ] || fail "Expected object file missing: $OBJECT_FILE"
grep -q 'Stage 4 approved-module boundary validation passed' "$LOG_FILE" || fail "Stage 4 approval proof missing from: $LOG_FILE"
grep -q 'Approved module calls:' "$LOG_FILE" || fail "Approved module call count missing from: $LOG_FILE"

info "Allowed Stage 4 module calls compiled successfully"
