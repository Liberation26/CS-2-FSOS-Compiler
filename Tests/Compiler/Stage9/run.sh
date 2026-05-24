#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
for TestScript in \
    01-compose-stage9-kernel-template.sh \
    02-compile-composed-stage9-kernel.sh \
    03-invalid-call-fails-before-native-compile.sh \
    04-qemu-stage9-boot.sh; do
    echo "[ OK ] [ TEST      ] Running Stage9/$TestScript"
    bash "$SCRIPT_DIR/$TestScript"
done
echo "[ OK ] [ TEST      ] Stage 9 test suite completed."
