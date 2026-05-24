#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_ROOT="$PROJECT_ROOT/OSes/Stage9/Build/Test03"
COMPILER_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj"
COMPILER_DLL="$PROJECT_ROOT/Source/Core/Oryn.Compiler/bin/Debug/net8.0/Oryn.Compiler.dll"
mkdir -p "$BUILD_ROOT"
rm -f "$BUILD_ROOT"/Forbidden.*
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
dotnet build "$COMPILER_PROJECT" -c Debug --nologo --disable-build-servers -v:minimal >/dev/null
set +e
dotnet "$COMPILER_DLL" compile "$SCRIPT_DIR/Sources/InvalidCallKernel.cs" --target x64-elf --output "$BUILD_ROOT/Forbidden.o" > "$BUILD_ROOT/Forbidden.log" 2>&1
STATUS=$?
set -e
[ "$STATUS" -ne 0 ] || { echo "[FAIL] Invalid call compiled successfully"; cat "$BUILD_ROOT/Forbidden.log"; exit 1; }
grep -F "Stage 4 module boundary rejected call: Diagnostics.SecretWrite" "$BUILD_ROOT/Forbidden.log" >/dev/null || { echo "[FAIL] Expected invalid-call rejection missing"; cat "$BUILD_ROOT/Forbidden.log"; exit 1; }
[ ! -f "$BUILD_ROOT/Forbidden.o" ] || { echo "[FAIL] Invalid call produced an object; expected failure before native compilation"; exit 1; }
[ ! -f "$BUILD_ROOT/Forbidden.generated.S" ] || { echo "[FAIL] Invalid call produced assembly; expected failure before backend emission"; exit 1; }
[ ! -f "$BUILD_ROOT/Forbidden.generated.c" ] || { echo "[FAIL] Invalid call produced C backend; expected failure before backend emission"; exit 1; }
echo "[ OK ] Invalid C# module call was blocked before backend/native compilation."
