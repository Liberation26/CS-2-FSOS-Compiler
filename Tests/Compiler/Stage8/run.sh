#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for TestScript in \
    01-compile-stage8-kernel.sh \
    02-api-contract-output-check.sh \
    03-contract-files-check.sh \
    04-uncontracted-call-fails.sh \
    05-qemu-stage8-boot.sh; do
    echo "[ OK ] [ TEST      ] Running Stage8/$TestScript"
    bash "$SCRIPT_DIR/$TestScript"
done
echo "[ OK ] [ TEST      ] Stage 8 test suite completed."
