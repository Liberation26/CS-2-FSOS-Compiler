#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_ROOT="$PROJECT_ROOT/OSes/Stage8/Build/Test04"
COMPILER_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj"
COMPILER_DLL="$PROJECT_ROOT/Source/Core/Oryn.Compiler/bin/Debug/net8.0/Oryn.Compiler.dll"
mkdir -p "$BUILD_ROOT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
dotnet build "$COMPILER_PROJECT" -c Debug --nologo --disable-build-servers -v:minimal >/dev/null
set +e
dotnet "$COMPILER_DLL" compile "$SCRIPT_DIR/Sources/ForbiddenUncontractedCallKernel.cs" --target x64-elf --output "$BUILD_ROOT/Forbidden.o" > "$BUILD_ROOT/Forbidden.log" 2>&1
STATUS=$?
set -e
[ "$STATUS" -ne 0 ] || { echo "[FAIL] Forbidden uncontracted call compiled successfully"; exit 1; }
grep -F "rejected call: Diagnostics.SecretWrite" "$BUILD_ROOT/Forbidden.log" >/dev/null || { echo "[FAIL] Expected rejection message missing"; cat "$BUILD_ROOT/Forbidden.log"; exit 1; }
echo "[ OK ] Uncontracted C# module call was rejected."
