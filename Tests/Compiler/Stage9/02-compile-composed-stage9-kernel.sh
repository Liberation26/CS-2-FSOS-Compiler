#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_ROOT="$PROJECT_ROOT/OSes/Stage9/Build/Test02"
COMPILER_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj"
COMPILER_DLL="$PROJECT_ROOT/Source/Core/Oryn.Compiler/bin/Debug/net8.0/Oryn.Compiler.dll"
mkdir -p "$BUILD_ROOT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
dotnet build "$COMPILER_PROJECT" -c Debug --nologo --disable-build-servers -v:minimal >/dev/null
dotnet "$COMPILER_DLL" compose-kernel --stage Stage9 --template "$PROJECT_ROOT/OSes/Stage9/Templates/Kernel.template.cs" --output "$BUILD_ROOT/Kernel.Generated.cs" > "$BUILD_ROOT/Compose.log" 2>&1
dotnet "$COMPILER_DLL" compile "$BUILD_ROOT/Kernel.Generated.cs" --target x64-elf --output "$BUILD_ROOT/Kernel.stage9.o" > "$BUILD_ROOT/Compile.log" 2>&1
[ -f "$BUILD_ROOT/Kernel.stage9.o" ] || { echo "[FAIL] Stage9 compiler did not produce object"; cat "$BUILD_ROOT/Compile.log"; exit 1; }
[ -f "$BUILD_ROOT/Kernel.stage9.generated.S" ] || { echo "[FAIL] Stage9 compiler did not produce assembly"; exit 1; }
[ -f "$BUILD_ROOT/Kernel.stage9.ir.json" ] || { echo "[FAIL] Stage9 compiler did not produce IR manifest"; exit 1; }
grep -F "Stage 9" "$BUILD_ROOT/Kernel.stage9.ir.json" >/dev/null || { echo "[FAIL] Stage9 backend manifest notes missing"; cat "$BUILD_ROOT/Kernel.stage9.ir.json"; exit 1; }
grep -F "Generated kernel source passed safe-subset and approved-call validation" "$BUILD_ROOT/Compose.log" >/dev/null || { echo "[FAIL] Stage9 pre-backend validation proof missing"; cat "$BUILD_ROOT/Compose.log"; exit 1; }
echo "[ OK ] Stage9 composed kernel compiled to direct ELF64 relocatable output."
