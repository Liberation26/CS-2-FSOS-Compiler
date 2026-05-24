using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oryn.Compiler;

internal sealed class ModuleApiContractCatalog
{
    private readonly SymbolTable ContractSymbols;

    private ModuleApiContractCatalog(SymbolTable ContractSymbols)
    {
        this.ContractSymbols = ContractSymbols;
    }

    public IReadOnlyCollection<BindingRecord> ApprovedCallContracts => ContractSymbols.Bindings
        .Where(Contract => Contract.AllowedInKernel)
        .OrderBy(Contract => Contract.ModuleName, StringComparer.Ordinal)
        .ThenBy(Contract => Contract.ManagedName, StringComparer.Ordinal)
        .ToList();

    public static ModuleApiContractCatalog CreateDefault()
    {
        string ContractDirectory = FindContractDirectory();
        return LoadFromDirectory(ContractDirectory);
    }

    public static ModuleApiContractCatalog LoadFromDirectory(string ContractDirectory)
    {
        if (!Directory.Exists(ContractDirectory))
        {
            throw new OrynCompileException($"Module API contract directory not found: {ContractDirectory}");
        }

        List<BindingRecord> Records = new();
        JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        foreach (string ContractPath in Directory.EnumerateFiles(ContractDirectory, "*.api-contract.json").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            ModuleApiContractFile? FileModel;
            try
            {
                FileModel = JsonSerializer.Deserialize<ModuleApiContractFile>(File.ReadAllText(ContractPath), Options);
            }
            catch (JsonException Exception)
            {
                throw new OrynCompileException($"Invalid module API contract JSON: {ContractPath}: {Exception.Message}");
            }

            if (FileModel is null || string.IsNullOrWhiteSpace(FileModel.Module))
            {
                throw new OrynCompileException($"Module API contract is missing module: {ContractPath}");
            }

            if (string.IsNullOrWhiteSpace(FileModel.Namespace))
            {
                throw new OrynCompileException($"Module API contract is missing namespace: {ContractPath}");
            }

            if (FileModel.Methods is null || FileModel.Methods.Count == 0)
            {
                throw new OrynCompileException($"Module API contract has no methods: {ContractPath}");
            }

            foreach (ModuleApiContractMethod Method in FileModel.Methods)
            {
                if (string.IsNullOrWhiteSpace(Method.ManagedName))
                {
                    throw new OrynCompileException($"Module API contract method is missing managedName: {ContractPath}");
                }

                if (string.IsNullOrWhiteSpace(Method.NativeSymbol))
                {
                    throw new OrynCompileException($"Module API contract method {Method.ManagedName} is missing nativeSymbol: {ContractPath}");
                }

                string TypeName = string.IsNullOrWhiteSpace(Method.TypeName) ? FileModel.Module : Method.TypeName;
                string MethodName = string.IsNullOrWhiteSpace(Method.MethodName) ? ExtractMethodName(Method.ManagedName) : Method.MethodName;
                string Signature = Method.Signature ?? $"void {MethodName}()";
                IReadOnlyList<string> ArgumentTypes = Method.ArgumentTypes is null ? new List<string>() : Method.ArgumentTypes;

                Records.Add(new BindingRecord(
                    FileModel.Module,
                    FileModel.Namespace,
                    TypeName,
                    MethodName,
                    Method.ManagedName,
                    Signature,
                    Method.NativeSymbol,
                    ArgumentTypes,
                    FileModel.MinimumStage,
                    FileModel.AllowedInKernel && Method.AllowedFromCSharpKernel,
                    Method.Summary ?? string.Empty));
            }
        }

        if (Records.Count == 0)
        {
            throw new OrynCompileException($"No module API contract records found in: {ContractDirectory}");
        }

        return new ModuleApiContractCatalog(new SymbolTable(Records));
    }

    public void RequireApprovedContract(BindingRecord Binding)
    {
        if (!ContractSymbols.TryResolve(Binding.ManagedName, out BindingRecord? Contract) || Contract is null)
        {
            throw new OrynCompileException($"Stage 8 module API contract rejected call: {Binding.ManagedName}. No approved API contract exists for this managed call.");
        }

        if (!Contract.AllowedInKernel)
        {
            throw new OrynCompileException($"Stage 8 module API contract rejected call: {Binding.ManagedName}. The contract exists but is not approved for C# kernel code.");
        }

        if (!Contract.NativeSymbol.Equals(Binding.NativeSymbol, StringComparison.Ordinal))
        {
            throw new OrynCompileException($"Stage 8 module API contract rejected call: {Binding.ManagedName}. Contract native symbol {Contract.NativeSymbol} does not match binding native symbol {Binding.NativeSymbol}.");
        }

        if (!Contract.NamespaceName.Equals(Binding.NamespaceName, StringComparison.Ordinal) || !Contract.TypeName.Equals(Binding.TypeName, StringComparison.Ordinal))
        {
            throw new OrynCompileException($"Stage 8 module API contract rejected call: {Binding.ManagedName}. Contract namespace/type does not match binding metadata.");
        }

        if (Contract.ArgumentCount != Binding.ArgumentCount)
        {
            throw new OrynCompileException($"Stage 8 module API contract rejected call: {Binding.ManagedName}. Contract argument count {Contract.ArgumentCount} does not match binding argument count {Binding.ArgumentCount}.");
        }

        for (int Index = 0; Index < Binding.ArgumentCount; Index++)
        {
            if (!Contract.ArgumentTypeNames[Index].Equals(Binding.ArgumentTypeNames[Index], StringComparison.Ordinal))
            {
                throw new OrynCompileException($"Stage 8 module API contract rejected call: {Binding.ManagedName}. Contract argument {Index + 1} type {Contract.ArgumentTypeNames[Index]} does not match binding type {Binding.ArgumentTypeNames[Index]}.");
            }
        }
    }

    private static string FindContractDirectory()
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
                string Candidate = Path.Combine(DirectoryInfo.FullName, "Source", "Sdk", "ApiContracts");
                if (Directory.Exists(Candidate))
                {
                    return Candidate;
                }

                DirectoryInfo = DirectoryInfo.Parent;
            }
        }

        throw new OrynCompileException("Could not locate Source/Sdk/ApiContracts. Run the compiler from inside an Oryn source tree or set the working directory to the repository root.");
    }

    private static string ExtractMethodName(string ManagedName)
    {
        int DotIndex = ManagedName.LastIndexOf('.');
        if (DotIndex < 0 || DotIndex == ManagedName.Length - 1)
        {
            throw new OrynCompileException($"API contract managedName must be Type.Method: {ManagedName}");
        }

        return ManagedName[(DotIndex + 1)..];
    }

    private sealed record ModuleApiContractFile(
        [property: JsonPropertyName("contractVersion")] string ContractVersion,
        [property: JsonPropertyName("module")] string Module,
        [property: JsonPropertyName("namespace")] string Namespace,
        [property: JsonPropertyName("minimumStage")] int MinimumStage,
        [property: JsonPropertyName("allowedInKernel")] bool AllowedInKernel,
        [property: JsonPropertyName("methods")] List<ModuleApiContractMethod> Methods);

    private sealed record ModuleApiContractMethod(
        [property: JsonPropertyName("managedName")] string ManagedName,
        [property: JsonPropertyName("typeName")] string? TypeName,
        [property: JsonPropertyName("methodName")] string? MethodName,
        [property: JsonPropertyName("signature")] string? Signature,
        [property: JsonPropertyName("nativeSymbol")] string NativeSymbol,
        [property: JsonPropertyName("allowedFromCSharpKernel")] bool AllowedFromCSharpKernel,
        [property: JsonPropertyName("argumentTypes")] List<string>? ArgumentTypes,
        [property: JsonPropertyName("summary")] string? Summary);
}
