#!/usr/bin/env bash
set -euo pipefail

ORYN_VERSION="1.0.4"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GENERATOR_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Generator/Oryn.Generator.csproj"

ok() { printf '[ OK ] [ ORYN     ] %s\n' "$1"; }
fail() { printf '[FAIL] [ ORYN     ] %s\n' "$1"; exit 1; }

RequireTool() {
    command -v "$1" >/dev/null 2>&1 || fail "Required tool not found: $1"
}

ReadManifestValue() {
    local ManifestPath="$1"
    local Key="$2"
    python3 - "$ManifestPath" "$Key" <<'PY'
import json, sys
with open(sys.argv[1], 'r', encoding='utf-8-sig') as handle:
    data = json.load(handle)
value = data.get(sys.argv[2], '')
if isinstance(value, list):
    print(','.join(str(item) for item in value))
else:
    print(value)
PY
}

Usage() {
    cat <<USAGE
Oryn ${ORYN_VERSION}

Usage:
  ./Oryn.sh generate
  ./Oryn.sh generate --os-name <name> [--kernel-name <name>] [--modules Memory]
  ./Oryn.sh build <OsName>
  ./Oryn.sh run <OsName>
  ./Oryn.sh test <OsName>
  ./Oryn.sh modules

Diagnostics and Panic are always enabled. User-selected modules exclude mandatory kernel modules needed to get the kernel running.
USAGE
}

Command="${1:-help}"
case "$Command" in
    help|--help|-h)
        Usage
        ;;
    modules)
        RequireTool dotnet
        dotnet run --project "$GENERATOR_PROJECT" -- modules
        ;;
    generate)
        shift || true
        RequireTool dotnet
        dotnet run --project "$GENERATOR_PROJECT" -- generate "$@"
        ;;
    build|run|test)
        [ $# -ge 2 ] || fail "Missing OS name. Example: ./Oryn.sh $Command MyOrynOS"
        OsName="$2"
        ManifestPath="$PROJECT_ROOT/OSes/$OsName/manifest.json"
        [ -f "$ManifestPath" ] || fail "Generated OS manifest not found: $ManifestPath"
        RequireTool python3
        UserModules="$(ReadManifestValue "$ManifestPath" UserSelectedModules)"
        KernelName="$(ReadManifestValue "$ManifestPath" KernelName)"
        export ORYN_OS_NAME="$OsName"
        export ORYN_KERNEL_NAME="$KernelName"
        export ORYN_COMPOSE_MODULES="$UserModules"
        export ORYN_QEMU_HEADLESS="${ORYN_QEMU_HEADLESS:-1}"
        if [ "$Command" = "build" ]; then
            export ORYN_SKIP_QEMU=1
        fi
        ok "Running generated OS workflow for $OsName"
        "$PROJECT_ROOT/Runqemu.sh" "$OsName"
        ;;
    *)
        fail "Unknown command: $Command"
        ;;
esac
