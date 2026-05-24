#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/01-compile-stage3-kernel.sh"
"$SCRIPT_DIR/02-elf64-object-check.sh"
"$SCRIPT_DIR/04-elf64-header-check.sh"
"$SCRIPT_DIR/05-elf64-section-check.sh"
"$SCRIPT_DIR/06-elf64-symbol-check.sh"
"$SCRIPT_DIR/07-elf64-relocation-check.sh"
"$SCRIPT_DIR/08-stage3-no-compiler-rebuild-check.sh"
"$SCRIPT_DIR/03-qemu-stage3-boot.sh"
