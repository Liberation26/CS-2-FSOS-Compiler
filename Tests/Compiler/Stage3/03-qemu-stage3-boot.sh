#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage3"
BUILD_ROOT="$ROOT_DIR/OSes/Stage3/Build/Runqemu"
LOG_FILE="$TEST_OUTPUT_DIR/03-qemu-stage3-boot.log"
SERIAL_LOG="$BUILD_ROOT/Qemu.serial.log"

info() { printf '[ OK ] [ STAGE3T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE3T ] %s\n' "$1"; exit 1; }

mkdir -p "$TEST_OUTPUT_DIR"

info "Running Stage 3 QEMU boot proof; timeout after halt is success"
set +e
ORYN_QEMU_DISPLAY=headless ORYN_QEMU_TIMEOUT="${ORYN_QEMU_TIMEOUT:-8}" "$ROOT_DIR/Runqemu.sh" Stage3 2>&1 | tee "$LOG_FILE"
STATUS=${PIPESTATUS[0]}
set -e

if [ "$STATUS" -ne 0 ]; then
    fail "Runqemu Stage3 QEMU path exited with status $STATUS. See: $LOG_FILE"
fi

[ -f "$SERIAL_LOG" ] || fail "Serial log missing: $SERIAL_LOG"
grep -q '\[ OK \] \[ BOOT32' "$SERIAL_LOG" || fail "BOOT32 proof missing from serial log"
grep -q '\[ OK \] \[ BOOT' "$SERIAL_LOG" || fail "Long-mode BOOT proof missing from serial log"
grep -q '\[ OK \] \[ KERNEL' "$SERIAL_LOG" || fail "Kernel proof missing from serial log"
grep -q 'QEMU timeout reached' "$LOG_FILE" || fail "Timeout-as-success proof missing from: $LOG_FILE"

info "Stage 3 QEMU boot proof passed"
info "Serial log: $SERIAL_LOG"
