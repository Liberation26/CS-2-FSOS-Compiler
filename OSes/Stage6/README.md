# Oryn Stage 6 Kernel

Stage 6 proves service/module manifest loading. The kernel no longer only proves that safe C# can become freestanding code; it now proves that JSON module metadata can decide what is exposed to the compiler, linked into the native image, and initialized through generated manifest glue.

Expected QEMU proof lines include:

```text
[SERIAL] [ OK ] [ KERNEL   ] Stage6 kernel entered
[SERIAL] [ OK ] [ MANIFEST ] Stage6 module manifest loading started
[SERIAL] [ OK ] [ MANIFEST ] initializing Runtime
[SERIAL] [ OK ] [ MANIFEST ] initializing Memory
[SERIAL] [ OK ] [ MANIFEST ] Stage6 selected modules initialized from manifest metadata
[SERIAL] [ OK ] [ KERNEL   ] Stage6 runtime marked kernel ready
[SERIAL] [ OK ] [ KERNEL   ] Stage6 kernel is halting forever
```

Run it with:

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh Stage6
```


## 0.6.1 fix

The Stage 6 boot proof now includes a native pre-kernel handoff marker and serial GRUB diagnostics. ManifestLoader glue is generated as an idempotent dispatcher and does not initialize itself recursively.
