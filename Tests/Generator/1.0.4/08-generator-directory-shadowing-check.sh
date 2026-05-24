#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
PROGRAM="$ROOT/Source/Core/Oryn.Generator/Program.cs"

if grep -q 'DirectoryInfo? Directory =' "$PROGRAM"; then
    echo "[FAIL] Program.cs reintroduced Directory local variable shadowing." >&2
    exit 1
fi

if ! grep -q 'System.IO.Directory.GetCurrentDirectory()' "$PROGRAM"; then
    echo "[FAIL] Program.cs does not use explicit System.IO.Directory.GetCurrentDirectory()." >&2
    exit 1
fi

if ! grep -q 'System.IO.Directory.Exists' "$PROGRAM"; then
    echo "[FAIL] Program.cs does not use explicit System.IO.Directory.Exists()." >&2
    exit 1
fi

echo "[ OK ] 1.0.5 generator project-root lookup avoids Directory shadowing."
