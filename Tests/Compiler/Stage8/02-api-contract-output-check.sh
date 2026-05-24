#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_ROOT="$PROJECT_ROOT/OSes/Stage8/Build/Test02"
mkdir -p "$BUILD_ROOT"
export ORYN_BUILD_COMPILER=1
export ORYN_SKIP_QEMU=1
export ORYN_BUILD_ROOT="$BUILD_ROOT"
"$PROJECT_ROOT/Runqemu.sh" Stage8
LOG="$BUILD_ROOT/Kernel.stage8.diagnostics.log"
[ -f "$LOG" ] || { echo "[FAIL] diagnostics log missing: $LOG"; exit 1; }
grep -F "Stage 8 module API contract validation passed" "$LOG" >/dev/null || { echo "[FAIL] Stage8 contract validation proof missing"; exit 1; }
grep -F "approved Oryn.Kernel.Diagnostics.Diagnostics.WriteOk -> Diagnostics_WriteOk" "$LOG" >/dev/null || { echo "[FAIL] Diagnostics.WriteOk contract proof missing"; exit 1; }
echo "[ OK ] Stage8 API contract diagnostics found."
