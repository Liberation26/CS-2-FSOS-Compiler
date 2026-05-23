using System.Text;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.X64;

internal sealed class X64AssemblyEmitter
{
    public string Emit(IReadOnlyList<IrInstruction> Instructions)
    {
        StringBuilder Builder = new();
        Builder.AppendLine(".section .text");
        Builder.AppendLine(".global Kernel_Main");
        Builder.AppendLine("Kernel_Main:");
        Builder.AppendLine("    push %rbp");
        Builder.AppendLine("    mov %rsp, %rbp");

        for (int Index = 0; Index < Instructions.Count; Index++)
        {
            IrInstruction Instruction = Instructions[Index];
            if (Instruction.Arguments.Count == 1)
            {
                Builder.AppendLine($"    lea Oryn_String_{Index}(%rip), %rdi");
            }

            Builder.AppendLine($"    call {Instruction.NativeSymbol}");
        }

        Builder.AppendLine("    pop %rbp");
        Builder.AppendLine("    ret");
        Builder.AppendLine();
        Builder.AppendLine(".section .rodata");

        for (int Index = 0; Index < Instructions.Count; Index++)
        {
            if (Instructions[Index].Arguments.Count == 1)
            {
                Builder.AppendLine($"Oryn_String_{Index}:");
                Builder.AppendLine($"    .asciz \"{NativeTextEscaper.EscapeCString(Instructions[Index].Arguments[0])}\"");
            }
        }

        return Builder.ToString();
    }
}
