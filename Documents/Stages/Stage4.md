# Stage 4: Approved Module Boundary

Stage 4 turns the compiler proof into the beginning of a usable OS-generation platform.

The user-facing kernel remains safe C#. It can call only approved Oryn module APIs. The native, unsafe, and freestanding details stay behind module bindings and native module implementations.

## Boundary rules

- Approved APIs are declared in `Source/Sdk/Bindings/*.binding.json`.
- Each binding records the module, namespace, type, method, signature, native symbol, argument types, stage, approval flag, and summary.
- `using Oryn.Kernel.*` namespaces are checked against the approved namespace list.
- Calls are checked against the approved binding catalogue before IR lowering.
- Unapproved calls fail with a Stage 4 module-boundary diagnostic.

## Current approved modules

- `Oryn.Kernel.Diagnostics.Diagnostics.WriteOk(string Message)`
- `Oryn.Kernel.Diagnostics.Diagnostics.WriteWarn(string Message)`
- `Oryn.Kernel.Diagnostics.Diagnostics.WriteFail(string Message)`
- `Oryn.Kernel.Memory.Memory.Initialize()`
- `Oryn.Kernel.Cpu.Cpu.HaltForever()`

## Tests

Stage 4 tests live under `Tests/Compiler/Stage4/`.

They prove that approved module calls compile and forbidden calls/forbidden Oryn namespaces fail before object emission.
