# Oryn Stage 2 Test Kernel

Stage 2 is the compiler-backed freestanding kernel proof. Its source lives at:

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

For a compile-only proof without launching QEMU:

```bash
ORYN_SKIP_QEMU=1 ./Runqemu.sh Stage2
```

## What the Stage 2 kernel proves

The Stage 2 test kernel intentionally exercises the currently supported compiler and module path:

- `Diagnostics.WriteOk("...")` approved module calls,
- `Memory.Initialize()` approved module call,
- `Cpu.HaltForever()` approved module call,
- integer local variables using rbp-relative stack slots,
- integer arithmetic through `Counter = Counter + 1`,
- `while` loop lowering to labels and jumps,
- `if` / `else` branch lowering,
- static helper methods through `WriteBanner()`,
- string literal table emission through `.rodata` and `.LstrN` labels.

Diagnostics currently accepts string literals only, so the loop prints a repeated proof line instead of formatting the counter value.

Expected serial proof lines include:

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

The Stage 2 compiler/run pipeline writes generated files under:

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
```

## Current Stage 2 capabilities

Stage 2 now has:

- real Oryn IR,
- control-flow graph diagnostics,
- clang/as-compatible x64 assembly emission,
- rbp-based 64-bit local stack slots,
- `.rodata` string literal tables,
- static helper method symbols such as `Kernel_WriteBanner`,
- JSON-loaded module bindings from `Source/Sdk/Bindings/`,
- and a dedicated Stage 2 test kernel that proves the supported subset at runtime.

Direct ELF64 object writing remains a Stage 3 task.
