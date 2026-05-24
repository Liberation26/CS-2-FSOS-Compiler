using System.Text;
using System.Text.RegularExpressions;
using Oryn.Compiler.Frontend.CSharpParser;
using Oryn.Compiler.Frontend.SafeSubsetValidator;

namespace Oryn.Compiler;

internal sealed record KernelCompositionCommand(
    string TemplatePath,
    string OutputPath,
    int MaximumStage,
    string OsName,
    string KernelName,
    IReadOnlyList<string> SelectedModuleNames,
    IReadOnlyList<string> MandatoryModuleNames);

internal sealed class KernelTemplateComposer
{
    private static readonly string[] DefaultMandatoryModuleNames =
    {
        "Runtime",
        "Diagnostics",
        "Panic",
        "Cpu",
        "ManifestLoader"
    };

    private readonly string Version;
    private readonly ModuleManifestCatalog ModuleManifestCatalog;
    private readonly BindingCatalog BindingCatalog;
    private readonly ModuleApiContractCatalog ApiContracts;
    private readonly ModuleDependencyResolver DependencyResolver = new();

    public KernelTemplateComposer(
        string Version,
        ModuleManifestCatalog ModuleManifestCatalog,
        BindingCatalog BindingCatalog,
        ModuleApiContractCatalog ApiContracts)
    {
        this.Version = Version;
        this.ModuleManifestCatalog = ModuleManifestCatalog;
        this.BindingCatalog = BindingCatalog;
        this.ApiContracts = ApiContracts;
    }

    public IReadOnlyList<string> Compose(KernelCompositionCommand Command)
    {
        if (!File.Exists(Command.TemplatePath))
        {
            throw new OrynCompileException($"Kernel template not found: {Command.TemplatePath}");
        }

        IReadOnlyList<ModuleManifestRecord> LinkedModules = ResolveLinkedModules(Command);
        IReadOnlySet<string> MandatoryNames = Command.MandatoryModuleNames.ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<ModuleManifestRecord> UserSelectedModules = LinkedModules
            .Where(Module => !MandatoryNames.Contains(Module.ModuleName))
            .OrderBy(Module => Module.InitializeOrder)
            .ThenBy(Module => Module.ModuleName, StringComparer.Ordinal)
            .ToList();
        IReadOnlyList<ModuleManifestRecord> MandatoryModules = LinkedModules
            .Where(Module => MandatoryNames.Contains(Module.ModuleName))
            .OrderBy(Module => Module.InitializeOrder)
            .ThenBy(Module => Module.ModuleName, StringComparer.Ordinal)
            .ToList();

        string TemplateText = File.ReadAllText(Command.TemplatePath);
        string GeneratedSource = ApplyTemplate(TemplateText, Command, LinkedModules, MandatoryModules, UserSelectedModules);

        ValidateGeneratedSource(Command, GeneratedSource);

        string OutputDirectory = Path.GetDirectoryName(Path.GetFullPath(Command.OutputPath)) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Command.OutputPath, GeneratedSource, Encoding.UTF8);

        List<string> Messages = new()
        {
            $"[ OK ] [ COMPOSE  ] Oryn kernel template composer version {Version}",
            $"[ OK ] [ COMPOSE  ] OS name: {Command.OsName}",
            $"[ OK ] [ COMPOSE  ] Kernel name: {Command.KernelName}",
            $"[ OK ] [ COMPOSE  ] Template: {Command.TemplatePath}",
            $"[ OK ] [ COMPOSE  ] Output: {Command.OutputPath}",
            $"[ OK ] [ COMPOSE  ] Mandatory kernel modules: {string.Join(", ", MandatoryModules.Select(Module => Module.ModuleName))}",
            $"[ OK ] [ COMPOSE  ] User-selected modules: {(UserSelectedModules.Count == 0 ? "<none>" : string.Join(", ", UserSelectedModules.Select(Module => Module.ModuleName)))}",
            $"[ OK ] [ COMPOSE  ] Linked module closure: {string.Join(", ", LinkedModules.Select(Module => Module.ModuleName))}"
        };

        foreach (ModuleManifestRecord Module in LinkedModules)
        {
            string Initializer = string.IsNullOrWhiteSpace(Module.InitializerManagedName) ? "<native/none>" : Module.InitializerManagedName;
            string Dependencies = Module.DependsOn.Count == 0 ? "<none>" : string.Join(", ", Module.DependsOn);
            string SelectionKind = MandatoryNames.Contains(Module.ModuleName) ? "mandatory" : "user-selected";
            Messages.Add($"[ OK ] [ COMPOSE  ] module={Module.ModuleName} kind={SelectionKind} namespace={Module.NamespaceName} initializer={Initializer} dependsOn={Dependencies}");
        }

        Messages.Add("[ OK ] [ COMPOSE  ] Generated kernel source passed safe-subset and approved-call validation before backend/native compilation.");
        return Messages;
    }

    private IReadOnlyList<ModuleManifestRecord> ResolveLinkedModules(KernelCompositionCommand Command)
    {
        IReadOnlyDictionary<string, ModuleManifestRecord> ByName = ModuleManifestCatalog.Manifests
            .Where(Module => Module.AllowedInKernel && Module.LinkByDefault && Module.Stage <= Command.MaximumStage)
            .ToDictionary(Module => Module.ModuleName, Module => Module, StringComparer.Ordinal);

        IReadOnlyList<string> RequestedNames;
        if (Command.SelectedModuleNames.Count == 0 && Command.MandatoryModuleNames.Count == 0)
        {
            RequestedNames = ByName.Values
                .OrderBy(Module => Module.InitializeOrder)
                .ThenBy(Module => Module.ModuleName, StringComparer.Ordinal)
                .Select(Module => Module.ModuleName)
                .ToList();
        }
        else
        {
            RequestedNames = Command.MandatoryModuleNames
                .Concat(Command.SelectedModuleNames)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        Dictionary<string, ModuleManifestRecord> Closure = new(StringComparer.Ordinal);
        foreach (string RequestedName in RequestedNames)
        {
            AddWithDependencies(RequestedName, ByName, Closure);
        }

        if (Closure.Count == 0)
        {
            throw new OrynCompileException("No kernel modules were selected for template composition.");
        }

        return DependencyResolver.Resolve(Closure.Values.ToList());
    }

    private static void AddWithDependencies(string ModuleName, IReadOnlyDictionary<string, ModuleManifestRecord> ByName, Dictionary<string, ModuleManifestRecord> Closure)
    {
        if (!ByName.TryGetValue(ModuleName, out ModuleManifestRecord? Module))
        {
            throw new OrynCompileException($"Kernel template composition selected unknown or unavailable module: {ModuleName}");
        }

        if (Closure.ContainsKey(ModuleName))
        {
            return;
        }

        foreach (string Dependency in Module.DependsOn)
        {
            AddWithDependencies(Dependency, ByName, Closure);
        }

        Closure.Add(ModuleName, Module);
    }

    private string ApplyTemplate(
        string TemplateText,
        KernelCompositionCommand Command,
        IReadOnlyList<ModuleManifestRecord> LinkedModules,
        IReadOnlyList<ModuleManifestRecord> MandatoryModules,
        IReadOnlyList<ModuleManifestRecord> UserSelectedModules)
    {
        string Usings = string.Join(Environment.NewLine, LinkedModules
            .Select(Module => Module.NamespaceName)
            .Where(NamespaceName => !string.IsNullOrWhiteSpace(NamespaceName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(NamespaceName => NamespaceName, StringComparer.Ordinal)
            .Select(NamespaceName => $"using {NamespaceName};"));

        List<string> BootLines = new();
        BootLines.Add($"        Diagnostics.WriteOk(\"{EscapeForStringLiteral(Command.OsName)} generated kernel template composition reached kernel code\");");
        BootLines.Add($"        Diagnostics.WriteOk(\"{EscapeForStringLiteral(Command.OsName)} mandatory kernel module count: {MandatoryModules.Count}\");");
        foreach (ModuleManifestRecord Module in MandatoryModules)
        {
            BootLines.Add($"        Diagnostics.WriteOk(\"{EscapeForStringLiteral(Command.OsName)} mandatory kernel module: {EscapeForStringLiteral(Module.ModuleName)}\");");
        }

        BootLines.Add($"        Diagnostics.WriteOk(\"{EscapeForStringLiteral(Command.OsName)} user-selected module count: {UserSelectedModules.Count}\");");
        foreach (ModuleManifestRecord Module in UserSelectedModules)
        {
            BootLines.Add($"        Diagnostics.WriteOk(\"{EscapeForStringLiteral(Command.OsName)} user-selected module: {EscapeForStringLiteral(Module.ModuleName)}\");");
        }

        if (UserSelectedModules.Count == 0)
        {
            BootLines.Add($"        Diagnostics.WriteOk(\"{EscapeForStringLiteral(Command.OsName)} user-selected modules: <none>\");");
        }

        string InitializerCalls = BuildInitializerCalls(Command, LinkedModules);

        return TemplateText
            .Replace("__ORYN_GENERATED_USINGS__", Usings, StringComparison.Ordinal)
            .Replace("__ORYN_KERNEL_BOOT_PROOF_LINES__", string.Join(Environment.NewLine, BootLines), StringComparison.Ordinal)
            .Replace("__ORYN_MODULE_INITIALIZATION_CALLS__", InitializerCalls, StringComparison.Ordinal)
            .Replace("__ORYN_COMPILER_VERSION__", Version, StringComparison.Ordinal)
            .Replace("__ORYN_OS_NAME__", EscapeForStringLiteral(Command.OsName), StringComparison.Ordinal)
            .Replace("__ORYN_KERNEL_NAME__", EscapeForStringLiteral(Command.KernelName), StringComparison.Ordinal);
    }

    private string BuildInitializerCalls(KernelCompositionCommand Command, IReadOnlyList<ModuleManifestRecord> LinkedModules)
    {
        ModuleManifestRecord? ManifestLoader = LinkedModules.FirstOrDefault(Module => Module.ModuleName.Equals("ManifestLoader", StringComparison.Ordinal));
        if (ManifestLoader is not null && !string.IsNullOrWhiteSpace(ManifestLoader.InitializerManagedName))
        {
            return $"        Diagnostics.WriteOk(\"{EscapeForStringLiteral(Command.OsName)} initializing linked modules through generated manifest glue\");{Environment.NewLine}        {ManifestLoader.InitializerManagedName}();";
        }

        List<string> Lines = new();
        foreach (ModuleManifestRecord Module in LinkedModules)
        {
            if (string.IsNullOrWhiteSpace(Module.InitializerManagedName))
            {
                continue;
            }

            Lines.Add($"        Diagnostics.WriteOk(\"{EscapeForStringLiteral(Command.OsName)} initializing module: {EscapeForStringLiteral(Module.ModuleName)}\");");
            Lines.Add($"        {Module.InitializerManagedName}();");
        }

        if (Lines.Count == 0)
        {
            Lines.Add($"        Diagnostics.WriteOk(\"{EscapeForStringLiteral(Command.OsName)} no managed module initializers selected\");");
        }

        return string.Join(Environment.NewLine, Lines);
    }

    private void ValidateGeneratedSource(KernelCompositionCommand Command, string GeneratedSource)
    {
        SafeSubsetValidator Validator = new(BindingCatalog.ApprovedNamespaces);
        IReadOnlyList<string> ValidationFailures = Validator.Validate(GeneratedSource);
        if (ValidationFailures.Count > 0)
        {
            throw new OrynCompileException($"{Command.OsName} generated kernel template failed safe-subset validation before compilation: " + string.Join(" | ", ValidationFailures));
        }

        KernelAst Ast = new CSharpKernelParser().Parse(Command.OutputPath, GeneratedSource);
        _ = new SemanticAnalyzer(BindingCatalog, ApiContracts).Bind(Ast);
    }

    private static string EscapeForStringLiteral(string Text)
    {
        return Text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    public static KernelCompositionCommand ParseCommand(string[] Args)
    {
        string? TemplatePath = ReadOption(Args, "--template");
        string? OutputPath = ReadOption(Args, "--output");
        string StageText = ReadOption(Args, "--stage") ?? "9";
        string ModulesText = ReadOption(Args, "--modules") ?? string.Empty;
        string MandatoryModulesText = ReadOption(Args, "--mandatory-modules") ?? string.Empty;
        string OsName = ReadOption(Args, "--os-name") ?? "Stage9";
        string KernelName = ReadOption(Args, "--kernel-name") ?? "Kernel";

        if (string.IsNullOrWhiteSpace(TemplatePath))
        {
            throw new OrynCompileException("Missing --template <template.cs>.");
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            throw new OrynCompileException("Missing --output <generated.cs>.");
        }

        int MaximumStage = ParseStage(StageText);
        IReadOnlyList<string> Modules = ModulesText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        IReadOnlyList<string> MandatoryModules = MandatoryModulesText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (MandatoryModulesText.Length == 0 && Modules.Count > 0)
        {
            MandatoryModules = DefaultMandatoryModuleNames;
        }

        return new KernelCompositionCommand(TemplatePath, OutputPath, MaximumStage, OsName, KernelName, Modules, MandatoryModules);
    }

    private static int ParseStage(string StageText)
    {
        string Normalized = StageText.Trim();
        if (Normalized.StartsWith("Stage", StringComparison.OrdinalIgnoreCase))
        {
            Normalized = Normalized[5..];
        }

        if (!int.TryParse(Normalized, out int Stage) || Stage < 1)
        {
            throw new OrynCompileException($"Invalid composition stage: {StageText}");
        }

        return Stage;
    }

    private static string? ReadOption(string[] Args, string OptionName)
    {
        for (int Index = 0; Index < Args.Length - 1; Index++)
        {
            if (Args[Index].Equals(OptionName, StringComparison.OrdinalIgnoreCase))
            {
                return Args[Index + 1];
            }
        }

        return null;
    }
}
