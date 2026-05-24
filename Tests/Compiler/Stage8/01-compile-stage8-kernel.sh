#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_ROOT="$PROJECT_ROOT/OSes/Stage8/Build/Test01"
mkdir -p "$BUILD_ROOT"
export ORYN_BUILD_COMPILER=1
export ORYN_SKIP_QEMU=1
export ORYN_BUILD_ROOT="$BUILD_ROOT"
"$PROJECT_ROOT/Runqemu.sh" Stage8
[ -f "$BUILD_ROOT/Kernel.stage8.o" ] || { echo "[FAIL] Stage8 object missing"; exit 1; }
echo "[ OK ] Stage8 compiler and linker output exists."
