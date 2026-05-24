# Oryn

Oryn is a C# to freestanding operating-system compiler project from Oryn Foundry.

The goal is to let developers write an Oryn-safe subset of C# and compile it into native freestanding output that can be linked into a bootable kernel. Oryn is not a general .NET runtime and is not intended to compile arbitrary C# applications. Oryn compiles a controlled, kernel-safe subset into native code for operating-system development.

## Version

Current version: **0.8.0**

## Stage 8 status

Stage 8 is complete. Oryn now proves **module API contracts and approved calls from C#**.

Stage 7 proved that selected modules can be resolved in dependency-safe order. Stage 8 keeps that dependency graph and adds a stricter API-contract layer. A C# call is no longer approved merely because a native symbol exists or because a binding file lists it. The compiler now requires an explicit module API contract that agrees with the binding metadata.

## The Stage 8 rule

An Oryn-safe C# module call is approved only when all of these agree:

1. the safe-subset validator accepts the source,
2. the namespace is an approved `Oryn.Kernel.*` namespace,
3. the binding exists under `Source/Sdk/Bindings/`,
4. the matching API contract exists under `Source/Sdk/ApiContracts/`,
5. the contract says the method is allowed from C# kernel code,
6. the binding and contract agree on namespace, type, method, native symbol, argument count, and argument types,
7. the selected module manifests can still be resolved through the Stage 7 dependency graph.

## API contract files

API contracts live under:

```text
Source/Sdk/ApiContracts/
```

Example contract shape:

```json
{
  "contractVersion": "0.8.0",
  "module": "Diagnostics",
  "namespace": "Oryn.Kernel.Diagnostics",
  "minimumStage": 4,
  "allowedInKernel": true,
  "summary": "Stage 8 approved C# API contract for Diagnostics.",
  "methods": [
    {
      "managedName": "Diagnostics.WriteOk",
      "typeName": "Diagnostics",
      "methodName": "WriteOk",
      "signature": "void WriteOk(string Message)",
      "nativeSymbol": "Diagnostics_WriteOk",
      "allowedFromCSharpKernel": true,
      "argumentTypes": [
        "String"
      ],
      "summary": "Writes a successful diagnostic status line."
    }
  ]
}
```

The contract is deliberately separate from the native C implementation. It is the user-facing API promise that says what Oryn-safe C# may call.

## Compiler flow

The intended compiler flow is now:

```text
Oryn-safe C# source
    ↓
Safe-subset validation
    ↓
Approved namespace validation
    ↓
Binding catalogue lookup
    ↓
Stage 8 module API contract validation
    ↓
Manifest-backed module exposure
    ↓
Manifest dependency graph validation
    ↓
Dependency-safe module order resolution
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
| Stage 8 | Module API contracts approve exactly which C# calls may bind to native module symbols. |

## Running Stage 8

From the repository root:

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh Stage8
```

Expected proof lines include:

```text
[SERIAL] [ OK ] [ BOOT32   ] Multiboot entry reached; preparing long mode
[SERIAL] [ OK ] [ BOOT     ] Long mode entered; calling Kernel_Main
[SERIAL] [ OK ] [ KERNEL   ] Stage8 native pre-kernel handoff reached
[SERIAL] [ OK ] [ KERNEL   ] Stage8 kernel entered
[SERIAL] [ OK ] [ CONTRACT ] Stage8 module API contract runtime proof started
[SERIAL] [ OK ] [ CONTRACT ] approved C# call Diagnostics.WriteOk -> Diagnostics_WriteOk
[SERIAL] [ OK ] [ CONTRACT ] approved C# call Runtime.MarkKernelReady -> Runtime_MarkKernelReady
[SERIAL] [ OK ] [ CONTRACT ] approved C# call Cpu.HaltForever -> Cpu_HaltForever
[SERIAL] [ OK ] [ MANIFEST ] Stage8 dependency graph loading started
[SERIAL] [ OK ] [ MANIFEST ] dependency Runtime -> <none>
[SERIAL] [ OK ] [ MANIFEST ] dependency Diagnostics -> Runtime
[SERIAL] [ OK ] [ MANIFEST ] dependency Memory -> Runtime, Diagnostics
[SERIAL] [ OK ] [ MANIFEST ] dependency Panic -> Runtime, Diagnostics
[SERIAL] [ OK ] [ MANIFEST ] dependency Cpu -> Runtime, Diagnostics
[SERIAL] [ OK ] [ MANIFEST ] resolved initialization order: Runtime, Diagnostics, Memory, Panic, Cpu
[SERIAL] [ OK ] [ CONTRACT ] Stage8 module API contract runtime proof completed
[SERIAL] [ OK ] [ KERNEL   ] Stage8 approved module API contracts initialized
[SERIAL] [ OK ] [ KERNEL   ] Stage8 kernel is halting forever
```

The kernel intentionally halts forever after proving the stage. The QEMU timeout is treated as success.

## Running tests

```bash
./Tests/Compiler/Stage8/run.sh
```

The Stage 8 tests check compiler output, contract diagnostics, API contract files, rejection of an uncontracted call, and QEMU boot proof output.

## Important directories

```text
Source/Core/Oryn.Compiler/        Compiler implementation
Source/Core/Oryn.Compiler/Frontend/ApiContracts/
                                      Stage 8 API contract catalogue loader
Source/Core/Oryn.Compiler/Manifests/
                                      Manifest dependency resolver
Source/Sdk/Bindings/              Safe C# API to native symbol bindings
Source/Sdk/ApiContracts/          Stage 8 approved C# API contracts
Source/Sdk/ModuleManifests/       Module metadata and dependency declarations
Source/Native/Modules/            Freestanding native module implementations
OSes/Stage8/                      Stage 8 proof kernel
Tests/Compiler/Stage8/            Stage 8 automated tests
Documents/ReleaseNotes/0.8.0.md   Stage 8 release notes
```

## Why Stage 8 comes before larger OS features

After Stage 8, Oryn knows:

- what modules exist,
- what each module needs,
- what order modules must start in,
- what native source must be linked,
- what API surface is exposed to kernel code,
- which C# calls are approved by explicit contract.

That is the base needed before adding larger OS pieces such as filesystem support, scheduler support, userland services, PolicyManager, ELF loading, driver selection, and target profiles.
