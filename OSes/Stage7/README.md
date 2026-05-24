# Oryn Stage 7

Stage 7 proves dependency-resolved module initialization.

Stage 6 proved that selected manifest modules could be loaded, compiled, linked, and initialized. Stage 7 adds graph rules so the selected modules no longer rely on being listed manually in a safe order.

The Stage 7 manifest flow:

1. Load selected records from `Source/Sdk/ModuleManifests/*.module.json`.
2. Validate that every selected module has all required dependencies selected.
3. Reject missing dependencies.
4. Reject circular dependencies.
5. Sort the selected graph into dependency-safe initialization order.
6. Generate `Build/Runqemu/ModuleManifest.Generated.c` from that resolved order.
7. Compile and link only the resolved selected native module sources.
8. Prove the same resolved order at runtime in QEMU.

Expected runtime proof:

```text
[ OK ] [ KERNEL   ] Stage7 kernel entered
[ OK ] [ MANIFEST ] Stage7 dependency graph loading started
[ OK ] [ MANIFEST ] dependency Runtime -> <none>
[ OK ] [ MANIFEST ] dependency Diagnostics -> Runtime
[ OK ] [ MANIFEST ] dependency Memory -> Runtime, Diagnostics
[ OK ] [ MANIFEST ] dependency Panic -> Runtime, Diagnostics
[ OK ] [ MANIFEST ] dependency Cpu -> Runtime, Diagnostics
[ OK ] [ MANIFEST ] resolved initialization order: Runtime, Diagnostics, Memory, Panic, Cpu
[ OK ] [ MANIFEST ] initializing Runtime
[ OK ] [ MANIFEST ] initializing Diagnostics
[ OK ] [ MANIFEST ] initializing Memory
[ OK ] [ MANIFEST ] initializing Panic
[ OK ] [ MANIFEST ] initializing Cpu
[ OK ] [ KERNEL   ] Stage7 dependency-resolved modules initialized
[ OK ] [ KERNEL   ] Stage7 kernel is halting forever
```
