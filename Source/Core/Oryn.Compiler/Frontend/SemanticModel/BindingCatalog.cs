namespace Oryn.Compiler;

internal sealed class BindingCatalog
{
    private readonly SymbolTable SymbolTable;

    private BindingCatalog(SymbolTable SymbolTable)
    {
        this.SymbolTable = SymbolTable;
    }

    public IReadOnlyCollection<BindingRecord> Bindings => SymbolTable.Bindings;

    public static BindingCatalog CreateDefault()
    {
        return new BindingCatalog(new SymbolTable(new[]
        {
            new BindingRecord("Diagnostics", "Diagnostics.WriteOk", "Diagnostics_WriteOk", 1, true),
            new BindingRecord("Diagnostics", "Diagnostics.WriteWarn", "Diagnostics_WriteWarn", 1, true),
            new BindingRecord("Diagnostics", "Diagnostics.WriteFail", "Diagnostics_WriteFail", 1, true),
            new BindingRecord("Memory", "Memory.Initialize", "Memory_Initialize", 0, true),
            new BindingRecord("Cpu", "Cpu.HaltForever", "Cpu_HaltForever", 0, true)
        }));
    }

    public bool TryResolve(string ManagedName, out BindingRecord? Binding)
    {
        return SymbolTable.TryResolve(ManagedName, out Binding);
    }
}
