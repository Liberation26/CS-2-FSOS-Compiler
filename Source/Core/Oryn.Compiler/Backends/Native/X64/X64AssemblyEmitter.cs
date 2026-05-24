using System.Text;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.X64;

internal sealed class X64AssemblyEmitter
{
    private sealed record LocalSlot(string Name, int Offset);
    private sealed record StringLiteral(string Value, string Symbol);

    private readonly Dictionary<string, string> LabelNames = new(StringComparer.Ordinal);

    public string Emit(OrynIrModule Module)
    {
        LabelNames.Clear();
        IReadOnlyList<StringLiteral> StringLiterals = BuildStringLiteralTable(Module.Methods.SelectMany(Method => Method.Instructions).ToList());
        Dictionary<string, string> StringSymbols = StringLiterals.ToDictionary(Literal => Literal.Value, Literal => Literal.Symbol, StringComparer.Ordinal);

        StringBuilder Builder = new();
        Builder.AppendLine("# Oryn Stage 2 string-literal-table x64 backend output.");
        Builder.AppendLine("# This file is assembled by clang/as and linked with native runtime modules.");
        Builder.AppendLine("# Locals use 64-bit stack slots addressed from rbp, for example Counter -> -8(%rbp).");
        Builder.AppendLine("# String literals are emitted once into .rodata as .LstrN labels.");
        Builder.AppendLine(".section .text");
        foreach (OrynIrMethod Method in Module.Methods)
        {
            EmitMethod(Builder, Method, StringSymbols);
        }

        Builder.AppendLine(".section .rodata");
        foreach (StringLiteral Literal in StringLiterals)
        {
            Builder.AppendLine($"{Literal.Symbol}:");
            Builder.AppendLine($"    .asciz \"{NativeTextEscaper.EscapeCString(Literal.Value)}\"");
        }

        Builder.AppendLine();
        Builder.AppendLine(".section .note.GNU-stack,\"\",@progbits");
        return Builder.ToString();
    }

    private void EmitMethod(StringBuilder Builder, OrynIrMethod Method, IReadOnlyDictionary<string, string> StringSymbols)
    {
        IReadOnlyList<IrInstruction> Instructions = Method.Instructions;
        Dictionary<string, LocalSlot> LocalSlots = AllocateLocals(Instructions);
        int LocalFrameSize = AlignTo(LocalSlots.Count * 8, 16);
        int EvaluationStackDepth = 0;

        Builder.AppendLine($".global {Method.NativeSymbol}");
        Builder.AppendLine($".type {Method.NativeSymbol}, @function");
        Builder.AppendLine($"{Method.NativeSymbol}:");
        Builder.AppendLine("    push %rbp");
        Builder.AppendLine("    mov %rsp, %rbp");
        if (LocalFrameSize > 0)
        {
            Builder.AppendLine($"    sub ${LocalFrameSize}, %rsp");
        }

        for (int InstructionIndex = 0; InstructionIndex < Instructions.Count; InstructionIndex++)
        {
            IrInstruction Instruction = Instructions[InstructionIndex];
            Builder.AppendLine($"    # IR {Instruction.Index:D3}: {FormatComment(Instruction)}");

            if (CanFoldStringLiteralIntoFollowingCall(Instructions, InstructionIndex))
            {
                Builder.AppendLine("    # folded into following call argument load");
                continue;
            }

            switch (Instruction.OpCode)
            {
                case "DeclareLocal":
                    EmitDeclareLocal(Builder, Instruction, LocalSlots);
                    break;

                case "ConstInt32":
                    Builder.AppendLine($"    mov ${Instruction.Int32Value ?? 0}, %eax");
                    Builder.AppendLine("    push %rax");
                    EvaluationStackDepth++;
                    break;

                case "ConstString":
                    Builder.AppendLine($"    lea {StringSymbol(StringSymbols, Instruction.StringValue)}(%rip), %rax");
                    Builder.AppendLine("    push %rax");
                    EvaluationStackDepth++;
                    break;

                case "LoadLocal":
                    LocalSlot LoadSlot = RequireLocal(LocalSlots, Instruction.Operand, Instruction.OpCode);
                    Builder.AppendLine($"    mov {LoadSlot.Offset}(%rbp), %rax");
                    Builder.AppendLine("    push %rax");
                    EvaluationStackDepth++;
                    break;

                case "StoreLocal":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode);
                    LocalSlot StoreSlot = RequireLocal(LocalSlots, Instruction.Operand, Instruction.OpCode);
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine($"    mov %rax, {StoreSlot.Offset}(%rbp)");
                    EvaluationStackDepth--;
                    break;

                case "AddInt32":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode, 2);
                    Builder.AppendLine("    pop %rbx");
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine("    add %ebx, %eax");
                    Builder.AppendLine("    push %rax");
                    EvaluationStackDepth--;
                    break;

                case "SubInt32":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode, 2);
                    Builder.AppendLine("    pop %rbx");
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine("    sub %ebx, %eax");
                    Builder.AppendLine("    push %rax");
                    EvaluationStackDepth--;
                    break;

                case "CompareEqualInt32":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode, 2);
                    Builder.AppendLine("    pop %rbx");
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine("    cmp %ebx, %eax");
                    Builder.AppendLine("    sete %al");
                    Builder.AppendLine("    movzbl %al, %eax");
                    Builder.AppendLine("    push %rax");
                    EvaluationStackDepth--;
                    break;

                case "CompareLessThanInt32":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode, 2);
                    Builder.AppendLine("    pop %rbx");
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine("    cmp %ebx, %eax");
                    Builder.AppendLine("    setl %al");
                    Builder.AppendLine("    movzbl %al, %eax");
                    Builder.AppendLine("    push %rax");
                    EvaluationStackDepth--;
                    break;

                case "Call":
                    bool PreviousFoldedString = InstructionIndex > 0 && CanFoldStringLiteralIntoFollowingCall(Instructions, InstructionIndex - 1);
                    EvaluationStackDepth = EmitCall(Builder, Instruction, EvaluationStackDepth, StringSymbols, PreviousFoldedString);
                    break;

                case "Label":
                    Builder.AppendLine($"{LabelSymbol(Instruction.Operand)}:");
                    break;

                case "Jump":
                    Builder.AppendLine($"    jmp {LabelSymbol(Instruction.Operand)}");
                    break;

                case "JumpIfFalse":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode);
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine("    test %eax, %eax");
                    Builder.AppendLine($"    je {LabelSymbol(Instruction.Operand)}");
                    EvaluationStackDepth--;
                    break;

                case "Return":
                    EmitReturn(Builder, LocalFrameSize);
                    break;

                default:
                    throw new OrynCompileException($"Unsupported IR instruction for Stage 2 x64 backend: {Instruction.OpCode}");
            }
        }

        Builder.AppendLine($".size {Method.NativeSymbol}, .-{Method.NativeSymbol}");
        Builder.AppendLine();
    }

    private static Dictionary<string, LocalSlot> AllocateLocals(IReadOnlyList<IrInstruction> Instructions)
    {
        Dictionary<string, LocalSlot> Slots = new(StringComparer.Ordinal);
        foreach (IrInstruction Instruction in Instructions)
        {
            if (Instruction.OpCode != "DeclareLocal" || string.IsNullOrWhiteSpace(Instruction.Operand))
            {
                continue;
            }

            if (!Slots.ContainsKey(Instruction.Operand))
            {
                int Offset = -8 * (Slots.Count + 1);
                Slots.Add(Instruction.Operand, new LocalSlot(Instruction.Operand, Offset));
            }
        }

        return Slots;
    }

    private static IReadOnlyList<StringLiteral> BuildStringLiteralTable(IReadOnlyList<IrInstruction> Instructions)
    {
        List<StringLiteral> Literals = new();
        HashSet<string> Seen = new(StringComparer.Ordinal);
        foreach (IrInstruction Instruction in Instructions)
        {
            if (Instruction.OpCode != "ConstString")
            {
                continue;
            }

            string Value = Instruction.StringValue ?? string.Empty;
            if (Seen.Add(Value))
            {
                Literals.Add(new StringLiteral(Value, $".Lstr{Literals.Count}"));
            }
        }

        return Literals;
    }

    private static void EmitDeclareLocal(StringBuilder Builder, IrInstruction Instruction, Dictionary<string, LocalSlot> LocalSlots)
    {
        LocalSlot Slot = RequireLocal(LocalSlots, Instruction.Operand, Instruction.OpCode);
        Builder.AppendLine($"    movq $0, {Slot.Offset}(%rbp)");
    }

    private static int EmitCall(
        StringBuilder Builder,
        IrInstruction Instruction,
        int EvaluationStackDepth,
        IReadOnlyDictionary<string, string> StringSymbols,
        bool PreviousFoldedString)
    {
        string CallName = Instruction.ManagedName ?? Instruction.Operand ?? "<unknown>";
        string NativeSymbol = Instruction.NativeSymbol ?? throw new OrynCompileException($"Call IR instruction is missing native symbol for {CallName}.");
        int ArgumentCount = Instruction.Arguments.Count;

        if (ArgumentCount > 1)
        {
            throw new OrynCompileException($"Stage 2 x64 backend currently supports up to one native call argument: {Instruction.ManagedName ?? NativeSymbol}");
        }

        if (ArgumentCount == 1)
        {
            if (PreviousFoldedString)
            {
                Builder.AppendLine($"    lea {StringSymbol(StringSymbols, Instruction.Arguments[0])}(%rip), %rdi");
            }
            else
            {
                RequireStack(EvaluationStackDepth, Instruction.OpCode);
                Builder.AppendLine("    pop %rdi");
                EvaluationStackDepth--;
            }
        }

        bool NeedsAlignmentSlot = (EvaluationStackDepth % 2) != 0;
        if (NeedsAlignmentSlot)
        {
            Builder.AppendLine("    sub $8, %rsp");
        }

        Builder.AppendLine($"    call {NativeSymbol}");

        if (NeedsAlignmentSlot)
        {
            Builder.AppendLine("    add $8, %rsp");
        }

        return EvaluationStackDepth;
    }

    private static void EmitReturn(StringBuilder Builder, int LocalFrameSize)
    {
        _ = LocalFrameSize;
        Builder.AppendLine("    leave");
        Builder.AppendLine("    ret");
    }

    private static LocalSlot RequireLocal(Dictionary<string, LocalSlot> LocalSlots, string? Name, string OpCode)
    {
        if (string.IsNullOrWhiteSpace(Name) || !LocalSlots.TryGetValue(Name, out LocalSlot? Slot))
        {
            string LocalName = Name ?? "<null>";
            throw new OrynCompileException($"Unknown local for {OpCode}: {LocalName}");
        }

        return Slot;
    }

    private static void RequireStack(int EvaluationStackDepth, string OpCode, int Required = 1)
    {
        if (EvaluationStackDepth < Required)
        {
            throw new OrynCompileException($"IR stack underflow while emitting Stage 2 x64 for {OpCode}.");
        }
    }

    private string LabelSymbol(string? Label)
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new OrynCompileException("Missing label operand in Stage 2 x64 backend.");
        }

        if (!LabelNames.TryGetValue(Label, out string? Symbol))
        {
            Symbol = "Oryn_Label_" + SanitizeSymbol(Label);
            LabelNames.Add(Label, Symbol);
        }

        return Symbol;
    }

    private static string StringSymbol(IReadOnlyDictionary<string, string> StringSymbols, string? Value)
    {
        string LiteralValue = Value ?? string.Empty;
        if (!StringSymbols.TryGetValue(LiteralValue, out string? Symbol))
        {
            throw new OrynCompileException($"String literal was not registered in the Stage 2 x64 literal table: {LiteralValue}");
        }

        return Symbol;
    }

    private static bool CanFoldStringLiteralIntoFollowingCall(IReadOnlyList<IrInstruction> Instructions, int InstructionIndex)
    {
        IrInstruction Instruction = Instructions[InstructionIndex];
        if (Instruction.OpCode != "ConstString" || InstructionIndex + 1 >= Instructions.Count)
        {
            return false;
        }

        IrInstruction NextInstruction = Instructions[InstructionIndex + 1];
        return NextInstruction.OpCode == "Call" &&
            NextInstruction.Arguments.Count == 1 &&
            string.Equals(Instruction.StringValue ?? string.Empty, NextInstruction.Arguments[0], StringComparison.Ordinal);
    }

    private static string SanitizeSymbol(string Value)
    {
        StringBuilder Builder = new();
        foreach (char Character in Value)
        {
            if ((Character >= 'A' && Character <= 'Z') ||
                (Character >= 'a' && Character <= 'z') ||
                (Character >= '0' && Character <= '9') ||
                Character == '_')
            {
                Builder.Append(Character);
            }
            else
            {
                Builder.Append('_');
            }
        }

        return Builder.ToString();
    }

    private static int AlignTo(int Value, int Alignment)
    {
        if (Value == 0)
        {
            return 0;
        }

        int Remainder = Value % Alignment;
        return Remainder == 0 ? Value : Value + Alignment - Remainder;
    }

    private static string FormatComment(IrInstruction Instruction)
    {
        string Operand = Instruction.Operand is null ? string.Empty : $" {Instruction.Operand}";
        string Value = Instruction.Int32Value is null && Instruction.StringValue is null ? string.Empty : $" value={Instruction.Int32Value?.ToString() ?? Instruction.StringValue}";
        string Native = string.IsNullOrWhiteSpace(Instruction.NativeSymbol) ? string.Empty : $" native={Instruction.NativeSymbol}";
        return Instruction.OpCode + Operand + Value + Native;
    }
}
