# Runtime Module

The Runtime module is introduced in Stage 5.

It provides the approved safe-C# entry contract for freestanding kernels:

- `Runtime.Initialize()` records that the Oryn runtime contract is active.
- `Runtime.MarkKernelReady()` records that the generated kernel reached its ready point.

The native implementation is deliberately small in Stage 5. It proves the boundary and gives later stages a stable place to add object, string, array, startup, and panic coordination without exposing unsafe details to user-facing kernel code.
