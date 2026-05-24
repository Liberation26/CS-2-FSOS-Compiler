#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage2"
BUILD_ROOT="$ROOT_DIR/OSes/Stage2/Build/Runqemu"
LOG_FILE="$TEST_OUTPUT_DIR/01-compile-stage2-kernel.log"

info() { printf '[ OK ] [ STAGE2T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE2T ] %s\n' "$1"; exit 1; }

mkdir -p "$TEST_OUTPUT_DIR"

info "Running Stage 2 compile/link/ISO proof without QEMU"
set +e
ORYN_SKIP_QEMU=1 "$ROOT_DIR/Runqemu.sh" Stage2 2>&1 | tee "$LOG_FILE"
STATUS=${PIPESTATUS[0]}
set -e

if [ "$STATUS" -ne 0 ]; then
    fail "Runqemu Stage2 compile path exited with status $STATUS. See: $LOG_FILE"
fi

IR_FILE="$BUILD_ROOT/Kernel.stage2.stage2.ir.json"
ASM_FILE="$BUILD_ROOT/Kernel.stage2.generated.S"
GENERATED_C="$BUILD_ROOT/Kernel.stage2.generated.c"
ELF_FILE="$BUILD_ROOT/OrynKernel.elf"
ISO_FILE="$BUILD_ROOT/OrynKernel.iso"
DIAGNOSTICS_FILE="$BUILD_ROOT/Kernel.stage2.diagnostics.log"

[ -f "$IR_FILE" ] || fail "IR file missing: $IR_FILE"
[ -f "$ASM_FILE" ] || fail "Assembly file missing: $ASM_FILE"
[ -f "$GENERATED_C" ] || fail "Generated C reference file missing: $GENERATED_C"
[ -f "$ELF_FILE" ] || fail "ELF file missing: $ELF_FILE"
[ -f "$ISO_FILE" ] || fail "ISO file missing: $ISO_FILE"
[ -f "$DIAGNOSTICS_FILE" ] || fail "Compiler diagnostics log missing: $DIAGNOSTICS_FILE"

grep -q 'Oryn.Compiler backend completed for Stage2' "$LOG_FILE" || fail "Compiler completion proof missing from: $LOG_FILE"
grep -q 'x86_64 freestanding kernel created' "$LOG_FILE" || fail "ELF creation proof missing from: $LOG_FILE"
grep -q 'Bootable kernel ISO created' "$LOG_FILE" || fail "ISO creation proof missing from: $LOG_FILE"
grep -q 'QEMU run skipped because ORYN_SKIP_QEMU=1' "$LOG_FILE" || fail "Expected QEMU skip proof missing from: $LOG_FILE"

info "Stage 2 compile/link/ISO proof passed"
info "IR: $IR_FILE"
info "Assembly: $ASM_FILE"
info "ELF: $ELF_FILE"
info "ISO: $ISO_FILE"
