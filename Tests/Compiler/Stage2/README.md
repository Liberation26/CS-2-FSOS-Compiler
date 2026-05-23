# Stage 2 compiler tests

Stage 2 tests should prove that the Stage 2 source tree builds through the compiler and boots through the freestanding x86_64 path.

Version 0.2.0 introduces the Stage 2 test area and the initial command to exercise it:

```bash
ORYN_SKIP_QEMU=1 ./Runqemu.sh Stage2
```

Expected build artifacts are written under:

```text
OSes/Stage2/Build/Runqemu/
```

The next Stage 2 test scripts should be added as the compiler grows beyond call-only lowering:

```text
01-compile-stage2-kernel.sh
02-ir-output-check.sh
03-assembly-output-check.sh
04-qemu-stage2-boot.sh
```
