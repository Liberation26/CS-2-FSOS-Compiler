#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
fail(){ echo "[FAIL] $1"; exit 1; }
ok(){ echo "[ OK ] $1"; }

PROGRAM="$ROOT/Applications/OrynVisualConfigurator/Program.cs"
[ -f "$PROGRAM" ] || fail "OrynVisualConfigurator Program.cs missing"

grep -q 'private const string Version = "2.0.2";' "$PROGRAM" || fail "OrynVisualConfigurator version was not bumped to 2.0.2"
grep -q 'Questions are loaded from the current Oryn Questions/\*.question.json files' "$PROGRAM" || fail "Configurator no longer advertises versioned question-file loading"
grep -q 'RenderPage("Oryn Visual Configurator", "<p>Unknown route.</p>", Navigation())' "$PROGRAM" || fail "Unknown-route RenderPage call still has the old missing-argument shape"
if grep -q 'RenderPage("Oryn Visual Configurator", "<p>Unknown route.</p>" + Navigation())' "$PROGRAM"; then
  fail "Old broken unknown-route RenderPage call is still present"
fi

grep -q '2.0.2' "$ROOT/VERSION" || fail "VERSION was not bumped to 2.0.2"
ok "2.0.2 visual configurator compile-shape checks passed."
