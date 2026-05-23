#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
COMPILER_DIR="$ROOT_DIR/Source/Core/Oryn.Compiler"
OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage1"
mkdir -p "$OUTPUT_DIR"

cd "$COMPILER_DIR"
dotnet run -- compile Tests/Stage0/Kernel.stage0.cs --target x64-elf --output "$OUTPUT_DIR/Kernel.o"

test -f "$OUTPUT_DIR/Kernel.stage1.json"
test -f "$OUTPUT_DIR/Kernel.generated.c"
test -f "$OUTPUT_DIR/Kernel.generated.S"
test -f "$OUTPUT_DIR/Kernel.o"

printf '[ OK ] Stage 1 compiler proof outputs written to: %s\n' "$OUTPUT_DIR"
