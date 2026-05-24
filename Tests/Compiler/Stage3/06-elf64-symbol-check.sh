#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage3"
BUILD_ROOT="$ROOT_DIR/OSes/Stage3/Build/Runqemu"
LOG_FILE="$TEST_OUTPUT_DIR/06-elf64-symbol-check.log"
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
if ".symtab" not in by_name or ".strtab" not in by_name:
    fail("symbol table or string table missing")
symtab = by_name[".symtab"]
strtab = by_name[".strtab"]
strtab_data = data[strtab["offset"]:strtab["offset"] + strtab["size"]]
if symtab["entsize"] != 24 or symtab["size"] % 24 != 0:
    fail("invalid ELF64 symbol table entry size")

symbols = []
for i in range(symtab["size"] // 24):
    off = symtab["offset"] + i * 24
    st_name, st_info, st_other, st_shndx, st_value, st_size = struct.unpack_from("<IBBHQQ", data, off)
    name = read_c_string(strtab_data, st_name)
    symbols.append({"index": i, "name": name, "bind": st_info >> 4, "type": st_info & 0x0F, "shndx": st_shndx, "value": st_value, "size": st_size})

by_symbol = {s["name"]: s for s in symbols if s["name"]}
required_defined_functions = ["Kernel_Main", "Kernel_WriteBanner"]
for name in required_defined_functions:
    symbol = by_symbol.get(name)
    if not symbol:
        fail(f"required function symbol missing: {name}")
    if symbol["bind"] != 1 or symbol["type"] != 2 or symbol["shndx"] == 0 or symbol["size"] == 0:
        fail(f"{name} is not a defined global function symbol")

required_undefined = ["Diagnostics_WriteOk", "Diagnostics_WriteFail", "Memory_Initialize", "Cpu_HaltForever"]
for name in required_undefined:
    symbol = by_symbol.get(name)
    if not symbol:
        fail(f"required external symbol missing: {name}")
    if symbol["bind"] != 1 or symbol["shndx"] != 0:
        fail(f"{name} should be an unresolved global external symbol")

string_symbols = [s for s in symbols if s["name"].startswith(".Lstr")]
if len(string_symbols) < 5:
    fail(f"expected multiple local string symbols, found {len(string_symbols)}")
for symbol in string_symbols:
    if symbol["bind"] != 0 or symbol["type"] != 1 or symbol["shndx"] == 0 or symbol["size"] == 0:
        fail(f"invalid local string object symbol: {symbol['name']}")

lines = [f"Object: {object_path}", "Required symbols: OK"]
for symbol in symbols:
    lines.append(f"[{symbol['index']:02d}] {symbol['name'] or '<null>'} bind={symbol['bind']} type={symbol['type']} shndx={symbol['shndx']} value={symbol['value']} size={symbol['size']}")
log_path.write_text("\n".join(lines) + "\n")
PY

info "Stage 3 ELF64 symbol proof passed"
info "Log: $LOG_FILE"
