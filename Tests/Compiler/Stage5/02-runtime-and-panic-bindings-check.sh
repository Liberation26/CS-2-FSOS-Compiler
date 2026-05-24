#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

RUNTIME_BINDING="$PROJECT_ROOT/Source/Sdk/Bindings/Runtime.binding.json"
PANIC_BINDING="$PROJECT_ROOT/Source/Sdk/Bindings/Panic.binding.json"

test -f "$RUNTIME_BINDING"
test -f "$PANIC_BINDING"
grep -q '"managedName": "Runtime.Initialize"' "$RUNTIME_BINDING"
grep -q '"managedName": "Runtime.MarkKernelReady"' "$RUNTIME_BINDING"
grep -q '"managedName": "Panic.Halt"' "$PANIC_BINDING"
grep -q '"allowedInKernel": true' "$RUNTIME_BINDING"
grep -q '"allowedInKernel": true' "$PANIC_BINDING"
test -f "$PROJECT_ROOT/Source/Native/Modules/Runtime/Runtime.Native.c"
test -f "$PROJECT_ROOT/Source/Native/Modules/Panic/Panic.Native.c"

printf '[ OK ] [ TEST      ] Stage 5 Runtime and Panic bindings verified.
'
