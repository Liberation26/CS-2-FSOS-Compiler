#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage3"
BUILD_ROOT="$ROOT_DIR/OSes/Stage3/Build/Runqemu"
LOG_FILE="$TEST_OUTPUT_DIR/02-elf64-object-check.log"
OBJECT_FILE="$BUILD_ROOT/Kernel.stage3.o"

info() { printf '[ OK ] [ STAGE3T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE3T ] %s\n' "$1"; exit 1; }

mkdir -p "$TEST_OUTPUT_DIR"

if [ ! -f "$OBJECT_FILE" ]; then
    ORYN_SKIP_QEMU=1 "$ROOT_DIR/Runqemu.sh" Stage3 >/dev/null
fi

[ -f "$OBJECT_FILE" ] || fail "Direct ELF64 object missing: $OBJECT_FILE"
command -v readelf >/dev/null 2>&1 || fail "Required tool not found: readelf"

readelf -h "$OBJECT_FILE" > "$LOG_FILE"
readelf -S "$OBJECT_FILE" >> "$LOG_FILE"
readelf -r "$OBJECT_FILE" >> "$LOG_FILE"
readelf -s "$OBJECT_FILE" >> "$LOG_FILE"

grep -q 'ELF64' "$LOG_FILE" || fail "Object is not ELF64: $OBJECT_FILE"
grep -q 'REL.*Relocatable file' "$LOG_FILE" || fail "Object is not relocatable: $OBJECT_FILE"
grep -q '.text' "$LOG_FILE" || fail ".text section missing: $OBJECT_FILE"
grep -q '.rodata' "$LOG_FILE" || fail ".rodata section missing: $OBJECT_FILE"
grep -q '.rela.text' "$LOG_FILE" || fail ".rela.text section missing: $OBJECT_FILE"
grep -q 'Kernel_Main' "$LOG_FILE" || fail "Kernel_Main symbol missing: $OBJECT_FILE"

info "Stage 3 ELF64 object structure proof passed"
info "Object: $OBJECT_FILE"
info "Log: $LOG_FILE"
