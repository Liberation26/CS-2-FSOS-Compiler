using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oryn.Compiler;

internal sealed class BindingCatalog
{
    private readonly SymbolTable SymbolTable;

    private BindingCatalog(SymbolTable SymbolTable)
    {
        this.SymbolTable = SymbolTable;
    }

    public IReadOnlyCollection<BindingRecord> Bindings => SymbolTable.Bindings;

    public static BindingCatalog CreateDefault()
    {
        string BindingsDirectory = FindBindingsDirectory();
        return LoadFromDirectory(BindingsDirectory);
    }

    public static BindingCatalog LoadFromDirectory(string BindingsDirectory)
    {
        if (!Directory.Exists(BindingsDirectory))
        {
            throw new OrynCompileException($"Binding directory not found: {BindingsDirectory}");
        }

        List<BindingRecord> Records = new();
        JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        foreach (string BindingPath in Directory.EnumerateFiles(BindingsDirectory, "*.binding.json").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            BindingFile? FileModel;
            try
            {
                FileModel = JsonSerializer.Deserialize<BindingFile>(File.ReadAllText(BindingPath), Options);
            }
            catch (JsonException Exception)
            {
                throw new OrynCompileException($"Invalid binding JSON: {BindingPath}: {Exception.Message}");
            }

            if (FileModel is null || string.IsNullOrWhiteSpace(FileModel.Module))
            {
                throw new OrynCompileException($"Binding JSON is missing module: {BindingPath}");
            }

            if (FileModel.Methods is null || FileModel.Methods.Count == 0)
            {
                throw new OrynCompileException($"Binding JSON has no methods: {BindingPath}");
            }

            foreach (BindingMethod Method in FileModel.Methods)
            {
                if (string.IsNullOrWhiteSpace(Method.ManagedName))
                {
                    throw new OrynCompileException($"Binding JSON method is missing managedName: {BindingPath}");
                }

                if (string.IsNullOrWhiteSpace(Method.NativeSymbol))
                {
                    throw new OrynCompileException($"Binding JSON method {Method.ManagedName} is missing nativeSymbol: {BindingPath}");
                }

                int ArgumentCount = Method.ArgumentCount ?? CountSignatureArguments(Method.Signature, Method.ManagedName, BindingPath);
                Records.Add(new BindingRecord(FileModel.Module, Method.ManagedName, Method.NativeSymbol, ArgumentCount, Method.AllowedInKernel));
            }
        }

        if (Records.Count == 0)
        {
            throw new OrynCompileException($"No binding JSON records found in: {BindingsDirectory}");
        }

        return new BindingCatalog(new SymbolTable(Records));
    }

    public bool TryResolve(string ManagedName, out BindingRecord? Binding)
    {
        return SymbolTable.TryResolve(ManagedName, out Binding);
    }

    private static string FindBindingsDirectory()
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
                string Candidate = Path.Combine(DirectoryInfo.FullName, "Source", "Sdk", "Bindings");
                if (Directory.Exists(Candidate))
                {
                    return Candidate;
                }

                DirectoryInfo = DirectoryInfo.Parent;
            }
        }

        throw new OrynCompileException("Could not locate Source/Sdk/Bindings. Run the compiler from inside an Oryn source tree or set the working directory to the repository root.");
    }

    private static int CountSignatureArguments(string? Signature, string ManagedName, string BindingPath)
    {
        if (string.IsNullOrWhiteSpace(Signature))
        {
            throw new OrynCompileException($"Binding JSON method {ManagedName} must provide argumentCount or signature: {BindingPath}");
        }

        int Open = Signature.IndexOf('(');
        int Close = Signature.LastIndexOf(')');
        if (Open < 0 || Close < Open)
        {
            throw new OrynCompileException($"Binding JSON method {ManagedName} has invalid signature: {Signature}");
        }

        string ArgumentsText = Signature.Substring(Open + 1, Close - Open - 1).Trim();
        if (ArgumentsText.Length == 0)
        {
            return 0;
        }

        return ArgumentsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private sealed record BindingFile(
        [property: JsonPropertyName("module")] string Module,
        [property: JsonPropertyName("namespace")] string? Namespace,
        [property: JsonPropertyName("stage")] int Stage,
        [property: JsonPropertyName("methods")] List<BindingMethod> Methods);

    private sealed record BindingMethod(
        [property: JsonPropertyName("managedName")] string ManagedName,
        [property: JsonPropertyName("signature")] string? Signature,
        [property: JsonPropertyName("nativeSymbol")] string NativeSymbol,
        [property: JsonPropertyName("allowedInKernel")] bool AllowedInKernel,
        [property: JsonPropertyName("argumentCount")] int? ArgumentCount,
        [property: JsonPropertyName("summary")] string? Summary);
}
