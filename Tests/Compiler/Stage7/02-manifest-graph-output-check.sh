#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BUILD="$ROOT/OSes/Stage7/Build/Tests"
mkdir -p "$BUILD"
export ORYN_BUILD_COMPILER=0
export ORYN_SKIP_QEMU=1
"$ROOT/Runqemu.sh" Stage7 | tee "$BUILD/02-manifest-graph-output-check.log"
LOG="$BUILD/02-manifest-graph-output-check.log"
grep -q 'dependency Runtime -> <none>' "$LOG"
grep -q 'dependency Diagnostics -> Runtime' "$LOG"
grep -q 'dependency Memory -> Runtime, Diagnostics' "$LOG"
grep -q 'dependency Panic -> Runtime, Diagnostics' "$LOG"
grep -q 'dependency Cpu -> Runtime, Diagnostics' "$LOG"
grep -q 'resolved initialization order: Runtime, Diagnostics, Memory, Panic, Cpu' "$LOG"
