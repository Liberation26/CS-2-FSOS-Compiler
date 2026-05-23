namespace Oryn.Compiler;

internal sealed record KernelAst(string SourcePath, IReadOnlyList<KernelCallAst> Calls);

internal sealed record KernelCallAst(string ManagedName, IReadOnlyList<string> Arguments);
