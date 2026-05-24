# Stage 2 compiler tests

Stage 2 tests should prove that the Stage 2 source tree builds through the compiler and boots through the freestanding x86_64 path.

Version 0.2.7 adds real Oryn IR plus a readable control-flow graph. Exercise it with:

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

The IR manifest should include explicit instructions such as `DeclareLocal`, `ConstInt32`, `StoreLocal`, `LoadLocal`, `AddInt32`, `CompareLessThanInt32`, `JumpIfFalse`, `Label`, `Jump`, `CompareEqualInt32`, `ConstString`, `Call`, and `Return`. It should also include `ControlFlowGraph` with named basic blocks and successor edges.

The expected compiler structure includes frontend parser, safe-subset validator, semantic model, kernel AST, symbol table, Oryn IR, control-flow graph, type lowering, native x64 backend, and object backend folders under `Source/Core/Oryn.Compiler/`.

Version 0.2.12 adds the Stage 2 string literal table proof. The generated assembly should contain `.section .rodata`, `.LstrN` labels, `.asciz` directives for diagnostics messages, RIP-relative `lea .LstrN(%rip), %rdi` argument loads, and calls such as `call Diagnostics_WriteOk`.


Version 0.2.13 adds the Stage 2 static helper method proof. The generated assembly should contain `Kernel_Main:`, `Kernel_WriteBanner:`, and `call Kernel_WriteBanner`, while the manifest should contain the helper method symbol metadata.
