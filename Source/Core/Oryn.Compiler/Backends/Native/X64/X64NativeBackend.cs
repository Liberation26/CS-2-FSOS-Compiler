using System.Text.Json;
using Oryn.Compiler.Backends.Native.Object;
using Oryn.Compiler.IR.ControlFlowGraph;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.X64;

internal sealed class X64NativeBackend
{
    private readonly X64CSourceEmitter CSourceEmitter = new();
    private readonly X64AssemblyEmitter AssemblyEmitter = new();
    private readonly NativeElf64ObjectEmitter ObjectEmitter = new();
    private readonly NativeDiagnosticsEmitter DiagnosticsEmitter = new();

    public BackendResult Emit(string Version, CompilerCommand Command, BoundKernelModel BoundModel, OrynIrModule IrModule, OrynControlFlowGraph ControlFlowGraph)
    {
        string FullOutputPath = Path.GetFullPath(Command.OutputPath);
        string OutputDirectory = Path.GetDirectoryName(FullOutputPath) ?? Directory.GetCurrentDirectory();
        string BaseName = Path.GetFileNameWithoutExtension(FullOutputPath);
        Directory.CreateDirectory(OutputDirectory);

        string StageTag = BaseName.Contains("stage3", StringComparison.OrdinalIgnoreCase) ? "stage3" : "stage2";
        string ManifestPath = Path.Combine(OutputDirectory, BaseName + "." + StageTag + ".ir.json");
        string CPath = Path.Combine(OutputDirectory, BaseName + ".generated.c");
        string AssemblyPath = Path.Combine(OutputDirectory, BaseName + ".generated.S");
        string DiagnosticsPath = Path.Combine(OutputDirectory, BaseName + ".diagnostics.log");

        CompilerManifest Manifest = new(
            Version,
            Command.Target,
            BoundModel.SourcePath,
            FullOutputPath,
            IrModule.EntrySymbol,
            IrModule.Instructions,
            IrModule.Methods,
            ControlFlowGraph,
            ControlFlowGraph.Blocks.Count,
            "Stage 3 writes a real ELF64 relocatable object directly from Oryn IR while still emitting readable C and assembly reference artifacts for diagnostics. The direct object contains .text, .rodata, .rela.text, .symtab, .strtab, .shstrtab, and .note.GNU-stack sections.");

        return new BackendResult(
            ManifestPath,
            CPath,
            AssemblyPath,
            DiagnosticsPath,
            FullOutputPath,
            Manifest,
            CSourceEmitter.Emit(IrModule),
            AssemblyEmitter.Emit(IrModule),
            ObjectEmitter.Emit(Manifest),
            DiagnosticsEmitter.Emit(Manifest, IrModule.Instructions, CPath, AssemblyPath));
    }

    public void Write(BackendResult BackendResult)
    {
        JsonSerializerOptions Options = new() { WriteIndented = true };
        File.WriteAllText(BackendResult.ManifestPath, JsonSerializer.Serialize(BackendResult.Manifest, Options));
        File.WriteAllText(BackendResult.CPath, BackendResult.CSource);
        File.WriteAllText(BackendResult.AssemblyPath, BackendResult.AssemblySource);
        File.WriteAllText(BackendResult.DiagnosticsPath, BackendResult.DiagnosticsText);
        File.WriteAllBytes(BackendResult.ObjectPath, BackendResult.ObjectBytes);
    }
}
