#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
LOG="$PROJECT_ROOT/OSes/Stage9/Build/Test04/Runqemu.log"
mkdir -p "$(dirname "$LOG")"
ORYN_BUILD_COMPILER=1 ORYN_QEMU_HEADLESS=1 bash "$PROJECT_ROOT/Runqemu.sh" Stage9 > "$LOG" 2>&1
grep -F "Stage9 generated kernel entered" "$LOG" >/dev/null || { echo "[FAIL] Stage9 generated kernel did not boot"; cat "$LOG"; exit 1; }
grep -F "[ COMPOSE ] Stage9 generated template composition runtime proof completed" "$LOG" >/dev/null || { echo "[FAIL] Stage9 runtime composition proof missing"; cat "$LOG"; exit 1; }
echo "[ OK ] Stage9 generated kernel booted in QEMU."
