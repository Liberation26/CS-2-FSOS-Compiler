# Stage 2 compiler tests

These tests prove the Stage 2 C# subset can be compiled into a useful freestanding x86_64 kernel and booted under QEMU.

Stage 1 proves approved calls can become a bootable freestanding kernel. Stage 2 proves Oryn can compile a useful C# subset with variables, branches, loops, helper methods, and module calls.

Run all Stage 2 tests with:

```bash
Tests/Compiler/Stage2/run.sh
```

## Test scripts

```text
01-compile-stage2-kernel.sh
02-ir-output-check.sh
03-assembly-output-check.sh
04-qemu-stage2-boot.sh
```

## What the tests check

The test set checks that:

- the compiler/run pipeline exits with status 0,
- the Stage 2 IR file exists,
- the generated assembly file exists,
- the ELF kernel exists,
- the bootable ISO exists,
- the IR includes locals, arithmetic, branches, loops, helper calls, and module calls,
- the x64 assembly includes stack-frame locals, `.rodata` string literals, labels, jumps, helper symbols, and native module calls,
- QEMU reaches the expected diagnostics,
- timeout after `Cpu.HaltForever()` is treated as success.

## Generated artifacts

Compile/link/ISO artifacts are written under:

```text
OSes/Stage2/Build/Runqemu/
```

Test logs are written under:

```text
Build/Compiler/Stage2/
```
