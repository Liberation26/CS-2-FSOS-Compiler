# Oryn Manual

Oryn is a C# to freestanding operating-system compiler project.

## Compiler stages

Stage 1 proves approved calls can become a bootable freestanding kernel. This means calls through approved Oryn SDK module APIs can be lowered into native freestanding code and linked into a kernel image.

Stage 2 proves Oryn can compile a useful C# subset with variables, branches, loops, helper methods, and module calls.

Stage 3 proves Oryn can write a real ELF64 relocatable object directly from Oryn IR and link that object into a bootable freestanding x86_64 kernel. The current Stage 3 kernel is the default target for `Runqemu.sh`.

## Running the Stage 3 proof

From the repository root:

```bash
./Runqemu.sh
```

or:

```bash
./Runqemu.sh Stage3
```

For compile/link/ISO generation without launching QEMU:

```bash
ORYN_SKIP_QEMU=1 ./Runqemu.sh Stage3
```

## Running Stage 3 tests

```bash
Tests/Compiler/Stage3/run.sh
```

The tests check compiler output, IR output, readable assembly reference output, direct ELF64 relocatable object creation, ELF and ISO creation, QEMU diagnostics, and the expected timeout success after the kernel halts forever.
