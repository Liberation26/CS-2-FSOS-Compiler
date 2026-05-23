# Oryn Stage 2

Stage 2 is the point where Oryn starts becoming a useful C#-to-freestanding compiler rather than only a module-call proof.

## Stage 2 part 1 in version 0.2.0

This first Stage 2 delivery deliberately keeps the known-good Stage 1 native backend path and adds the Stage 2 operating-system source tree, build selection, and documentation baseline.

The Stage 2 part 1 kernel proves:

```text
Diagnostics.WriteOk lowers to the native Diagnostics module
Memory.Initialize lowers to the native Memory module
Cpu.HaltForever lowers to the native CPU halt module
Runqemu.sh can select and build OSes/Stage2 instead of only OSes/Stage1
```

## Stage 2 phase 2 in version 0.2.3

Stage 2 phase 2 creates proper compiler separation. `Program.cs` no longer contains the compiler implementation. It now delegates to a CLI and pipeline, while parser, validator, semantic model, kernel AST, symbol table, IR, CFG, and native backend code live in their own folders.

The backend proof remains intentionally conservative: the generated x64 assembly is still used to build the freestanding kernel, and the `.o` file is still a placeholder until the ELF64 object writer lands.

## Run it

From the repository root:

```bash
./Runqemu.sh Stage2
```

Stage 1 remains available as a regression proof:

```bash
./Runqemu.sh Stage1
```

## Next Stage 2 compiler work

The next Stage 2 parts should add the real language features in this order:

```text
1. Stage 2 IR records for locals and constants
2. local variable declarations and assignments
3. integer arithmetic
4. if / else lowering
5. while lowering
6. static helper method lowering
7. direct x64 assembly lowering from the richer IR
```

## About integer constants

A tiny value such as `0` can still be represented as `ConstInt32 0` in the IR because the C# source type is `int`, and `int` is a 32-bit language type. That does not mean the backend must wastefully encode or store it as a large immediate everywhere. The backend can still choose the smallest valid machine encoding while preserving the source-level type.
