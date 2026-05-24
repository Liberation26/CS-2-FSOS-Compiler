#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for Test in "$DIR"/[0-9][0-9]-*.sh; do
  echo "[ OK ] [ TEST      ] Running $(basename "$Test")"
  "$Test"
done
echo "[ OK ] [ TEST      ] 1.0.4 generator tests completed."
