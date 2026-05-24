#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
SERIAL_LOG="$PROJECT_ROOT/OSes/Stage5/Build/Runqemu/Qemu.serial.log"

ORYN_STAGE=Stage5 ORYN_BUILD_COMPILER=1 ORYN_QEMU_DISPLAY=headless ORYN_QEMU_TIMEOUT="${ORYN_QEMU_TIMEOUT:-8}" "$PROJECT_ROOT/Runqemu.sh" Stage5

test -f "$SERIAL_LOG"
grep -q "Stage5 kernel entered" "$SERIAL_LOG"
grep -q "Stage5 runtime contract initialized" "$SERIAL_LOG"
grep -q "Stage5 memory module initialized" "$SERIAL_LOG"
grep -q "Stage5 loop and branch proof worked" "$SERIAL_LOG"
grep -q "Stage5 runtime marked kernel ready" "$SERIAL_LOG"
grep -q "Stage5 kernel is halting forever" "$SERIAL_LOG"

printf '[ OK ] [ TEST      ] Stage 5 QEMU boot diagnostics verified.
'
