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
        List<BoundKernelCall> BoundCalls = new();

        foreach (KernelCallAst Call in KernelAst.Calls)
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

            BoundCalls.Add(new BoundKernelCall(Call.ManagedName, Binding.NativeSymbol, Call.Arguments));
        }

        if (BoundCalls.Count == 0)
        {
            throw new OrynCompileException("Kernel Main does not contain any Stage 2 module calls.");
        }

        return new BoundKernelModel(KernelAst.SourcePath, BoundCalls);
    }
}

internal sealed record BoundKernelModel(string SourcePath, IReadOnlyList<BoundKernelCall> Calls);

internal sealed record BoundKernelCall(string ManagedName, string NativeSymbol, IReadOnlyList<string> Arguments);
