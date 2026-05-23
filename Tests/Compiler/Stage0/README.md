# Stage 0 Compiler Tests

Stage 0 proves the native compiler command surface and the first minimal kernel source shape.

The future Stage 0 proof command is:

```sh
dotnet run --project Source/Core/Oryn.Compiler -- compile Source/Core/Oryn.Compiler/Tests/Stage0/Kernel.stage0.cs --target x64-elf --output Build/Generated/Kernel.o
```

In version 0.0.0 this command intentionally reports that native compilation is not implemented yet. The filesystem and command surface are now present so the native object writer can be added next.
