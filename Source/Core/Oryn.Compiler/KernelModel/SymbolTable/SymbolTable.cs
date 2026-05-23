namespace Oryn.Compiler;

internal sealed class SymbolTable
{
    private readonly IReadOnlyDictionary<string, BindingRecord> BindingsByManagedName;

    public SymbolTable(IEnumerable<BindingRecord> Bindings)
    {
        BindingsByManagedName = Bindings.ToDictionary(Binding => Binding.ManagedName, StringComparer.Ordinal);
    }

    public bool TryResolve(string ManagedName, out BindingRecord? Binding)
    {
        return BindingsByManagedName.TryGetValue(ManagedName, out Binding);
    }

    public IReadOnlyCollection<BindingRecord> Bindings => BindingsByManagedName.Values.ToList();
}
