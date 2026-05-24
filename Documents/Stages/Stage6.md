# Stage 6 - Service/module manifest loading

Stage 6 introduces manifest-driven module selection. Module metadata is stored under `Source/Sdk/ModuleManifests/` as JSON. Each manifest describes the safe namespace, native source, binding file, initializer, order, and whether the module is exposed to safe kernel code.

Stage 6 proves three things:

1. **Expose** - the compiler loads manifest metadata and reports approved modules in compiler diagnostics.
2. **Link** - `Runqemu.sh Stage6` reads the same manifest set and links only selected native module sources plus generated manifest glue.
3. **Initialize** - generated `ModuleManifest.Generated.c` calls selected module initializers in manifest order and prints predictable proof lines.

This keeps module selection outside hand-written kernel build logic and moves Oryn toward generated OS assembly driven by metadata.
