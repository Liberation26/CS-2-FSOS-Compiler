#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for Test in "$DIR"/[0-9][0-9]-*.sh; do
    printf '[ OK ] [ TEST      ] Running %s
' "$(basename "$Test")"
    bash "$Test"
    printf '[ OK ] [ TEST      ] Passed %s
' "$(basename "$Test")"
done
