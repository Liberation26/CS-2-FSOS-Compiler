namespace Oryn.Compiler;

internal sealed class SymbolTable
{
    private readonly IReadOnlyDictionary<string, BindingRecord> BindingsByManagedName;

    public SymbolTable(IEnumerable<BindingRecord> Bindings)
    {
        Dictionary<string, BindingRecord> Records = new(StringComparer.Ordinal);
        foreach (BindingRecord Binding in Bindings)
        {
            Records[Binding.ManagedName] = Binding;
            Records[Binding.FullyQualifiedManagedName] = Binding;
        }

        BindingsByManagedName = Records;
    }

    public bool TryResolve(string ManagedName, out BindingRecord? Binding)
    {
        return BindingsByManagedName.TryGetValue(ManagedName, out Binding);
    }

    public IReadOnlyCollection<BindingRecord> Bindings => BindingsByManagedName.Values.Distinct().OrderBy(Binding => Binding.ModuleName, StringComparer.Ordinal).ThenBy(Binding => Binding.ManagedName, StringComparer.Ordinal).ToList();
}
