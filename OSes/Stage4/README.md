# Stage 4: Approved Module Boundary

Stage 4 proves that safe user-facing C# kernel code can call approved Oryn module APIs while unsafe native and freestanding details remain behind module bindings.

The source in `Source/Kernel.cs` only uses approved APIs from:

- `Oryn.Kernel.Diagnostics`
- `Oryn.Kernel.Memory`
- `Oryn.Kernel.Cpu`

The compiler validates those calls against `Source/Sdk/Bindings/*.binding.json` before lowering to Oryn IR and before emitting native output.
