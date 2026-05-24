#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
MANIFEST="$ROOT/OSes/GeneratedTestOS/manifest.json"
[ -f "$MANIFEST" ] || { echo "[FAIL] Manifest missing. Run 02-generator-creates-os-folder.sh first."; exit 1; }
grep -Fq '"MandatoryKernelModules"' "$MANIFEST" || { echo "[FAIL] MandatoryKernelModules missing."; exit 1; }
grep -Fq '"Diagnostics"' "$MANIFEST" || { echo "[FAIL] Diagnostics must be mandatory."; exit 1; }
grep -Fq '"Panic"' "$MANIFEST" || { echo "[FAIL] Panic must be mandatory."; exit 1; }
grep -Fq '"UserSelectedModules"' "$MANIFEST" || { echo "[FAIL] UserSelectedModules missing."; exit 1; }
python3 - "$MANIFEST" <<'PY'
import json, sys
with open(sys.argv[1], 'r', encoding='utf-8-sig') as handle:
    data = json.load(handle)
mandatory = set(data['MandatoryKernelModules'])
selected = set(data['UserSelectedModules'])
intersection = mandatory & selected
if intersection:
    raise SystemExit('[FAIL] Mandatory modules leaked into user-selected modules: ' + ', '.join(sorted(intersection)))
PY
echo "[ OK ] 1.0.4 mandatory modules are separated from user-selected modules."
