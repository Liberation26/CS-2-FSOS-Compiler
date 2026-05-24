# Oryn Stage 3 Test Kernel

Stage 3 is the current compiler-backed freestanding kernel proof.

Stage 1 proves approved calls can become a bootable freestanding kernel.

Stage 2 proves Oryn can compile a useful C# subset with variables, branches, loops, helper methods, and module calls.

Stage 3 proves Oryn can write a real ELF64 relocatable object directly from Oryn IR and link that object into a bootable freestanding x86_64 kernel.

The Stage 3 source lives at:

```text
OSes/Stage3/Source/Kernel.cs
```

Run it with:

```bash
./Runqemu.sh
```

or explicitly:

```bash
./Runqemu.sh Stage3
```

`Runqemu.sh` does not build the compiler unless requested. It uses the existing compiler DLL by default. To force a compiler rebuild for this run:

```bash
ORYN_BUILD_COMPILER=1 ./Runqemu.sh Stage3
```

For a compile/link/ISO proof without launching QEMU:

```bash
ORYN_SKIP_QEMU=1 ./Runqemu.sh Stage3
```

## What the Stage 3 kernel proves

The Stage 3 test kernel exercises the currently supported compiler and native module path:

- `Diagnostics.WriteOk("...")` approved module calls,
- `Memory.Initialize()` approved module call,
- `Cpu.HaltForever()` approved module call,
- integer local variables using rbp-relative 64-bit stack slots,
- integer arithmetic through `Counter = Counter + 1`,
- `while` loop lowering to labels and jumps,
- `if` / `else` branch lowering,
- static helper method calls through `WriteBanner()`,
- string literal table emission through `.rodata` and `.LstrN` labels,
- JSON-loaded module bindings from `Source/Sdk/Bindings/`,
- direct ELF64 relocatable object emission for the kernel body.

Diagnostics currently accepts string literals only, so the loop prints a repeated proof line instead of formatting the counter value.

Expected runtime proof lines include:

```text
[ OK ] Stage3 kernel entered
[ OK ] Stage3 memory initialized
[ OK ] Stage3 loop tick
[ OK ] Stage3 loop tick
[ OK ] Stage3 loop tick
[ OK ] Stage3 branch worked
[ OK ] Stage3 helper method worked
[ OK ] Stage3 kernel is halting forever
```

## Generated backend files

The Stage 3 run pipeline writes generated files under:

```text
OSes/Stage3/Build/Runqemu/
```

Important outputs are:

```text
Kernel.stage3.o
Kernel.stage3.generated.S
Kernel.stage3.generated.c
Kernel.stage3.stage3.ir.json
Kernel.stage3.diagnostics.log
OrynKernel.elf
OrynKernel.iso
Qemu.serial.log
Qemu.debugcon.log
```

`Kernel.stage3.o` is the direct ELF64 relocatable object written by Oryn. The generated `.S` file is retained as a readable reference artifact.


## Stage 3 object inspection

Oryn 0.3.3 adds structural verification for the direct ELF64 object writer. The Stage 3 test suite now checks the generated `Kernel.stage3.o` directly.

The object inspection tests prove:

- the object is ELF64, little-endian, x86-64, and relocatable,
- the section header table is present,
- `.text`, `.rodata`, `.rela.text`, `.symtab`, `.strtab`, `.shstrtab`, and `.note.GNU-stack` exist,
- `Kernel_Main` and `Kernel_WriteBanner` are defined function symbols,
- approved module calls remain unresolved external symbols until link time,
- string literals are emitted as local `.rodata` object symbols,
- relocations target valid symbols and use the currently supported x86-64 relocation kinds.

Run the object inspection tests with:

```bash
Tests/Compiler/Stage3/04-elf64-header-check.sh
Tests/Compiler/Stage3/05-elf64-section-check.sh
Tests/Compiler/Stage3/06-elf64-symbol-check.sh
Tests/Compiler/Stage3/07-elf64-relocation-check.sh
```

## Tests

The Stage 3 compiler/boot tests live under:

```text
Tests/Compiler/Stage3/
```

Run all Stage 3 tests with:

```bash
Tests/Compiler/Stage3/run.sh
```
