# Stage 1 Compiler Proof

This test proves that the first compiler stage can read the sample kernel source and emit backend files.

Expected command:

```bash
cd Source/Core/Oryn.Compiler
dotnet run -- compile Tests/Stage0/Kernel.stage0.cs --target x64-elf --output ../../../../Build/Kernel.o
```

Expected outputs:

```text
Build/Kernel.stage1.json
Build/Kernel.generated.c
Build/Kernel.generated.S
Build/Kernel.o
```

`Build/Kernel.o` is a placeholder until the ELF64 writer is implemented.
