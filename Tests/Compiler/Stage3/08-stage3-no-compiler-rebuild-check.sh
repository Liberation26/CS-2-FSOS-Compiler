#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage3"
LOG_FILE="$TEST_OUTPUT_DIR/08-stage3-no-compiler-rebuild-check.log"
BUILD_LOG="$ROOT_DIR/Build/Oryn.Compiler.build.log"

info() { printf '[ OK ] [ STAGE3T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE3T ] %s\n' "$1"; exit 1; }

mkdir -p "$TEST_OUTPUT_DIR"
rm -f "$BUILD_LOG"

info "Running Stage 3 compile path with compiler rebuild explicitly disabled"
set +e
ORYN_BUILD_COMPILER=0 ORYN_SKIP_QEMU=1 "$ROOT_DIR/Runqemu.sh" Stage3 2>&1 | tee "$LOG_FILE"
STATUS=${PIPESTATUS[0]}
set -e

if [ "$STATUS" -ne 0 ]; then
    fail "Runqemu Stage3 no-rebuild path exited with status $STATUS. See: $LOG_FILE"
fi

if grep -q 'Building Oryn.Compiler' "$LOG_FILE"; then
    fail "Runqemu attempted to build Oryn.Compiler even though ORYN_BUILD_COMPILER=0. See: $LOG_FILE"
fi

if [ -f "$BUILD_LOG" ]; then
    fail "Compiler build log was created even though ORYN_BUILD_COMPILER=0: $BUILD_LOG"
fi

grep -q 'Running Oryn.Compiler backend for Stage3' "$LOG_FILE" || fail "Compiler backend run proof missing from: $LOG_FILE"
grep -q 'QEMU run skipped because ORYN_SKIP_QEMU=1' "$LOG_FILE" || fail "QEMU skip proof missing from: $LOG_FILE"

info "Stage 3 no-automatic-compiler-rebuild proof passed"
info "Log: $LOG_FILE"
