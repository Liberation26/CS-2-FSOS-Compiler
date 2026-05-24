#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_ROOT="$PROJECT_ROOT/OSes/Stage5/Build/TestCompile"
OUTPUT_OBJECT="$BUILD_ROOT/Kernel.stage5.o"
COMPILER_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj"
COMPILER_DLL="$PROJECT_ROOT/Source/Core/Oryn.Compiler/bin/Debug/net8.0/Oryn.Compiler.dll"

rm -rf "$BUILD_ROOT"
mkdir -p "$BUILD_ROOT"

dotnet build "$COMPILER_PROJECT" -c Debug --nologo --disable-build-servers -v:minimal

dotnet "$COMPILER_DLL" compile "$PROJECT_ROOT/OSes/Stage5/Source/Kernel.cs" --target x64-elf --output "$OUTPUT_OBJECT" | tee "$BUILD_ROOT/compiler.stdout.log"

test -f "$OUTPUT_OBJECT"
test -f "$BUILD_ROOT/Kernel.stage5.stage5.ir.json"
test -f "$BUILD_ROOT/Kernel.stage5.generated.S"
test -f "$BUILD_ROOT/Kernel.stage5.diagnostics.log"
grep -q "Stage 5 runtime contract validation passed" "$BUILD_ROOT/compiler.stdout.log"
grep -q "Runtime.Initialize" "$BUILD_ROOT/Kernel.stage5.stage5.ir.json"
grep -q "Panic.Halt" "$BUILD_ROOT/Kernel.stage5.stage5.ir.json"

printf '[ OK ] [ TEST      ] Stage 5 kernel compile artifacts verified.
'
