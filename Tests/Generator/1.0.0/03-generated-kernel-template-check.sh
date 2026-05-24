#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
TEMPLATE="$ROOT/OSes/GeneratedTestOS/Templates/Kernel.template.cs"
[ -f "$TEMPLATE" ] || { echo "[FAIL] Kernel template was not created."; exit 1; }
grep -Fq '__ORYN_KERNEL_BOOT_PROOF_LINES__' "$TEMPLATE" || { echo "[FAIL] Template is missing boot proof placeholder."; exit 1; }
grep -Fq '__ORYN_MODULE_INITIALIZATION_CALLS__' "$TEMPLATE" || { echo "[FAIL] Template is missing module initializer placeholder."; exit 1; }
echo "[ OK ] 1.0.0 generated kernel template contains composition placeholders."
