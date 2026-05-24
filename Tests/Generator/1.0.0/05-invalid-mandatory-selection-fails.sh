#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
set +e
(cd "$ROOT" && dotnet run --project Source/Core/Oryn.Generator -- generate --os-name InvalidSelectionOS --modules Diagnostics) > /tmp/oryn-invalid-selection.log 2>&1
STATUS=$?
set -e
if [ "$STATUS" -eq 0 ]; then
  cat /tmp/oryn-invalid-selection.log
  echo "[FAIL] Generator accepted Diagnostics as a user-selected module."
  exit 1
fi
grep -Fq 'mandatory kernel module' /tmp/oryn-invalid-selection.log || { cat /tmp/oryn-invalid-selection.log; echo "[FAIL] Expected mandatory module failure message."; exit 1; }
echo "[ OK ] 1.0.0 rejects mandatory modules in user-selected module list."
