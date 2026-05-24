#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage3"
BUILD_ROOT="$ROOT_DIR/OSes/Stage3/Build/Runqemu"
LOG_FILE="$TEST_OUTPUT_DIR/01-compile-stage3-kernel.log"

info() { printf '[ OK ] [ STAGE3T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE3T ] %s\n' "$1"; exit 1; }

mkdir -p "$TEST_OUTPUT_DIR"

info "Running Stage 3 direct ELF64 object compile/link/ISO proof without QEMU"
set +e
ORYN_SKIP_QEMU=1 "$ROOT_DIR/Runqemu.sh" Stage3 2>&1 | tee "$LOG_FILE"
STATUS=${PIPESTATUS[0]}
set -e

if [ "$STATUS" -ne 0 ]; then
    fail "Runqemu Stage3 compile path exited with status $STATUS. See: $LOG_FILE"
fi

IR_FILE="$BUILD_ROOT/Kernel.stage3.stage3.ir.json"
ASM_FILE="$BUILD_ROOT/Kernel.stage3.generated.S"
OBJECT_FILE="$BUILD_ROOT/Kernel.stage3.o"
ELF_FILE="$BUILD_ROOT/OrynKernel.elf"
ISO_FILE="$BUILD_ROOT/OrynKernel.iso"
DIAGNOSTICS_FILE="$BUILD_ROOT/Kernel.stage3.diagnostics.log"

[ -f "$IR_FILE" ] || fail "IR file missing: $IR_FILE"
[ -f "$ASM_FILE" ] || fail "Assembly reference file missing: $ASM_FILE"
[ -f "$OBJECT_FILE" ] || fail "Direct ELF64 object missing: $OBJECT_FILE"
[ -f "$ELF_FILE" ] || fail "ELF file missing: $ELF_FILE"
[ -f "$ISO_FILE" ] || fail "ISO file missing: $ISO_FILE"
[ -f "$DIAGNOSTICS_FILE" ] || fail "Compiler diagnostics log missing: $DIAGNOSTICS_FILE"

grep -q 'direct ELF64 relocatable object' "$LOG_FILE" || fail "Direct ELF64 object proof missing from: $LOG_FILE"
grep -q 'Using direct Oryn ELF64 object writer output' "$LOG_FILE" || fail "Direct object link proof missing from: $LOG_FILE"
grep -q 'x86_64 freestanding kernel created' "$LOG_FILE" || fail "ELF creation proof missing from: $LOG_FILE"
grep -q 'Bootable kernel ISO created' "$LOG_FILE" || fail "ISO creation proof missing from: $LOG_FILE"
grep -q 'QEMU run skipped because ORYN_SKIP_QEMU=1' "$LOG_FILE" || fail "Expected QEMU skip proof missing from: $LOG_FILE"

info "Stage 3 direct ELF64 compile/link/ISO proof passed"
info "IR: $IR_FILE"
info "Object: $OBJECT_FILE"
info "ELF: $ELF_FILE"
info "ISO: $ISO_FILE"
