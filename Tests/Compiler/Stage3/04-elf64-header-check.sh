#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage3"
BUILD_ROOT="$ROOT_DIR/OSes/Stage3/Build/Runqemu"
LOG_FILE="$TEST_OUTPUT_DIR/04-elf64-header-check.log"
OBJECT_FILE="$BUILD_ROOT/Kernel.stage3.o"

info() { printf '[ OK ] [ STAGE3T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE3T ] %s\n' "$1"; exit 1; }

mkdir -p "$TEST_OUTPUT_DIR"
[ -f "$OBJECT_FILE" ] || fail "Direct ELF64 object missing: $OBJECT_FILE. Run 01-compile-stage3-kernel.sh first."
command -v python3 >/dev/null 2>&1 || fail "Required tool not found: python3"

python3 - "$OBJECT_FILE" "$LOG_FILE" <<'PY'
import struct
import sys
from pathlib import Path

object_path = Path(sys.argv[1])
log_path = Path(sys.argv[2])
data = object_path.read_bytes()

def fail(message: str) -> None:
    log_path.write_text(f"[FAIL] {message}\n")
    print(f"[FAIL] [ STAGE3T ] {message}")
    sys.exit(1)

if len(data) < 64:
    fail(f"ELF file too small: {object_path}")
if data[0:4] != b"\x7fELF":
    fail("ELF magic is missing")
if data[4] != 2:
    fail(f"ELF class is not 64-bit: {data[4]}")
if data[5] != 1:
    fail(f"ELF data encoding is not little-endian: {data[5]}")
if data[6] != 1:
    fail(f"ELF version byte is not current: {data[6]}")

(
    e_type,
    e_machine,
    e_version,
    e_entry,
    e_phoff,
    e_shoff,
    e_flags,
    e_ehsize,
    e_phentsize,
    e_phnum,
    e_shentsize,
    e_shnum,
    e_shstrndx,
) = struct.unpack_from("<HHIQQQIHHHHHH", data, 16)

checks = []
checks.append(("type", e_type == 1, f"expected relocatable ET_REL(1), got {e_type}"))
checks.append(("machine", e_machine == 62, f"expected x86-64 EM_X86_64(62), got {e_machine}"))
checks.append(("version", e_version == 1, f"expected e_version 1, got {e_version}"))
checks.append(("entry", e_entry == 0, f"expected relocatable e_entry 0, got {e_entry}"))
checks.append(("program-header-offset", e_phoff == 0, f"expected no program header table, got e_phoff {e_phoff}"))
checks.append(("program-header-count", e_phnum == 0, f"expected no program headers, got {e_phnum}"))
checks.append(("elf-header-size", e_ehsize == 64, f"expected ELF64 header size 64, got {e_ehsize}"))
checks.append(("section-header-size", e_shentsize == 64, f"expected ELF64 section header size 64, got {e_shentsize}"))
checks.append(("section-header-offset", e_shoff > 0, f"expected section header table offset > 0, got {e_shoff}"))
checks.append(("section-header-count", e_shnum >= 8, f"expected at least 8 sections, got {e_shnum}"))
checks.append(("section-name-index", 0 < e_shstrndx < e_shnum, f"invalid shstrtab index {e_shstrndx} for {e_shnum} sections"))

for _, ok, message in checks:
    if not ok:
        fail(message)

if e_shoff + (e_shentsize * e_shnum) > len(data):
    fail("section header table extends past end of file")

lines = [
    f"Object: {object_path}",
    "ELF magic: OK",
    "Class: ELF64",
    "Endian: little",
    "Type: ET_REL relocatable",
    "Machine: EM_X86_64",
    f"Section header offset: {e_shoff}",
    f"Section count: {e_shnum}",
    f"Section string table index: {e_shstrndx}",
]
log_path.write_text("\n".join(lines) + "\n")
PY

info "Stage 3 ELF64 header proof passed"
info "Log: $LOG_FILE"
