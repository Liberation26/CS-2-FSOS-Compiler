using System.Text;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.X64;

internal sealed class X64CSourceEmitter
{
    public string Emit(IReadOnlyList<IrInstruction> Instructions)
    {
        StringBuilder Builder = new();
        Builder.AppendLine("#include <stdint.h>");
        Builder.AppendLine();
        Builder.AppendLine("extern void Diagnostics_WriteOk(const char* Message);");
        Builder.AppendLine("extern void Diagnostics_WriteWarn(const char* Message);");
        Builder.AppendLine("extern void Diagnostics_WriteFail(const char* Message);");
        Builder.AppendLine("extern void Memory_Initialize(void);");
        Builder.AppendLine("extern void Cpu_HaltForever(void);");
        Builder.AppendLine();

        for (int Index = 0; Index < Instructions.Count; Index++)
        {
            if (Instructions[Index].Arguments.Count == 1)
            {
                Builder.AppendLine($"static const char Oryn_String_{Index}[] = \"{NativeTextEscaper.EscapeCString(Instructions[Index].Arguments[0])}\";");
            }
        }

        Builder.AppendLine();
        Builder.AppendLine("void Kernel_Main(void)");
        Builder.AppendLine("{");
        for (int Index = 0; Index < Instructions.Count; Index++)
        {
            IrInstruction Instruction = Instructions[Index];
            if (Instruction.Arguments.Count == 1)
            {
                Builder.AppendLine($"    {Instruction.NativeSymbol}(Oryn_String_{Index});");
            }
            else
            {
                Builder.AppendLine($"    {Instruction.NativeSymbol}();");
            }
        }

        Builder.AppendLine("}");
        return Builder.ToString();
    }
}
