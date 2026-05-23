using System.Text;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.X64;

internal sealed class NativeDiagnosticsEmitter
{
    public string Emit(CompilerManifest Manifest, IReadOnlyList<IrInstruction> Instructions, string CPath, string AssemblyPath)
    {
        StringBuilder Builder = new();
        Builder.AppendLine("[ OK ] [ COMPILER ] Oryn.Compiler Stage 2 phase 2 diagnostics");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Version: {Manifest.CompilerVersion}");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Source: {Manifest.SourcePath}");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Target: {Manifest.Target}");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Entry symbol: {Manifest.EntrySymbol}");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Basic blocks: {Manifest.BasicBlockCount}");
        Builder.AppendLine($"[ OK ] [ COMPILER ] Lowered calls: {Instructions.Count}");

        foreach (IrInstruction Instruction in Instructions)
        {
            Builder.AppendLine($"[ OK ] [ LOWERING ] #{Instruction.Index} {Instruction.ManagedName} -> {Instruction.NativeSymbol}");

            if (Instruction.ManagedName.StartsWith("Diagnostics.", StringComparison.Ordinal))
            {
                string Message = Instruction.Arguments.Count == 1 ? Instruction.Arguments[0] : string.Empty;
                Builder.AppendLine($"[ OK ] [ RUNTIME  ] Kernel diagnostic will be emitted: {Message}");
            }
        }

        Builder.AppendLine($"[ OK ] [ BACKEND  ] C output: {CPath}");
        Builder.AppendLine($"[ OK ] [ BACKEND  ] x64 assembly output: {AssemblyPath}");
        Builder.AppendLine("[ OK ] [ BACKEND  ] Diagnostics.Write* calls lower to runtime Diagnostics_Write* symbols.");
        Builder.AppendLine("[ OK ] [ BACKEND  ] Runtime diagnostics write to QEMU serial and VGA when built with DEBUG=1.");
        return Builder.ToString();
    }
}
