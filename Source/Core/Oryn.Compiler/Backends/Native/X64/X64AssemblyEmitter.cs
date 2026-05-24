using System.Text;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.X64;

internal sealed class X64AssemblyEmitter
{
    private sealed record LocalSlot(string Name, int Offset);

    private readonly Dictionary<string, string> LabelNames = new(StringComparer.Ordinal);

    public string Emit(IReadOnlyList<IrInstruction> Instructions)
    {
        LabelNames.Clear();
        Dictionary<string, LocalSlot> LocalSlots = AllocateLocals(Instructions);
        int LocalFrameSize = AlignTo(LocalSlots.Count * 8, 16);
        int EvaluationStackDepth = 0;

        StringBuilder Builder = new();
        Builder.AppendLine("# Oryn Stage 2 Phase 6 real x64 backend output.");
        Builder.AppendLine("# This file is assembled by clang/as and linked with native runtime modules.");
        Builder.AppendLine("# Locals use 64-bit stack slots addressed from rbp, for example Counter -> -8(%rbp).");
        Builder.AppendLine(".section .text");
        Builder.AppendLine(".global Kernel_Main");
        Builder.AppendLine(".type Kernel_Main, @function");
        Builder.AppendLine("Kernel_Main:");
        Builder.AppendLine("    push %rbp");
        Builder.AppendLine("    mov %rsp, %rbp");
        if (LocalFrameSize > 0)
        {
            Builder.AppendLine($"    sub ${LocalFrameSize}, %rsp");
        }

        foreach (IrInstruction Instruction in Instructions)
        {
            Builder.AppendLine($"    # IR {Instruction.Index:D3}: {FormatComment(Instruction)}");
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
                    Builder.AppendLine($"    lea {StringSymbol(Instruction)}(%rip), %rax");
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
                    EvaluationStackDepth = EmitCall(Builder, Instruction, EvaluationStackDepth);
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

        Builder.AppendLine(".size Kernel_Main, .-Kernel_Main");
        Builder.AppendLine();
        Builder.AppendLine(".section .rodata");
        foreach (IrInstruction Instruction in Instructions.Where(Instruction => Instruction.OpCode == "ConstString"))
        {
            Builder.AppendLine($"{StringSymbol(Instruction)}:");
            Builder.AppendLine($"    .asciz \"{NativeTextEscaper.EscapeCString(Instruction.StringValue ?? string.Empty)}\"");
        }

        Builder.AppendLine();
        Builder.AppendLine(".section .note.GNU-stack,\"\",@progbits");
        return Builder.ToString();
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

    private static void EmitDeclareLocal(StringBuilder Builder, IrInstruction Instruction, Dictionary<string, LocalSlot> LocalSlots)
    {
        LocalSlot Slot = RequireLocal(LocalSlots, Instruction.Operand, Instruction.OpCode);
        Builder.AppendLine($"    movq $0, {Slot.Offset}(%rbp)");
    }

    private static int EmitCall(StringBuilder Builder, IrInstruction Instruction, int EvaluationStackDepth)
    {
        string CallName = Instruction.ManagedName ?? Instruction.Operand ?? "<unknown>";
        string NativeSymbol = Instruction.NativeSymbol ?? throw new OrynCompileException($"Call IR instruction is missing native symbol for {CallName}.");
        int ArgumentCount = Instruction.Arguments.Count;
        if (ArgumentCount == 0 && Instruction.ManagedName is not null && Instruction.ManagedName.StartsWith("Diagnostics.", StringComparison.Ordinal))
        {
            ArgumentCount = 1;
        }

        if (ArgumentCount > 1)
        {
            throw new OrynCompileException($"Stage 2 x64 backend currently supports up to one native call argument: {Instruction.ManagedName ?? NativeSymbol}");
        }

        if (ArgumentCount == 1)
        {
            RequireStack(EvaluationStackDepth, Instruction.OpCode);
            Builder.AppendLine("    pop %rdi");
            EvaluationStackDepth--;
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

    private static string StringSymbol(IrInstruction Instruction)
    {
        return $"Oryn_String_{Instruction.Index:D3}";
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
