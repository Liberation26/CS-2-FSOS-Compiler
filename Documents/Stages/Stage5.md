# Stage 5: Runtime Contract

Stage 5 makes the approved module boundary useful as a kernel runtime contract.

## What Stage 5 adds

- `Oryn.Kernel.Runtime.Runtime.Initialize()`
- `Oryn.Kernel.Runtime.Runtime.MarkKernelReady()`
- `Oryn.Kernel.Panic.Panic.Halt(string Reason)`
- native Runtime and Panic modules linked into the freestanding kernel
- a Stage 5 boot kernel under `OSes/Stage5/`
- Stage 5 compiler and QEMU tests under `Tests/Compiler/Stage5/`

## Why this matters

Stage 4 proved that safe C# can only call approved module APIs. Stage 5 proves that those approved APIs can form a small runtime contract for generated kernels.

The unsafe details remain behind native module implementations. User-facing kernel code stays in the Oryn-safe C# subset.

## Stage 5 pipeline

```text
OSes/Stage5/Source/Kernel.cs
  -> safe-subset validation
  -> approved module binding validation
  -> semantic binding
  -> Oryn IR
  -> CFG proof
  -> direct ELF64 relocatable object
  -> linked freestanding kernel ELF
  -> GRUB ISO
  -> QEMU serial proof
```

## Expected result

The Stage 5 kernel reaches the runtime-ready proof line and then halts forever. The QEMU timeout is treated as success because the freestanding kernel remains alive in its halt loop.
