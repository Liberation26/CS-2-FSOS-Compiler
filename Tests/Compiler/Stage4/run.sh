#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/01-approved-module-calls-compile.sh"
"$SCRIPT_DIR/02-forbidden-module-call-fails.sh"
"$SCRIPT_DIR/03-forbidden-namespace-fails.sh"
