using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Oryn.Compiler;

internal static class Program
{
    private const string Version = "0.1.0";

    private static readonly IReadOnlyDictionary<string, BindingRecord> Bindings = new Dictionary<string, BindingRecord>(StringComparer.Ordinal)
    {
        ["Diagnostics.WriteOk"] = new("Diagnostics", "Diagnostics.WriteOk", "Diagnostics_WriteOk", 1, true),
        ["Diagnostics.WriteWarn"] = new("Diagnostics", "Diagnostics.WriteWarn", "Diagnostics_WriteWarn", 1, true),
        ["Diagnostics.WriteFail"] = new("Diagnostics", "Diagnostics.WriteFail", "Diagnostics_WriteFail", 1, true),
        ["Memory.Initialize"] = new("Memory", "Memory.Initialize", "Memory_Initialize", 0, true),
        ["Cpu.HaltForever"] = new("Cpu", "Cpu.HaltForever", "Cpu_HaltForever", 0, true)
    };

    private static int Main(string[] Args)
    {
        Console.WriteLine("[ OK ] Oryn.Compiler started.");
        Console.WriteLine($"[ OK ] Version: {Version}");

        if (Args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        string Command = Args[0];

        if (Command.Equals("modules", StringComparison.OrdinalIgnoreCase))
        {
            PrintModules();
            return 0;
        }

        if (Command.Equals("compile", StringComparison.OrdinalIgnoreCase))
        {
            return Compile(Args.Skip(1).ToArray());
        }

        Console.WriteLine($"[FAIL] Unknown command: {Command}");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  oryn compiler modules");
        Console.WriteLine("  oryn compiler compile <source.cs> --target x64-elf --output <output.o>");
        Console.WriteLine();
        Console.WriteLine("Stage 1 compile output:");
        Console.WriteLine("  <output>.stage1.json    lowered Oryn IR and backend manifest");
        Console.WriteLine("  <output>.generated.c     freestanding C backend snippet");
        Console.WriteLine("  <output>.generated.S     x64 assembly backend sketch");
        Console.WriteLine("  <output>                 text placeholder for the future ELF64 object");
    }

    private static void PrintModules()
    {
        Console.WriteLine("[ OK ] Available starter modules:");
        Console.WriteLine("  Diagnostics  namespace=Oryn.Kernel.Diagnostics native=Diagnostics_WriteOk/Diagnostics_WriteWarn/Diagnostics_WriteFail");
        Console.WriteLine("  Cpu          namespace=Oryn.Kernel.Cpu         native=Cpu_HaltForever");
        Console.WriteLine("  Memory       namespace=Oryn.Kernel.Memory      native=Memory_Initialize");
    }

    private static int Compile(string[] Args)
    {
        if (Args.Length == 0)
        {
            Console.WriteLine("[FAIL] Missing source file.");
            PrintUsage();
            return 1;
        }

        string SourcePath = Args[0];
        string Target = ReadOption(Args, "--target") ?? "x64-elf";
        string? OutputPath = ReadOption(Args, "--output");

        if (!Target.Equals("x64-elf", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[FAIL] Unsupported Stage 1 target: {Target}");
            Console.WriteLine("[ OK ] Supported target: x64-elf");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            Console.WriteLine("[FAIL] Missing --output <output.o>.");
            return 1;
        }

        if (!File.Exists(SourcePath))
        {
            Console.WriteLine($"[FAIL] Source file not found: {SourcePath}");
            return 1;
        }

        string SourceText = File.ReadAllText(SourcePath);
        List<string> ValidationFailures = ValidateSafeSubset(SourceText);
        if (ValidationFailures.Count > 0)
        {
            Console.WriteLine("[FAIL] Safe-subset validation failed.");
            foreach (string Failure in ValidationFailures)
            {
                Console.WriteLine($"[FAIL] {Failure}");
            }

            return 2;
        }

        ParseResult ParseResult;
        try
        {
            ParseResult = ParseKernelMain(SourcePath, SourceText);
        }
        catch (OrynCompileException Exception)
        {
            Console.WriteLine($"[FAIL] {Exception.Message}");
            return 2;
        }

        BackendResult BackendResult = LowerToBackend(ParseResult, Target, OutputPath);
        WriteBackendFiles(BackendResult);

        Console.WriteLine($"[ OK ] Parsed source: {SourcePath}");
        Console.WriteLine($"[ OK ] Safe-subset validation passed.");
        Console.WriteLine($"[ OK ] Lowered calls: {ParseResult.Calls.Count}");
        Console.WriteLine($"[ OK ] Backend target: {Target}");
        Console.WriteLine($"[ OK ] Wrote IR manifest: {BackendResult.ManifestPath}");
        Console.WriteLine($"[ OK ] Wrote C backend: {BackendResult.CPath}");
        Console.WriteLine($"[ OK ] Wrote x64 backend sketch: {BackendResult.AssemblyPath}");
        Console.WriteLine($"[ OK ] Wrote object placeholder: {BackendResult.ObjectPath}");
        return 0;
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

    private static List<string> ValidateSafeSubset(string SourceText)
    {
        List<string> Failures = new();
        string[] ForbiddenTokens =
        {
            " new ", " throw ", " try ", " catch ", " finally ", " async ", " await ", " delegate ",
            " dynamic ", " foreach ", " lock ", " typeof", " sizeof", " stackalloc", " fixed ", " unsafe "
        };

        string SearchText = " " + Regex.Replace(SourceText, @"\s+", " ") + " ";
        foreach (string Token in ForbiddenTokens)
        {
            if (SearchText.Contains(Token, StringComparison.Ordinal))
            {
                Failures.Add($"Forbidden Stage 1 construct detected: {Token.Trim()}");
            }
        }

        if (SourceText.Contains("=>", StringComparison.Ordinal))
        {
            Failures.Add("Forbidden Stage 1 construct detected: lambda/expression body.");
        }

        if (!SourceText.Contains("public static void Main", StringComparison.Ordinal))
        {
            Failures.Add("Stage 1 requires public static void Main().");
        }

        return Failures;
    }

    private static ParseResult ParseKernelMain(string SourcePath, string SourceText)
    {
        Match MainMatch = Regex.Match(SourceText, @"public\s+static\s+void\s+Main\s*\(\s*\)\s*\{");
        if (!MainMatch.Success)
        {
            throw new OrynCompileException("Could not find public static void Main().");
        }

        int BodyStart = MainMatch.Index + MainMatch.Length - 1;
        int BodyEnd = FindMatchingBrace(SourceText, BodyStart);
        string BodyText = SourceText.Substring(BodyStart + 1, BodyEnd - BodyStart - 1);
        string CleanBody = StripComments(BodyText);
        List<CallRecord> Calls = new();

        foreach (string Statement in SplitStatements(CleanBody))
        {
            if (string.IsNullOrWhiteSpace(Statement))
            {
                continue;
            }

            Calls.Add(ParseStatement(Statement.Trim()));
        }

        if (Calls.Count == 0)
        {
            throw new OrynCompileException("Kernel Main does not contain any Stage 1 module calls.");
        }

        return new ParseResult(SourcePath, Calls);
    }

    private static int FindMatchingBrace(string Text, int OpenBraceIndex)
    {
        int Depth = 0;
        for (int Index = OpenBraceIndex; Index < Text.Length; Index++)
        {
            if (Text[Index] == '{')
            {
                Depth++;
            }
            else if (Text[Index] == '}')
            {
                Depth--;
                if (Depth == 0)
                {
                    return Index;
                }
            }
        }

        throw new OrynCompileException("Could not find the closing brace for Kernel.Main().");
    }

    private static string StripComments(string Text)
    {
        string WithoutBlockComments = Regex.Replace(Text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(WithoutBlockComments, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }

    private static IEnumerable<string> SplitStatements(string BodyText)
    {
        StringBuilder Current = new();
        bool InString = false;
        bool Escape = false;

        foreach (char Character in BodyText)
        {
            Current.Append(Character);

            if (InString)
            {
                if (Escape)
                {
                    Escape = false;
                }
                else if (Character == '\\')
                {
                    Escape = true;
                }
                else if (Character == '"')
                {
                    InString = false;
                }
            }
            else if (Character == '"')
            {
                InString = true;
            }
            else if (Character == ';')
            {
                yield return Current.ToString();
                Current.Clear();
            }
        }

        if (!string.IsNullOrWhiteSpace(Current.ToString()))
        {
            yield return Current.ToString();
        }
    }

    private static CallRecord ParseStatement(string Statement)
    {
        Match CallMatch = Regex.Match(Statement, @"^(?<Class>[A-Za-z_][A-Za-z0-9_]*)\.(?<Method>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<Args>.*)\)\s*;$", RegexOptions.Singleline);
        if (!CallMatch.Success)
        {
            throw new OrynCompileException($"Unsupported Stage 1 statement: {Statement}");
        }

        string ManagedName = CallMatch.Groups["Class"].Value + "." + CallMatch.Groups["Method"].Value;
        if (!Bindings.TryGetValue(ManagedName, out BindingRecord? Binding))
        {
            throw new OrynCompileException($"No approved Stage 1 binding for call: {ManagedName}");
        }

        string ArgumentText = CallMatch.Groups["Args"].Value.Trim();
        List<string> Arguments = ParseArguments(ManagedName, ArgumentText, Binding.ArgumentCount);
        return new CallRecord(ManagedName, Binding.NativeSymbol, Arguments);
    }

    private static List<string> ParseArguments(string ManagedName, string ArgumentText, int ExpectedCount)
    {
        if (ExpectedCount == 0)
        {
            if (!string.IsNullOrWhiteSpace(ArgumentText))
            {
                throw new OrynCompileException($"Call {ManagedName} does not accept Stage 1 arguments.");
            }

            return new List<string>();
        }

        if (ExpectedCount == 1)
        {
            Match StringMatch = Regex.Match(ArgumentText, "^\\\"(?<Value>(?:\\\\.|[^\\\"])*)\\\"$");
            if (!StringMatch.Success)
            {
                throw new OrynCompileException($"Call {ManagedName} currently accepts one string literal only.");
            }

            return new List<string> { Regex.Unescape(StringMatch.Groups["Value"].Value) };
        }

        throw new OrynCompileException($"Unsupported binding argument count for {ManagedName}: {ExpectedCount}");
    }

    private static BackendResult LowerToBackend(ParseResult ParseResult, string Target, string OutputPath)
    {
        string FullOutputPath = Path.GetFullPath(OutputPath);
        string OutputDirectory = Path.GetDirectoryName(FullOutputPath) ?? Directory.GetCurrentDirectory();
        string BaseName = Path.GetFileNameWithoutExtension(FullOutputPath);
        Directory.CreateDirectory(OutputDirectory);

        List<IrInstruction> Instructions = new();
        for (int Index = 0; Index < ParseResult.Calls.Count; Index++)
        {
            CallRecord Call = ParseResult.Calls[Index];
            Instructions.Add(new IrInstruction(Index, "CallNative", Call.ManagedName, Call.NativeSymbol, Call.Arguments));
        }

        string ManifestPath = Path.Combine(OutputDirectory, BaseName + ".stage1.json");
        string CPath = Path.Combine(OutputDirectory, BaseName + ".generated.c");
        string AssemblyPath = Path.Combine(OutputDirectory, BaseName + ".generated.S");

        CompilerManifest Manifest = new(
            Version,
            Target,
            ParseResult.SourcePath,
            FullOutputPath,
            "Kernel_Main",
            Instructions,
            "Stage 1 emits C and x64 assembly backend text. The .o file is a placeholder until the ELF64 object writer lands.");

        string CSource = EmitCSource(Instructions);
        string AssemblySource = EmitAssemblySource(Instructions);
        string ObjectPlaceholder = EmitObjectPlaceholder(Manifest);

        return new BackendResult(ManifestPath, CPath, AssemblyPath, FullOutputPath, Manifest, CSource, AssemblySource, ObjectPlaceholder);
    }

    private static string EmitCSource(IReadOnlyList<IrInstruction> Instructions)
    {
        StringBuilder Builder = new();
        Builder.AppendLine("#include <stdint.h>");
        Builder.AppendLine();
        Builder.AppendLine("extern void Diagnostics_WriteOk(const char* Message);");
        Builder.AppendLine("extern void Diagnostics_WriteWarn(const char* Message);");
        Builder.AppendLine("extern void Diagnostics_WriteFail(const char* Message);");
        Builder.AppendLine("extern void Memory_Initialize(void);");
        Builder.AppendLine("extern void Cpu_HaltForever(void);");
        Builder.AppendLine();

        for (int Index = 0; Index < Instructions.Count; Index++)
        {
            if (Instructions[Index].Arguments.Count == 1)
            {
                Builder.AppendLine($"static const char Oryn_String_{Index}[] = \"{EscapeCString(Instructions[Index].Arguments[0])}\";");
            }
        }

        Builder.AppendLine();
        Builder.AppendLine("void Kernel_Main(void)");
        Builder.AppendLine("{");
        for (int Index = 0; Index < Instructions.Count; Index++)
        {
            IrInstruction Instruction = Instructions[Index];
            if (Instruction.Arguments.Count == 1)
            {
                Builder.AppendLine($"    {Instruction.NativeSymbol}(Oryn_String_{Index});");
            }
            else
            {
                Builder.AppendLine($"    {Instruction.NativeSymbol}();");
            }
        }

        Builder.AppendLine("}");
        return Builder.ToString();
    }

    private static string EmitAssemblySource(IReadOnlyList<IrInstruction> Instructions)
    {
        StringBuilder Builder = new();
        Builder.AppendLine(".section .text");
        Builder.AppendLine(".global Kernel_Main");
        Builder.AppendLine("Kernel_Main:");
        Builder.AppendLine("    push %rbp");
        Builder.AppendLine("    mov %rsp, %rbp");

        for (int Index = 0; Index < Instructions.Count; Index++)
        {
            IrInstruction Instruction = Instructions[Index];
            if (Instruction.Arguments.Count == 1)
            {
                Builder.AppendLine($"    lea Oryn_String_{Index}(%rip), %rdi");
            }

            Builder.AppendLine($"    call {Instruction.NativeSymbol}");
        }

        Builder.AppendLine("    pop %rbp");
        Builder.AppendLine("    ret");
        Builder.AppendLine();
        Builder.AppendLine(".section .rodata");

        for (int Index = 0; Index < Instructions.Count; Index++)
        {
            if (Instructions[Index].Arguments.Count == 1)
            {
                Builder.AppendLine($"Oryn_String_{Index}:");
                Builder.AppendLine($"    .asciz \"{EscapeCString(Instructions[Index].Arguments[0])}\"");
            }
        }

        return Builder.ToString();
    }

    private static string EmitObjectPlaceholder(CompilerManifest Manifest)
    {
        StringBuilder Builder = new();
        Builder.AppendLine("Oryn Stage 1 object placeholder");
        Builder.AppendLine($"Version: {Manifest.CompilerVersion}");
        Builder.AppendLine($"Target: {Manifest.Target}");
        Builder.AppendLine($"EntrySymbol: {Manifest.EntrySymbol}");
        Builder.AppendLine();
        Builder.AppendLine("This file intentionally is not a linkable ELF64 object yet.");
        Builder.AppendLine("Use the generated .c or .S backend output for the first backend proof.");
        return Builder.ToString();
    }

    private static void WriteBackendFiles(BackendResult BackendResult)
    {
        JsonSerializerOptions Options = new() { WriteIndented = true };
        File.WriteAllText(BackendResult.ManifestPath, JsonSerializer.Serialize(BackendResult.Manifest, Options));
        File.WriteAllText(BackendResult.CPath, BackendResult.CSource);
        File.WriteAllText(BackendResult.AssemblyPath, BackendResult.AssemblySource);
        File.WriteAllText(BackendResult.ObjectPath, BackendResult.ObjectPlaceholder);
    }

    private static string EscapeCString(string Value)
    {
        return Value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}

internal sealed record BindingRecord(string ModuleName, string ManagedName, string NativeSymbol, int ArgumentCount, bool AllowedInKernel);

internal sealed record CallRecord(string ManagedName, string NativeSymbol, IReadOnlyList<string> Arguments);

internal sealed record ParseResult(string SourcePath, IReadOnlyList<CallRecord> Calls);

internal sealed record IrInstruction(int Index, string OpCode, string ManagedName, string NativeSymbol, IReadOnlyList<string> Arguments);

internal sealed record CompilerManifest(
    string CompilerVersion,
    string Target,
    string SourcePath,
    string OutputPath,
    string EntrySymbol,
    IReadOnlyList<IrInstruction> Instructions,
    string Notes);

internal sealed record BackendResult(
    string ManifestPath,
    string CPath,
    string AssemblyPath,
    string ObjectPath,
    CompilerManifest Manifest,
    string CSource,
    string AssemblySource,
    string ObjectPlaceholder);

internal sealed class OrynCompileException : Exception
{
    public OrynCompileException(string Message)
        : base(Message)
    {
    }
}
