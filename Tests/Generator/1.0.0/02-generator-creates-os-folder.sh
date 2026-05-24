#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
OS_NAME="GeneratedTestOS"
rm -rf "$ROOT/OSes/$OS_NAME"
(cd "$ROOT" && dotnet run --project Source/Core/Oryn.Generator -- generate --os-name "$OS_NAME" --kernel-name GeneratedTestKernel --modules Memory)
[ -d "$ROOT/OSes/$OS_NAME" ] || { echo "[FAIL] OS folder was not created."; exit 1; }
[ -f "$ROOT/OSes/$OS_NAME/Answers/$OS_NAME.answers.json" ] || { echo "[FAIL] Answers file was not created."; exit 1; }
[ -f "$ROOT/OSes/$OS_NAME/manifest.json" ] || { echo "[FAIL] Manifest file was not created."; exit 1; }
echo "[ OK ] 1.0.0 generator creates OS folder, answers, and manifest."
