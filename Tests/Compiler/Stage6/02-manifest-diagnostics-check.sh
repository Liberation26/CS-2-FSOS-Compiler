#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
LOG="$ROOT/OSes/Stage6/Build/Runqemu/Kernel.stage6.diagnostics.log"
test -f "$LOG"
grep -q 'Stage 6 proves service/module manifest loading' "$ROOT/OSes/Stage6/Build/Runqemu/Kernel.stage6.stage6.ir.json"
grep -q 'ModuleManifests' "$ROOT/OSes/Stage6/Build/Runqemu/Kernel.stage6.stage6.ir.json"
