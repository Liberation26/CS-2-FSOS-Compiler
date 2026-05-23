# Stage 2 compiler tests

Stage 2 tests should prove that the Stage 2 source tree builds through the compiler and boots through the freestanding x86_64 path.

Version 0.2.3 keeps the Stage 2 boot proof and adds compiler-structure separation. Exercise it with:

```bash
ORYN_SKIP_QEMU=1 ./Runqemu.sh Stage2
```

Expected build artifacts are written under:

```text
OSes/Stage2/Build/Runqemu/
```

The next Stage 2 test scripts should be added as the compiler grows beyond call-only lowering:

```text
01-compile-stage2-kernel.sh
02-ir-output-check.sh
03-assembly-output-check.sh
04-qemu-stage2-boot.sh
```


The expected compiler structure now includes frontend parser, safe-subset validator, semantic model, kernel AST, symbol table, Oryn IR, control-flow graph, type lowering, native x64 backend, and object backend folders under `Source/Core/Oryn.Compiler/`.
