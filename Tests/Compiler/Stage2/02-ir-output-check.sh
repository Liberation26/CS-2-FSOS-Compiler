#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_ROOT="$ROOT_DIR/OSes/Stage2/Build/Runqemu"
IR_FILE="$BUILD_ROOT/Kernel.stage2.stage2.ir.json"

info() { printf '[ OK ] [ STAGE2T ] %s\n' "$1"; }
fail() { printf '[FAIL] [ STAGE2T ] %s\n' "$1"; exit 1; }

if [ ! -f "$IR_FILE" ]; then
    info "IR file missing; running Stage 2 compile test first"
    "$SCRIPT_DIR/01-compile-stage2-kernel.sh"
fi

[ -f "$IR_FILE" ] || fail "IR file missing after compile: $IR_FILE"

grep -q '"OpCode": "DeclareLocal"' "$IR_FILE" || fail "DeclareLocal missing from IR"
grep -q '"OpCode": "StoreLocal"' "$IR_FILE" || fail "StoreLocal missing from IR"
grep -q '"OpCode": "LoadLocal"' "$IR_FILE" || fail "LoadLocal missing from IR"
grep -q '"OpCode": "ConstInt32"' "$IR_FILE" || fail "ConstInt32 missing from IR"
grep -q '"OpCode": "AddInt32"' "$IR_FILE" || fail "AddInt32 missing from IR"
grep -q '"OpCode": "CompareLessThanInt32"' "$IR_FILE" || fail "CompareLessThanInt32 missing from IR"
grep -q '"OpCode": "CompareEqualInt32"' "$IR_FILE" || fail "CompareEqualInt32 missing from IR"
grep -q '"OpCode": "JumpIfFalse"' "$IR_FILE" || fail "JumpIfFalse missing from IR"
grep -q '"OpCode": "Jump"' "$IR_FILE" || fail "Jump missing from IR"
grep -q '"OpCode": "Label"' "$IR_FILE" || fail "Label missing from IR"
grep -q '"ControlFlowGraph"' "$IR_FILE" || fail "ControlFlowGraph missing from IR manifest"
grep -q '"Successors"' "$IR_FILE" || fail "CFG successors missing from IR manifest"
grep -q '"ManagedName": "Kernel.WriteBanner"' "$IR_FILE" || fail "Kernel.WriteBanner binding missing from IR manifest"
grep -q '"NativeSymbol": "Kernel_WriteBanner"' "$IR_FILE" || fail "Kernel_WriteBanner native symbol missing from IR manifest"
grep -q '"ManagedName": "Diagnostics.WriteOk"' "$IR_FILE" || fail "Diagnostics.WriteOk call missing from IR manifest"
grep -q '"ManagedName": "Memory.Initialize"' "$IR_FILE" || fail "Memory.Initialize call missing from IR manifest"
grep -q '"ManagedName": "Cpu.HaltForever"' "$IR_FILE" || fail "Cpu.HaltForever call missing from IR manifest"

info "Stage 2 IR output checks passed: $IR_FILE"
