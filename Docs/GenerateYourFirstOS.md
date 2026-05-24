# Generate your first OS with Oryn 1.0.1

Run:

```bash
./Oryn.sh generate --os-name MyOrynOS --kernel-name MyOrynKernel --modules Memory
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
[ OK ] [ KERNEL   ] MyOrynOS user-selected module: Memory
[ OK ] [ KERNEL   ] MyOrynOS generated kernel is halting forever
```
