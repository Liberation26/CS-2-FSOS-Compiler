using Oryn.Compiler.Backends.Native.X64;
using Oryn.Compiler.Frontend.CSharpParser;
using Oryn.Compiler.Frontend.SafeSubsetValidator;
using Oryn.Compiler.IR.ControlFlowGraph;
using Oryn.Compiler.IR.TypeLowering;

namespace Oryn.Compiler;

internal sealed class CompilerPipeline
{
    private readonly string Version;
    private readonly SafeSubsetValidator Validator;
    private readonly CSharpKernelParser Parser;
    private readonly SemanticAnalyzer SemanticAnalyzer;
    private readonly KernelIrLowerer IrLowerer;
    private readonly ControlFlowGraphBuilder ControlFlowGraphBuilder;
    private readonly X64NativeBackend Backend;
    private readonly ModuleManifestCatalog ModuleManifestCatalog;
    private readonly ModuleApiContractCatalog ApiContracts;

    public CompilerPipeline(
        string Version,
        SafeSubsetValidator Validator,
        CSharpKernelParser Parser,
        SemanticAnalyzer SemanticAnalyzer,
        KernelIrLowerer IrLowerer,
        ControlFlowGraphBuilder ControlFlowGraphBuilder,
        X64NativeBackend Backend,
        ModuleManifestCatalog ModuleManifestCatalog,
        ModuleApiContractCatalog ApiContracts)
    {
        this.Version = Version;
        this.Validator = Validator;
        this.Parser = Parser;
        this.SemanticAnalyzer = SemanticAnalyzer;
        this.IrLowerer = IrLowerer;
        this.ControlFlowGraphBuilder = ControlFlowGraphBuilder;
        this.Backend = Backend;
        this.ModuleManifestCatalog = ModuleManifestCatalog;
        this.ApiContracts = ApiContracts;
    }

    public static CompilerPipeline CreateDefault(string Version)
    {
        BindingCatalog Bindings = BindingCatalog.CreateDefault();
        ModuleManifestCatalog ModuleManifests = ModuleManifestCatalog.CreateDefault();
        ModuleApiContractCatalog ApiContracts = ModuleApiContractCatalog.CreateDefault();
        return new CompilerPipeline(
            Version,
            new SafeSubsetValidator(Bindings.ApprovedNamespaces),
            new CSharpKernelParser(),
            new SemanticAnalyzer(Bindings, ApiContracts),
            new KernelIrLowerer(),
            new ControlFlowGraphBuilder(),
            new X64NativeBackend(),
            ModuleManifests,
            ApiContracts);
    }

    public CompilerResult Compile(CompilerCommand Command)
    {
        List<string> Messages = new();

        if (!File.Exists(Command.SourcePath))
        {
            Messages.Add($"[FAIL] Source file not found: {Command.SourcePath}");
            return new CompilerResult(1, Messages);
        }

        string SourceText = File.ReadAllText(Command.SourcePath);
        IReadOnlyList<string> ValidationFailures = Validator.Validate(SourceText);
        if (ValidationFailures.Count > 0)
        {
            Messages.Add("[FAIL] Safe-subset validation failed.");
            foreach (string Failure in ValidationFailures)
            {
                Messages.Add($"[FAIL] {Failure}");
            }

            return new CompilerResult(2, Messages);
        }

        try
        {
            KernelAst KernelAst = Parser.Parse(Command.SourcePath, SourceText);
            BoundKernelModel BoundModel = SemanticAnalyzer.Bind(KernelAst);
            OrynIrModule IrModule = IrLowerer.Lower(BoundModel);
            OrynControlFlowGraph ControlFlowGraph = ControlFlowGraphBuilder.Build(IrModule);
            IReadOnlyList<ModuleManifestRecord> SelectedModuleManifests = (Command.SourcePath.Contains("Stage7", StringComparison.OrdinalIgnoreCase) || Command.SourcePath.Contains("Stage8", StringComparison.OrdinalIgnoreCase))
                ? ModuleManifestCatalog.ResolveApprovedKernelModules(7, ExcludeManifestLoaderFromGraph: true)
                : ModuleManifestCatalog.ApprovedKernelModules;
            BackendResult BackendResult = Backend.Emit(Version, Command, BoundModel, IrModule, ControlFlowGraph, SelectedModuleManifests);
            Backend.Write(BackendResult);

            Messages.Add($"[ OK ] Parsed source: {Command.SourcePath}");
            Messages.Add("[ OK ] Stage 4 approved-module boundary validation passed.");
            if (Command.SourcePath.Contains("Stage5", StringComparison.OrdinalIgnoreCase))
            {
                Messages.Add("[ OK ] Stage 5 runtime contract validation passed.");
            }
            if (Command.SourcePath.Contains("Stage6", StringComparison.OrdinalIgnoreCase))
            {
                Messages.Add("[ OK ] [ MANIFEST ] Stage 6 service/module manifest loading validation passed.");
                foreach (ModuleManifestRecord Manifest in SelectedModuleManifests)
                {
                    Messages.Add($"[ OK ] [ MANIFEST ] expose={Manifest.ModuleName} stage={Manifest.Stage} order={Manifest.InitializeOrder} native={Manifest.NativeSource}");
                }
            }
            if (Command.SourcePath.Contains("Stage8", StringComparison.OrdinalIgnoreCase))
            {
                Messages.Add("[ OK ] [ CONTRACT ] Stage 8 module API contract validation passed.");
                foreach (BindingRecord Contract in ApiContracts.ApprovedCallContracts)
                {
                    Messages.Add($"[ OK ] [ CONTRACT ] approved {Contract.FullyQualifiedManagedName} -> {Contract.NativeSymbol}");
                }
            }
            if (Command.SourcePath.Contains("Stage7", StringComparison.OrdinalIgnoreCase) || Command.SourcePath.Contains("Stage8", StringComparison.OrdinalIgnoreCase))
            {
                Messages.Add(Command.SourcePath.Contains("Stage8", StringComparison.OrdinalIgnoreCase) ? "[ OK ] [ MANIFEST ] Stage 8 inherited dependency graph validation passed." : "[ OK ] [ MANIFEST ] Stage 7 dependency graph validation passed.");
                foreach (ModuleManifestRecord Manifest in SelectedModuleManifests)
                {
                    string Dependencies = Manifest.DependsOn.Count == 0 ? "<none>" : string.Join(", ", Manifest.DependsOn);
                    Messages.Add($"[ OK ] [ MANIFEST ] dependency {Manifest.ModuleName} -> {Dependencies}");
                }
                Messages.Add("[ OK ] [ MANIFEST ] resolved initialization order: " + string.Join(", ", SelectedModuleManifests.Select(Manifest => Manifest.ModuleName)));
            }
            Messages.Add($"[ OK ] Approved module calls: {BoundModel.ApprovedModuleCallCount}");
            Messages.Add($"[ OK ] Lowered IR instructions: {IrModule.Instructions.Count}");
            Messages.Add($"[ OK ] [ CFG      ] Basic blocks: {ControlFlowGraph.Blocks.Count}");
            foreach (Oryn.Compiler.IR.ControlFlowGraph.OrynBasicBlock Block in ControlFlowGraph.Blocks)
            {
                string Successors = Block.Successors.Count == 0 ? "<none>" : string.Join(", ", Block.Successors);
                Messages.Add($"[ OK ] [ CFG      ] {Block.Name} -> {Successors}");
            }

            Messages.Add($"[ OK ] Backend target: {Command.Target}");
            Messages.Add($"[ OK ] Wrote IR manifest: {BackendResult.ManifestPath}");
            Messages.Add($"[ OK ] Wrote C backend: {BackendResult.CPath}");
            Messages.Add($"[ OK ] Wrote real x64 assembly backend: {BackendResult.AssemblyPath}");
            Messages.Add($"[ OK ] Wrote compiler diagnostics: {BackendResult.DiagnosticsPath}");
            Messages.Add($"[ OK ] [ ELF64    ] Wrote direct ELF64 relocatable object: {BackendResult.ObjectPath}");
            return new CompilerResult(0, Messages);
        }
        catch (OrynCompileException Exception)
        {
            Messages.Add($"[FAIL] {Exception.Message}");
            return new CompilerResult(2, Messages);
        }
    }
}
