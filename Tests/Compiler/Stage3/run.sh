#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/01-compile-stage3-kernel.sh"
"$SCRIPT_DIR/02-elf64-object-check.sh"
"$SCRIPT_DIR/03-qemu-stage3-boot.sh"
