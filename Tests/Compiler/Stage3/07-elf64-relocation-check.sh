#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage3"
BUILD_ROOT="$ROOT_DIR/OSes/Stage3/Build/Runqemu"
LOG_FILE="$TEST_OUTPUT_DIR/07-elf64-relocation-check.log"
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

def read_c_string(blob: bytes, offset: int) -> str:
    if offset >= len(blob):
        return ""
    end = blob.find(b"\x00", offset)
    if end < 0:
        end = len(blob)
    return blob[offset:end].decode("utf-8", "replace")

if data[:4] != b"\x7fELF" or data[4] != 2 or data[5] != 1:
    fail("not a little-endian ELF64 object")
header = struct.unpack_from("<HHIQQQIHHHHHH", data, 16)
e_shoff, e_shentsize, e_shnum, e_shstrndx = header[5], header[10], header[11], header[12]
sections = []
for index in range(e_shnum):
    sh = struct.unpack_from("<IIQQQQIIQQ", data, e_shoff + index * e_shentsize)
    sections.append({"index": index, "name_offset": sh[0], "type": sh[1], "flags": sh[2], "offset": sh[4], "size": sh[5], "link": sh[6], "info": sh[7], "entsize": sh[9]})
shstr = sections[e_shstrndx]
shstr_data = data[shstr["offset"]:shstr["offset"] + shstr["size"]]
for section in sections:
    section["name"] = read_c_string(shstr_data, section["name_offset"])
by_name = {section["name"]: section for section in sections}
for name in [".rela.text", ".symtab", ".strtab", ".text"]:
    if name not in by_name:
        fail(f"required section missing: {name}")
rela = by_name[".rela.text"]
symtab = by_name[".symtab"]
strtab = by_name[".strtab"]
if rela["type"] != 4 or rela["entsize"] != 24 or rela["size"] % 24 != 0:
    fail(".rela.text is not a valid ELF64 RELA section")
if sections[rela["info"]]["name"] != ".text":
    fail(".rela.text does not target .text")
if sections[rela["link"]]["name"] != ".symtab":
    fail(".rela.text does not link to .symtab")

strtab_data = data[strtab["offset"]:strtab["offset"] + strtab["size"]]
symbols = []
for i in range(symtab["size"] // 24):
    st_name, st_info, st_other, st_shndx, st_value, st_size = struct.unpack_from("<IBBHQQ", data, symtab["offset"] + i * 24)
    symbols.append({"index": i, "name": read_c_string(strtab_data, st_name), "bind": st_info >> 4, "type": st_info & 0x0F, "shndx": st_shndx})

relocations = []
for i in range(rela["size"] // 24):
    r_offset, r_info, r_addend = struct.unpack_from("<QQq", data, rela["offset"] + i * 24)
    symbol_index = r_info >> 32
    rel_type = r_info & 0xffffffff
    if symbol_index >= len(symbols):
        fail(f"relocation {i} references invalid symbol index {symbol_index}")
    relocations.append({"index": i, "offset": r_offset, "symbol": symbols[symbol_index]["name"], "type": rel_type, "addend": r_addend})

if not relocations:
    fail("no relocations found")
allowed_types = {2, 4}  # R_X86_64_PC32, R_X86_64_PLT32
for relocation in relocations:
    if relocation["type"] not in allowed_types:
        fail(f"unsupported relocation type {relocation['type']} for {relocation['symbol']}")

required_symbols = ["Diagnostics_WriteOk", "Diagnostics_WriteFail", "Memory_Initialize", "Cpu_HaltForever", "Kernel_WriteBanner"]
for name in required_symbols:
    if not any(r["symbol"] == name for r in relocations):
        fail(f"required relocation target missing: {name}")
if not any(r["symbol"].startswith(".Lstr") and r["type"] == 2 for r in relocations):
    fail("required PC32 relocation to .rodata string symbol missing")
if not any(r["type"] == 4 for r in relocations):
    fail("required PLT32 call relocation missing")

lines = [f"Object: {object_path}", "Required relocations: OK"]
for relocation in relocations:
    lines.append(f"[{relocation['index']:02d}] offset=0x{relocation['offset']:x} type={relocation['type']} symbol={relocation['symbol']} addend={relocation['addend']}")
log_path.write_text("\n".join(lines) + "\n")
PY

info "Stage 3 ELF64 relocation proof passed"
info "Log: $LOG_FILE"
