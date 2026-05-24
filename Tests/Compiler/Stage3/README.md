# Stage 3 compiler tests

Stage 3 proves that Oryn can write a real ELF64 relocatable object directly from Oryn IR and link that object into a bootable freestanding x86_64 kernel.

The tests check:

- compiler exits 0
- IR file exists
- readable assembly reference file exists
- direct ELF64 relocatable object exists
- object contains `.text`, `.rodata`, `.rela.text`, symbols, and relocation records
- linked ELF exists
- bootable ISO exists
- QEMU reaches expected diagnostics
- timeout after the halted kernel is treated as success

Run all Stage 3 tests:

```sh
./Tests/Compiler/Stage3/run.sh
```
