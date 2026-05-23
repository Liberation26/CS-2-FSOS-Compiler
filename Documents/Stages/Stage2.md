# Stage 2 Compiler Plan

## 0.2.6 Stage 2 Phase 3: Real Oryn IR

Stage 2 now has a real intermediate representation between the bound kernel model and native backend emission.

The IR is deliberately simple, explicit, and stack-style. For example:

```csharp
int Counter = 0;
Counter = Counter + 1;
Diagnostics.WriteOk("Done");
```

lowers to the same shape as:

```text
DeclareLocal Counter
ConstInt32 0
StoreLocal Counter
LoadLocal Counter
ConstInt32 1
AddInt32
StoreLocal Counter
ConstString "Done"
Call Diagnostics.WriteOk -> Diagnostics_WriteOk
Return
```

Implemented minimum instructions:

```text
Label
Jump
JumpIfFalse
Return
Call
DeclareLocal
LoadLocal
StoreLocal
ConstInt32
ConstString
AddInt32
SubInt32
CompareEqualInt32
CompareLessThanInt32
```

Supported Stage 2 source constructs now include:

- `int` local declarations.
- local assignments.
- `+` and `-` Int32 expressions.
- `==` and `<` Int32 comparisons.
- `if` / `else` blocks.
- `while` loops.
- `return;`.
- approved module calls such as `Diagnostics.WriteOk("Done");`, `Memory.Initialize();`, and `Cpu.HaltForever();`.

The compiler writes the lowered manifest to:

```text
<output>.stage2.ir.json
```

The generated C and x64 assembly backend sketches now consume the same IR stream.
