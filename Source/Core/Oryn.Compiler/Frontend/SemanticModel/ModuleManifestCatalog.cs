using System.Text.Json;

namespace Oryn.Compiler;

internal sealed class ModuleManifestCatalog
{
    private readonly IReadOnlyList<ModuleManifestRecord> Records;
    private readonly ModuleDependencyResolver DependencyResolver = new();

    private ModuleManifestCatalog(IReadOnlyList<ModuleManifestRecord> Records)
    {
        this.Records = Records;
    }

    public IReadOnlyList<ModuleManifestRecord> Manifests => Records;

    public IReadOnlyList<ModuleManifestRecord> ApprovedKernelModules => Records
        .Where(Record => Record.AllowedInKernel)
        .OrderBy(Record => Record.InitializeOrder)
        .ThenBy(Record => Record.ModuleName, StringComparer.Ordinal)
        .ToList();

    public IReadOnlyList<ModuleManifestRecord> ResolveApprovedKernelModules(int MaximumStage, bool ExcludeManifestLoaderFromGraph)
    {
        IReadOnlyList<ModuleManifestRecord> Selected = Records
            .Where(Record => Record.AllowedInKernel && Record.LinkByDefault && Record.Stage <= MaximumStage)
            .Where(Record => !ExcludeManifestLoaderFromGraph || !Record.ModuleName.Equals("ManifestLoader", StringComparison.Ordinal))
            .ToList();
        return DependencyResolver.Resolve(Selected);
    }

    public static ModuleManifestCatalog CreateDefault()
    {
        string DirectoryPath = FindManifestDirectory();
        return LoadFromDirectory(DirectoryPath);
    }

    public static ModuleManifestCatalog LoadFromDirectory(string DirectoryPath)
    {
        if (!Directory.Exists(DirectoryPath))
        {
            throw new OrynCompileException($"Module manifest directory not found: {DirectoryPath}");
        }

        List<ModuleManifestRecord> Records = new();
        JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        foreach (string ManifestPath in Directory.EnumerateFiles(DirectoryPath, "*.module.json").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            ModuleManifestFile? FileModel;
            try
            {
                FileModel = JsonSerializer.Deserialize<ModuleManifestFile>(File.ReadAllText(ManifestPath), Options);
            }
            catch (JsonException Exception)
            {
                throw new OrynCompileException($"Invalid module manifest JSON: {ManifestPath}: {Exception.Message}");
            }

            if (FileModel is null || string.IsNullOrWhiteSpace(FileModel.Module))
            {
                throw new OrynCompileException($"Module manifest is missing module: {ManifestPath}");
            }

            if (string.IsNullOrWhiteSpace(FileModel.Namespace))
            {
                throw new OrynCompileException($"Module manifest is missing namespace: {ManifestPath}");
            }

            if (string.IsNullOrWhiteSpace(FileModel.NativeSource))
            {
                throw new OrynCompileException($"Module manifest is missing nativeSource: {ManifestPath}");
            }

            Records.Add(new ModuleManifestRecord(
                FileModel.Module,
                FileModel.Namespace,
                FileModel.Stage,
                FileModel.AllowedInKernel,
                FileModel.LinkByDefault,
                FileModel.InitializeOrder,
                FileModel.DependsOn ?? Array.Empty<string>(),
                FileModel.InitializerManagedName ?? string.Empty,
                FileModel.InitializerNativeSymbol ?? string.Empty,
                FileModel.NativeSource,
                FileModel.BindingPath ?? string.Empty,
                FileModel.Summary ?? string.Empty,
                Path.GetFullPath(ManifestPath)));
        }

        if (Records.Count == 0)
        {
            throw new OrynCompileException($"No module manifest records found in: {DirectoryPath}");
        }

        return new ModuleManifestCatalog(Records);
    }

    private static string FindManifestDirectory()
    {
        List<string> StartDirectories = new()
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (string StartDirectory in StartDirectories)
        {
            DirectoryInfo? DirectoryInfo = new(StartDirectory);
            while (DirectoryInfo is not null)
            {
                string Candidate = Path.Combine(DirectoryInfo.FullName, "Source", "Sdk", "ModuleManifests");
                if (Directory.Exists(Candidate))
                {
                    return Candidate;
                }

                DirectoryInfo = DirectoryInfo.Parent;
            }
        }

        throw new OrynCompileException("Could not locate Source/Sdk/ModuleManifests. Run the compiler from inside an Oryn source tree or set the working directory to the repository root.");
    }

    private sealed class ModuleManifestFile
    {
        public string? Module { get; set; }
        public string? Namespace { get; set; }
        public int Stage { get; set; }
        public bool AllowedInKernel { get; set; }
        public bool LinkByDefault { get; set; }
        public int InitializeOrder { get; set; }
        public string[]? DependsOn { get; set; }
        public string? InitializerManagedName { get; set; }
        public string? InitializerNativeSymbol { get; set; }
        public string? NativeSource { get; set; }
        public string? BindingPath { get; set; }
        public string? Summary { get; set; }
    }
}

internal sealed record ModuleManifestRecord(
    string ModuleName,
    string NamespaceName,
    int Stage,
    bool AllowedInKernel,
    bool LinkByDefault,
    int InitializeOrder,
    IReadOnlyList<string> DependsOn,
    string InitializerManagedName,
    string InitializerNativeSymbol,
    string NativeSource,
    string BindingPath,
    string Summary,
    string ManifestPath);
