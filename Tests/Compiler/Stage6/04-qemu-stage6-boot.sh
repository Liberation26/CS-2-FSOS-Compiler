#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
export ORYN_BUILD_COMPILER=0
export ORYN_SKIP_QEMU=0
export ORYN_QEMU_DISPLAY=headless
"$ROOT/Runqemu.sh" Stage6
LOG="$ROOT/OSes/Stage6/Build/Runqemu/Qemu.serial.log"
grep -q 'Stage6 native pre-kernel handoff reached' "$LOG"
grep -q 'Stage6 kernel entered' "$LOG"
grep -q 'Stage6 module manifest loading started' "$LOG"
grep -q 'ManifestLoader glue is active' "$LOG"
grep -q 'initializing Runtime' "$LOG"
grep -q 'initializing Memory' "$LOG"
grep -q 'generated Stage 6 manifest runtime completed' "$LOG"
grep -q 'Stage6 kernel is halting forever' "$LOG"
