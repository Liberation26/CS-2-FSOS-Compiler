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
        Builder.AppendLine("void Kernel_Main(void)");
        Builder.AppendLine("{");

        Stack<string> ExpressionStack = new();
        int TemporaryIndex = 0;
        foreach (IrInstruction Instruction in Instructions)
        {
            switch (Instruction.OpCode)
            {
                case "DeclareLocal":
                    Builder.AppendLine($"    int32_t {Instruction.Operand} = 0;");
                    break;

                case "ConstInt32":
                    ExpressionStack.Push(Instruction.Int32Value?.ToString() ?? "0");
                    break;

                case "ConstString":
                    ExpressionStack.Push($"\"{NativeTextEscaper.EscapeCString(Instruction.StringValue ?? string.Empty)}\"");
                    break;

                case "LoadLocal":
                    ExpressionStack.Push(Instruction.Operand ?? string.Empty);
                    break;

                case "StoreLocal":
                    Builder.AppendLine($"    {Instruction.Operand} = {Pop(ExpressionStack, Instruction.OpCode)};");
                    break;

                case "AddInt32":
                    PushBinary(ExpressionStack, "+");
                    break;

                case "SubInt32":
                    PushBinary(ExpressionStack, "-");
                    break;

                case "CompareEqualInt32":
                    PushBinary(ExpressionStack, "==");
                    break;

                case "CompareLessThanInt32":
                    PushBinary(ExpressionStack, "<");
                    break;

                case "Call":
                    EmitCall(Builder, ExpressionStack, Instruction);
                    break;

                case "Label":
                    Builder.AppendLine($"{Instruction.Operand}:");
                    break;

                case "Jump":
                    Builder.AppendLine($"    goto {Instruction.Operand};");
                    break;

                case "JumpIfFalse":
                    string Condition = Pop(ExpressionStack, Instruction.OpCode);
                    Builder.AppendLine($"    if (!({Condition})) goto {Instruction.Operand};");
                    break;

                case "Return":
                    Builder.AppendLine("    return;");
                    break;

                default:
                    string Temporary = $"Oryn_Temporary_{TemporaryIndex++}";
                    Builder.AppendLine($"    /* Unsupported IR instruction preserved for diagnostics: {Instruction.OpCode} -> {Temporary} */");
                    break;
            }
        }

        Builder.AppendLine("}");
        return Builder.ToString();
    }

    private static void EmitCall(StringBuilder Builder, Stack<string> ExpressionStack, IrInstruction Instruction)
    {
        int ArgumentCount = Instruction.Arguments.Count;
        if (ArgumentCount == 0 && Instruction.ManagedName is not null && Instruction.ManagedName.StartsWith("Diagnostics.", StringComparison.Ordinal))
        {
            ArgumentCount = 1;
        }

        List<string> Arguments = new();
        for (int Index = 0; Index < ArgumentCount; Index++)
        {
            Arguments.Insert(0, Pop(ExpressionStack, Instruction.OpCode));
        }

        Builder.AppendLine($"    {Instruction.NativeSymbol}({string.Join(", ", Arguments)});");
    }

    private static void PushBinary(Stack<string> ExpressionStack, string Operator)
    {
        string Right = Pop(ExpressionStack, Operator);
        string Left = Pop(ExpressionStack, Operator);
        ExpressionStack.Push($"({Left} {Operator} {Right})");
    }

    private static string Pop(Stack<string> ExpressionStack, string OpCode)
    {
        if (ExpressionStack.Count == 0)
        {
            throw new OrynCompileException($"IR stack underflow while emitting C for {OpCode}.");
        }

        return ExpressionStack.Pop();
    }
}
