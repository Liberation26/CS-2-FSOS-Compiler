#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_ROOT="$ROOT_DIR/OSes/Stage2/Build/Runqemu"
ASM_FILE="$BUILD_ROOT/Kernel.stage2.generated.S"

info() { printf '[ OK ] [ STAGE2T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE2T ] %s\n' "$1"; exit 1; }

if [ ! -f "$ASM_FILE" ]; then
    info "Assembly file missing; running Stage 2 compile test first"
    "$SCRIPT_DIR/01-compile-stage2-kernel.sh"
fi

[ -f "$ASM_FILE" ] || fail "Assembly file missing after compile: $ASM_FILE"

grep -q '^Kernel_Main:' "$ASM_FILE" || fail "Kernel_Main symbol missing"
grep -q '^Kernel_WriteBanner:' "$ASM_FILE" || fail "Kernel_WriteBanner symbol missing"
grep -q 'call Kernel_WriteBanner' "$ASM_FILE" || fail "helper method call missing"
grep -q 'call Diagnostics_WriteOk' "$ASM_FILE" || fail "Diagnostics_WriteOk call missing"
grep -q 'call Memory_Initialize' "$ASM_FILE" || fail "Memory_Initialize call missing"
grep -q 'call Cpu_HaltForever' "$ASM_FILE" || fail "Cpu_HaltForever call missing"

grep -q 'push %rbp' "$ASM_FILE" || fail "stack frame prologue push missing"
grep -q 'mov %rsp, %rbp' "$ASM_FILE" || fail "stack frame rbp setup missing"
grep -Eq 'sub \$[0-9]+, %rsp' "$ASM_FILE" || fail "stack allocation missing"
grep -q -- '-8(%rbp)' "$ASM_FILE" || fail "rbp-relative local slot missing"
grep -q 'leave' "$ASM_FILE" || fail "stack frame epilogue leave missing"
grep -q 'ret' "$ASM_FILE" || fail "ret missing"

grep -q '^\.section \.rodata' "$ASM_FILE" || fail ".rodata section missing"
grep -q '^\.Lstr[0-9][0-9]*:' "$ASM_FILE" || fail ".LstrN label missing"
grep -q '\.asciz "Stage2 kernel entered"' "$ASM_FILE" || fail "entry string literal missing"
grep -q '\.asciz "Stage2 memory initialized"' "$ASM_FILE" || fail "memory string literal missing"
grep -q '\.asciz "Stage2 loop tick"' "$ASM_FILE" || fail "loop string literal missing"
grep -q '\.asciz "Stage2 branch worked"' "$ASM_FILE" || fail "branch string literal missing"
grep -q '\.asciz "Stage2 helper method worked"' "$ASM_FILE" || fail "helper string literal missing"
grep -q '\.asciz "Stage2 kernel is halting forever"' "$ASM_FILE" || fail "halt string literal missing"
grep -Eq 'lea \.Lstr[0-9]+\(%rip\), %rdi' "$ASM_FILE" || fail "RIP-relative string load missing"

test "$(grep -c 'call Cpu_HaltForever' "$ASM_FILE")" -eq 1 || fail "Cpu_HaltForever should be emitted exactly once"
test "$(grep -c 'call Kernel_WriteBanner' "$ASM_FILE")" -eq 1 || fail "Kernel_WriteBanner should be called exactly once"

info "Stage 2 assembly output checks passed: $ASM_FILE"
