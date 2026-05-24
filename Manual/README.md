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

## Stage 3 object inspection

Oryn 0.3.3 adds direct inspection tests for `OSes/Stage3/Build/Runqemu/Kernel.stage3.o`.

The object inspection tests check that the generated kernel object is a little-endian ELF64 x86-64 relocatable object and that it contains the expected sections, symbols, and relocations.

The checks include:

- `.text`
- `.rodata`
- `.rela.text`
- `.symtab`
- `.strtab`
- `.shstrtab`
- `.note.GNU-stack`
- `Kernel_Main`
- `Kernel_WriteBanner`
- unresolved external symbols for approved module calls
- `R_X86_64_PC32` and `R_X86_64_PLT32` relocation records

These tests help prove that Stage 3 is not merely producing a file that happens to link. It is producing a structurally valid object file that the linker can consume in the expected way.

## Compiler build policy

The Stage 3 tests do not rebuild the compiler by default. They use the existing compiler DLL.

To rebuild the compiler deliberately:

```bash
ORYN_BUILD_COMPILER=1 Tests/Compiler/Stage3/run.sh
```
