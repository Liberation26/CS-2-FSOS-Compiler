namespace Oryn.Compiler;

internal sealed record OrynIrModule(string EntrySymbol, IReadOnlyList<IrInstruction> Instructions);

internal sealed record IrInstruction(int Index, string OpCode, string ManagedName, string NativeSymbol, IReadOnlyList<string> Arguments);
