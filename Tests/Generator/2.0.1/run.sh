#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
ok() { printf '[ OK ] [ TEST      ] %s\n' "$1"; }
fail() { printf '[FAIL] [ TEST      ] %s\n' "$1"; exit 1; }
PROGRAM="$ROOT/Applications/OrynVisualConfigurator/Program.cs"
[ -f "$PROGRAM" ] || fail "OrynVisualConfigurator Program.cs missing"
grep -q 'HttpListener' "$PROGRAM" || fail "browser visual configurator server not found"
grep -q 'RenderQuestionField' "$PROGRAM" || fail "question renderer not found"
grep -q 'out string? ModeValue' "$PROGRAM" || fail "nullable-safe TryGetValue pattern for mode not found"
if grep -q 'out string Mode' "$PROGRAM"; then
  fail "nullable warning-prone out string Mode pattern still present"
fi
grep -q 'ORYN_VISUALCFG_TERMINAL' "$PROGRAM" || fail "terminal fallback switch missing"
grep -q '2.0.1' "$ROOT/VERSION" || fail "VERSION was not bumped to 2.0.1"
ok "2.0.1 visual configurator checks passed."
