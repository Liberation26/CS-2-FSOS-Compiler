#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_ROOT="$PROJECT_ROOT/OSes/Stage9/Build/Test01"
COMPILER_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj"
COMPILER_DLL="$PROJECT_ROOT/Source/Core/Oryn.Compiler/bin/Debug/net8.0/Oryn.Compiler.dll"
mkdir -p "$BUILD_ROOT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
dotnet build "$COMPILER_PROJECT" -c Debug --nologo --disable-build-servers -v:minimal >/dev/null
dotnet "$COMPILER_DLL" compose-kernel --stage Stage9 --template "$PROJECT_ROOT/OSes/Stage9/Templates/Kernel.template.cs" --output "$BUILD_ROOT/Kernel.Generated.cs" > "$BUILD_ROOT/Compose.log" 2>&1
[ -f "$BUILD_ROOT/Kernel.Generated.cs" ] || { echo "[FAIL] Stage9 composer did not generate Kernel.Generated.cs"; cat "$BUILD_ROOT/Compose.log"; exit 1; }
grep -F "[ OK ] [ COMPOSE  ] Selected modules:" "$BUILD_ROOT/Compose.log" >/dev/null || { echo "[FAIL] Composer module-selection proof missing"; cat "$BUILD_ROOT/Compose.log"; exit 1; }
grep -F "ManifestLoader.InitializeSelected();" "$BUILD_ROOT/Kernel.Generated.cs" >/dev/null || { echo "[FAIL] Generated kernel did not contain manifest initialization call"; cat "$BUILD_ROOT/Kernel.Generated.cs"; exit 1; }
grep -F "Stage9 selected module:" "$BUILD_ROOT/Kernel.Generated.cs" >/dev/null || { echo "[FAIL] Generated kernel did not contain selected-module proof lines"; cat "$BUILD_ROOT/Kernel.Generated.cs"; exit 1; }
echo "[ OK ] Stage9 kernel template composition generated a validated kernel source file."
