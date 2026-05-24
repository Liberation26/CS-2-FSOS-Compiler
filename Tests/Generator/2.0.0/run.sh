#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
fail() { printf '[FAIL] [ TEST     ] %s\n' "$1"; exit 1; }
ok() { printf '[ OK ] [ TEST     ] %s\n' "$1"; }

[ -f "$ROOT/Applications/OrynVisualConfigurator/OrynVisualConfigurator.csproj" ] || fail "OrynVisualConfigurator project missing."
[ -f "$ROOT/Applications/OrynVisualConfigurator/Program.cs" ] || fail "OrynVisualConfigurator Program.cs missing."
[ -f "$ROOT/Questions/001-os-title.question.json" ] || fail "OS Title question missing."
[ -f "$ROOT/Questions/002-os-name.question.json" ] || fail "strict OS Name question missing."
if grep -q 'Spaces are allowed' "$ROOT/Questions/002-os-name.question.json"; then
    fail "OS Name question must not allow spaces."
fi
if ! grep -q 'Questions/\*.question.json' "$ROOT/README.md"; then
    fail "README does not document question JSON source of truth."
fi
if ! grep -q 'OrynVisualConfigurator' "$ROOT/Oryn.sh"; then
    fail "Oryn.sh does not reference OrynVisualConfigurator."
fi
bash -n "$ROOT/Oryn.sh"
bash -n "$ROOT/Runqemu.sh"
bash -n "$ROOT/update.sh"
ok "2.0.2 visual configurator packaging checks passed."
