# Stage1 OS Kernel

This folder contains the first visible Oryn-generated operating-system source tree.

## Source files

- `Source/Kernel.cs` is the Stage 1 C# kernel input file.
- `Runqemu.sh` compiles this file by default.

The Stage 1 compiler currently supports a deliberately tiny safe subset:

- `public static void Main()` as the kernel entry point
- `Diagnostics.WriteOk("message")`
- `Diagnostics.WriteWarn("message")`
- `Diagnostics.WriteFail("message")`
- `Memory.Initialize()`
- `Cpu.HaltForever()`

## Build output

`Runqemu.sh` writes backend/build output under:

```text
OSes/Stage1/Build/Runqemu/
```

That output includes the Stage 1 manifest, generated assembly, generated C backend proof, object files, and the linked freestanding ELF kernel.

## Diagnostics output

The compiler writes `Build/Runqemu/Kernel.stage1.diagnostics.log` during `Runqemu.sh`. Runtime `Diagnostics.Write*` calls are emitted to QEMU serial and VGA text output in DEBUG builds.

## 0.1.6 boot output

Stage 1 now builds a GRUB ISO for QEMU instead of asking QEMU to load the ELF64 kernel directly. The important output files are:

```text
OSes/Stage1/Build/Runqemu/OrynKernel.elf
OSes/Stage1/Build/Runqemu/OrynKernel.iso
OSes/Stage1/Build/Runqemu/Kernel.stage1.diagnostics.log
```
