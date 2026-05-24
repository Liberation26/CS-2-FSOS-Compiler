#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
for File in \
  "$ROOT/Questions/001-os-name.question.json" \
  "$ROOT/Questions/002-kernel-name.question.json" \
  "$ROOT/Questions/003-target-profile.question.json" \
  "$ROOT/Questions/004-vm-profile.question.json" \
  "$ROOT/Questions/005-modules.question.json" \
  "$ROOT/Questions/006-build-mode.question.json"; do
  [ -f "$File" ] || { echo "[FAIL] Missing question file: $File"; exit 1; }
done
echo "[ OK ] 1.0.5 questionnaire JSON files exist."
