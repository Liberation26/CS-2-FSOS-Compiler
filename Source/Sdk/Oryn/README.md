# Oryn Freestanding SDK

This is the compile-time-only `.Oryn` SDK surface for generated freestanding kernels.

Generated kernel code may use namespaces such as:

```csharp
using Oryn.Diagnostics;
using Oryn.Cpu;
using Oryn.Runtime;
```

These declarations are not a normal runtime library. They are an approved API surface that the Oryn compiler maps to native freestanding module symbols.

For 2.0.0, the approved calls are still intentionally small and match the tested booting kernel milestone.
