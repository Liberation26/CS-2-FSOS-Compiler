namespace Oryn.Compiler;

internal sealed class SemanticAnalyzer
{
    private readonly BindingCatalog BindingCatalog;
    private readonly ModuleApiContractCatalog ApiContracts;

    public SemanticAnalyzer(BindingCatalog BindingCatalog, ModuleApiContractCatalog ApiContracts)
    {
        this.BindingCatalog = BindingCatalog;
        this.ApiContracts = ApiContracts;
    }

    public BoundKernelModel Bind(KernelAst KernelAst)
    {
        Dictionary<string, KernelMethodAst> KernelMethods = KernelAst.Methods.ToDictionary(Method => "Kernel." + Method.Name, Method => Method, StringComparer.Ordinal);
        List<BoundKernelMethod> Methods = new();

        foreach (KernelMethodAst Method in KernelAst.Methods)
        {
            Methods.Add(BindMethod(Method, KernelMethods));
        }

        BoundKernelMethod? MainMethod = Methods.FirstOrDefault(Method => Method.Name == "Main" && Method.IsPublic);
        if (MainMethod is null)
        {
            throw new OrynCompileException("Kernel Main does not contain any Stage 2 statements.");
        }

        return new BoundKernelModel(KernelAst.SourcePath, MainMethod, Methods);
    }

    private BoundKernelMethod BindMethod(KernelMethodAst Method, IReadOnlyDictionary<string, KernelMethodAst> KernelMethods)
    {
        Dictionary<string, BoundLocal> Locals = new(StringComparer.Ordinal);
        List<BoundStatement> Statements = BindStatements(Method.Statements, Locals, KernelMethods);

        if (Statements.Count == 0)
        {
            throw new OrynCompileException($"Kernel method {Method.Name} does not contain any Stage 2 statements.");
        }

        return new BoundKernelMethod(Method.Name, Method.NativeSymbol, Method.IsPublic, Statements, Locals.Values.ToList());
    }

    private List<BoundStatement> BindStatements(IEnumerable<KernelStatementAst> Statements, Dictionary<string, BoundLocal> Locals, IReadOnlyDictionary<string, KernelMethodAst> KernelMethods)
    {
        List<BoundStatement> BoundStatements = new();
        foreach (KernelStatementAst Statement in Statements)
        {
            BoundStatements.Add(BindStatement(Statement, Locals, KernelMethods));
        }

        return BoundStatements;
    }

    private BoundStatement BindStatement(KernelStatementAst Statement, Dictionary<string, BoundLocal> Locals, IReadOnlyDictionary<string, KernelMethodAst> KernelMethods)
    {
        return Statement switch
        {
            KernelLocalDeclarationAst Declaration => BindLocalDeclaration(Declaration, Locals, KernelMethods),
            KernelAssignmentAst Assignment => BindAssignment(Assignment, Locals, KernelMethods),
            KernelCallStatementAst Call => BindCall(Call, Locals, KernelMethods),
            KernelIfAst If => new BoundIf(BindExpression(If.Condition, Locals), BindStatements(If.ThenStatements, Locals, KernelMethods), BindStatements(If.ElseStatements, Locals, KernelMethods)),
            KernelWhileAst While => new BoundWhile(BindExpression(While.Condition, Locals), BindStatements(While.BodyStatements, Locals, KernelMethods)),
            KernelReturnAst => new BoundReturn(),
            _ => throw new OrynCompileException($"Unsupported Stage 2 statement node: {Statement.GetType().Name}")
        };
    }

    private BoundStatement BindLocalDeclaration(KernelLocalDeclarationAst Declaration, Dictionary<string, BoundLocal> Locals, IReadOnlyDictionary<string, KernelMethodAst> KernelMethods)
    {
        _ = KernelMethods;
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

    private BoundStatement BindAssignment(KernelAssignmentAst Assignment, Dictionary<string, BoundLocal> Locals, IReadOnlyDictionary<string, KernelMethodAst> KernelMethods)
    {
        _ = KernelMethods;
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

    private BoundStatement BindCall(KernelCallStatementAst Call, Dictionary<string, BoundLocal> Locals, IReadOnlyDictionary<string, KernelMethodAst> KernelMethods)
    {
        if (BindingCatalog.TryResolve(Call.ManagedName, out BindingRecord? Binding) && Binding is not null)
        {
            if (!Binding.AllowedInKernel)
            {
                throw new OrynCompileException($"Stage 4 module boundary rejected call: {Call.ManagedName}. The method exists in the catalogue but is not approved for safe kernel code.");
            }

            if (Call.Arguments.Count != Binding.ArgumentCount)
            {
                throw new OrynCompileException($"Stage 4 module boundary rejected call: {Call.ManagedName}. Expected {Binding.ArgumentCount} argument(s) for {Binding.Signature} but received {Call.Arguments.Count}.");
            }

            List<BoundExpression> Arguments = Call.Arguments.Select(Argument => BindExpression(Argument, Locals)).ToList();
            for (int Index = 0; Index < Arguments.Count; Index++)
            {
                string ExpectedType = Binding.ArgumentTypeNames[Index];
                string ActualType = Arguments[Index].TypeName;
                if (!ExpectedType.Equals(ActualType, StringComparison.Ordinal))
                {
                    throw new OrynCompileException($"Stage 4 module boundary rejected call: {Call.ManagedName}. Argument {Index + 1} expected {ExpectedType} but received {ActualType}.");
                }
            }

            ApiContracts.RequireApprovedContract(Binding);
            return new BoundCall(Call.ManagedName, Binding.NativeSymbol, Arguments, false);
        }

        if (KernelMethods.TryGetValue(Call.ManagedName, out KernelMethodAst? Method))
        {
            if (Call.Arguments.Count != 0)
            {
                throw new OrynCompileException($"Static helper method calls currently require zero arguments: {Call.ManagedName}");
            }

            return new BoundCall(Call.ManagedName, Method.NativeSymbol, Array.Empty<BoundExpression>(), true);
        }

        throw new OrynCompileException($"Stage 4 module boundary rejected call: {Call.ManagedName}. Add it to Source/Sdk/Bindings with allowedInKernel=true before safe user-facing kernel code can call it.");
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

internal sealed record BoundKernelModel(string SourcePath, BoundKernelMethod MainMethod, IReadOnlyList<BoundKernelMethod> Methods)
{
    public IReadOnlyList<BoundStatement> Statements => MainMethod.Statements;
    public IReadOnlyList<BoundLocal> Locals => MainMethod.Locals;

    public int ApprovedModuleCallCount => Methods.Sum(Method => CountApprovedModuleCalls(Method.Statements));

    private static int CountApprovedModuleCalls(IEnumerable<BoundStatement> Statements)
    {
        int Count = 0;
        foreach (BoundStatement Statement in Statements)
        {
            Count += Statement switch
            {
                BoundCall Call when !Call.IsStaticHelperCall => 1,
                BoundIf If => CountApprovedModuleCalls(If.ThenStatements) + CountApprovedModuleCalls(If.ElseStatements),
                BoundWhile While => CountApprovedModuleCalls(While.BodyStatements),
                _ => 0
            };
        }

        return Count;
    }
}

internal sealed record BoundKernelMethod(string Name, string NativeSymbol, bool IsPublic, IReadOnlyList<BoundStatement> Statements, IReadOnlyList<BoundLocal> Locals);

internal sealed record BoundLocal(string Name, string TypeName);

internal abstract record BoundStatement;

internal sealed record BoundLocalDeclaration(BoundLocal Local, BoundExpression? Initializer) : BoundStatement;

internal sealed record BoundAssignment(BoundLocal Local, BoundExpression Value) : BoundStatement;

internal sealed record BoundCall(string ManagedName, string NativeSymbol, IReadOnlyList<BoundExpression> Arguments, bool IsStaticHelperCall) : BoundStatement;

internal sealed record BoundIf(BoundExpression Condition, IReadOnlyList<BoundStatement> ThenStatements, IReadOnlyList<BoundStatement> ElseStatements) : BoundStatement;

internal sealed record BoundWhile(BoundExpression Condition, IReadOnlyList<BoundStatement> BodyStatements) : BoundStatement;

internal sealed record BoundReturn() : BoundStatement;

internal abstract record BoundExpression(string TypeName);

internal sealed record BoundIntLiteral(int Value) : BoundExpression("Int32");

internal sealed record BoundStringLiteral(string Value) : BoundExpression("String");

internal sealed record BoundLocalReference(BoundLocal Local) : BoundExpression(Local.TypeName);

internal sealed record BoundBinaryExpression(string Operation, string ResultTypeName, BoundExpression Left, BoundExpression Right) : BoundExpression(ResultTypeName);
