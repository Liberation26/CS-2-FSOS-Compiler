#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
PROGRAM="$ROOT/Source/Core/Oryn.Generator/Program.cs"
ORYN_SH="$ROOT/Oryn.sh"
RUNQEMU="$ROOT/Runqemu.sh"
QUESTIONS="$ROOT/Questions"

grep -Fq 'Hello from __ORYN_OS_NAME__' "$PROGRAM"
grep -Fq 'VmDisplayMode' "$PROGRAM"
grep -Fq 'Should the virtual machine be visual or headless?' "$QUESTIONS/005-vm-display-mode.question.json"
grep -Fq 'ORYN_QEMU_DISPLAY' "$ORYN_SH"
grep -Fq 'visual|visible|headed|head|gui' "$RUNQEMU"
grep -Fq 'Hello from ${STAGE_NAME}' "$RUNQEMU"

echo '[ OK ] [ TEST      ] 1.0.7 generated OS hello and VM display-mode checks passed.'
