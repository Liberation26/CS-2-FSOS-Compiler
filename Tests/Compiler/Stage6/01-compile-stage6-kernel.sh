#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BUILD="$ROOT/OSes/Stage6/Build/Tests"
mkdir -p "$BUILD"
export ORYN_BUILD_COMPILER=1
export ORYN_SKIP_QEMU=1
"$ROOT/Runqemu.sh" Stage6 | tee "$BUILD/01-compile-stage6-kernel.log"
test -f "$ROOT/OSes/Stage6/Build/Runqemu/Kernel.stage6.o"
test -f "$ROOT/OSes/Stage6/Build/Runqemu/Kernel.stage6.stage6.ir.json"
