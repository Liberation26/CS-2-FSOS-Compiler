using Oryn.Compiler;
namespace Oryn.Compiler.IR.ControlFlowGraph;

internal sealed class ControlFlowGraphBuilder
{
    public OrynControlFlowGraph Build(OrynIrModule Module)
    {
        List<OrynBasicBlock> Blocks = new()
        {
            new OrynBasicBlock("entry", Module.Instructions)
        };

        return new OrynControlFlowGraph(Module.EntrySymbol, Blocks);
    }
}

internal sealed record OrynControlFlowGraph(string EntrySymbol, IReadOnlyList<OrynBasicBlock> Blocks);

internal sealed record OrynBasicBlock(string Name, IReadOnlyList<IrInstruction> Instructions);
