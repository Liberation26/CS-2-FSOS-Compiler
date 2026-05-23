namespace Oryn.Compiler;

internal sealed record OrynIrModule(string EntrySymbol, IReadOnlyList<IrInstruction> Instructions);

internal sealed record IrInstruction(
    int Index,
    string OpCode,
    string? Operand,
    int? Int32Value,
    string? StringValue,
    string? ManagedName,
    string? NativeSymbol,
    IReadOnlyList<string> Arguments)
{
    public static IrInstruction Create(
        int Index,
        string OpCode,
        string? Operand = null,
        int? Int32Value = null,
        string? StringValue = null,
        string? ManagedName = null,
        string? NativeSymbol = null,
        IReadOnlyList<string>? Arguments = null)
    {
        return new IrInstruction(Index, OpCode, Operand, Int32Value, StringValue, ManagedName, NativeSymbol, Arguments ?? Array.Empty<string>());
    }
}
