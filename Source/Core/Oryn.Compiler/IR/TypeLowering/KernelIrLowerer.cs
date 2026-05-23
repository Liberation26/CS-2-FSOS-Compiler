using Oryn.Compiler;
namespace Oryn.Compiler.IR.TypeLowering;

internal sealed class KernelIrLowerer
{
    public OrynIrModule Lower(BoundKernelModel BoundModel)
    {
        List<IrInstruction> Instructions = new();
        for (int Index = 0; Index < BoundModel.Calls.Count; Index++)
        {
            BoundKernelCall Call = BoundModel.Calls[Index];
            Instructions.Add(new IrInstruction(Index, "CallNative", Call.ManagedName, Call.NativeSymbol, Call.Arguments));
        }

        return new OrynIrModule("Kernel_Main", Instructions);
    }
}
