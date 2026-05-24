#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
CONTRACT_DIR="$PROJECT_ROOT/Source/Sdk/ApiContracts"
[ -d "$CONTRACT_DIR" ] || { echo "[FAIL] API contract directory missing"; exit 1; }
for Contract in Diagnostics Runtime Memory Panic Cpu ManifestLoader; do
    [ -f "$CONTRACT_DIR/${Contract}.api-contract.json" ] || { echo "[FAIL] Missing API contract: $Contract"; exit 1; }
done
grep -R '"allowedFromCSharpKernel": true' "$CONTRACT_DIR" >/dev/null || { echo "[FAIL] No approved C# API contract methods found"; exit 1; }
echo "[ OK ] Stage8 API contract files are present."
