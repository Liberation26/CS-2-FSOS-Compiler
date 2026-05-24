#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
GLUE="$ROOT/OSes/Stage7/Build/Runqemu/ModuleManifest.Generated.c"
[ -f "$GLUE" ] || { ORYN_BUILD_COMPILER=0 ORYN_SKIP_QEMU=1 "$ROOT/Runqemu.sh" Stage7 >/dev/null; }
python3 - "$GLUE" <<'PY'
import pathlib
import sys
text = pathlib.Path(sys.argv[1]).read_text(encoding='utf-8')
need = [
    'initializing Runtime',
    'initializing Diagnostics',
    'initializing Memory',
    'initializing Panic',
    'initializing Cpu',
]
positions = [text.index(item) for item in need]
if positions != sorted(positions):
    raise SystemExit('[FAIL] initializer order in generated glue is not dependency-safe')
print('[ OK ] [ TEST      ] generated glue initializer order is dependency-safe')
PY
