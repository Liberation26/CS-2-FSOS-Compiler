# Oryn Filesystem

Oryn's repository filesystem is designed around a real compiler pipeline rather than a template-only generator.

## Root

```text
README.md
LICENSE
VERSION
Docs/
Documents/
Manual/
Modules/
Scripts/
Source/
Tests/
```

## Source/Core/Oryn.Compiler

`Source/Core/Oryn.Compiler/` contains the compiler itself.

```text
Frontend/
KernelModel/
IR/
Backends/
Runtime/
Tests/
```

### Frontend

```text
Frontend/CSharpParser/
Frontend/SemanticModel/
Frontend/SafeSubsetValidator/
```

The frontend is responsible for reading C# source, building semantic information through Roslyn, and rejecting unsupported language features.

### KernelModel

```text
KernelModel/KernelAst/
KernelModel/ModuleAst/
KernelModel/SymbolTable/
KernelModel/CapabilityTable/
```

The kernel model describes the OS-facing meaning of the user's source code. It is not a direct copy of Roslyn's syntax tree.

### IR

```text
IR/OrynIr/
IR/ControlFlowGraph/
IR/TypeLowering/
```

The IR is the backend-independent representation used by Oryn before native code generation.

### Backends

```text
Backends/Native/X64/
Backends/Native/Object/
```

The first backend emits x86_64 machine code and ELF64 relocatable object files.

### Runtime

```text
Runtime/ObjectModel/
Runtime/StringModel/
Runtime/ArrayModel/
Runtime/Panic/
Runtime/Startup/
```

Runtime folders contain the compiler-owned models for features that do not exist in a normal .NET runtime inside a freestanding kernel.

## Source/Sdk

The SDK provides safe API assemblies and binding metadata.

```text
Source/Sdk/Apis/
Source/Sdk/Bindings/
```

The API assemblies are compile-time references. They are not runtime .NET dependencies inside the generated kernel.

## Source/Native

Native source provides trusted implementations for approved module APIs.

```text
Source/Native/Modules/
Source/Native/Runtime/
```

## Modules

`Modules/` contains module-level descriptions, documentation, and future implementation catalogues.

## Tests

`Tests/` contains compiler, backend, object writer, and boot proof tests.
