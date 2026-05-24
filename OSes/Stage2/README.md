# Oryn Stage 2 OS

Stage 2 is the compiler-backed freestanding kernel proof.

The source kernel lives at:

```text
OSes/Stage2/Source/Kernel.cs
```

Run it with:

```bash
./Runqemu.sh Stage2
```

For a compile-only proof without launching QEMU:

```bash
ORYN_SKIP_QEMU=1 ./Runqemu.sh Stage2
```

## Stage 2 Phase 3 in version 0.2.7

Stage 2 now has real Oryn IR. The compiler no longer treats the kernel body as a flat list of native calls. It parses a small safe C# subset into statement/expression AST nodes, binds locals and approved module calls, and lowers the result into an explicit stack-style IR stream.

The Stage 2 sample kernel now exercises:

- diagnostics calls,
- memory initialization,
- an `int Counter` local,
- a `while` loop,
- Int32 addition,
- Int32 less-than and equality comparisons,
- an `if` / `else` branch,
- and a final CPU halt call.

The IR manifest is written as:

```text
OSes/Stage2/Build/Runqemu/Kernel.stage2.stage2.ir.json
```

The generated backend files are:

```text
OSes/Stage2/Build/Runqemu/Kernel.stage2.generated.S
OSes/Stage2/Build/Runqemu/Kernel.stage2.generated.c
OSes/Stage2/Build/Runqemu/Kernel.stage2.diagnostics.log
```


Version 0.2.7 proves Stage 2 control-flow graph generation by compiling a loop and branch into explicit labels, jumps, basic blocks, and successor edges.

## Stage 2 Phase 5 backend proof

The Stage 2 kernel is compiled through the real x64 backend. Oryn.Compiler lowers `Source/Kernel.cs` into Oryn IR, emits `Kernel.stage2.generated.S`, and `Runqemu.sh` assembles that file with clang before linking the final freestanding ELF64 kernel.

The generated C file is still produced for readability only; the linked Stage 2 kernel body comes from the generated assembly.


## Stage 2 Phase 6 stack/local proof

Stage 2 now emits a simple x64 stack-frame model for generated kernels. Integer locals are assigned 64-bit rbp-relative slots, starting at `-8(%rbp)`, and generated methods use `push %rbp`, `mov %rsp, %rbp`, frame reservation, `leave`, and `ret`.
