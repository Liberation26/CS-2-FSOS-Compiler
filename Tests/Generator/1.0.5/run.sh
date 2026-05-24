#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
"$SCRIPT_DIR/10-manifest-source-path-bom-trim-check.sh"
echo "[ OK ] [ TEST      ] Generator 1.0.5 regression tests passed."
