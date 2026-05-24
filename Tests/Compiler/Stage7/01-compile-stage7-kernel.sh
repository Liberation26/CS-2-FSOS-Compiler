#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BUILD="$ROOT/OSes/Stage7/Build/Tests"
mkdir -p "$BUILD"
export ORYN_BUILD_COMPILER=1
export ORYN_SKIP_QEMU=1
"$ROOT/Runqemu.sh" Stage7 | tee "$BUILD/01-compile-stage7-kernel.log"
test -f "$ROOT/OSes/Stage7/Build/Runqemu/Kernel.stage7.o"
test -f "$ROOT/OSes/Stage7/Build/Runqemu/Kernel.stage7.stage7.ir.json"
test -f "$ROOT/OSes/Stage7/Build/Runqemu/ModuleManifest.Generated.c"
grep -q 'Stage 7 dependency graph validation passed' "$BUILD/01-compile-stage7-kernel.log"
grep -q 'resolved initialization order: Runtime, Diagnostics, Memory, Panic, Cpu' "$BUILD/01-compile-stage7-kernel.log"
