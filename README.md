# Oryn

Oryn is a C# to freestanding operating-system compiler project.

The goal is to let developers write an Oryn-safe subset of C# and compile it into native freestanding output that can be linked into a bootable kernel.

Oryn is not a general .NET runtime, and it is not intended to compile arbitrary C# applications. Oryn compiles a controlled kernel-safe C# subset into native code for operating-system development.

## Version

Current version: `0.2.0`

## Core idea

The intended compiler flow is:

```text
Oryn-safe C# source
    ↓
Roslyn parse and semantic analysis
    ↓
Oryn safe-subset validation
    ↓
Oryn kernel/module model
    ↓
Oryn IR
    ↓
Oryn native code generator
    ↓
ELF64 relocatable object files
    ↓
freestanding linker
    ↓
bootable kernel image
    ↓
QEMU or hardware test
```

## Stage 1 language target

Stage 1 supports kernel-safe C# without runtime-heavy features.

Supported:

```text
static classes
static methods
structs
enums
constants
if / else
switch
while / for
basic arithmetic
method calls
simple fields
module API calls
```

Not supported yet:

```text
new object()
GC allocation
exceptions
reflection
async
LINQ
dynamic
arbitrary generics
delegates
normal .NET libraries
```

Example target source:

```csharp
using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Memory;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Kernel entered");
        Memory.Initialize();
        Cpu.HaltForever();
    }
}
```

## SDK model

End-user code should use Oryn SDK namespaces such as:

```csharp
using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Cpu;
using Oryn.Kernel.Memory;
```

These are Oryn-approved compile-time API assemblies. They exist so Roslyn and editors can provide type checking, autocomplete, and documentation.

They are not runtime .NET DLLs loaded by the kernel.

Each module has three parts:

```text
1. API assembly
   Used by Roslyn and the user's editor.

2. Binding metadata
   Used by Oryn.Compiler to lower approved C# calls to native symbols.

3. Native implementation
   Linked into the generated freestanding kernel.
```

For example:

```text
Diagnostics.WriteOk(string)
    ↓
Diagnostics_WriteOk
```

## Initial filesystem

The repository is arranged around a native compiler pipeline:

```text
Source/Core/Oryn.Compiler/
  Frontend/
    CSharpParser/
    SemanticModel/
    SafeSubsetValidator/

  KernelModel/
    KernelAst/
    ModuleAst/
    SymbolTable/
    CapabilityTable/

  IR/
    OrynIr/
    ControlFlowGraph/
    TypeLowering/

  Backends/
    Native/
      X64/
      Object/

  Runtime/
    ObjectModel/
    StringModel/
    ArrayModel/
    Panic/
    Startup/

  Tests/
```

SDK and module layout:

```text
Source/Sdk/Apis/
  Oryn.Kernel.Diagnostics/
  Oryn.Kernel.Cpu/
  Oryn.Kernel.Memory/

Source/Sdk/Bindings/
  Diagnostics.binding.json
  Cpu.binding.json
  Memory.binding.json

Source/Native/Modules/
  Diagnostics/
  Cpu/
  Memory/

Source/Native/Runtime/
```

## Stage 1 compiler and backend

Version `0.1.0` provides the first working compiler proof. The `compile` command now:

```text
1. Reads an Oryn-safe C# kernel source file.
2. Validates that unsupported runtime-heavy constructs are not present.
3. Finds `public static void Main()`.
4. Lowers approved SDK calls such as `Diagnostics.WriteOk`, `Memory.Initialize`, and `Cpu.HaltForever` into Oryn IR records.
5. Emits a backend manifest, a freestanding C backend snippet, and an x64 assembly backend sketch.
```

Example:

```bash
cd Source/Core/Oryn.Compiler
dotnet run -- compile Tests/Stage0/Kernel.stage0.cs --target x64-elf --output ../../../../Build/Kernel.o
```

This writes:

```text
Build/Kernel.stage1.json
Build/Kernel.generated.c
Build/Kernel.generated.S
Build/Kernel.o
```

`Kernel.o` is intentionally a text placeholder in Stage 1. The real ELF64 relocatable writer is the next backend milestone. The generated `.c` and `.S` files are the first backend proof outputs.


## Stage 2 part 1

Version `0.2.0` starts the Stage 2 line. Stage 2 part 1 adds a separate Stage 2 OS source tree and lets the root run script select which stage to build.

```bash
./Runqemu.sh Stage2
./Runqemu.sh Stage1
```

`Stage2` is now the default when no argument is supplied. Stage 2 part 1 intentionally keeps the known-good Stage 1 native call backend so the new stage starts from a bootable baseline before locals, arithmetic, branches, loops, and helper methods are added.

A C# literal such as `0` may be represented as `ConstInt32 0` in the IR because C# `int` is a 32-bit language type. The backend can still choose a compact x64 encoding for the value.

## RunQEMU handoff

Version `0.1.1` adds `Runqemu.sh` at the repository root. After `update.sh` copies `ChangedFiles/`, commits, and attempts to push, it now launches:

```bash
./Runqemu.sh
```

`Runqemu.sh` performs the first end-to-end freestanding backend proof:

```text
1. Runs Oryn.Compiler against the Stage 1 test kernel source.
2. Produces the Stage 1 backend assembly.
3. Generates a small freestanding x86_64 boot harness.
4. Builds native Diagnostics, Cpu, and Memory module objects.
5. Links an ELF64 freestanding kernel image.
6. Runs that kernel in qemu-system-x86_64.
```

The generated kernel intentionally halts forever after `Cpu.HaltForever()`, so `Runqemu.sh` treats the configured timeout as a successful proof that the kernel remained running. Set `ORYN_SKIP_QEMU=1` to build the freestanding kernel without launching QEMU.

## Native backend target

The first native backend target is:

```text
Architecture: x86_64
Mode: Long Mode
Object format: ELF64 relocatable object
Kernel mode: freestanding
```

The first full native object writer should generate an object with:

```text
.text
.rodata
.symtab
.strtab
.rela.text
```

and native symbols such as:

```text
Kernel_Main
Diagnostics_WriteOk
Memory_Initialize
Cpu_HaltForever
```


## Stage 1 limitations

Stage 1 does not yet compile arbitrary C#. It intentionally accepts a narrow proof subset: `public static void Main()` containing approved static module API calls. It does not yet lower variables, branches, loops, structs, fields, arithmetic, or user-defined methods. Those belong to later compiler stages.

## Update workflow

Run `./update.sh` from anywhere after downloading an `Oryn-*.zip` archive. The updater extracts the latest archive from `~/Downloads` into a temporary directory, copies `ChangedFiles/` into `~/Dev/OrynFoundry`, stages the result, commits it, and then attempts to push to GitHub.

The updater does not expect `ChangedFiles/` to already exist inside `~/Dev/OrynFoundry`.


## Update behaviour

`update.sh` extracts the selected `Oryn-*.zip` archive into `/tmp`, finds `ChangedFiles/`, copies those files into `~/Dev/OrynFoundry`, initialises Git there when the folder exists but has no `.git/`, sets the origin remote to `https://github.com/Liberation26/CS-2-FSOS-Compiler.git`, commits the copied files, and then attempts to push.

Existing non-Git target folders are no longer treated as fatal errors.


## Stage 1 limitations

Stage 1 does not yet compile arbitrary C#. It intentionally accepts a narrow proof subset: `public static void Main()` containing approved static module API calls. It does not yet lower variables, branches, loops, structs, fields, arithmetic, or user-defined methods. Those belong to later compiler stages.

## Update workflow 0.0.4

`update.sh` prints its own version at startup, extracts the selected `Oryn-*.zip` archive into `/tmp`, copies `ChangedFiles/` into `~/Dev/OrynFoundry`, initialises Git in place when needed, commits the update, and attempts to push to the configured GitHub remote.

### 0.0.4 updater behaviour

`update.sh` selects the highest versioned `Oryn-x.y.z.zip` from `~/Downloads` when no path is supplied and self-resets from the selected archive if that archive contains a different updater.

## Stage 1 limitations

Stage 1 does not yet compile arbitrary C#. It intentionally accepts a narrow proof subset: `public static void Main()` containing approved static module API calls. It does not yet lower variables, branches, loops, structs, fields, arithmetic, or user-defined methods. Those belong to later compiler stages.

## Update workflow 0.0.5

`update.sh` now uses the GitHub remote `https://github.com/Liberation26/CS-2-FSOS-Compiler.git`.

The updater continues to display its own version number, choose the highest semantic `Oryn-x.y.z.zip` from `~/Downloads`, extract to `/tmp`, reset itself when a different archive updater is present, copy `ChangedFiles/` into `~/Dev/OrynFoundry`, commit the copied files, and attempt to push.

## Version 0.1.2 build fix

`Source/Core/Oryn.Compiler/Tests/**/*.cs` files are compiler input samples, not part of the .NET compiler application. The compiler project excludes those files from normal SDK compilation so `Runqemu.sh` can pass them to `Oryn.Compiler compile` without `.NET` trying to resolve `Oryn.Kernel.*` namespaces as project references.

## Version 0.1.3 QEMU launch fix

`Runqemu.sh` now starts QEMU in headed mode by default so the freestanding kernel run has a visible QEMU window. Set `ORYN_QEMU_DISPLAY=headless` or `ORYN_QEMU_HEADLESS=1` to restore the previous non-graphical mode.

The QEMU launch remains bounded by `ORYN_QEMU_TIMEOUT` because the generated proof kernel intentionally reaches `Cpu.HaltForever()`. A timeout is therefore treated as a successful boot-and-halt proof, not as a failed run.


## Stage 1 OS source location

As of `0.1.4`, the visible Stage 1 kernel source is written under:

```text
OSes/Stage1/Source/Kernel.cs
```

`Runqemu.sh` uses that file by default, emits backend/build output under `OSes/Stage1/Build/Runqemu/`, links the freestanding kernel, and then runs it in QEMU.

The compiler test file under `Source/Core/Oryn.Compiler/Tests/Stage0/Kernel.stage0.cs` is only a compiler test fixture.

## Version 0.1.6 compiler and runtime diagnostics

`Oryn.Compiler compile` now writes an explicit compiler diagnostics log next to the Stage 1 backend outputs:

```text
OSes/Stage1/Build/Runqemu/Kernel.stage1.diagnostics.log
```

The log records each lowered call and states which `Diagnostics.Write*` calls will become runtime kernel diagnostics. The native Diagnostics module now writes DEBUG builds to both QEMU serial and VGA text memory, so the generated Stage 1 kernel should visibly emit lines such as:

```text
[ OK ] [ KERNEL   ] Stage1 kernel entered
[ OK ] [ KERNEL   ] Stage1 memory module initialized
[ OK ] [ KERNEL   ] Stage1 kernel is halting forever
```

`Runqemu.sh` fails early if the compiler diagnostics log is not produced.

## Version 0.1.6 x86_64 QEMU boot path

`Runqemu.sh` now boots the generated x86_64 freestanding kernel through a GRUB ISO.

The generated kernel remains an ELF64 image:

```text
OSes/Stage1/Build/Runqemu/OrynKernel.elf
```

The script now creates and boots:

```text
OSes/Stage1/Build/Runqemu/OrynKernel.iso
```

This avoids QEMU's direct `-kernel` ELF loader rejecting the 64-bit image.

Required extra host tool for the ISO path:

```text
grub-mkrescue
```

`xorriso` may also be required by the host `grub-mkrescue` installation.

## Version 0.2.0 Stage 2 part 1

`Runqemu.sh` now accepts a stage selector:

```bash
./Runqemu.sh Stage1
./Runqemu.sh Stage2
```

When no stage is supplied, the script defaults to `Stage2`.

Stage 2 part 1 adds:

```text
OSes/Stage2/Source/Kernel.cs
OSes/Stage2/README.md
Tests/Compiler/Stage2/README.md
```

This first Stage 2 delivery deliberately keeps the known-good Stage 1 native call backend while establishing the Stage 2 source tree and build path. The next Stage 2 compiler work is to add real IR records for locals/constants, then assignments, arithmetic, branches, loops, and helper methods.
