# Generate your first OS with Oryn 1.0.1

Run:

```bash
./Oryn.sh generate --os-name MyOrynOS --kernel-name MyOrynKernel --modules None
```

Then build:

```bash
./Oryn.sh build MyOrynOS
```

Then run:

```bash
./Oryn.sh run MyOrynOS
```

Expected serial proof includes:

```text
[ OK ] [ KERNEL   ] MyOrynOS generated kernel entered
[ OK ] [ KERNEL   ] MyOrynOS kernel name MyOrynKernel
[ OK ] [ KERNEL   ] MyOrynOS mandatory kernel module: Runtime
[ OK ] [ KERNEL   ] MyOrynOS mandatory kernel module: Diagnostics
[ OK ] [ KERNEL   ] MyOrynOS mandatory kernel module: Panic
[ OK ] [ KERNEL   ] MyOrynOS mandatory kernel module: Cpu
[ OK ] [ KERNEL   ] MyOrynOS mandatory kernel module: ManifestLoader
[ OK ] [ KERNEL   ] MyOrynOS user-selected modules: <none>
[ OK ] [ KERNEL   ] MyOrynOS generated kernel is halting forever
```

## 1.0.8 generated OS proof

Generated kernels now print `Hello from <OsName>` during boot. The generation questions also ask whether QEMU should run in `Headless` mode or `Visual` mode, and the generated manifest records that choice as `VmDisplayMode`.

## VM display answer

For `VM display mode`, answer `Headless` for an automated run that closes after the proof timeout, or `Visual` to open a QEMU window that stays open until you close it.

Generator prompts show the accepted options on a separate `OPTIONS` line. In interactive terminals, those options are displayed in a different colour so the expected answers are easier to see.
