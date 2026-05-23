using System.Text;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.X64;

internal sealed class X64AssemblyEmitter
{
    public string Emit(IReadOnlyList<IrInstruction> Instructions)
    {
        StringBuilder Builder = new();
        IReadOnlyList<string> Locals = Instructions
            .Where(Instruction => Instruction.OpCode == "DeclareLocal" && !string.IsNullOrWhiteSpace(Instruction.Operand))
            .Select(Instruction => Instruction.Operand!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Builder.AppendLine(".section .text");
        Builder.AppendLine(".global Kernel_Main");
        Builder.AppendLine("Kernel_Main:");
        Builder.AppendLine("    push %rbp");
        Builder.AppendLine("    mov %rsp, %rbp");

        foreach (IrInstruction Instruction in Instructions)
        {
            Builder.AppendLine($"    # IR {Instruction.Index}: {FormatComment(Instruction)}");
            switch (Instruction.OpCode)
            {
                case "DeclareLocal":
                    break;

                case "ConstInt32":
                    Builder.AppendLine($"    mov ${Instruction.Int32Value ?? 0}, %eax");
                    Builder.AppendLine("    push %rax");
                    break;

                case "ConstString":
                    Builder.AppendLine($"    lea Oryn_String_{Instruction.Index}(%rip), %rax");
                    Builder.AppendLine("    push %rax");
                    break;

                case "LoadLocal":
                    Builder.AppendLine($"    mov Oryn_Local_{Instruction.Operand}(%rip), %eax");
                    Builder.AppendLine("    push %rax");
                    break;

                case "StoreLocal":
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine($"    mov %eax, Oryn_Local_{Instruction.Operand}(%rip)");
                    break;

                case "AddInt32":
                    Builder.AppendLine("    pop %rbx");
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine("    add %ebx, %eax");
                    Builder.AppendLine("    push %rax");
                    break;

                case "SubInt32":
                    Builder.AppendLine("    pop %rbx");
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine("    sub %ebx, %eax");
                    Builder.AppendLine("    push %rax");
                    break;

                case "CompareEqualInt32":
                    Builder.AppendLine("    pop %rbx");
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine("    cmp %ebx, %eax");
                    Builder.AppendLine("    sete %al");
                    Builder.AppendLine("    movzbl %al, %eax");
                    Builder.AppendLine("    push %rax");
                    break;

                case "CompareLessThanInt32":
                    Builder.AppendLine("    pop %rbx");
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine("    cmp %ebx, %eax");
                    Builder.AppendLine("    setl %al");
                    Builder.AppendLine("    movzbl %al, %eax");
                    Builder.AppendLine("    push %rax");
                    break;

                case "Call":
                    if (Instruction.Arguments.Count > 0 || (Instruction.ManagedName is not null && Instruction.ManagedName.StartsWith("Diagnostics.", StringComparison.Ordinal)))
                    {
                        Builder.AppendLine("    pop %rdi");
                    }

                    Builder.AppendLine($"    call {Instruction.NativeSymbol}");
                    break;

                case "Label":
                    Builder.AppendLine($"{Instruction.Operand}:");
                    break;

                case "Jump":
                    Builder.AppendLine($"    jmp {Instruction.Operand}");
                    break;

                case "JumpIfFalse":
                    Builder.AppendLine("    pop %rax");
                    Builder.AppendLine("    test %eax, %eax");
                    Builder.AppendLine($"    je {Instruction.Operand}");
                    break;

                case "Return":
                    Builder.AppendLine("    pop %rbp");
                    Builder.AppendLine("    ret");
                    break;
            }
        }

        Builder.AppendLine();
        Builder.AppendLine(".section .rodata");
        foreach (IrInstruction Instruction in Instructions.Where(Instruction => Instruction.OpCode == "ConstString"))
        {
            Builder.AppendLine($"Oryn_String_{Instruction.Index}:");
            Builder.AppendLine($"    .asciz \"{NativeTextEscaper.EscapeCString(Instruction.StringValue ?? string.Empty)}\"");
        }

        if (Locals.Count > 0)
        {
            Builder.AppendLine();
            Builder.AppendLine(".section .bss");
            Builder.AppendLine(".align 4");
            foreach (string Local in Locals)
            {
                Builder.AppendLine($"Oryn_Local_{Local}:");
                Builder.AppendLine("    .long 0");
            }
        }

        return Builder.ToString();
    }

    private static string FormatComment(IrInstruction Instruction)
    {
        string Operand = Instruction.Operand is null ? string.Empty : $" {Instruction.Operand}";
        string Value = Instruction.Int32Value is null && Instruction.StringValue is null ? string.Empty : $" value={Instruction.Int32Value?.ToString() ?? Instruction.StringValue}";
        return Instruction.OpCode + Operand + Value;
    }
}
