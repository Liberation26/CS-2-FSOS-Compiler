#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage3"
BUILD_ROOT="$ROOT_DIR/OSes/Stage3/Build/Runqemu"
LOG_FILE="$TEST_OUTPUT_DIR/05-elf64-section-check.log"
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

if data[:4] != b"\x7fELF" or data[4] != 2 or data[5] != 1:
    fail("not a little-endian ELF64 object")
header = struct.unpack_from("<HHIQQQIHHHHHH", data, 16)
e_shoff, e_shentsize, e_shnum, e_shstrndx = header[5], header[10], header[11], header[12]
sections = []
for index in range(e_shnum):
    off = e_shoff + index * e_shentsize
    if off + 64 > len(data):
        fail(f"section header {index} extends past end of file")
    sh = struct.unpack_from("<IIQQQQIIQQ", data, off)
    sections.append({
        "index": index,
        "name_offset": sh[0],
        "type": sh[1],
        "flags": sh[2],
        "addr": sh[3],
        "offset": sh[4],
        "size": sh[5],
        "link": sh[6],
        "info": sh[7],
        "addralign": sh[8],
        "entsize": sh[9],
    })
if not (0 <= e_shstrndx < len(sections)):
    fail("invalid section string table index")
shstr = sections[e_shstrndx]
shstr_data = data[shstr["offset"]:shstr["offset"] + shstr["size"]]

def read_c_string(blob: bytes, offset: int) -> str:
    if offset >= len(blob):
        return ""
    end = blob.find(b"\x00", offset)
    if end < 0:
        end = len(blob)
    return blob[offset:end].decode("utf-8", "replace")

for section in sections:
    section["name"] = read_c_string(shstr_data, section["name_offset"])
by_name = {section["name"]: section for section in sections}
required = [".text", ".rodata", ".rela.text", ".symtab", ".strtab", ".shstrtab", ".note.GNU-stack"]
for name in required:
    if name not in by_name:
        fail(f"required section missing: {name}")

SHT_PROGBITS = 1
SHT_SYMTAB = 2
SHT_STRTAB = 3
SHT_RELA = 4
SHF_WRITE = 0x1
SHF_ALLOC = 0x2
SHF_EXECINSTR = 0x4

text = by_name[".text"]
if text["type"] != SHT_PROGBITS or not (text["flags"] & SHF_ALLOC) or not (text["flags"] & SHF_EXECINSTR):
    fail(".text is not executable/allocated SHT_PROGBITS")
if text["size"] == 0:
    fail(".text section is empty")
rodata = by_name[".rodata"]
if rodata["type"] != SHT_PROGBITS or not (rodata["flags"] & SHF_ALLOC) or (rodata["flags"] & SHF_WRITE):
    fail(".rodata is not read-only allocated SHT_PROGBITS")
if rodata["size"] == 0:
    fail(".rodata section is empty")
rela = by_name[".rela.text"]
if rela["type"] != SHT_RELA or rela["entsize"] != 24 or rela["size"] == 0:
    fail(".rela.text is not a non-empty ELF64 RELA section")
if sections[rela["info"]]["name"] != ".text":
    fail(".rela.text does not target .text through sh_info")
symtab = by_name[".symtab"]
if symtab["type"] != SHT_SYMTAB or symtab["entsize"] != 24 or symtab["size"] == 0:
    fail(".symtab is not a non-empty ELF64 symbol table")
if sections[symtab["link"]]["name"] != ".strtab":
    fail(".symtab does not link to .strtab")
for name in [".strtab", ".shstrtab"]:
    if by_name[name]["type"] != SHT_STRTAB or by_name[name]["size"] == 0:
        fail(f"{name} is not a non-empty string table")
if by_name[".note.GNU-stack"]["flags"] != 0:
    fail(".note.GNU-stack should be non-alloc/non-exec")

lines = [f"Object: {object_path}", "Required sections: OK"]
for section in sections:
    lines.append(f"[{section['index']:02d}] {section['name']} type={section['type']} flags=0x{section['flags']:x} size={section['size']} link={section['link']} info={section['info']} entsize={section['entsize']}")
log_path.write_text("\n".join(lines) + "\n")
PY

info "Stage 3 ELF64 section proof passed"
info "Log: $LOG_FILE"
