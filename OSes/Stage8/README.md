# Stage 8 - module API contracts / approved calls from C#

Stage 8 proves that user-facing Oryn-safe C# can only call module APIs that are approved by explicit API contract JSON files.

Stage 7 proved that selected module manifests can be resolved as a dependency-safe graph. Stage 8 keeps that manifest graph and adds a separate API-contract layer under:

```text
Source/Sdk/ApiContracts/
```

The compiler now checks each module call from C# against:

1. the safe-subset validator,
2. the binding catalogue under `Source/Sdk/Bindings/`,
3. the module API contract catalogue under `Source/Sdk/ApiContracts/`, and
4. the selected module manifests under `Source/Sdk/ModuleManifests/`.

A C# call is approved only when the binding and API contract agree on namespace, type, managed method name, native symbol, argument count, and argument types.

## Proof kernel

The Stage 8 proof kernel uses approved calls from:

- `Oryn.Kernel.Diagnostics`
- `Oryn.Kernel.ManifestLoader`
- `Oryn.Kernel.Runtime`
- `Oryn.Kernel.Cpu`

Expected proof lines include:

```text
[ OK ] [ KERNEL   ] Stage8 kernel entered
[ OK ] [ CONTRACT ] Stage8 module API contract runtime proof started
[ OK ] [ CONTRACT ] approved C# call Diagnostics.WriteOk -> Diagnostics_WriteOk
[ OK ] [ CONTRACT ] approved C# call Runtime.MarkKernelReady -> Runtime_MarkKernelReady
[ OK ] [ CONTRACT ] approved C# call Cpu.HaltForever -> Cpu_HaltForever
[ OK ] [ MANIFEST ] Stage8 dependency graph loading started
[ OK ] [ MANIFEST ] resolved initialization order: Runtime, Diagnostics, Memory, Panic, Cpu
[ OK ] [ CONTRACT ] Stage8 module API contract runtime proof completed
[ OK ] [ KERNEL   ] Stage8 approved module API contracts initialized
[ OK ] [ KERNEL   ] Stage8 kernel is halting forever
```

Run with:

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh Stage8
```
