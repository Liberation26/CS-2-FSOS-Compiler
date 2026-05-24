#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage2"
BUILD_ROOT="$ROOT_DIR/OSes/Stage2/Build/Runqemu"
LOG_FILE="$TEST_OUTPUT_DIR/04-qemu-stage2-boot.log"

info() { printf '[ OK ] [ STAGE2T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE2T ] %s\n' "$1"; exit 1; }

mkdir -p "$TEST_OUTPUT_DIR"

info "Running Stage 2 QEMU boot proof"
set +e
"$ROOT_DIR/Runqemu.sh" Stage2 2>&1 | tee "$LOG_FILE"
STATUS=${PIPESTATUS[0]}
set -e

if [ "$STATUS" -ne 0 ]; then
    fail "Runqemu Stage2 QEMU path exited with status $STATUS. See: $LOG_FILE"
fi

grep -q 'Stage2 kernel entered' "$LOG_FILE" || fail "Stage2 entry diagnostic missing from QEMU output"
grep -q 'Stage2 memory initialized' "$LOG_FILE" || fail "Stage2 memory diagnostic missing from QEMU output"
grep -q 'Stage2 loop tick' "$LOG_FILE" || fail "Stage2 loop diagnostic missing from QEMU output"
grep -q 'Stage2 branch worked' "$LOG_FILE" || fail "Stage2 branch diagnostic missing from QEMU output"
grep -q 'Stage2 helper method worked' "$LOG_FILE" || fail "Stage2 helper diagnostic missing from QEMU output"
grep -q 'Stage2 kernel is halting forever' "$LOG_FILE" || fail "Stage2 halt diagnostic missing from QEMU output"
grep -q 'QEMU timeout reached after' "$LOG_FILE" || fail "QEMU timeout success proof missing from output"
grep -q 'remained running as expected' "$LOG_FILE" || fail "halt-loop success wording missing from output"

SERIAL_LOG="$BUILD_ROOT/Qemu.serial.log"
DEBUGCON_LOG="$BUILD_ROOT/Qemu.debugcon.log"
if [ ! -s "$SERIAL_LOG" ] && [ ! -s "$DEBUGCON_LOG" ]; then
    fail "Neither serial nor debugcon QEMU proof log contains output"
fi

info "Stage 2 QEMU boot proof passed"
