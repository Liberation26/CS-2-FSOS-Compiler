# Oryn

Oryn is a C# to freestanding operating-system compiler project from Oryn Foundry.

The goal is to let developers write an Oryn-safe subset of C# and compile it into native freestanding output that can be linked into a bootable kernel. Oryn is not a general .NET runtime and is not intended to compile arbitrary C# applications. Oryn compiles a controlled, kernel-safe subset into native code for operating-system development.

## Version

Current version: **0.7.0**

## Stage 7 status

Stage 7 is complete. Oryn now proves module dependency resolution.

Stage 6 proved that selected manifest modules can be loaded, compiled, linked, and initialized. Stage 7 adds dependency graph rules so modules are no longer trusted merely because they were manually listed in the right order.

Module manifests live under:

```text
Source/Sdk/ModuleManifests/
```

Each selected manifest can declare:

- module name
- safe namespace
- stage introduced
- whether it is allowed in kernel code
- whether it should be linked by default
- dependency list with `dependsOn`
- secondary `initializeOrder` tie-breaker
- managed initializer name
- native initializer symbol
- native source path
- binding path

`initializeOrder` is now secondary. The Stage 7 proof comes from dependency resolution.

Example:

```json
{
  "module": "Memory",
  "namespace": "Oryn.Kernel.Memory",
  "stage": 4,
  "allowedInKernel": true,
  "linkByDefault": true,
  "initializeOrder": 30,
  "dependsOn": [
    "Runtime",
    "Diagnostics"
  ],
  "initializerManagedName": "Memory.Initialize",
  "initializerNativeSymbol": "Memory_Initialize",
  "nativeSource": "Source/Native/Modules/Memory/Memory.Native.c",
  "bindingPath": "Source/Sdk/Bindings/Memory.binding.json",
  "summary": "Memory module selected for early freestanding kernels."
}
```

## Compiler flow

The intended compiler flow is now:

```text
Oryn-safe C# source
    ↓
Safe-subset validation
    ↓
Manifest-backed module exposure
    ↓
Manifest dependency graph validation
    ↓
Dependency-safe module order resolution
    ↓
Binding and semantic analysis
    ↓
Oryn IR
    ↓
Control-flow graph
    ↓
Direct ELF64 relocatable object writer
    ↓
Resolved native module linking
    ↓
Generated dependency-resolved manifest glue
    ↓
Freestanding bootable kernel
```

## What each stage proves

| Stage | Proof |
| --- | --- |
| Stage 1 | Approved calls can become a bootable freestanding kernel. |
| Stage 2 | Oryn can compile variables, branches, loops, helper methods, and module calls into useful IR and native output. |
| Stage 3 | Oryn can write a real ELF64 relocatable object directly. |
| Stage 4 | Safe user-facing C# calls are checked against the approved module boundary. |
| Stage 5 | Runtime, diagnostics, memory, panic, and CPU bindings form a minimal freestanding runtime contract. |
| Stage 6 | Service/module manifests drive module exposure, native linking, and manifest glue initialization. |
| Stage 7 | Module dependencies are validated, missing dependencies and cycles are rejected, and modules initialize in dependency-safe order. |

## Running Stage 7

From the repository root:

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh Stage7
```

Expected proof lines include:

```text
[SERIAL] [ OK ] [ BOOT32   ] Multiboot entry reached; preparing long mode
[SERIAL] [ OK ] [ BOOT     ] Long mode entered; calling Kernel_Main
[SERIAL] [ OK ] [ KERNEL   ] Stage7 native pre-kernel handoff reached
[SERIAL] [ OK ] [ KERNEL   ] Stage7 kernel entered
[SERIAL] [ OK ] [ MANIFEST ] Stage7 dependency graph loading started
[SERIAL] [ OK ] [ MANIFEST ] dependency Runtime -> <none>
[SERIAL] [ OK ] [ MANIFEST ] dependency Diagnostics -> Runtime
[SERIAL] [ OK ] [ MANIFEST ] dependency Memory -> Runtime, Diagnostics
[SERIAL] [ OK ] [ MANIFEST ] dependency Panic -> Runtime, Diagnostics
[SERIAL] [ OK ] [ MANIFEST ] dependency Cpu -> Runtime, Diagnostics
[SERIAL] [ OK ] [ MANIFEST ] resolved initialization order: Runtime, Diagnostics, Memory, Panic, Cpu
[SERIAL] [ OK ] [ MANIFEST ] initializing Runtime
[SERIAL] [ OK ] [ MANIFEST ] initializing Diagnostics
[SERIAL] [ OK ] [ MANIFEST ] initializing Memory
[SERIAL] [ OK ] [ MANIFEST ] initializing Panic
[SERIAL] [ OK ] [ MANIFEST ] initializing Cpu
[SERIAL] [ OK ] [ KERNEL   ] Stage7 dependency-resolved modules initialized
[SERIAL] [ OK ] [ KERNEL   ] Stage7 kernel is halting forever
```

The kernel intentionally halts forever after proving the stage. The QEMU timeout is treated as success.

## Running tests

```bash
./Tests/Compiler/Stage7/run.sh
```

The Stage 7 tests check compiler success, manifest graph output, generated glue order, missing dependency rejection, circular dependency rejection, and QEMU boot proof output.

## Important directories

```text
Source/Core/Oryn.Compiler/        Compiler implementation
Source/Core/Oryn.Compiler/Manifests/
                                      Manifest dependency resolver
Source/Sdk/Bindings/              Safe C# API to native symbol bindings
Source/Sdk/ModuleManifests/       Module metadata and dependency declarations
Source/Native/Modules/            Freestanding native module implementations
OSes/Stage7/                      Stage 7 proof kernel
Tests/Compiler/Stage7/            Stage 7 automated tests
Documents/ReleaseNotes/0.7.0.md   Stage 7 release notes
```

## Why Stage 7 comes before filesystem and userland

After Stage 7, Oryn knows:

- what modules exist
- what each module needs
- what order modules must start in
- what native source must be linked
- what API surface is exposed to kernel code

That is the base needed before adding larger OS pieces such as filesystem support, scheduler support, userland services, PolicyManager, ELF loading, driver selection, and target profiles.
