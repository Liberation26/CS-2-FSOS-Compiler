#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage4"
LOG_FILE="$OUTPUT_DIR/02-forbidden-module-call-fails.log"
COMPILER_DLL="$ROOT_DIR/Source/Core/Oryn.Compiler/bin/Debug/net8.0/Oryn.Compiler.dll"
SOURCE_FILE="$SCRIPT_DIR/Sources/ForbiddenCallKernel.cs"
OBJECT_FILE="$OUTPUT_DIR/ForbiddenCallKernel.stage4.o"

info() { printf '[ OK ] [ STAGE4T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE4T ] %s\n' "$1"; exit 1; }

mkdir -p "$OUTPUT_DIR"
command -v dotnet >/dev/null 2>&1 || fail "Required tool not found: dotnet"
[ -f "$COMPILER_DLL" ] || dotnet build "$ROOT_DIR/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj" -c Debug --nologo --disable-build-servers -v:minimal >"$OUTPUT_DIR/compiler-build.log" 2>&1 || fail "Compiler build failed. See: $OUTPUT_DIR/compiler-build.log"
rm -f "$OBJECT_FILE"

set +e
(cd "$ROOT_DIR" && dotnet "$COMPILER_DLL" compile "$SOURCE_FILE" --target x64-elf --output "$OBJECT_FILE") 2>&1 | tee "$LOG_FILE"
STATUS=${PIPESTATUS[0]}
set -e

[ "$STATUS" -ne 0 ] || fail "Forbidden module call unexpectedly compiled. See: $LOG_FILE"
[ ! -f "$OBJECT_FILE" ] || fail "Forbidden module call unexpectedly wrote object file: $OBJECT_FILE"
grep -q 'Stage 4 module boundary rejected call: Console.WriteLine' "$LOG_FILE" || fail "Expected forbidden-call diagnostic missing from: $LOG_FILE"

info "Forbidden Stage 4 module call failed as expected"
