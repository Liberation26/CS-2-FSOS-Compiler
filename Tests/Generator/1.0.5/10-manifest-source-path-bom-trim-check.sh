#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
RUNQEMU="$ROOT/Runqemu.sh"

grep -Fq "with out.open('w', encoding='utf-8') as handle" "$RUNQEMU"
grep -Fq "lstrip('\ufeff').strip()" "$RUNQEMU"
grep -Fq "NativeSource="\${NativeSource#\$'\xef\xbb\xbf'}"" "$RUNQEMU" || grep -Fq "\xef\xbb\xbf" "$RUNQEMU"

echo "[ OK ] [ TEST      ] Manifest source path BOM trimming is present."
