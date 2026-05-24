#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

info() { printf '[ OK ] [ STAGE2T ] %s\n' "$1"; }

info "Running Stage 2 compiler and boot test suite"
"$SCRIPT_DIR/01-compile-stage2-kernel.sh"
"$SCRIPT_DIR/02-ir-output-check.sh"
"$SCRIPT_DIR/03-assembly-output-check.sh"
"$SCRIPT_DIR/04-qemu-stage2-boot.sh"
info "Stage 2 compiler and boot test suite passed"
