namespace Oryn.Compiler;

internal sealed record BindingRecord(string ModuleName, string ManagedName, string NativeSymbol, int ArgumentCount, bool AllowedInKernel);
