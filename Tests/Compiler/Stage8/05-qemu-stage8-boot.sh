#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_ROOT="$PROJECT_ROOT/OSes/Stage8/Build/Test05"
mkdir -p "$BUILD_ROOT"
export ORYN_BUILD_COMPILER=1
export ORYN_BUILD_ROOT="$BUILD_ROOT"
export ORYN_QEMU_HEADLESS=1
export ORYN_QEMU_TIMEOUT="8"
"$PROJECT_ROOT/Runqemu.sh" Stage8
LOG="$BUILD_ROOT/Qemu.serial.log"
[ -f "$LOG" ] || { echo "[FAIL] QEMU serial log missing"; exit 1; }
grep -F "Stage8 kernel is halting forever" "$LOG" >/dev/null || { echo "[FAIL] Stage8 halt proof missing"; exit 1; }
echo "[ OK ] Stage8 QEMU boot proof passed."
