#!/usr/bin/env bash
set -euo pipefail

ORYN_VERSION="1.0.8"
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
  ./Oryn.sh generate --os-name <name> [--kernel-name <name>] [--modules None|Memory] [--vm-display-mode Headless|Visual]
  ./Oryn.sh build <OsName>
  ./Oryn.sh run <OsName>
  ./Oryn.sh test <OsName>
  ./Oryn.sh modules

Diagnostics and Panic are always enabled. User-selected modules exclude mandatory kernel modules needed to get the kernel running.

Question answer guide:
  OS name: any friendly OS name, for example My Oryn OS. Spaces are allowed.
  Kernel name: letters, numbers, and underscores, for example MyOrynOSKernel.
  Target architecture: x64-elf.
  Virtual machine profile: RunQemu.
  VM display mode: Headless or Visual. Headless closes automatically after the proof timeout. Visual stays open until you close the VM window.
  Additional modules: None or Memory. Use None for no optional modules.
  Build mode: Debug.
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
        VmDisplayMode="$(ReadManifestValue "$ManifestPath" VmDisplayMode)"
        case "$VmDisplayMode" in
            Visual|visual|Visible|visible|Headed|headed|Gui|gui)
                DefaultQemuDisplay="visual"
                DefaultQemuHeadless="0"
                ;;
            Headless|headless|None|none|Off|off|"")
                DefaultQemuDisplay="headless"
                DefaultQemuHeadless="1"
                ;;
            *)
                fail "Unsupported VM display mode in manifest: $VmDisplayMode. Use Headless or Visual."
                ;;
        esac
        export ORYN_OS_NAME="$OsName"
        export ORYN_KERNEL_NAME="$KernelName"
        export ORYN_COMPOSE_MODULES="$UserModules"
        export ORYN_QEMU_DISPLAY="${ORYN_QEMU_DISPLAY:-$DefaultQemuDisplay}"
        export ORYN_QEMU_HEADLESS="${ORYN_QEMU_HEADLESS:-$DefaultQemuHeadless}"
        if [ "$Command" = "build" ]; then
            export ORYN_SKIP_QEMU=1
        fi
        ok "Running generated OS workflow for $OsName"
        ok "VM display mode: ${ORYN_QEMU_DISPLAY}"
        "$PROJECT_ROOT/Runqemu.sh" "$OsName"
        ;;
    *)
        fail "Unknown command: $Command"
        ;;
esac
