#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
export ORYN_BUILD_COMPILER=0
export ORYN_SKIP_QEMU=0
export ORYN_QEMU_DISPLAY=headless
"$ROOT/Runqemu.sh" Stage7
LOG="$ROOT/OSes/Stage7/Build/Runqemu/Qemu.serial.log"
grep -q 'Stage7 native pre-kernel handoff reached' "$LOG"
grep -q 'Stage7 kernel entered' "$LOG"
grep -q 'Stage7 dependency graph loading started' "$LOG"
grep -q 'dependency Runtime -> <none>' "$LOG"
grep -q 'dependency Diagnostics -> Runtime' "$LOG"
grep -q 'dependency Memory -> Runtime, Diagnostics' "$LOG"
grep -q 'dependency Panic -> Runtime, Diagnostics' "$LOG"
grep -q 'dependency Cpu -> Runtime, Diagnostics' "$LOG"
grep -q 'resolved initialization order: Runtime, Diagnostics, Memory, Panic, Cpu' "$LOG"
grep -q 'initializing Runtime' "$LOG"
grep -q 'initializing Diagnostics' "$LOG"
grep -q 'initializing Memory' "$LOG"
grep -q 'initializing Panic' "$LOG"
grep -q 'initializing Cpu' "$LOG"
grep -q 'Stage7 dependency-resolved modules initialized' "$LOG"
grep -q 'Stage7 kernel is halting forever' "$LOG"
