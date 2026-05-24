# Stage 2 Compiler Plan

## 0.2.7 Stage 2 Phase 4: Control Flow Graph

Stage 2 now builds a simple readable control-flow graph from the IR instruction stream. This is intentionally not an optimiser. It exists so labels, branches, loops, and fallthroughs are represented reliably before later backend work.

A loop such as:

```csharp
while (Counter < 3)
{
    Counter = Counter + 1;
}
```

lowers to this IR shape:

```text
Label LoopStart0
LoadLocal Counter
ConstInt32 3
CompareLessThanInt32
JumpIfFalse LoopEnd0
LoadLocal Counter
ConstInt32 1
AddInt32
StoreLocal Counter
Jump LoopStart0
Label LoopEnd0
```

The control-flow graph then records basic blocks and successor edges, for example:

```text
Entry -> LoopStart0
LoopStart0 -> LoopEnd0, Block2
Block2 -> LoopStart0
LoopEnd0 -> ...
```

The `.stage2.ir.json` manifest now includes:

```text
Instructions
ControlFlowGraph
BasicBlockCount
```

The generated diagnostics log also prints `[ CFG ]` lines so branch structure can be checked quickly during tests.

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

## 0.2.8 CFG proof visibility

The CFG proof lines are now printed in two places:

- compiler standard output, immediately after IR lowering;
- `Runqemu.sh`, by echoing the `[ OK ] [ CFG      ]` lines from `Kernel.stage2.diagnostics.log`.

This means a normal Stage 2 run should visibly show the generated basic blocks and successor edges without opening the JSON manifest or diagnostics log manually.
