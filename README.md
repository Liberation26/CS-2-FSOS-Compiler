# Oryn

Oryn is a C# to freestanding operating-system compiler project from Oryn Foundry.

The goal is to let developers write an Oryn-safe subset of C# and compile it into native freestanding output that can be linked into a bootable kernel. Oryn is not a general .NET runtime and is not intended to compile arbitrary C# applications. Oryn compiles a controlled, kernel-safe subset into native code for operating-system development.

## Version

Current version: **0.9.2**

## Stage 9 status

Stage 9 is complete. Oryn now proves **generated kernel template composition from selected modules**.

Stage 8 proved that C# calls must match explicit API contracts before they can bind to native module symbols. Stage 9 keeps that rule and adds a generator step before compilation: selected module manifests are resolved, a kernel template is filled in, the generated C# source is validated, and only then is it lowered to IR and native output.

## The Stage 9 rule

A generated Oryn kernel is accepted only when all of these pass before backend/native compilation:

1. selected module manifests exist and are approved for kernel use,
2. dependencies are included and resolved in dependency-safe order,
3. the kernel template placeholders are expanded into C# source,
4. generated `using` statements refer only to approved `Oryn.Kernel.*` namespaces,
5. generated module calls pass safe-subset validation,
6. generated module calls match approved bindings and API contracts,
7. invalid calls fail without creating generated C, generated assembly, or ELF64 object artifacts.

## Template composition flow

```text
selected module manifests
    ↓
dependency-resolved module set
    ↓
OSes/Stage9/Templates/Kernel.template.cs
    ↓
OSes/Stage9/Build/Runqemu/Generated/Kernel.Generated.cs
    ↓
safe-subset validation
    ↓
approved-call/API-contract validation
    ↓
Oryn IR and CFG
    ↓
direct ELF64 relocatable object writer
    ↓
resolved native module linking
    ↓
freestanding bootable kernel
```

## Template placeholders

Stage 9 templates use these placeholders:

```text
__ORYN_GENERATED_USINGS__
__ORYN_KERNEL_BOOT_PROOF_LINES__
__ORYN_MODULE_INITIALIZATION_CALLS__
__ORYN_COMPILER_VERSION__
```

The compiler command is:

```bash
dotnet Source/Core/Oryn.Compiler/bin/Debug/net8.0/Oryn.Compiler.dll compose-kernel \
  --stage Stage9 \
  --template OSes/Stage9/Templates/Kernel.template.cs \
  --output OSes/Stage9/Build/Runqemu/Generated/Kernel.Generated.cs
```

`Runqemu.sh Stage9` runs this composition step automatically before invoking the normal compiler backend.

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
| Stage 9 | A kernel source file is generated from a selected module template and validated before backend/native compilation. |

## Running Stage 9

From the repository root:

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh Stage9
```

Expected proof lines include:

```text
[ OK ] [ COMPOSE  ] Oryn kernel template composer version 0.9.2
[ OK ] [ COMPOSE  ] Selected modules: Runtime, Diagnostics, Memory, Panic, Cpu, ManifestLoader
[SERIAL] [ OK ] [ KERNEL   ] Stage9 native pre-kernel handoff reached
[SERIAL] [ OK ] [ KERNEL   ] Stage9 generated kernel entered
[SERIAL] [ OK ] [ KERNEL   ] Stage9 generated kernel template composition reached kernel code
[SERIAL] [ OK ] [ MANIFEST ] Stage9 dependency graph loading started
[SERIAL] [ OK ] [ COMPOSE ] Stage9 generated template composition runtime proof completed
[SERIAL] [ OK ] [ KERNEL   ] Stage9 generated kernel is halting forever
```

The kernel intentionally halts forever after proving the stage. The QEMU timeout is treated as success.

## Running tests

```bash
./Tests/Compiler/Stage9/run.sh
```

The Stage 9 tests check template composition, generated-kernel compilation, invalid-call rejection before backend/native compilation, and QEMU boot proof output.

## Important directories

```text
Source/Core/Oryn.Compiler/KernelComposition/
                                      Stage 9 kernel template composer
Source/Core/Oryn.Compiler/Frontend/ApiContracts/
                                      API contract catalogue loader
Source/Core/Oryn.Compiler/Manifests/
                                      Manifest dependency resolver
Source/Sdk/Bindings/              Safe C# API to native symbol bindings
Source/Sdk/ApiContracts/          Approved C# API contracts
Source/Sdk/ModuleManifests/       Module metadata and dependency declarations
Source/Native/Modules/            Freestanding native module implementations
OSes/Stage9/                      Stage 9 generated-template proof kernel
Tests/Compiler/Stage9/            Stage 9 automated tests
Documents/ReleaseNotes/0.9.2.md   Stage 9 Runqemu dispatcher fix
Documents/ReleaseNotes/0.9.0.md   Stage 9 release notes
```
