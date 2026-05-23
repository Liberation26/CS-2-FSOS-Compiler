# Stage 2 compiler tests

Stage 2 tests should prove that the Stage 2 source tree builds through the compiler and boots through the freestanding x86_64 path.

Version 0.2.6 adds real Oryn IR. Exercise it with:

```bash
ORYN_SKIP_QEMU=1 ./Runqemu.sh Stage2
```

Expected build artifacts are written under:

```text
OSes/Stage2/Build/Runqemu/
```

Key Stage 2 Phase 3 proof artifact:

```text
OSes/Stage2/Build/Runqemu/Kernel.stage2.stage2.ir.json
```

The IR manifest should include explicit instructions such as `DeclareLocal`, `ConstInt32`, `StoreLocal`, `LoadLocal`, `AddInt32`, `CompareLessThanInt32`, `JumpIfFalse`, `Label`, `Jump`, `CompareEqualInt32`, `ConstString`, `Call`, and `Return`.

The expected compiler structure includes frontend parser, safe-subset validator, semantic model, kernel AST, symbol table, Oryn IR, control-flow graph, type lowering, native x64 backend, and object backend folders under `Source/Core/Oryn.Compiler/`.
