using System.Text.Json;
using Oryn.Compiler.Backends.Native.Object;
using Oryn.Compiler.IR.ControlFlowGraph;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.X64;

internal sealed class X64NativeBackend
{
    private readonly X64CSourceEmitter CSourceEmitter = new();
    private readonly X64AssemblyEmitter AssemblyEmitter = new();
    private readonly NativeObjectPlaceholderEmitter ObjectEmitter = new();
    private readonly NativeDiagnosticsEmitter DiagnosticsEmitter = new();

    public BackendResult Emit(string Version, CompilerCommand Command, BoundKernelModel BoundModel, OrynIrModule IrModule, OrynControlFlowGraph ControlFlowGraph)
    {
        string FullOutputPath = Path.GetFullPath(Command.OutputPath);
        string OutputDirectory = Path.GetDirectoryName(FullOutputPath) ?? Directory.GetCurrentDirectory();
        string BaseName = Path.GetFileNameWithoutExtension(FullOutputPath);
        Directory.CreateDirectory(OutputDirectory);

        string ManifestPath = Path.Combine(OutputDirectory, BaseName + ".stage2.ir.json");
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
            ControlFlowGraph,
            ControlFlowGraph.Blocks.Count,
            "Stage 2 phase 6 emits real clang/as-compatible x64 assembly from Oryn IR with a simple rbp-based 64-bit local stack-slot model, while keeping the generated C file as a readable reference artifact. Direct ELF64 object writing remains a Stage 3 task.");

        return new BackendResult(
            ManifestPath,
            CPath,
            AssemblyPath,
            DiagnosticsPath,
            FullOutputPath,
            Manifest,
            CSourceEmitter.Emit(IrModule.Instructions),
            AssemblyEmitter.Emit(IrModule.Instructions),
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
        File.WriteAllText(BackendResult.ObjectPath, BackendResult.ObjectPlaceholder);
    }
}
