#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
"$SCRIPT_DIR/11-none-module-selection-check.sh"
echo "[ OK ] [ TEST      ] Generator 1.0.6 regression tests passed."
