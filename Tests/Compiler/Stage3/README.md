# Stage 3 compiler tests

Stage 3 proves that Oryn can write a real ELF64 relocatable object directly from Oryn IR and link that object into a bootable freestanding x86_64 kernel.

The tests check:

- compiler exits 0
- IR file exists
- readable assembly reference file exists
- direct ELF64 relocatable object exists
- linked ELF exists
- bootable ISO exists
- QEMU reaches expected diagnostics
- timeout after the halted kernel is treated as success
- generated object has ELF magic and ELF64 class
- generated object is an x86-64 `ET_REL` relocatable object
- section header table exists and is well-formed
- `.text`, `.rodata`, `.rela.text`, `.symtab`, `.strtab`, `.shstrtab`, and `.note.GNU-stack` exist
- `.text` is allocated/executable and non-empty
- `.rodata` is allocated/read-only and non-empty
- `.rela.text` targets `.text` and links to `.symtab`
- `.symtab` links to `.strtab`
- `Kernel_Main` and `Kernel_WriteBanner` are defined global function symbols
- approved runtime/module calls are unresolved global external symbols
- string literals are local `.rodata` object symbols
- relocations target valid symbols
- Stage 3 uses only the supported relocation kinds currently emitted by Oryn: `R_X86_64_PC32` and `R_X86_64_PLT32`
- compiler rebuilding is not performed unless `ORYN_BUILD_COMPILER=1` is explicitly set

## Test scripts

```text
01-compile-stage3-kernel.sh
02-elf64-object-check.sh
03-qemu-stage3-boot.sh
04-elf64-header-check.sh
05-elf64-section-check.sh
06-elf64-symbol-check.sh
07-elf64-relocation-check.sh
08-stage3-no-compiler-rebuild-check.sh
run.sh
```

`02-elf64-object-check.sh` keeps the readable `readelf`-based summary check. The later `04` through `07` scripts perform stricter structural parsing with `python3` so the test output is deterministic and does not depend on `readelf` formatting.

## Compiler build policy

The Stage 3 tests do not build `Oryn.Compiler` automatically.

They use the existing compiler DLL, or `ORYN_COMPILER_DLL` if you provide one.

To rebuild the compiler before running Stage 3, do it explicitly:

```sh
ORYN_BUILD_COMPILER=1 ./Tests/Compiler/Stage3/run.sh
```

Run all Stage 3 tests without rebuilding the compiler:

```sh
./Tests/Compiler/Stage3/run.sh
```

Run only the structural object checks after a Stage 3 compile:

```sh
./Tests/Compiler/Stage3/04-elf64-header-check.sh
./Tests/Compiler/Stage3/05-elf64-section-check.sh
./Tests/Compiler/Stage3/06-elf64-symbol-check.sh
./Tests/Compiler/Stage3/07-elf64-relocation-check.sh
```
