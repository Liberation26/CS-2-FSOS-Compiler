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

    public IReadOnlyCollection<string> ApprovedNamespaces => Bindings
        .Where(Binding => Binding.AllowedInKernel)
        .Select(Binding => Binding.NamespaceName)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(NamespaceName => NamespaceName, StringComparer.Ordinal)
        .ToList();

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

            if (string.IsNullOrWhiteSpace(FileModel.Namespace))
            {
                throw new OrynCompileException($"Binding JSON is missing namespace: {BindingPath}");
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

                string TypeName = string.IsNullOrWhiteSpace(Method.TypeName) ? FileModel.Module : Method.TypeName;
                string MethodName = string.IsNullOrWhiteSpace(Method.MethodName) ? ExtractMethodName(Method.ManagedName) : Method.MethodName;
                string Signature = Method.Signature ?? $"void {MethodName}()";
                IReadOnlyList<string> ArgumentTypeNames = Method.ArgumentTypes is { Count: > 0 }
                    ? Method.ArgumentTypes
                    : ExtractSignatureArgumentTypes(Signature, Method.ManagedName, BindingPath);
                int ArgumentCount = Method.ArgumentCount ?? ArgumentTypeNames.Count;
                if (ArgumentCount != ArgumentTypeNames.Count)
                {
                    throw new OrynCompileException($"Binding JSON method {Method.ManagedName} has argumentCount {ArgumentCount} but {ArgumentTypeNames.Count} argument type(s): {BindingPath}");
                }

                Records.Add(new BindingRecord(
                    FileModel.Module,
                    FileModel.Namespace,
                    TypeName,
                    MethodName,
                    Method.ManagedName,
                    Signature,
                    Method.NativeSymbol,
                    ArgumentTypeNames,
                    FileModel.Stage,
                    Method.AllowedInKernel,
                    Method.Summary ?? string.Empty));
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

    private static string ExtractMethodName(string ManagedName)
    {
        int DotIndex = ManagedName.LastIndexOf('.');
        if (DotIndex < 0 || DotIndex == ManagedName.Length - 1)
        {
            throw new OrynCompileException($"Binding managedName must be Type.Method: {ManagedName}");
        }

        return ManagedName[(DotIndex + 1)..];
    }

    private static IReadOnlyList<string> ExtractSignatureArgumentTypes(string Signature, string ManagedName, string BindingPath)
    {
        int Open = Signature.IndexOf('(');
        int Close = Signature.LastIndexOf(')');
        if (Open < 0 || Close < Open)
        {
            throw new OrynCompileException($"Binding JSON method {ManagedName} has invalid signature: {Signature}");
        }

        string ArgumentsText = Signature.Substring(Open + 1, Close - Open - 1).Trim();
        if (ArgumentsText.Length == 0)
        {
            return Array.Empty<string>();
        }

        List<string> ArgumentTypes = new();
        foreach (string Argument in ArgumentsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string[] Parts = Argument.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (Parts.Length == 0)
            {
                throw new OrynCompileException($"Binding JSON method {ManagedName} has invalid argument in signature: {Signature}: {BindingPath}");
            }

            ArgumentTypes.Add(NormalizeTypeName(Parts[0]));
        }

        return ArgumentTypes;
    }

    private static string NormalizeTypeName(string TypeName)
    {
        return TypeName switch
        {
            "string" => "String",
            "int" => "Int32",
            "void" => "Void",
            _ => TypeName
        };
    }

    private sealed record BindingFile(
        [property: JsonPropertyName("module")] string Module,
        [property: JsonPropertyName("namespace")] string Namespace,
        [property: JsonPropertyName("stage")] int Stage,
        [property: JsonPropertyName("methods")] List<BindingMethod> Methods);

    private sealed record BindingMethod(
        [property: JsonPropertyName("managedName")] string ManagedName,
        [property: JsonPropertyName("typeName")] string? TypeName,
        [property: JsonPropertyName("methodName")] string? MethodName,
        [property: JsonPropertyName("signature")] string? Signature,
        [property: JsonPropertyName("nativeSymbol")] string NativeSymbol,
        [property: JsonPropertyName("allowedInKernel")] bool AllowedInKernel,
        [property: JsonPropertyName("argumentCount")] int? ArgumentCount,
        [property: JsonPropertyName("argumentTypes")] List<string>? ArgumentTypes,
        [property: JsonPropertyName("summary")] string? Summary);
}
