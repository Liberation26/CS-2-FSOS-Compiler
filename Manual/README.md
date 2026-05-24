# Oryn Manual

Oryn is a C# to freestanding operating-system compiler project from Oryn Foundry.

Current version: **0.9.0**

## Current proof stage

Stage 9 proves generated kernel template composition from selected modules.

The Stage 9 composer reads module manifests, resolves dependencies, fills `OSes/Stage9/Templates/Kernel.template.cs`, validates the generated C# source against the safe subset and API contracts, and only then lets the backend emit IR, diagnostics, reference C/assembly, and direct ELF64 output.

Invalid calls are blocked before backend/native compilation. The Stage 9 invalid-call test confirms no generated C, generated assembly, or ELF64 object is produced when an unapproved call is present.

## Useful commands

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh Stage9
./Tests/Compiler/Stage9/run.sh
```

See also:

- `README.md`
- `OSes/Stage9/README.md`
- `Documents/Stages/Stage9.md`
- `Documents/ReleaseNotes/0.9.0.md`
