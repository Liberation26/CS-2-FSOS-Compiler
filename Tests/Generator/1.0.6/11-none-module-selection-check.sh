#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
PROGRAM="$ROOT/Source/Core/Oryn.Generator/Program.cs"
QUESTION="$ROOT/Questions/005-modules.question.json"
RUNQEMU="$ROOT/Runqemu.sh"

grep -q 'DefaultUserSelectedModules = Array.Empty<string>()' "$PROGRAM"
grep -q '"Choices": \["None", "Memory"\]' "$QUESTION"
grep -q 'None cannot be combined with other user-selected modules' "$PROGRAM"
grep -q -- '--mandatory-modules "$COMPOSE_MANDATORY_MODULES" --modules "${ORYN_COMPOSE_MODULES:-}"' "$RUNQEMU"

echo "[ OK ] 1.0.6 None additional-module selection is explicit and preserves mandatory-only composition."
