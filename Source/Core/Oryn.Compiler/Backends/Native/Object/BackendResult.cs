using Oryn.Compiler.IR.ControlFlowGraph;

namespace Oryn.Compiler;

internal sealed record CompilerManifest(
    string CompilerVersion,
    string Target,
    string SourcePath,
    string OutputPath,
    string EntrySymbol,
    IReadOnlyList<IrInstruction> Instructions,
    IReadOnlyList<OrynIrMethod> Methods,
    OrynControlFlowGraph ControlFlowGraph,
    int BasicBlockCount,
    IReadOnlyList<ModuleManifestRecord> ModuleManifests,
    string Notes);

internal sealed record BackendResult(
    string ManifestPath,
    string CPath,
    string AssemblyPath,
    string DiagnosticsPath,
    string ObjectPath,
    CompilerManifest Manifest,
    string CSource,
    string AssemblySource,
    byte[] ObjectBytes,
    string DiagnosticsText);
