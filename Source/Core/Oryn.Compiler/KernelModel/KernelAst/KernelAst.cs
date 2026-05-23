namespace Oryn.Compiler;

internal sealed record KernelAst(string SourcePath, IReadOnlyList<KernelStatementAst> Statements);

internal abstract record KernelStatementAst;

internal sealed record KernelLocalDeclarationAst(string TypeName, string Name, KernelExpressionAst? Initializer) : KernelStatementAst;

internal sealed record KernelAssignmentAst(string Name, KernelExpressionAst Value) : KernelStatementAst;

internal sealed record KernelCallStatementAst(string ManagedName, IReadOnlyList<KernelExpressionAst> Arguments) : KernelStatementAst;

internal sealed record KernelIfAst(KernelExpressionAst Condition, IReadOnlyList<KernelStatementAst> ThenStatements, IReadOnlyList<KernelStatementAst> ElseStatements) : KernelStatementAst;

internal sealed record KernelWhileAst(KernelExpressionAst Condition, IReadOnlyList<KernelStatementAst> BodyStatements) : KernelStatementAst;

internal sealed record KernelReturnAst() : KernelStatementAst;

internal abstract record KernelExpressionAst;

internal sealed record KernelIntLiteralAst(int Value) : KernelExpressionAst;

internal sealed record KernelStringLiteralAst(string Value) : KernelExpressionAst;

internal sealed record KernelLocalReferenceAst(string Name) : KernelExpressionAst;

internal sealed record KernelBinaryExpressionAst(string Operator, KernelExpressionAst Left, KernelExpressionAst Right) : KernelExpressionAst;
