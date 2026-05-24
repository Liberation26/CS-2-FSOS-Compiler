# Oryn Stage 2 Test Kernel

Stage 2 is the current compiler-backed freestanding kernel proof.

Stage 1 proves approved calls can become a bootable freestanding kernel. Stage 2 proves Oryn can compile a useful C# subset with variables, branches, loops, helper methods, and module calls.

The Stage 2 source lives at:

```text
OSes/Stage2/Source/Kernel.cs
```

Run it with:

```bash
./Runqemu.sh
```

or explicitly:

```bash
./Runqemu.sh Stage2
```

For a compile/link/ISO proof without launching QEMU:

```bash
ORYN_SKIP_QEMU=1 ./Runqemu.sh Stage2
```

## What the Stage 2 kernel proves

The Stage 2 test kernel exercises the currently supported compiler and native module path:

- `Diagnostics.WriteOk("...")` approved module calls,
- `Memory.Initialize()` approved module call,
- `Cpu.HaltForever()` approved module call,
- integer local variables using rbp-relative 64-bit stack slots,
- integer arithmetic through `Counter = Counter + 1`,
- `while` loop lowering to labels and jumps,
- `if` / `else` branch lowering,
- static helper method calls through `WriteBanner()`,
- string literal table emission through `.rodata` and `.LstrN` labels,
- JSON-loaded module bindings from `Source/Sdk/Bindings/`.

Diagnostics currently accepts string literals only, so the loop prints a repeated proof line instead of formatting the counter value.

Expected runtime proof lines include:

```text
[ OK ] Stage2 kernel entered
[ OK ] Stage2 memory initialized
[ OK ] Stage2 loop tick
[ OK ] Stage2 loop tick
[ OK ] Stage2 loop tick
[ OK ] Stage2 branch worked
[ OK ] Stage2 helper method worked
[ OK ] Stage2 kernel is halting forever
```

## Generated backend files

The Stage 2 run pipeline writes generated files under:

```text
OSes/Stage2/Build/Runqemu/
```

Important outputs are:

```text
Kernel.stage2.generated.S
Kernel.stage2.generated.c
Kernel.stage2.stage2.ir.json
Kernel.stage2.diagnostics.log
OrynKernel.elf
OrynKernel.iso
Qemu.serial.log
Qemu.debugcon.log
```

## Tests

The Stage 2 compiler/boot tests live under:

```text
Tests/Compiler/Stage2/
```

Run all Stage 2 tests with:

```bash
Tests/Compiler/Stage2/run.sh
```

Direct ELF64 object writing remains a Stage 3 task.
