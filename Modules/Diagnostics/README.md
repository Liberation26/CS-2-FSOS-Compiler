# Diagnostics Module

The Diagnostics module provides approved kernel-safe diagnostic output APIs.

Stage 1 API namespace:

```csharp
using Oryn.Kernel.Diagnostics;
```

Approved calls:

```text
Diagnostics.WriteOk(string Message)
Diagnostics.WriteWarn(string Message)
Diagnostics.WriteFail(string Message)
```
