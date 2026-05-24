using Oryn.Compiler;

namespace Oryn.Compiler.IR.ControlFlowGraph;

internal sealed class ControlFlowGraphBuilder
{
    public OrynControlFlowGraph Build(OrynIrModule Module)
    {
        if (Module.Instructions.Count == 0)
        {
            return new OrynControlFlowGraph(Module.EntrySymbol, Array.Empty<OrynBasicBlock>());
        }

        Dictionary<string, int> LabelIndexes = BuildLabelIndex(Module.Instructions);
        SortedSet<int> LeaderIndexes = FindLeaderIndexes(Module.Instructions, LabelIndexes);
        List<int> Leaders = LeaderIndexes.OrderBy(Index => Index).ToList();
        List<OrynBasicBlock> Blocks = new();
        for (int Index = 0; Index < Leaders.Count; Index++)
        {
            int StartIndex = Leaders[Index];
            int EndIndex = Index + 1 < Leaders.Count ? Leaders[Index + 1] : Module.Instructions.Count;
            IReadOnlyList<IrInstruction> BlockInstructions = Module.Instructions
                .Skip(StartIndex)
                .Take(EndIndex - StartIndex)
                .ToList();

            string Name = GetBlockName(BlockInstructions, Index);
            Blocks.Add(new OrynBasicBlock(Name, BlockInstructions, Array.Empty<string>()));
        }

        Dictionary<string, string> LabelToBlockName = BuildLabelToBlockName(Blocks);
        List<OrynBasicBlock> BlocksWithSuccessors = new();
        for (int Index = 0; Index < Blocks.Count; Index++)
        {
            OrynBasicBlock Block = Blocks[Index];
            IReadOnlyList<string> Successors = FindSuccessors(Block, Blocks, Index, LabelToBlockName);
            BlocksWithSuccessors.Add(Block with { Successors = Successors });
        }

        return new OrynControlFlowGraph(Module.EntrySymbol, BlocksWithSuccessors);
    }

    private static Dictionary<string, int> BuildLabelIndex(IReadOnlyList<IrInstruction> Instructions)
    {
        Dictionary<string, int> Labels = new(StringComparer.Ordinal);
        foreach (IrInstruction Instruction in Instructions)
        {
            if (Instruction.OpCode == "Label" && !string.IsNullOrWhiteSpace(Instruction.Operand))
            {
                Labels[Instruction.Operand] = Instruction.Index;
            }
        }

        return Labels;
    }

    private static SortedSet<int> FindLeaderIndexes(IReadOnlyList<IrInstruction> Instructions, Dictionary<string, int> LabelIndexes)
    {
        SortedSet<int> Leaders = new() { 0 };
        foreach (IrInstruction Instruction in Instructions)
        {
            if (Instruction.OpCode == "Label")
            {
                Leaders.Add(Instruction.Index);
            }

            if (Instruction.OpCode == "Jump" || Instruction.OpCode == "JumpIfFalse")
            {
                if (!string.IsNullOrWhiteSpace(Instruction.Operand) && LabelIndexes.TryGetValue(Instruction.Operand, out int TargetIndex))
                {
                    Leaders.Add(TargetIndex);
                }

                int FallthroughIndex = Instruction.Index + 1;
                if (Instruction.OpCode == "JumpIfFalse" && FallthroughIndex < Instructions.Count)
                {
                    Leaders.Add(FallthroughIndex);
                }
            }
        }

        return Leaders;
    }

    private static string GetBlockName(IReadOnlyList<IrInstruction> Instructions, int BlockIndex)
    {
        IrInstruction? FirstLabel = Instructions.FirstOrDefault(Instruction => Instruction.OpCode == "Label" && !string.IsNullOrWhiteSpace(Instruction.Operand));
        return FirstLabel?.Operand ?? (BlockIndex == 0 ? "Entry" : $"Block{BlockIndex}");
    }

    private static Dictionary<string, string> BuildLabelToBlockName(IReadOnlyList<OrynBasicBlock> Blocks)
    {
        Dictionary<string, string> LabelToBlockName = new(StringComparer.Ordinal);
        foreach (OrynBasicBlock Block in Blocks)
        {
            foreach (IrInstruction Instruction in Block.Instructions)
            {
                if (Instruction.OpCode == "Label" && !string.IsNullOrWhiteSpace(Instruction.Operand))
                {
                    LabelToBlockName[Instruction.Operand] = Block.Name;
                    break;
                }
            }
        }

        return LabelToBlockName;
    }

    private static IReadOnlyList<string> FindSuccessors(
        OrynBasicBlock Block,
        IReadOnlyList<OrynBasicBlock> Blocks,
        int BlockIndex,
        Dictionary<string, string> LabelToBlockName)
    {
        IrInstruction? Last = Block.Instructions.LastOrDefault();
        if (Last is null || Last.OpCode == "Return")
        {
            return Array.Empty<string>();
        }

        List<string> Successors = new();
        if ((Last.OpCode == "Jump" || Last.OpCode == "JumpIfFalse") && !string.IsNullOrWhiteSpace(Last.Operand))
        {
            if (LabelToBlockName.TryGetValue(Last.Operand, out string? TargetBlockName))
            {
                Successors.Add(TargetBlockName);
            }
        }

        if (Last.OpCode != "Jump" && BlockIndex + 1 < Blocks.Count)
        {
            Successors.Add(Blocks[BlockIndex + 1].Name);
        }

        return Successors.Distinct(StringComparer.Ordinal).ToList();
    }
}

internal sealed record OrynControlFlowGraph(string EntrySymbol, IReadOnlyList<OrynBasicBlock> Blocks);

internal sealed record OrynBasicBlock(string Name, IReadOnlyList<IrInstruction> Instructions, IReadOnlyList<string> Successors);
