using System.Text;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.X64;

internal sealed class X64CSourceEmitter
{
    private sealed record NativeCallExtern(string NativeSymbol, int ArgumentCount);

    public string Emit(OrynIrModule Module)
    {
        StringBuilder Builder = new();
        Builder.AppendLine("#include <stdint.h>");
        Builder.AppendLine();

        foreach (NativeCallExtern Extern in CollectNativeCallExterns(Module))
        {
            if (Extern.ArgumentCount == 0)
            {
                Builder.AppendLine($"extern void {Extern.NativeSymbol}(void);");
            }
            else if (Extern.ArgumentCount == 1)
            {
                Builder.AppendLine($"extern void {Extern.NativeSymbol}(const char* Argument0);");
            }
            else
            {
                throw new OrynCompileException($"Stage 2 C backend currently supports up to one native call argument: {Extern.NativeSymbol}");
            }
        }

        foreach (OrynIrMethod Method in Module.Methods)
        {
            Builder.AppendLine($"void {Method.NativeSymbol}(void);");
        }

        Builder.AppendLine();
        foreach (OrynIrMethod Method in Module.Methods)
        {
            EmitMethod(Builder, Method);
        }

        return Builder.ToString();
    }

    private static IReadOnlyList<NativeCallExtern> CollectNativeCallExterns(OrynIrModule Module)
    {
        Dictionary<string, NativeCallExtern> Externs = new(StringComparer.Ordinal);
        HashSet<string> MethodSymbols = Module.Methods.Select(Method => Method.NativeSymbol).ToHashSet(StringComparer.Ordinal);

        foreach (IrInstruction Instruction in Module.Methods.SelectMany(Method => Method.Instructions))
        {
            if (Instruction.OpCode != "Call" || string.IsNullOrWhiteSpace(Instruction.NativeSymbol))
            {
                continue;
            }

            if (MethodSymbols.Contains(Instruction.NativeSymbol))
            {
                continue;
            }

            int ArgumentCount = Instruction.Arguments.Count;
            if (!Externs.TryGetValue(Instruction.NativeSymbol, out NativeCallExtern? Existing))
            {
                Externs.Add(Instruction.NativeSymbol, new NativeCallExtern(Instruction.NativeSymbol, ArgumentCount));
                continue;
            }

            if (Existing.ArgumentCount != ArgumentCount)
            {
                throw new OrynCompileException($"Native binding has inconsistent argument counts in IR: {Instruction.NativeSymbol}");
            }
        }

        return Externs.Values.OrderBy(Extern => Extern.NativeSymbol, StringComparer.Ordinal).ToList();
    }

    private static void EmitMethod(StringBuilder Builder, OrynIrMethod Method)
    {
        Builder.AppendLine($"void {Method.NativeSymbol}(void)");
        Builder.AppendLine("{");

        Stack<string> ExpressionStack = new();
        int TemporaryIndex = 0;
        foreach (IrInstruction Instruction in Method.Instructions)
        {
            switch (Instruction.OpCode)
            {
                case "DeclareLocal":
                    Builder.AppendLine($"    int64_t {Instruction.Operand} = 0;");
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
        Builder.AppendLine();
    }

    private static void EmitCall(StringBuilder Builder, Stack<string> ExpressionStack, IrInstruction Instruction)
    {
        int ArgumentCount = Instruction.Arguments.Count;

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
