# Oryn

Current version: **0.5.0**

Oryn is a C# to freestanding operating-system compiler project from Oryn Foundry.

The goal is to let developers write an Oryn-safe subset of C# and compile it into native freestanding output that can be linked into a bootable kernel. Oryn is not a general .NET runtime and it is not intended to compile arbitrary C# applications. Oryn compiles a controlled kernel-safe C# subset into native code for operating-system development.

## Stage 5 status

Stage 5 is now the default development stage.

Stage 5 proves that Oryn can compile safe C# kernel-facing code through the approved module boundary into a bootable freestanding x86_64 kernel with a small runtime contract.

The Stage 5 pipeline is:

```text
OSes/Stage5/Source/Kernel.cs
  -> Oryn safe-subset validation
  -> approved module binding validation
  -> semantic binding
  -> Oryn IR
  -> control-flow graph proof
  -> direct ELF64 relocatable object written by Oryn
  -> linked freestanding x86_64 kernel ELF
  -> GRUB ISO
  -> QEMU serial proof run
```

## What Stage 5 adds

Stage 1 proved approved calls could become a bootable freestanding kernel.

Stage 2 proved variables, branches, loops, helper methods, and module calls.

Stage 3 proved Oryn could write a real ELF64 relocatable object directly instead of relying on assembler output as the primary object path.

Stage 4 proved that user-facing safe C# can only call approved Oryn module APIs.

Stage 5 adds the first explicit runtime contract:

- `Oryn.Kernel.Runtime.Runtime.Initialize()`
- `Oryn.Kernel.Runtime.Runtime.MarkKernelReady()`
- `Oryn.Kernel.Panic.Panic.Halt(string Reason)`
- native Runtime and Panic modules
- Stage 5 boot kernel
- Stage 5 compiler tests
- Stage 5 QEMU boot proof

In plain terms: Stage 5 proves that a safe C# kernel can enter an approved Oryn runtime path, initialize memory, use diagnostics, run control flow, expose an approved panic route, mark itself ready, and halt forever in QEMU.

## Current safe C# subset

The current Oryn-safe subset supports:

- `public static void Main()` as the kernel entry point;
- private static helper methods with no parameters;
- local `int` variables;
- integer literals;
- string literals passed to approved module calls;
- assignment;
- `+` and `-` integer arithmetic;
- `==` and `<` integer comparisons;
- `if` / `else`;
- `while` loops;
- explicit `return` statements;
- approved Oryn module calls.

Unsupported C# features are intentionally rejected. Oryn is building a freestanding compiler and runtime contract, not a general .NET runtime.

## Approved modules in Stage 5

The approved module catalogue lives under `Source/Sdk/Bindings/`.

Current approved kernel-facing modules are:

| Module | Approved safe C# API | Native symbol |
| --- | --- | --- |
| Runtime | `Runtime.Initialize()` | `Runtime_Initialize` |
| Runtime | `Runtime.MarkKernelReady()` | `Runtime_MarkKernelReady` |
| Diagnostics | `Diagnostics.WriteOk(string Message)` | `Diagnostics_WriteOk` |
| Diagnostics | `Diagnostics.WriteWarn(string Message)` | `Diagnostics_WriteWarn` |
| Diagnostics | `Diagnostics.WriteFail(string Message)` | `Diagnostics_WriteFail` |
| Memory | `Memory.Initialize()` | `Memory_Initialize` |
| Panic | `Panic.Halt(string Reason)` | `Panic_Halt` |
| Cpu | `Cpu.HaltForever()` | `Cpu_HaltForever` |

Safe C# code may use only approved `Oryn.Kernel.*` namespaces and approved binding catalogue entries. Unsafe details stay behind native module implementations.

## Run Stage 5

From the repository root:

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh Stage5
```

`Stage5` is also the default:

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh
```

The expected serial proof includes:

```text
[ OK ] [ BOOT32   ] Multiboot entry reached; preparing long mode
[ OK ] [ BOOT     ] Long mode entered; calling Kernel_Main
[ OK ] [ KERNEL   ] Stage5 kernel entered
[ OK ] [ KERNEL   ] Stage5 runtime contract initialized
[ OK ] [ KERNEL   ] Stage5 memory module initialized
[ OK ] [ KERNEL   ] Stage5 loop and branch proof worked
[ OK ] [ KERNEL   ] Stage5 runtime marked kernel ready
[ OK ] [ KERNEL   ] Stage5 kernel is halting forever
```

The kernel intentionally halts forever. The QEMU timeout is treated as success when the expected diagnostics have appeared.

## Run Stage 5 tests

```bash
./Tests/Compiler/Stage5/run.sh
```

The tests check that:

- the compiler exits successfully;
- the Stage 5 IR manifest exists;
- generated assembly exists;
- the direct ELF64 object exists;
- Runtime and Panic bindings are present and approved;
- the Stage 5 kernel boots in QEMU;
- the QEMU serial log reaches the expected Stage 5 diagnostics;
- timeout after the halt loop is treated as success.

## Repository layout

```text
OSes/Stage5/                 Stage 5 proof kernel
Source/Core/Oryn.Compiler/   Oryn compiler implementation
Source/Sdk/Apis/             compile-time SDK API declarations
Source/Sdk/Bindings/         approved module binding metadata
Source/Native/Modules/       native freestanding module implementations
Tests/Compiler/Stage5/       Stage 5 compiler and boot tests
Documents/Stages/Stage5.md   Stage 5 design note
Documents/ReleaseNotes/      release notes
```

## Development rule

Oryn code generation must keep the user-facing kernel source safe. Hardware access, CPU halt loops, panic handling, diagnostics output, memory setup, and future runtime services belong behind approved modules and binding metadata.
