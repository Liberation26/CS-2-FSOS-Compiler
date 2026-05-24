#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
(cd "$ROOT" && ORYN_QEMU_HEADLESS=1 ORYN_QEMU_TIMEOUT=8 ./Oryn.sh run GeneratedTestOS)
echo "[ OK ] 1.0.3 generated OS boots in QEMU."
