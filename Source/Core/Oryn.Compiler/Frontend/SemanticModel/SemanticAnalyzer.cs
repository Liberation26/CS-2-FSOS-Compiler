namespace Oryn.Compiler;

internal sealed class SemanticAnalyzer
{
    private readonly BindingCatalog BindingCatalog;

    public SemanticAnalyzer(BindingCatalog BindingCatalog)
    {
        this.BindingCatalog = BindingCatalog;
    }

    public BoundKernelModel Bind(KernelAst KernelAst)
    {
        Dictionary<string, BoundLocal> Locals = new(StringComparer.Ordinal);
        List<BoundStatement> Statements = BindStatements(KernelAst.Statements, Locals);

        if (Statements.Count == 0)
        {
            throw new OrynCompileException("Kernel Main does not contain any Stage 2 statements.");
        }

        return new BoundKernelModel(KernelAst.SourcePath, Statements, Locals.Values.ToList());
    }

    private List<BoundStatement> BindStatements(IEnumerable<KernelStatementAst> Statements, Dictionary<string, BoundLocal> Locals)
    {
        List<BoundStatement> BoundStatements = new();
        foreach (KernelStatementAst Statement in Statements)
        {
            BoundStatements.Add(BindStatement(Statement, Locals));
        }

        return BoundStatements;
    }

    private BoundStatement BindStatement(KernelStatementAst Statement, Dictionary<string, BoundLocal> Locals)
    {
        return Statement switch
        {
            KernelLocalDeclarationAst Declaration => BindLocalDeclaration(Declaration, Locals),
            KernelAssignmentAst Assignment => BindAssignment(Assignment, Locals),
            KernelCallStatementAst Call => BindCall(Call, Locals),
            KernelIfAst If => new BoundIf(BindExpression(If.Condition, Locals), BindStatements(If.ThenStatements, Locals), BindStatements(If.ElseStatements, Locals)),
            KernelWhileAst While => new BoundWhile(BindExpression(While.Condition, Locals), BindStatements(While.BodyStatements, Locals)),
            KernelReturnAst => new BoundReturn(),
            _ => throw new OrynCompileException($"Unsupported Stage 2 statement node: {Statement.GetType().Name}")
        };
    }

    private BoundStatement BindLocalDeclaration(KernelLocalDeclarationAst Declaration, Dictionary<string, BoundLocal> Locals)
    {
        if (Declaration.TypeName != "Int32")
        {
            throw new OrynCompileException($"Unsupported Stage 2 local type: {Declaration.TypeName}");
        }

        if (Locals.ContainsKey(Declaration.Name))
        {
            throw new OrynCompileException($"Duplicate Stage 2 local: {Declaration.Name}");
        }

        BoundExpression? Initializer = Declaration.Initializer is null ? null : BindExpression(Declaration.Initializer, Locals);
        if (Initializer is not null && Initializer.TypeName != "Int32")
        {
            throw new OrynCompileException($"Local {Declaration.Name} requires an Int32 initializer.");
        }

        BoundLocal Local = new(Declaration.Name, "Int32");
        Locals.Add(Declaration.Name, Local);
        return new BoundLocalDeclaration(Local, Initializer);
    }

    private BoundStatement BindAssignment(KernelAssignmentAst Assignment, Dictionary<string, BoundLocal> Locals)
    {
        if (!Locals.TryGetValue(Assignment.Name, out BoundLocal? Local))
        {
            throw new OrynCompileException($"Assignment targets undeclared Stage 2 local: {Assignment.Name}");
        }

        BoundExpression Value = BindExpression(Assignment.Value, Locals);
        if (Value.TypeName != Local.TypeName)
        {
            throw new OrynCompileException($"Assignment to {Assignment.Name} expected {Local.TypeName} but received {Value.TypeName}.");
        }

        return new BoundAssignment(Local, Value);
    }

    private BoundStatement BindCall(KernelCallStatementAst Call, Dictionary<string, BoundLocal> Locals)
    {
        if (!BindingCatalog.TryResolve(Call.ManagedName, out BindingRecord? Binding) || Binding is null)
        {
            throw new OrynCompileException($"No approved Stage 2 binding for call: {Call.ManagedName}");
        }

        if (!Binding.AllowedInKernel)
        {
            throw new OrynCompileException($"Binding is not allowed in kernel code: {Call.ManagedName}");
        }

        if (Call.Arguments.Count != Binding.ArgumentCount)
        {
            throw new OrynCompileException($"Call {Call.ManagedName} expected {Binding.ArgumentCount} argument(s) but received {Call.Arguments.Count}.");
        }

        List<BoundExpression> Arguments = Call.Arguments.Select(Argument => BindExpression(Argument, Locals)).ToList();
        foreach (BoundExpression Argument in Arguments)
        {
            if (Binding.ArgumentCount > 0 && Argument.TypeName != "String")
            {
                throw new OrynCompileException($"Call {Call.ManagedName} currently accepts string arguments only.");
            }
        }

        return new BoundCall(Call.ManagedName, Binding.NativeSymbol, Arguments);
    }

    private BoundExpression BindExpression(KernelExpressionAst Expression, Dictionary<string, BoundLocal> Locals)
    {
        return Expression switch
        {
            KernelIntLiteralAst IntLiteral => new BoundIntLiteral(IntLiteral.Value),
            KernelStringLiteralAst StringLiteral => new BoundStringLiteral(StringLiteral.Value),
            KernelLocalReferenceAst LocalReference => BindLocalReference(LocalReference, Locals),
            KernelBinaryExpressionAst Binary => BindBinary(Binary, Locals),
            _ => throw new OrynCompileException($"Unsupported Stage 2 expression node: {Expression.GetType().Name}")
        };
    }

    private static BoundExpression BindLocalReference(KernelLocalReferenceAst LocalReference, Dictionary<string, BoundLocal> Locals)
    {
        if (!Locals.TryGetValue(LocalReference.Name, out BoundLocal? Local))
        {
            throw new OrynCompileException($"Use of undeclared Stage 2 local: {LocalReference.Name}");
        }

        return new BoundLocalReference(Local);
    }

    private BoundExpression BindBinary(KernelBinaryExpressionAst Binary, Dictionary<string, BoundLocal> Locals)
    {
        BoundExpression Left = BindExpression(Binary.Left, Locals);
        BoundExpression Right = BindExpression(Binary.Right, Locals);
        if (Left.TypeName != "Int32" || Right.TypeName != "Int32")
        {
            throw new OrynCompileException($"Operator {Binary.Operator} requires Int32 operands.");
        }

        return Binary.Operator switch
        {
            "+" => new BoundBinaryExpression("AddInt32", "Int32", Left, Right),
            "-" => new BoundBinaryExpression("SubInt32", "Int32", Left, Right),
            "==" => new BoundBinaryExpression("CompareEqualInt32", "Int32", Left, Right),
            "<" => new BoundBinaryExpression("CompareLessThanInt32", "Int32", Left, Right),
            _ => throw new OrynCompileException($"Unsupported Stage 2 operator: {Binary.Operator}")
        };
    }
}

internal sealed record BoundKernelModel(string SourcePath, IReadOnlyList<BoundStatement> Statements, IReadOnlyList<BoundLocal> Locals);

internal sealed record BoundLocal(string Name, string TypeName);

internal abstract record BoundStatement;

internal sealed record BoundLocalDeclaration(BoundLocal Local, BoundExpression? Initializer) : BoundStatement;

internal sealed record BoundAssignment(BoundLocal Local, BoundExpression Value) : BoundStatement;

internal sealed record BoundCall(string ManagedName, string NativeSymbol, IReadOnlyList<BoundExpression> Arguments) : BoundStatement;

internal sealed record BoundIf(BoundExpression Condition, IReadOnlyList<BoundStatement> ThenStatements, IReadOnlyList<BoundStatement> ElseStatements) : BoundStatement;

internal sealed record BoundWhile(BoundExpression Condition, IReadOnlyList<BoundStatement> BodyStatements) : BoundStatement;

internal sealed record BoundReturn() : BoundStatement;

internal abstract record BoundExpression(string TypeName);

internal sealed record BoundIntLiteral(int Value) : BoundExpression("Int32");

internal sealed record BoundStringLiteral(string Value) : BoundExpression("String");

internal sealed record BoundLocalReference(BoundLocal Local) : BoundExpression(Local.TypeName);

internal sealed record BoundBinaryExpression(string Operation, string ResultTypeName, BoundExpression Left, BoundExpression Right) : BoundExpression(ResultTypeName);
