#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
(cd "$ROOT" && ./Oryn.sh build GeneratedTestOS)
echo "[ OK ] 1.0.2 generated OS builds through Oryn.sh."
