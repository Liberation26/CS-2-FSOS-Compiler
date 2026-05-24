#!/usr/bin/env bash
set -euo pipefail

ORYN_VERSION="2.0.2"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GENERATOR_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Generator/Oryn.Generator.csproj"
CONFIGURATOR_PROJECT="$PROJECT_ROOT/Applications/OrynVisualConfigurator/OrynVisualConfigurator.csproj"

ok() { printf '[ OK ] [ ORYN     ] %s\n' "$1"; }
warn() { printf '[WARN] [ ORYN     ] %s\n' "$1"; }
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

ResolveOsPath() {
    local MaybePath="${1:-}"
    if [ -n "$MaybePath" ]; then
        if [ -d "$PROJECT_ROOT/OSes/$MaybePath" ]; then
            (cd "$PROJECT_ROOT/OSes/$MaybePath" && pwd)
            return 0
        fi
        if [ -d "$PROJECT_ROOT/$MaybePath" ]; then
            (cd "$PROJECT_ROOT/$MaybePath" && pwd)
            return 0
        fi
        if [ -d "$MaybePath" ]; then
            (cd "$MaybePath" && pwd)
            return 0
        fi
        fail "Could not resolve OS path or name: $MaybePath"
    fi

    local Current="$PWD"
    while [ "$Current" != "/" ]; do
        if [ -f "$Current/manifest.json" ] && [ -d "$Current/Answers" ]; then
            printf '%s\n' "$Current"
            return 0
        fi
        Current="$(dirname "$Current")"
    done

    fail "No OS name/path supplied and current directory is not inside an Oryn OS project."
}

NeedsConfigurator() {
    local OsPath="$1"
    python3 - "$PROJECT_ROOT" "$OsPath" <<'PY'
import glob, json, os, sys
root, os_path = sys.argv[1], sys.argv[2]
manifest_path = os.path.join(os_path, 'manifest.json')
answers_dir = os.path.join(os_path, 'Answers')
answer_files = sorted(glob.glob(os.path.join(answers_dir, '*.answers.json')), key=os.path.getmtime, reverse=True)
config_path = answer_files[0] if answer_files else manifest_path
try:
    with open(config_path, 'r', encoding='utf-8-sig') as handle:
        data = json.load(handle)
except Exception:
    print('yes')
    sys.exit(0)
for question_path in sorted(glob.glob(os.path.join(root, 'Questions', '*.question.json'))):
    with open(question_path, 'r', encoding='utf-8-sig') as handle:
        question = json.load(handle)
    if question.get('Required', False):
        key = question.get('AnswerKey', '')
        if key not in data or data.get(key) in (None, ''):
            print('yes')
            sys.exit(0)
if not data.get('VisualConfiguratorCompleted', False):
    print('yes')
else:
    print('no')
PY
}

Usage() {
    cat <<USAGE
Oryn ${ORYN_VERSION}

Usage:
  ./Oryn.sh new
  ./Oryn.sh configure [<OsName|OS path>]
  ./Oryn.sh configure --search
  ./Oryn.sh configure --load
  ./Oryn.sh generate --terminal [generator options]
  ./Oryn.sh build [<OsName|OS path>]
  ./Oryn.sh run [<OsName|OS path>]
  ./Oryn.sh test [<OsName|OS path>]
  ./Oryn.sh modules
  ./Oryn.sh sdk

Oryn 2.0.2 is visual-first. New OS configuration is handled by Applications/OrynVisualConfigurator.
The visual configurator reads the current version's Questions/*.question.json files, shows all questions, and renders known choices as browser dropdowns or check boxes.

Path handling:
  ./Oryn.sh configure          Detects the OS project from the current directory.
  ./Oryn.sh configure DES      Opens OSes/DES.
  ./Oryn.sh configure OSes/DES Opens that relative path.
  ./Oryn.sh configure --search Searches OSes/ for projects.
  ./Oryn.sh configure --load   Prompts for a directory to load.

Project rule:
  OS Title may contain spaces and is human-facing.
  OS Name and Kernel Name must contain only letters and numbers, start with a letter, and must not contain spaces.

Build/run regenerate from saved answers. The configurator is automatically run only when a project has not been visually configured or when new required questions are missing.
USAGE
}

RunConfigurator() {
    RequireTool dotnet
    dotnet run --project "$CONFIGURATOR_PROJECT" -- "$@"
}

Command="${1:-help}"
case "$Command" in
    help|--help|-h)
        Usage
        ;;
    sdk)
        ok "Host-side .NET SDK: $PROJECT_ROOT/Source/Core/Oryn.Sdk"
        ok "Freestanding .Oryn SDK: $PROJECT_ROOT/Source/Sdk/Oryn"
        ok "Visual configurator: $PROJECT_ROOT/Applications/OrynVisualConfigurator"
        ok "Question schema files: $PROJECT_ROOT/Questions"
        ;;
    modules)
        RequireTool dotnet
        dotnet run --project "$GENERATOR_PROJECT" -- modules
        ;;
    generate)
        shift || true
        if [ "${1:-}" = "--terminal" ]; then
            shift || true
            RequireTool dotnet
            dotnet run --project "$GENERATOR_PROJECT" -- generate "$@"
        else
            RunConfigurator new "$@"
        fi
        ;;
    new|create|create-os|generate-all)
        shift || true
        RunConfigurator new "$@"
        GeneratedOsName="$(find "$PROJECT_ROOT/OSes" -mindepth 1 -maxdepth 1 -type d -printf '%T@ %f\n' 2>/dev/null | sort -nr | awk 'NR==1{print $2}')"
        [ -n "$GeneratedOsName" ] || fail "Could not determine generated OS folder."
        ok "Single-command visual SDK generation created OS: $GeneratedOsName"
        ok "Building and running generated freestanding OS from current downloaded SDK/source tree."
        "$0" run "$GeneratedOsName"
        ;;
    configure|visual-configure)
        shift || true
        case "${1:-}" in
            --search|search)
                RunConfigurator search
                ;;
            --load|load)
                RunConfigurator load
                ;;
            "")
                RunConfigurator configure
                ;;
            *)
                RunConfigurator configure "$1"
                ;;
        esac
        ;;
    build|run|test)
        shift || true
        OsPath="$(ResolveOsPath "${1:-}")"
        ManifestPath="$OsPath/manifest.json"
        [ -f "$ManifestPath" ] || fail "Generated OS manifest not found: $ManifestPath"
        RequireTool python3
        if [ "$(NeedsConfigurator "$OsPath")" = "yes" ]; then
            warn "This OS project has not completed visual configuration or has missing required questions."
            RunConfigurator configure "$OsPath"
        fi
        OsName="$(ReadManifestValue "$ManifestPath" OsName)"
        [ -n "$OsName" ] || OsName="$(basename "$OsPath")"
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
