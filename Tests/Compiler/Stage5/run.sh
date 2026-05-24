#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/01-compile-stage5-kernel.sh"
"$SCRIPT_DIR/02-runtime-and-panic-bindings-check.sh"
"$SCRIPT_DIR/03-qemu-stage5-boot.sh"
