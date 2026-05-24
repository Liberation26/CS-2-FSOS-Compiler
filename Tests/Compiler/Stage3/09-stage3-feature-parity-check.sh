#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TEST_OUTPUT_DIR="$ROOT_DIR/Build/Compiler/Stage3"
BUILD_ROOT="$ROOT_DIR/OSes/Stage3/Build/Runqemu"
LOG_FILE="$TEST_OUTPUT_DIR/09-stage3-feature-parity-check.log"
IR_FILE="$BUILD_ROOT/Kernel.stage3.stage3.ir.json"
ASM_FILE="$BUILD_ROOT/Kernel.stage3.generated.S"
OBJECT_FILE="$BUILD_ROOT/Kernel.stage3.o"

info() { printf '[ OK ] [ STAGE3T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE3T ] %s\n' "$1"; exit 1; }

mkdir -p "$TEST_OUTPUT_DIR"

if [ ! -f "$IR_FILE" ] || [ ! -f "$OBJECT_FILE" ]; then
    ORYN_SKIP_QEMU=1 "$ROOT_DIR/Runqemu.sh" Stage3 >/dev/null
fi

[ -f "$IR_FILE" ] || fail "IR file missing: $IR_FILE"
[ -f "$ASM_FILE" ] || fail "Assembly reference file missing: $ASM_FILE"
[ -f "$OBJECT_FILE" ] || fail "Direct ELF64 object missing: $OBJECT_FILE"
command -v python3 >/dev/null 2>&1 || fail "Required tool not found: python3"

python3 - "$IR_FILE" "$ASM_FILE" "$LOG_FILE" <<'PY'
import json
import sys
from pathlib import Path

ir_path = Path(sys.argv[1])
asm_path = Path(sys.argv[2])
log_path = Path(sys.argv[3])

def fail(message: str) -> None:
    log_path.write_text(f"[FAIL] {message}\n")
    print(f"[FAIL] [ STAGE3T ] {message}")
    sys.exit(1)

try:
    module = json.loads(ir_path.read_text())
except Exception as exc:
    fail(f"could not parse IR JSON: {exc}")

methods = module.get("Methods") or module.get("methods") or []
if not methods:
    fail("IR module contains no methods")

instructions = []
method_names = []
for method in methods:
    name = method.get("NativeSymbol") or method.get("nativeSymbol") or method.get("Name") or method.get("name") or "<unknown>"
    method_names.append(name)
    for instruction in method.get("Instructions") or method.get("instructions") or []:
        instructions.append(instruction)

def value(item, upper, lower):
    return item.get(upper) if item.get(upper) is not None else item.get(lower)

opcodes = {value(instruction, "OpCode", "opCode") for instruction in instructions}
required_opcodes = [
    "DeclareLocal",
    "StoreLocal",
    "LoadLocal",
    "ConstInt32",
    "ConstString",
    "AddInt32",
    "SubInt32",
    "CompareEqualInt32",
    "CompareLessThanInt32",
    "Label",
    "Jump",
    "JumpIfFalse",
    "Call",
    "Return",
]
for opcode in required_opcodes:
    if opcode not in opcodes:
        fail(f"required Stage 3 parity IR opcode missing: {opcode}")

required_methods = ["Kernel_Main", "Kernel_WriteBanner", "Kernel_WriteReturnProof"]
for method in required_methods:
    if method not in method_names:
        fail(f"required Stage 3 parity method missing from IR: {method}")

managed_calls = {value(instruction, "ManagedName", "managedName") for instruction in instructions if value(instruction, "OpCode", "opCode") == "Call"}
required_calls = [
    "Diagnostics.WriteOk",
    "Diagnostics.WriteFail",
    "Memory.Initialize",
    "Cpu.HaltForever",
    "Kernel.WriteBanner",
    "Kernel.WriteReturnProof",
]
for call in required_calls:
    if call not in managed_calls:
        fail(f"required Stage 3 parity call missing from IR: {call}")

strings = {value(instruction, "StringValue", "stringValue") for instruction in instructions if value(instruction, "OpCode", "opCode") == "ConstString"}
required_strings = [
    "Stage3 loop tick",
    "Stage3 branch worked",
    "Stage3 subtraction worked",
    "Stage3 integer arithmetic worked",
    "Stage3 helper method worked",
    "Stage3 explicit return worked",
    "Stage3 parity proof complete",
]
for value in required_strings:
    if value not in strings:
        fail(f"required Stage 3 parity string missing from IR: {value}")

asm_text = asm_path.read_text(errors="replace")
for symbol in required_methods:
    if symbol not in asm_text:
        fail(f"required Stage 3 parity symbol missing from assembly reference: {symbol}")

lines = [
    f"IR: {ir_path}",
    f"Assembly: {asm_path}",
    "Stage 3 feature parity IR proof: OK",
    "Methods: " + ", ".join(method_names),
    "Opcodes: " + ", ".join(sorted(op for op in opcodes if op)),
    "Calls: " + ", ".join(sorted(call for call in managed_calls if call)),
]
log_path.write_text("\n".join(lines) + "\n")
PY

info "Stage 3 feature parity IR proof passed"
info "Log: $LOG_FILE"
