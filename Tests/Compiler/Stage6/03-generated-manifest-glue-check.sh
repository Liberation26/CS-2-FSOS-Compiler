#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
GLUE="$ROOT/OSes/Stage6/Build/Runqemu/ModuleManifest.Generated.c"
test -f "$GLUE"
grep -q 'ModuleManifest_InitializeSelected' "$GLUE"
grep -q 'initializing Runtime' "$GLUE"
grep -q 'Runtime_Initialize' "$GLUE"
grep -q 'Memory_Initialize' "$GLUE"
