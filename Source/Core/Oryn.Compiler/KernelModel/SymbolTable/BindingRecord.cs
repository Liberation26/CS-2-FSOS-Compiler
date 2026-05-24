namespace Oryn.Compiler;

internal sealed record BindingRecord(
    string ModuleName,
    string NamespaceName,
    string TypeName,
    string MethodName,
    string ManagedName,
    string Signature,
    string NativeSymbol,
    IReadOnlyList<string> ArgumentTypeNames,
    int Stage,
    bool AllowedInKernel,
    string Summary)
{
    public int ArgumentCount => ArgumentTypeNames.Count;

    public string FullyQualifiedManagedName => $"{NamespaceName}.{TypeName}.{MethodName}";
}
