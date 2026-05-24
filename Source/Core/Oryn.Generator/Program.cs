using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Oryn.Generator;

internal static class Program
{
    private const string Version = "2.0.1";
    private static readonly string[] MandatoryKernelModules = { "Runtime", "Diagnostics", "Panic", "Cpu", "ManifestLoader" };
    private static readonly string[] DefaultUserSelectedModules = Array.Empty<string>();
    private static readonly string[] AvailableUserSelectableModules = { "Memory" };
    private static readonly string[] DisplayUserSelectableModules = { "None", "Memory" };
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private const string AnsiReset = "\u001b[0m";
    private const string AnsiOptionColour = "\u001b[96m";
    private const string AnsiDefaultColour = "\u001b[93m";

    private static int Main(string[] Args)
    {
        Console.WriteLine("[ OK ] Oryn.Generator started.");
        Console.WriteLine($"[ OK ] Version: {Version}");

        try
        {
            if (Args.Length == 0 || Args[0].Equals("help", StringComparison.OrdinalIgnoreCase) || Args[0].Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                PrintUsage();
                return 0;
            }

            string Command = Args[0];
            if (Command.Equals("generate", StringComparison.OrdinalIgnoreCase))
            {
                Generate(Args.Skip(1).ToArray());
                return 0;
            }

            if (Command.Equals("modules", StringComparison.OrdinalIgnoreCase))
            {
                PrintModules();
                return 0;
            }

            Console.WriteLine($"[FAIL] Unknown generator command: {Command}");
            PrintUsage();
            return 1;
        }
        catch (Exception Exception)
        {
            Console.WriteLine($"[FAIL] {Exception.Message}");
            return 2;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project Source/Core/Oryn.Generator -- generate");
        Console.WriteLine("  dotnet run --project Source/Core/Oryn.Generator -- generate --os-title <title> --os-name <StrictName> [--kernel-name <StrictKernelName>] [--modules None|Memory] [--vm-display-mode Headless|Visual]");
        Console.WriteLine("  dotnet run --project Source/Core/Oryn.Generator -- modules");
        Console.WriteLine();
        Console.WriteLine("Diagnostics and Panic are always enabled. Mandatory kernel modules are not user-selected modules.");
        Console.WriteLine("Headless VM mode closes automatically after the proof timeout. Visual VM mode stays open until the user closes the VM window.");
    }

    private static void PrintModules()
    {
        Console.WriteLine("[ OK ] Mandatory kernel modules, always linked and hidden from user selection:");
        foreach (string Module in MandatoryKernelModules)
        {
            Console.WriteLine($"  [mandatory] {Module}");
        }

        Console.WriteLine("[ OK ] User-selectable modules for 2.0.1:");
        Console.WriteLine("  [available] None");
        foreach (string Module in AvailableUserSelectableModules)
        {
            Console.WriteLine($"  [available] {Module}");
        }
    }

    private static void Generate(string[] Args)
    {
        string ProjectRoot = LocateProjectRoot();
        ValidateQuestionFiles(ProjectRoot);

        bool NonInteractive = HasFlag(Args, "--non-interactive") || HasFlag(Args, "--defaults");

        PrintQuestionHelpBanner();

        string OsTitle = ReadOption(Args, "--os-title") ?? ReadOption(Args, "--title") ?? Prompt("OS title", "My Oryn OS", "Friendly display title. Spaces are allowed here only because this is not used as a path or symbol.", NonInteractive);
        string OsName = ReadOption(Args, "--os-name") ?? ReadOption(Args, "--name") ?? Prompt("OS name", SanitizeStrictName(OsTitle), "Strict technical name. Use letters and numbers only. No spaces. Must start with a letter.", NonInteractive);
        OsName = NormalizeStrictName(OsName, "OS name");
        string KernelName = ReadOption(Args, "--kernel-name") ?? Prompt("Kernel name", OsName + "Kernel", "Strict kernel name. Use letters and numbers only. No spaces. Must start with a letter.", NonInteractive);
        KernelName = NormalizeStrictName(KernelName, "Kernel name");
        string Target = ReadOption(Args, "--target") ?? Prompt("Target architecture", "x64-elf", "Valid answer: x64-elf", NonInteractive);
        Target = NormalizeTarget(Target);
        string VmProfile = ReadOption(Args, "--vm-profile") ?? Prompt("Virtual machine profile", "RunQemu", "Valid answer: RunQemu", NonInteractive);
        VmProfile = NormalizeVmProfile(VmProfile);
        string VmDisplayMode = ReadOption(Args, "--vm-display-mode") ?? ReadOption(Args, "--display-mode") ?? ReadOption(Args, "--qemu-display") ?? Prompt("VM display mode", "Headless", "Valid answers: Headless or Visual. Headless closes automatically after the proof timeout. Visual opens a QEMU window and stays open until you close it.", NonInteractive);
        VmDisplayMode = NormalizeVmDisplayMode(VmDisplayMode);
        string BuildMode = ReadOption(Args, "--build-mode") ?? Prompt("Build mode", "Debug", "Valid answer: Debug", NonInteractive);
        BuildMode = NormalizeBuildMode(BuildMode);

        Console.WriteLine("[ OK ] [GENERATOR] Mandatory kernel modules are always linked and hidden from user selection:");
        Console.WriteLine("[ OK ] [GENERATOR]   " + string.Join(", ", MandatoryKernelModules));
        Console.WriteLine("[ OK ] [GENERATOR] Diagnostics and Panic are always enabled.");
        Console.WriteLine("[ OK ] [GENERATOR] User-selectable modules for 2.0.1:");
        Console.WriteLine("[ OK ] [GENERATOR]   None, " + string.Join(", ", AvailableUserSelectableModules));

        string ModulesText = ReadOption(Args, "--modules") ?? Prompt("Additional user-selected modules", FormatModuleDefault(DefaultUserSelectedModules), "Valid answers: None or Memory. Use comma-separated values only when more optional modules are available. Mandatory boot/kernel modules are linked automatically and must not be typed here.", NonInteractive);

        string SafeOsName = OsName;
        string SafeKernelName = KernelName;
        string[] UserSelectedModules = ParseUserSelectedModules(ModulesText);
        ValidateUserSelectedModules(UserSelectedModules);

        string OsRoot = Path.Combine(ProjectRoot, "OSes", SafeOsName);
        string AnswersDirectory = Path.Combine(OsRoot, "Answers");
        string SourceDirectory = Path.Combine(OsRoot, "Source");
        string TemplateDirectory = Path.Combine(OsRoot, "Templates");
        string BuildDirectory = Path.Combine(OsRoot, "Build");

        Directory.CreateDirectory(AnswersDirectory);
        Directory.CreateDirectory(SourceDirectory);
        Directory.CreateDirectory(TemplateDirectory);
        Directory.CreateDirectory(BuildDirectory);

        string TemplatePath = Path.Combine(TemplateDirectory, "Kernel.template.cs");
        string SourcePath = Path.Combine(SourceDirectory, "Kernel.cs");
        string AnswersPath = Path.Combine(AnswersDirectory, SafeOsName + ".answers.json");
        string ManifestPath = Path.Combine(OsRoot, "manifest.json");
        string ReadmePath = Path.Combine(OsRoot, "README.md");

        File.WriteAllText(TemplatePath, BuildKernelTemplate(), Utf8NoBom);
        File.WriteAllText(SourcePath, BuildUncomposedKernelSource(OsTitle, SafeOsName, SafeKernelName), Utf8NoBom);
        File.WriteAllText(AnswersPath, BuildAnswersJson(OsTitle, SafeOsName, SafeKernelName, Target, VmProfile, VmDisplayMode, BuildMode, UserSelectedModules), Utf8NoBom);
        File.WriteAllText(ManifestPath, BuildManifestJson(OsTitle, SafeOsName, SafeKernelName, Target, VmProfile, VmDisplayMode, BuildMode, UserSelectedModules), Utf8NoBom);
        File.WriteAllText(ReadmePath, BuildOsReadme(OsTitle, SafeOsName, SafeKernelName), Utf8NoBom);

        Console.WriteLine($"[ OK ] [GENERATOR] OS folder created: {OsRoot}");
        Console.WriteLine($"[ OK ] [GENERATOR] Answers saved: {AnswersPath}");
        Console.WriteLine($"[ OK ] [GENERATOR] Manifest saved: {ManifestPath}");
        Console.WriteLine($"[ OK ] [GENERATOR] VM display mode: {VmDisplayMode}");
        Console.WriteLine($"[ OK ] [GENERATOR] Mandatory kernel modules: {string.Join(", ", MandatoryKernelModules)}");
        Console.WriteLine($"[ OK ] [GENERATOR] User-selected modules: {(UserSelectedModules.Length == 0 ? "<none>" : string.Join(", ", UserSelectedModules))}");
    }

    private static void PrintQuestionHelpBanner()
    {
        Console.WriteLine("[ OK ] [GENERATOR] Each question shows its expected answer format, accepted options, and default.");
        Console.WriteLine("[ OK ] [GENERATOR] Accepted options are shown in a different colour when the terminal supports ANSI colours.");
        Console.WriteLine("[ OK ] [GENERATOR] Press Enter to accept the default shown in square brackets.");
    }

    private static string Prompt(string Label, string DefaultValue, string ExpectedAnswer, bool NonInteractive)
    {
        string ColouredExpectedAnswer = ColourOptionText(ExpectedAnswer);
        string ColouredDefaultValue = ColourDefaultText(DefaultValue);

        if (NonInteractive)
        {
            Console.WriteLine($"[ OK ] [QUESTION ] {Label}");
            Console.WriteLine($"[ OK ] [OPTIONS  ] {ColouredExpectedAnswer}");
            Console.WriteLine($"[ OK ] [DEFAULT  ] {ColouredDefaultValue}");
            return DefaultValue;
        }

        Console.WriteLine($"[QUESTION] {Label}");
        Console.WriteLine($"[OPTIONS ] {ColouredExpectedAnswer}");
        Console.Write($"[ANSWER  ] [{ColouredDefaultValue}]: ");
        string? Answer = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(Answer))
        {
            return DefaultValue;
        }

        return Answer.Trim();
    }

    private static string ColourOptionText(string Text)
    {
        if (!UseAnsiColour())
        {
            return Text;
        }

        return AnsiOptionColour + Text + AnsiReset;
    }

    private static string ColourDefaultText(string Text)
    {
        if (!UseAnsiColour())
        {
            return Text;
        }

        return AnsiDefaultColour + Text + AnsiReset;
    }

    private static bool UseAnsiColour()
    {
        if (Console.IsOutputRedirected)
        {
            return false;
        }

        string? NoColour = Environment.GetEnvironmentVariable("NO_COLOR");
        if (!string.IsNullOrWhiteSpace(NoColour))
        {
            return false;
        }

        string? OrynNoColour = Environment.GetEnvironmentVariable("ORYN_NO_COLOR");
        if (!string.IsNullOrWhiteSpace(OrynNoColour))
        {
            return false;
        }

        return true;
    }

    private static void ValidateQuestionFiles(string ProjectRoot)
    {
        string QuestionDirectory = Path.Combine(ProjectRoot, "Questions");
        if (!Directory.Exists(QuestionDirectory))
        {
            throw new InvalidOperationException("Question directory not found: " + QuestionDirectory);
        }

        string[] QuestionFiles = Directory.GetFiles(QuestionDirectory, "*.question.json").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
        if (QuestionFiles.Length == 0)
        {
            throw new InvalidOperationException("No JSON question files were found in: " + QuestionDirectory);
        }

        foreach (string QuestionFile in QuestionFiles)
        {
            using JsonDocument Document = JsonDocument.Parse(File.ReadAllText(QuestionFile));
            JsonElement Root = Document.RootElement;
            if (!Root.TryGetProperty("Id", out _) || !Root.TryGetProperty("AnswerKey", out _))
            {
                throw new InvalidOperationException("Question file is missing Id or AnswerKey: " + QuestionFile);
            }
        }

        Console.WriteLine($"[ OK ] [GENERATOR] Loaded JSON question files: {QuestionFiles.Length}");
    }

    private static string NormalizeTarget(string Value)
    {
        string Normalized = Value.Trim();
        if (Normalized.Equals("x64-elf", StringComparison.OrdinalIgnoreCase))
        {
            return "x64-elf";
        }

        throw new InvalidOperationException("Unsupported target architecture: " + Value + ". Use x64-elf.");
    }

    private static string NormalizeVmProfile(string Value)
    {
        string Normalized = Value.Trim();
        if (Normalized.Equals("RunQemu", StringComparison.OrdinalIgnoreCase) ||
            Normalized.Equals("Qemu", StringComparison.OrdinalIgnoreCase))
        {
            return "RunQemu";
        }

        throw new InvalidOperationException("Unsupported virtual machine profile: " + Value + ". Use RunQemu.");
    }

    private static string NormalizeBuildMode(string Value)
    {
        string Normalized = Value.Trim();
        if (Normalized.Equals("Debug", StringComparison.OrdinalIgnoreCase))
        {
            return "Debug";
        }

        throw new InvalidOperationException("Unsupported build mode: " + Value + ". Use Debug.");
    }

    private static string NormalizeVmDisplayMode(string Value)
    {
        string Normalized = Value.Trim();
        if (Normalized.Equals("Headless", StringComparison.OrdinalIgnoreCase) ||
            Normalized.Equals("None", StringComparison.OrdinalIgnoreCase) ||
            Normalized.Equals("Off", StringComparison.OrdinalIgnoreCase))
        {
            return "Headless";
        }

        if (Normalized.Equals("Visual", StringComparison.OrdinalIgnoreCase) ||
            Normalized.Equals("Visible", StringComparison.OrdinalIgnoreCase) ||
            Normalized.Equals("Headed", StringComparison.OrdinalIgnoreCase) ||
            Normalized.Equals("Gui", StringComparison.OrdinalIgnoreCase))
        {
            return "Visual";
        }

        throw new InvalidOperationException("Unsupported VM display mode: " + Value + ". Use Headless or Visual.");
    }

    private static string FormatModuleDefault(string[] Modules)
    {
        return Modules.Length == 0 ? "None" : string.Join(',', Modules);
    }

    private static string[] ParseUserSelectedModules(string ModulesText)
    {
        string[] RawModules = ModulesText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (RawModules.Length == 0)
        {
            return Array.Empty<string>();
        }

        bool ContainsNone = RawModules.Any(Module => Module.Equals("None", StringComparison.OrdinalIgnoreCase));
        if (ContainsNone && RawModules.Length > 1)
        {
            throw new InvalidOperationException("None cannot be combined with other user-selected modules.");
        }

        if (ContainsNone)
        {
            return Array.Empty<string>();
        }

        return RawModules;
    }

    private static void ValidateUserSelectedModules(string[] Modules)
    {
        HashSet<string> Available = new(AvailableUserSelectableModules, StringComparer.Ordinal);
        HashSet<string> Mandatory = new(MandatoryKernelModules, StringComparer.Ordinal);
        foreach (string Module in Modules)
        {
            if (Mandatory.Contains(Module))
            {
                throw new InvalidOperationException($"{Module} is a mandatory kernel module and must not be listed as user-selected. Diagnostics and Panic are always enabled.");
            }

            if (!Available.Contains(Module))
            {
                throw new InvalidOperationException($"Unknown or unavailable user-selectable module for 2.0.1: {Module}");
            }
        }
    }

    private static string BuildKernelTemplate() => """
// Generated kernel template for the Oryn freestanding SDK surface.
// The using directives below are .Oryn approved namespaces, not normal host .NET runtime APIs.
__ORYN_GENERATED_USINGS__

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("__ORYN_OS_NAME__ generated kernel entered");
        Diagnostics.WriteOk("Hello from __ORYN_OS_NAME__");
        Diagnostics.WriteOk("__ORYN_OS_NAME__ kernel name __ORYN_KERNEL_NAME__");
        Diagnostics.WriteOk("__ORYN_OS_NAME__ compiler version __ORYN_COMPILER_VERSION__");
__ORYN_KERNEL_BOOT_PROOF_LINES__
__ORYN_MODULE_INITIALIZATION_CALLS__
        Runtime.MarkKernelReady();
        Diagnostics.WriteOk("__ORYN_OS_NAME__ generated kernel is halting forever");
        Cpu.HaltForever();
    }
}
""";

    private static string BuildUncomposedKernelSource(string OsTitle, string OsName, string KernelName)
    {
        string[] Lines =
        {
            $"// Generated by Oryn.Generator {Version}.",
            "// This file is the user-facing generated source stub. Runqemu.sh composes Templates/Kernel.template.cs before compiling.",
            $"// OS title: {OsTitle}",
            $"// OS name: {OsName}",
            $"// Kernel: {KernelName}",
            string.Empty,
            "public static class Kernel",
            "{",
            "    public static void Main()",
            "    {",
            "    }",
            "}"
        };

        return string.Join(Environment.NewLine, Lines) + Environment.NewLine;
    }

    private static string BuildAnswersJson(string OsTitle, string OsName, string KernelName, string Target, string VmProfile, string VmDisplayMode, string BuildMode, string[] UserSelectedModules)
    {
        object Model = new
        {
            OrynVersion = Version,
            QuestionSchemaVersion = Version,
            OsTitle,
            OsName,
            KernelName,
            Target,
            DefaultBootMode = "long-mode",
            SdkVersion = Version,
            HostSdk = "Source/Core/Oryn.Sdk",
            FreestandingSdk = "Source/Sdk/Oryn",
            VmProfile,
            VmDisplayMode,
            BuildMode,
            MandatoryKernelModules,
            DiagnosticsAlwaysEnabled = true,
            PanicAlwaysEnabled = true,
            VisualConfiguratorCompleted = true,
            LastConfiguredWithOrynVersion = Version,
            LastConfiguredWithQuestionSchemaVersion = Version,
            UserSelectedModules
        };
        return JsonSerializer.Serialize(Model, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
    }

    private static string BuildManifestJson(string OsTitle, string OsName, string KernelName, string Target, string VmProfile, string VmDisplayMode, string BuildMode, string[] UserSelectedModules)
    {
        object Model = new
        {
            OrynVersion = Version,
            QuestionSchemaVersion = Version,
            OsTitle,
            OsName,
            KernelName,
            Target,
            DefaultBootMode = "long-mode",
            SdkVersion = Version,
            HostSdk = "Source/Core/Oryn.Sdk",
            FreestandingSdk = "Source/Sdk/Oryn",
            VmProfile,
            VmDisplayMode,
            BuildMode,
            MandatoryKernelModules,
            UserSelectedModules,
            SdkPolicy = "Generated kernels use the .Oryn freestanding SDK namespaces under Source/Sdk/Oryn. Host tools may use the .NET Oryn.Sdk project under Source/Core/Oryn.Sdk.",
            LinkedModulePolicy = "Mandatory kernel modules are linked automatically. User-selected modules exclude modules needed to get the kernel running. Diagnostics and Panic are always enabled."
        };
        return JsonSerializer.Serialize(Model, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
    }

    private static string BuildOsReadme(string OsTitle, string OsName, string KernelName) => $"""
# {OsTitle}

Generated by Oryn {Version}.

Technical OS name: `{OsName}`

Kernel name: `{KernelName}`

## Build and run

From the Oryn repository root:

```bash
./Oryn.sh build {OsName}
./Oryn.sh run {OsName}
```

The generated manifest records mandatory kernel modules separately from user-selected modules. Runtime, Diagnostics, Panic, Cpu, and ManifestLoader are linked automatically. Diagnostics and Panic are always enabled. The generated kernel prints `Hello from {OsName}` during boot. The VM display mode is chosen during generation and can be Headless or Visual. Headless mode closes automatically after the proof timeout. Visual mode stays open until you close the VM window.

## SDKs used

- Host-side .NET SDK: `Source/Core/Oryn.Sdk`
- Freestanding `.Oryn` SDK declarations: `Source/Sdk/Oryn`

The generated kernel uses approved `.Oryn` namespaces such as `Oryn.Diagnostics`, `Oryn.Cpu`, and `Oryn.Runtime`. These are compile-time declarations that map to native freestanding module symbols.
""";

    private static string LocateProjectRoot()
    {
        DirectoryInfo? CurrentDirectory = new(System.IO.Directory.GetCurrentDirectory());
        while (CurrentDirectory is not null)
        {
            if (File.Exists(Path.Combine(CurrentDirectory.FullName, "VERSION")) && System.IO.Directory.Exists(Path.Combine(CurrentDirectory.FullName, "Source")))
            {
                return CurrentDirectory.FullName;
            }

            CurrentDirectory = CurrentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not locate Oryn project root. Run the generator from inside the Oryn source tree.");
    }

    private static string? ReadOption(string[] Args, string Name)
    {
        for (int Index = 0; Index < Args.Length - 1; Index++)
        {
            if (Args[Index].Equals(Name, StringComparison.OrdinalIgnoreCase))
            {
                return Args[Index + 1];
            }
        }

        return null;
    }

    private static bool HasFlag(string[] Args, string Name)
    {
        return Args.Any(Arg => Arg.Equals(Name, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeStrictName(string Name)
    {
        string Sanitized = Regex.Replace(Name.Trim(), "[^A-Za-z0-9]", string.Empty);
        if (string.IsNullOrWhiteSpace(Sanitized))
        {
            return "MyOrynOS";
        }

        if (!char.IsLetter(Sanitized[0]))
        {
            return "Oryn" + Sanitized;
        }

        return Sanitized;
    }

    private static string NormalizeStrictName(string Name, string Label)
    {
        string Trimmed = Name.Trim();
        if (!Regex.IsMatch(Trimmed, "^[A-Za-z][A-Za-z0-9]*$"))
        {
            throw new InvalidOperationException(Label + " must start with a letter and contain only letters and numbers. Spaces are not allowed.");
        }

        return Trimmed;
    }

    private static string SanitizeFileName(string Name)
    {
        string WithoutSpaces = Regex.Replace(Name.Trim(), "\\s+", string.Empty);
        return Regex.Replace(WithoutSpaces, "[^A-Za-z0-9._-]", string.Empty);
    }

    private static string SanitizeIdentifier(string Name)
    {
        string Sanitized = Regex.Replace(Name.Trim(), "[^A-Za-z0-9_]", string.Empty);
        if (string.IsNullOrWhiteSpace(Sanitized))
        {
            return "OrynKernel";
        }

        if (char.IsDigit(Sanitized[0]))
        {
            return "Oryn" + Sanitized;
        }

        return Sanitized;
    }
}
