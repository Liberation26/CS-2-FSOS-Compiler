#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
PROGRAM="$ROOT/Applications/OrynVisualConfigurator/Program.cs"
ORYN_SH="$ROOT/Oryn.sh"
[ -f "$PROGRAM" ] || { echo "[FAIL] Missing OrynVisualConfigurator Program.cs"; exit 1; }
[ -f "$ORYN_SH" ] || { echo "[FAIL] Missing Oryn.sh"; exit 1; }
grep -q "GuessInitialAnswers" "$PROGRAM" || { echo "[FAIL] Configurator does not guess initial form answers"; exit 1; }
grep -q "id='oryn-config-form'" "$PROGRAM" || { echo "[FAIL] Configurator does not render a single forms-based configuration page"; exit 1; }
grep -q "dataset.userEdited" "$PROGRAM" || { echo "[FAIL] Configurator does not auto-fill OS/kernel identifiers from OS Title"; exit 1; }
grep -q "Do not re-open" "$ORYN_SH" || { echo "[FAIL] Oryn.sh still appears to re-open the configurator for old marker-only projects"; exit 1; }
if grep -q "if not data.get('VisualConfiguratorCompleted', False):" "$ORYN_SH"; then
    echo "[FAIL] Oryn.sh still forces configurator from VisualConfiguratorCompleted marker absence"
    exit 1
fi
echo "[ OK ] 2.0.3 forms-based visual configurator regression checks passed."
