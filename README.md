# Oryn

Oryn is a C# Operating System Development Platform from Oryn Foundry. Its purpose is to let a developer describe safe, user-facing kernel code in a restricted C# subset and have Oryn turn that code into freestanding native output for a bootable operating-system project.

## Current status

Oryn has completed the Stage 3 compiler proof and now includes the Stage 4 approved module boundary.

### What Stage 3 already proves

Stage 3 proves that Oryn is no longer only producing sketches or host-dependent output.

The Stage 3 pipeline can:

1. read a safe C# kernel source file;
2. validate the safe subset;
3. parse variables, branches, loops, helper methods, returns, integer arithmetic, string literals, and approved module calls;
4. lower the source into explicit Oryn IR;
5. build a basic control-flow graph;
6. emit readable diagnostic C and x64 assembly reference artifacts;
7. write a real ELF64 relocatable object directly from Oryn IR;
8. link that object with native freestanding module implementations;
9. produce a bootable GRUB ISO;
10. boot the generated kernel in QEMU and reach expected serial diagnostics.

Stage 3 therefore proves the core compiler path:

```text
Kernel.cs
  -> safe-subset validation
  -> Oryn IR
  -> control-flow graph
  -> direct ELF64 relocatable object
  -> linked freestanding kernel ELF
  -> bootable ISO
  -> QEMU proof output
```

The Stage 3 kernel lives in `OSes/Stage3/Source/Kernel.cs` and exercises:

- `Diagnostics.WriteOk` / `Diagnostics.WriteFail`
- `Memory.Initialize`
- `Cpu.HaltForever`
- local `int` variables
- `while` loops
- `if` / `else` branches
- `+`, `-`, `==`, and `<`
- private static helper methods
- explicit `return`

The Stage 3 tests live in `Tests/Compiler/Stage3/` and check IR, assembly, ELF64 sections, symbols, relocations, no compiler rebuild dependency during the proof run, feature parity, and QEMU boot diagnostics.

### What Stage 4 adds

Stage 4 introduces the approved module boundary.

Safe user-facing C# kernel code can now call approved Oryn module APIs, while unsafe native/freestanding details stay hidden behind approved modules.

Stage 4 adds:

- an approved module catalogue in `Source/Sdk/Bindings/*.binding.json`;
- namespace, type, method, signature, argument-type, native-symbol, stage, and approval metadata;
- compiler validation for approved `Oryn.Kernel.*` namespaces;
- compiler validation for approved module calls;
- clear failure diagnostics for unapproved calls;
- tests proving allowed calls compile;
- tests proving forbidden calls fail.

The current approved modules are:

| Module | Approved namespace | Approved API |
| --- | --- | --- |
| Diagnostics | `Oryn.Kernel.Diagnostics` | `Diagnostics.WriteOk(string)`, `Diagnostics.WriteWarn(string)`, `Diagnostics.WriteFail(string)` |
| Memory | `Oryn.Kernel.Memory` | `Memory.Initialize()` |
| Cpu | `Oryn.Kernel.Cpu` | `Cpu.HaltForever()` |

This is the point where Oryn starts becoming a real OS-generation platform: end-user C# stays safe and small, and platform/native details are mediated by approved modules.

## Running the current stage

Build and run the current Stage 4 proof:

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh Stage4
```

Run only the Stage 4 compiler-boundary tests:

```bash
./Tests/Compiler/Stage4/run.sh
```

Run the Stage 3 compiler/object/boot proof tests:

```bash
./Tests/Compiler/Stage3/run.sh
```

## Repository layout

| Path | Purpose |
| --- | --- |
| `Source/Core/Oryn.Compiler/` | The Oryn compiler. |
| `Source/Sdk/Apis/` | Safe C# API surface for approved modules. |
| `Source/Sdk/Bindings/` | Approved module catalogue and native binding metadata. |
| `Source/Native/Modules/` | Freestanding native implementations hidden behind module APIs. |
| `OSes/Stage3/` | Stage 3 direct ELF64 object writer proof kernel. |
| `OSes/Stage4/` | Stage 4 approved module boundary proof kernel. |
| `Tests/Compiler/Stage3/` | Stage 3 compiler, object, and boot proof tests. |
| `Tests/Compiler/Stage4/` | Stage 4 approved/forbidden module-boundary tests. |
| `Documents/Stages/` | Stage design notes. |
| `Documents/ReleaseNotes/` | Versioned release notes. |

## Version

Current version: `0.4.0`.
