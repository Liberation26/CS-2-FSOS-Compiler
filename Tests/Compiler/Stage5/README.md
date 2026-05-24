# Stage 5 Compiler Tests

Stage 5 tests prove the runtime contract is wired end to end.

Tests:

1. `01-compile-stage5-kernel.sh` checks that the Stage 5 kernel compiles and emits IR, diagnostics, assembly, and direct ELF64 object output.
2. `02-runtime-and-panic-bindings-check.sh` checks that Runtime and Panic binding metadata exists and is approved.
3. `03-qemu-stage5-boot.sh` boots the Stage 5 kernel and checks the expected serial diagnostics. QEMU timeout after the halt loop is success.
