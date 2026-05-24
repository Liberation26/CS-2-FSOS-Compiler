#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
MANIFEST="$ROOT/Source/Sdk/ModuleManifests/Runtime.module.json"
BACKUP="$(mktemp)"
cp "$MANIFEST" "$BACKUP"
restore() { cp "$BACKUP" "$MANIFEST"; rm -f "$BACKUP"; }
trap restore EXIT
python3 - "$MANIFEST" <<'PY'
import json, pathlib, sys
path = pathlib.Path(sys.argv[1])
data = json.loads(path.read_text(encoding='utf-8'))
data['dependsOn'] = ['Cpu']
path.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
PY
set +e
ORYN_BUILD_COMPILER=0 ORYN_SKIP_QEMU=1 "$ROOT/Runqemu.sh" Stage7 > "$ROOT/OSes/Stage7/Build/Tests/05-circular-dependency-rejected.log" 2>&1
STATUS=$?
set -e
[ "$STATUS" -ne 0 ] || { cat "$ROOT/OSes/Stage7/Build/Tests/05-circular-dependency-rejected.log"; echo '[FAIL] circular dependency was accepted'; exit 1; }
grep -q 'circular module dependency detected' "$ROOT/OSes/Stage7/Build/Tests/05-circular-dependency-rejected.log"
