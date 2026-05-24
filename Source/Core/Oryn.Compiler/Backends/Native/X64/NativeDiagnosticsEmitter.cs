using System.Text;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.X64;

internal sealed class NativeDiagnosticsEmitter
{
    public string Emit(CompilerManifest Manifest, IReadOnlyList<IrInstruction> Instructions, string CPath, string AssemblyPath)
    {
        StringBuilder Builder = new();
        Builder.AppendLine("[ OK ] [ COMPILER ] Oryn.Compiler Stage 2 phase 5 diagnostics");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Version: {Manifest.CompilerVersion}");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Source: {Manifest.SourcePath}");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Target: {Manifest.Target}");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Entry symbol: {Manifest.EntrySymbol}");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Basic blocks: {Manifest.BasicBlockCount}");
        Builder.AppendLine($"[ OK ] [ COMPILER ] IR instructions: {Instructions.Count}");
        Builder.AppendLine("[ OK ] [ IR       ] Real Oryn IR is stack-style and explicit: locals, constants, arithmetic, comparisons, calls, labels, jumps, conditional jumps, and returns.");
        Builder.AppendLine("[ OK ] [ CFG      ] Basic blocks and successor edges are generated from labels, jumps, conditional jumps, and fallthroughs.");

        foreach (Oryn.Compiler.IR.ControlFlowGraph.OrynBasicBlock Block in Manifest.ControlFlowGraph.Blocks)
        {
            string Successors = Block.Successors.Count == 0 ? "<none>" : string.Join(", ", Block.Successors);
            Builder.AppendLine($"[ OK ] [ CFG      ] {Block.Name} -> {Successors}");
        }

        foreach (IrInstruction Instruction in Instructions)
        {
            Builder.AppendLine(FormatInstruction(Instruction));
        }

        Builder.AppendLine($"[ OK ] [ BACKEND  ] C output: {CPath}");
        Builder.AppendLine($"[ OK ] [ BACKEND  ] real x64 assembly output: {AssemblyPath}");
        Builder.AppendLine("[ OK ] [ BACKEND  ] Stage 2 Phase 5 lowers Oryn IR directly to clang/as-compatible x64 assembly.");
        Builder.AppendLine("[ OK ] [ BACKEND  ] Runtime diagnostics write to QEMU serial and VGA when built with DEBUG=1; ELF64 object writing is deferred to Stage 3.");
        return Builder.ToString();
    }

    private static string FormatInstruction(IrInstruction Instruction)
    {
        StringBuilder Builder = new();
        Builder.Append($"[ OK ] [ IR       ] #{Instruction.Index:D3} {Instruction.OpCode}");
        if (!string.IsNullOrWhiteSpace(Instruction.Operand))
        {
            Builder.Append($" {Instruction.Operand}");
        }

        if (Instruction.Int32Value is not null)
        {
            Builder.Append($" {Instruction.Int32Value}");
        }

        if (Instruction.StringValue is not null)
        {
            Builder.Append($" \"{Instruction.StringValue}\"");
        }

        if (!string.IsNullOrWhiteSpace(Instruction.NativeSymbol))
        {
            Builder.Append($" -> {Instruction.NativeSymbol}");
        }

        return Builder.ToString();
    }
}
