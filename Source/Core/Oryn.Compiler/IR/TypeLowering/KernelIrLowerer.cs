using Oryn.Compiler;
namespace Oryn.Compiler.IR.TypeLowering;

internal sealed class KernelIrLowerer
{
    private readonly List<IrInstruction> Instructions = new();
    private int LabelIndex;

    public OrynIrModule Lower(BoundKernelModel BoundModel)
    {
        Instructions.Clear();
        LabelIndex = 0;

        foreach (BoundStatement Statement in BoundModel.Statements)
        {
            LowerStatement(Statement);
        }

        if (Instructions.Count == 0 || Instructions[^1].OpCode != "Return")
        {
            Emit("Return");
        }

        return new OrynIrModule("Kernel_Main", Instructions.ToList());
    }

    private void LowerStatement(BoundStatement Statement)
    {
        switch (Statement)
        {
            case BoundLocalDeclaration Declaration:
                Emit("DeclareLocal", Operand: Declaration.Local.Name);
                if (Declaration.Initializer is not null)
                {
                    LowerExpression(Declaration.Initializer);
                    Emit("StoreLocal", Operand: Declaration.Local.Name);
                }
                break;

            case BoundAssignment Assignment:
                LowerExpression(Assignment.Value);
                Emit("StoreLocal", Operand: Assignment.Local.Name);
                break;

            case BoundCall Call:
                foreach (BoundExpression Argument in Call.Arguments)
                {
                    LowerExpression(Argument);
                }

                Emit("Call", Operand: Call.ManagedName, ManagedName: Call.ManagedName, NativeSymbol: Call.NativeSymbol, Arguments: ExtractConstantArguments(Call.Arguments));
                break;

            case BoundIf If:
                int IfLabelIndex = NextLabelIndex();
                string ElseLabel = $"IfElse{IfLabelIndex}";
                string EndLabel = $"IfEnd{IfLabelIndex}";
                LowerExpression(If.Condition);
                Emit("JumpIfFalse", Operand: ElseLabel);
                LowerStatements(If.ThenStatements);
                Emit("Jump", Operand: EndLabel);
                Emit("Label", Operand: ElseLabel);
                LowerStatements(If.ElseStatements);
                Emit("Label", Operand: EndLabel);
                break;

            case BoundWhile While:
                int LoopLabelIndex = NextLabelIndex();
                string StartLabel = $"LoopStart{LoopLabelIndex}";
                string EndWhileLabel = $"LoopEnd{LoopLabelIndex}";
                Emit("Label", Operand: StartLabel);
                LowerExpression(While.Condition);
                Emit("JumpIfFalse", Operand: EndWhileLabel);
                LowerStatements(While.BodyStatements);
                Emit("Jump", Operand: StartLabel);
                Emit("Label", Operand: EndWhileLabel);
                break;

            case BoundReturn:
                Emit("Return");
                break;

            default:
                throw new OrynCompileException($"Unsupported bound Stage 2 statement: {Statement.GetType().Name}");
        }
    }

    private void LowerStatements(IEnumerable<BoundStatement> Statements)
    {
        foreach (BoundStatement Statement in Statements)
        {
            LowerStatement(Statement);
        }
    }

    private void LowerExpression(BoundExpression Expression)
    {
        switch (Expression)
        {
            case BoundIntLiteral IntLiteral:
                Emit("ConstInt32", Int32Value: IntLiteral.Value);
                break;

            case BoundStringLiteral StringLiteral:
                Emit("ConstString", StringValue: StringLiteral.Value, Arguments: new[] { StringLiteral.Value });
                break;

            case BoundLocalReference LocalReference:
                Emit("LoadLocal", Operand: LocalReference.Local.Name);
                break;

            case BoundBinaryExpression Binary:
                LowerExpression(Binary.Left);
                LowerExpression(Binary.Right);
                Emit(Binary.Operation);
                break;

            default:
                throw new OrynCompileException($"Unsupported bound Stage 2 expression: {Expression.GetType().Name}");
        }
    }

    private static IReadOnlyList<string> ExtractConstantArguments(IReadOnlyList<BoundExpression> Arguments)
    {
        List<string> Values = new();
        foreach (BoundExpression Argument in Arguments)
        {
            if (Argument is BoundStringLiteral StringLiteral)
            {
                Values.Add(StringLiteral.Value);
            }
        }

        return Values;
    }

    private int NextLabelIndex()
    {
        int Current = LabelIndex;
        LabelIndex++;
        return Current;
    }

    private void Emit(
        string OpCode,
        string? Operand = null,
        int? Int32Value = null,
        string? StringValue = null,
        string? ManagedName = null,
        string? NativeSymbol = null,
        IReadOnlyList<string>? Arguments = null)
    {
        Instructions.Add(IrInstruction.Create(Instructions.Count, OpCode, Operand, Int32Value, StringValue, ManagedName, NativeSymbol, Arguments));
    }
}
