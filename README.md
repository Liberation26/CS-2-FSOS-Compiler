# Oryn

Oryn is a C# to freestanding operating-system compiler project from Oryn Foundry.

The goal is to let developers write an Oryn-safe subset of C# and compile it into native freestanding output that can be linked into a bootable kernel. Oryn is not a general .NET runtime and is not intended to compile arbitrary C# applications. Oryn compiles a controlled, kernel-safe subset into native code for operating-system development.

## Version

Current version: **0.6.0**

## Stage 6 status

Stage 6 is complete. Oryn now proves service/module manifest loading. This means generated OS module metadata can drive what the compiler exposes, what the build links, and what the booting kernel initializes.

Stage 6 adds JSON module manifests under:

```text
Source/Sdk/ModuleManifests/
```

Each manifest declares:

- module name
- safe namespace
- stage introduced
- whether it is allowed in kernel code
- whether it should be linked by default
- initializer order
- managed initializer name
- native initializer symbol
- native source path
- binding path

The Stage 6 build reads these manifests, generates native manifest glue, links selected native module objects, and calls selected initializers from the kernel through `ManifestLoader.InitializeSelected()`.

## Compiler flow

The intended compiler flow is now:

```text
Oryn-safe C# source
    ↓
Safe-subset validation
    ↓
Manifest-backed module exposure
    ↓
Binding and semantic analysis
    ↓
Oryn IR
    ↓
Control-flow graph
    ↓
Direct ELF64 relocatable object writer
    ↓
Manifest-driven native module linking
    ↓
Generated manifest initialization glue
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
| Stage 6 | Service/module manifests drive module exposure, native linking, and initialization order. |

## Running Stage 6

From the repository root:

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh Stage6
```

Expected proof lines include:

```text
[SERIAL] [ OK ] [ KERNEL   ] Stage6 kernel entered
[SERIAL] [ OK ] [ MANIFEST ] Stage6 module manifest loading started
[SERIAL] [ OK ] [ MANIFEST ] initializing Runtime
[SERIAL] [ OK ] [ MANIFEST ] initializing Memory
[SERIAL] [ OK ] [ MANIFEST ] Stage6 selected modules initialized from manifest metadata
[SERIAL] [ OK ] [ KERNEL   ] Stage6 runtime marked kernel ready
[SERIAL] [ OK ] [ KERNEL   ] Stage6 kernel is halting forever
```

The kernel intentionally halts forever after proving the stage. The QEMU timeout is treated as success.

## Running tests

```bash
./Tests/Compiler/Stage6/run.sh
```

The Stage 6 tests check compiler success, manifest diagnostics, generated manifest glue, linked native module selection, and QEMU boot proof output.

## Important directories

```text
Source/Core/Oryn.Compiler/        Compiler implementation
Source/Sdk/Bindings/              Safe C# API to native symbol bindings
Source/Sdk/ModuleManifests/       Stage 6 module metadata
Source/Native/Modules/            Freestanding native module implementations
OSes/Stage6/                      Stage 6 proof kernel
Tests/Compiler/Stage6/            Stage 6 automated tests
Documents/Stages/Stage6.md        Stage 6 design notes
```

## Current limitation

Stage 6 proves manifest-driven selection and initialization. It does not yet provide dynamic service loading, runtime ELF service startup, dependency solving, or a production module package manager. Those are later stages.
