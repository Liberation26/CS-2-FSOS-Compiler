#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
PROGRAM="$ROOT/Source/Core/Oryn.Generator/Program.cs"
RUNQEMU="$ROOT/Runqemu.sh"
ORYN="$ROOT/Oryn.sh"
grep -Fq 'Visual opens a QEMU window and stays open until you close it.' "$PROGRAM"
grep -Fq 'Console.WriteLine($"[OPTIONS ] {ColouredExpectedAnswer}");' "$PROGRAM"
grep -Fq 'AnsiOptionColour' "$PROGRAM"
grep -Fq 'ORYN_NO_COLOR' "$PROGRAM"
grep -Fq 'timeout "${TIMEOUT_ARGS[@]}" qemu-system-x86_64' "$RUNQEMU"
grep -Fq 'qemu-system-x86_64 "${QEMU_ARGS[@]}"' "$RUNQEMU"
grep -Fq 'Visual stays open until you close the VM window' "$ORYN"
echo '[ OK ] 1.0.8 VM display and coloured question option checks passed.'
