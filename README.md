# Oryn

Oryn is a C# to freestanding operating-system compiler project.

The goal is to let developers write an Oryn-safe subset of C# and compile it into native freestanding output that can be linked into a bootable kernel.

Oryn is not a general .NET runtime, and it is not intended to compile arbitrary C# applications. Oryn compiles a controlled kernel-safe C# subset into native code for operating-system development.

## Version

Current version: `0.0.4`

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

## Native backend target

The first native backend target is:

```text
Architecture: x86_64
Mode: Long Mode
Object format: ELF64 relocatable object
Kernel mode: freestanding
```

The first compiler proof should generate an object with:

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


## Update workflow

Run `./update.sh` from anywhere after downloading an `Oryn-*.zip` archive. The updater extracts the latest archive from `~/Downloads` into a temporary directory, copies `ChangedFiles/` into `~/Dev/OrynFoundry`, stages the result, commits it, and then attempts to push to GitHub.

The updater does not expect `ChangedFiles/` to already exist inside `~/Dev/OrynFoundry`.


## Update behaviour

`update.sh` extracts the selected `Oryn-*.zip` archive into `/tmp`, finds `ChangedFiles/`, copies those files into `~/Dev/OrynFoundry`, initialises Git there when the folder exists but has no `.git/`, sets the origin remote to `https://github.com/Liberation26/C--2-FSOS-Compiler.git`, commits the copied files, and then attempts to push.

Existing non-Git target folders are no longer treated as fatal errors.


## Update workflow 0.0.4

`update.sh` prints its own version at startup, extracts the selected `Oryn-*.zip` archive into `/tmp`, copies `ChangedFiles/` into `~/Dev/OrynFoundry`, initialises Git in place when needed, commits the update, and attempts to push to the configured GitHub remote.

### 0.0.4 updater behaviour

`update.sh` selects the highest versioned `Oryn-x.y.z.zip` from `~/Downloads` when no path is supplied and self-resets from the selected archive if that archive contains a different updater.
