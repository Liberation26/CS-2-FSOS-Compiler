# Stage 7 compiler tests

Stage 7 tests prove dependency-resolved module manifests.

| Test | Purpose |
| --- | --- |
| `01-compile-stage7-kernel.sh` | Builds Stage 7, writes object/IR/glue, and checks build-time graph diagnostics. |
| `02-manifest-graph-output-check.sh` | Checks dependency graph output from the manifest resolver. |
| `03-dependency-order-check.sh` | Checks generated glue initializes modules in resolved dependency order. |
| `04-missing-dependency-rejected.sh` | Temporarily injects a missing dependency and verifies rejection. |
| `05-circular-dependency-rejected.sh` | Temporarily injects a cycle and verifies rejection. |
| `06-qemu-stage7-boot.sh` | Boots Stage 7 in QEMU and checks runtime proof lines. |

Run all Stage 7 tests from the repository root:

```bash
./Tests/Compiler/Stage7/run.sh
```
