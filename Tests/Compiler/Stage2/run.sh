#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage2"
mkdir -p "$OUTPUT_DIR"

cd "$ROOT_DIR/Source/Core/Oryn.Compiler"
dotnet run -- compile "$ROOT_DIR/OSes/Stage2/Source/Kernel.cs" --target x64-elf --output "$OUTPUT_DIR/Kernel.o" | tee "$OUTPUT_DIR/Compiler.stdout.log"

MANIFEST="$OUTPUT_DIR/Kernel.stage2.ir.json"
DIAGNOSTICS="$OUTPUT_DIR/Kernel.diagnostics.log"

test -f "$MANIFEST"
test -f "$OUTPUT_DIR/Kernel.generated.c"
test -f "$OUTPUT_DIR/Kernel.generated.S"
test -f "$OUTPUT_DIR/Kernel.o"

grep -q '"OpCode": "Label"' "$MANIFEST"
grep -q '"Operand": "LoopStart0"' "$MANIFEST"
grep -q '"Operand": "LoopEnd0"' "$MANIFEST"
grep -q '"OpCode": "JumpIfFalse"' "$MANIFEST"
grep -q '"OpCode": "Jump"' "$MANIFEST"
grep -q '"ControlFlowGraph"' "$MANIFEST"
grep -q '"Successors"' "$MANIFEST"
grep -q '\[ OK \] \[ CFG      \]' "$DIAGNOSTICS"
grep -q '\[ OK \] \[ CFG      \]' "$OUTPUT_DIR/Compiler.stdout.log"

printf '[ OK ] Stage 2 control-flow graph proof outputs written to: %s\n' "$OUTPUT_DIR"
