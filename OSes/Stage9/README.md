# Stage 9: generated kernel template composition

Stage 9 proves that Oryn can compose a safe C# kernel from a template and the selected module manifests before the normal compiler backend runs.

The Stage 9 flow is:

```text
selected module manifests
    -> dependency-resolved module set
    -> OSes/Stage9/Templates/Kernel.template.cs
    -> OSes/Stage9/Build/Runqemu/Generated/Kernel.Generated.cs
    -> safe-subset validation
    -> approved-call/API-contract validation
    -> Oryn IR/CFG/native ELF64 object
    -> linked freestanding kernel
```

The template uses these placeholders:

- `__ORYN_GENERATED_USINGS__` inserts the namespaces for selected modules.
- `__ORYN_KERNEL_BOOT_PROOF_LINES__` inserts deterministic diagnostics proving which modules were selected.
- `__ORYN_MODULE_INITIALIZATION_CALLS__` inserts the selected module initialization call path.
- `__ORYN_COMPILER_VERSION__` inserts the compiler version.

Invalid calls are rejected before backend/native compilation. A forbidden call must fail during safe-subset/semantic approved-call validation and must not create the generated C, generated assembly, or ELF64 object artifacts.
