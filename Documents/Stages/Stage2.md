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

The generated C reference output and real x64 assembly backend now consume the same IR stream.

## 0.2.8 CFG proof visibility

The CFG proof lines are now printed in two places:

- compiler standard output, immediately after IR lowering;
- `Runqemu.sh`, by echoing the `[ OK ] [ CFG      ]` lines from `Kernel.stage2.diagnostics.log`.

This means a normal Stage 2 run should visibly show the generated basic blocks and successor edges without opening the JSON manifest or diagnostics log manually.

## 0.2.9 Stage 2 Phase 5: Real x64 backend

Stage 2 Phase 5 generates real x64 assembly from Oryn IR. The backend emits `Kernel.generated.S` using a simple stack-style evaluation model, stack-frame backed local variables, labels, jumps, conditional jumps, integer arithmetic, integer comparisons, and calls to the native runtime symbols exposed by the binding catalogue.

The current build flow is:

```text
Kernel.cs
  -> Oryn IR
  -> Kernel.generated.S
  -> clang -c Kernel.generated.S
  -> link with native modules
  -> OrynKernel.elf
  -> GRUB ISO
  -> QEMU
```

The C backend remains useful for inspection, but the Stage 2 kernel object now comes from the generated assembly. Writing ELF64 object files directly is reserved for Stage 3.


## 0.2.10 Stage 2 Phase 5 syntax fix

The `0.2.10` package corrects invalid C# string escaping in the Phase 5 x64 assembly emitter. The fix is intentionally small: it preserves the IR-to-x64 lowering path from `0.2.9` and only changes the diagnostic construction that prevented `Oryn.Compiler` from compiling.


## Phase 6 - Stack/local variable model

Stage 2 now emits an explicit rbp-based stack frame for `Kernel_Main` in the x64 backend. Each declared integer local is assigned a 64-bit slot even when the source type is `int`, keeping code generation simple and predictable for the current backend.

The generated method shape is:

```asm
Kernel_Main:
    push %rbp
    mov %rsp, %rbp
    sub $N, %rsp
    # locals and calls
    leave
    ret
```

Local slot examples are emitted as readable assembly comments and instructions, such as `Counter -> -8(%rbp)`.

## String literal table

Stage 2 now emits string literals into a dedicated `.rodata` table in the generated x64 assembly. Distinct literals are assigned stable assembler-local labels in first-use order:

```asm
.section .rodata
.Lstr0:
    .asciz "Stage2 kernel entered"
```

Approved module calls that take a string, such as diagnostics calls, load the literal address with RIP-relative addressing before calling the native module binding:

```asm
lea .Lstr0(%rip), %rdi
call Diagnostics_WriteOk
```

This keeps Stage 2 freestanding: generated kernels do not depend on hosted string storage, and native module calls receive explicit pointers to immutable kernel text data.
