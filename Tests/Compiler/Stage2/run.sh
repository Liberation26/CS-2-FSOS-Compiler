#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage2"
mkdir -p "$OUTPUT_DIR"

cd "$ROOT_DIR/Source/Core/Oryn.Compiler"
dotnet run -- compile "$ROOT_DIR/OSes/Stage2/Source/Kernel.cs" --target x64-elf --output "$OUTPUT_DIR/Kernel.o" | tee "$OUTPUT_DIR/Compiler.stdout.log"

MANIFEST="$OUTPUT_DIR/Kernel.stage2.ir.json"
DIAGNOSTICS="$OUTPUT_DIR/Kernel.diagnostics.log"
ASSEMBLY="$OUTPUT_DIR/Kernel.generated.S"

test -f "$MANIFEST"
test -f "$OUTPUT_DIR/Kernel.generated.c"
test -f "$ASSEMBLY"
test -f "$OUTPUT_DIR/Kernel.o"

grep -q '"OpCode": "Label"' "$MANIFEST"
grep -q '"Operand": "LoopStart0"' "$MANIFEST"
grep -q '"Operand": "LoopEnd0"' "$MANIFEST"
grep -q '"OpCode": "JumpIfFalse"' "$MANIFEST"
grep -q '"OpCode": "Jump"' "$MANIFEST"
grep -q '"ControlFlowGraph"' "$MANIFEST"
grep -q '"Successors"' "$MANIFEST"
grep -q '"ManagedName": "Kernel.WriteBanner"' "$MANIFEST"
grep -q '"NativeSymbol": "Kernel_WriteBanner"' "$MANIFEST"
grep -q '\[ OK \] \[ CFG      \]' "$DIAGNOSTICS"
grep -q '\[ OK \] \[ CFG      \]' "$OUTPUT_DIR/Compiler.stdout.log"

grep -q '^Kernel_Main:' "$ASSEMBLY"
grep -q '^Kernel_WriteBanner:' "$ASSEMBLY"
grep -q 'call Kernel_WriteBanner' "$ASSEMBLY"
grep -q 'call Cpu_HaltForever' "$ASSEMBLY"

grep -q 'push %rbp' "$ASSEMBLY"
grep -q 'mov %rsp, %rbp' "$ASSEMBLY"
grep -q 'sub \$32, %rsp\|sub \$16, %rsp' "$ASSEMBLY"
grep -q -- '-8(%rbp)' "$ASSEMBLY"
grep -q 'movq \$0, -8(%rbp)' "$ASSEMBLY"
grep -q 'leave' "$ASSEMBLY"

grep -q '^\.section \.rodata' "$ASSEMBLY"
grep -q '^\.Lstr[0-9][0-9]*:' "$ASSEMBLY"
grep -q '\.[a]sciz "Hello from helper method"' "$ASSEMBLY"
grep -q 'lea \.Lstr[0-9][0-9]*(%rip), %rdi' "$ASSEMBLY"
grep -q 'call Diagnostics_WriteOk' "$ASSEMBLY"

printf '[ OK ] Stage 2 CFG, stack/local, string literal, and static helper proof outputs written to: %s\n' "$OUTPUT_DIR"
