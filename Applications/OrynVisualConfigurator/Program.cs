using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OrynVisualConfigurator;

internal sealed record Question(string Id, int Order, string Prompt, string AnswerKey, string Type, bool Required, string Default, string ExpectedAnswer, string[] Choices);

internal static class Program
{
    private const string Version = "2.0.0";
    private const string Cyan = "\u001b[96m";
    private const string Yellow = "\u001b[93m";
    private const string Green = "\u001b[92m";
    private const string Reset = "\u001b[0m";

    private static int Main(string[] Args)
    {
        try
        {
            string ProjectRoot = LocateProjectRoot();
            List<Question> Questions = LoadQuestions(ProjectRoot);
            Dictionary<string, string> Arguments = ParseArgs(Args);

            PrintHeader();

            if (Arguments.TryGetValue("mode", out string Mode))
            {
                if (Mode.Equals("create", StringComparison.OrdinalIgnoreCase) || Mode.Equals("new", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateNew(ProjectRoot, Questions);
                }

                if (Mode.Equals("configure", StringComparison.OrdinalIgnoreCase))
                {
                    string? ProjectPath = Arguments.GetValueOrDefault("path");
                    return ConfigureExisting(ProjectRoot, Questions, ProjectPath);
                }

                if (Mode.Equals("search", StringComparison.OrdinalIgnoreCase))
                {
                    return SearchAndConfigure(ProjectRoot, Questions);
                }

                if (Mode.Equals("load", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadAndConfigure(ProjectRoot, Questions);
                }
            }

            return StartMenu(ProjectRoot, Questions);
        }
        catch (Exception Exception)
        {
            Console.WriteLine("[FAIL] [VISUALCFG] " + Exception.Message);
            return 1;
        }
    }

    private static void PrintHeader()
    {
        Console.WriteLine("[ OK ] [VISUALCFG] OrynVisualConfigurator " + Version);
        Console.WriteLine("[ OK ] [VISUALCFG] Questions are loaded from the current Oryn Questions/*.question.json files.");
    }

    private static int StartMenu(string ProjectRoot, List<Question> Questions)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(Colour("Oryn Visual Configurator", Green));
            Console.WriteLine("  1. Create New OS");
            Console.WriteLine("  2. Open OS From Current Directory");
            Console.WriteLine("  3. Search Existing OS Projects");
            Console.WriteLine("  4. Load OS Directory...");
            Console.WriteLine("  5. Exit");
            Console.Write("Choose: ");
            string Choice = (Console.ReadLine() ?? string.Empty).Trim();
            if (Choice == "1") return CreateNew(ProjectRoot, Questions);
            if (Choice == "2") return ConfigureExisting(ProjectRoot, Questions, null);
            if (Choice == "3") return SearchAndConfigure(ProjectRoot, Questions);
            if (Choice == "4") return LoadAndConfigure(ProjectRoot, Questions);
            if (Choice == "5" || Choice.Equals("exit", StringComparison.OrdinalIgnoreCase)) return 0;
            Console.WriteLine("[WARN] [VISUALCFG] Unknown menu choice.");
        }
    }

    private static int CreateNew(string ProjectRoot, List<Question> Questions)
    {
        Dictionary<string, object> Answers = AskQuestions(Questions, null);
        return RunGenerator(ProjectRoot, Answers);
    }

    private static int ConfigureExisting(string ProjectRoot, List<Question> Questions, string? PathOrName)
    {
        string ProjectPath = ResolveProjectPath(ProjectRoot, PathOrName);
        Dictionary<string, object> ExistingAnswers = LoadExistingAnswers(ProjectPath);
        Console.WriteLine("[ OK ] [VISUALCFG] Opened OS project: " + ProjectPath);
        Dictionary<string, object> Answers = AskQuestions(Questions, ExistingAnswers);
        return RunGenerator(ProjectRoot, Answers);
    }

    private static int SearchAndConfigure(string ProjectRoot, List<Question> Questions)
    {
        List<string> Projects = FindProjects(ProjectRoot).ToList();
        if (Projects.Count == 0)
        {
            Console.WriteLine("[WARN] [VISUALCFG] No OS projects found under: " + Path.Combine(ProjectRoot, "OSes"));
            return StartMenu(ProjectRoot, Questions);
        }

        Console.WriteLine(Colour("Existing Oryn OS projects", Green));
        for (int Index = 0; Index < Projects.Count; Index++)
        {
            string Manifest = Path.Combine(Projects[Index], "manifest.json");
            string VersionText = ReadJsonValue(Manifest, "OrynVersion") ?? "unknown";
            string Title = ReadJsonValue(Manifest, "OsTitle") ?? Path.GetFileName(Projects[Index]);
            Console.WriteLine($"  {Index + 1}. {Title} - {Projects[Index]} - Oryn {VersionText}");
        }

        Console.Write("Choose project: ");
        if (!int.TryParse(Console.ReadLine(), out int Choice) || Choice < 1 || Choice > Projects.Count)
        {
            Console.WriteLine("[WARN] [VISUALCFG] No project selected.");
            return 0;
        }

        return ConfigureExisting(ProjectRoot, Questions, Projects[Choice - 1]);
    }

    private static int LoadAndConfigure(string ProjectRoot, List<Question> Questions)
    {
        Console.Write("Directory path to load: ");
        string? Input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(Input))
        {
            Console.WriteLine("[WARN] [VISUALCFG] No directory entered.");
            return 0;
        }

        string Candidate = Path.GetFullPath(Input.Trim());
        if (!Directory.Exists(Candidate))
        {
            Console.WriteLine("[FAIL] [VISUALCFG] Directory not found: " + Candidate);
            return 1;
        }

        if (IsOrynProject(Candidate))
        {
            return ConfigureExisting(ProjectRoot, Questions, Candidate);
        }

        List<string> ChildProjects = Directory.GetDirectories(Candidate).Where(IsOrynProject).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();
        if (ChildProjects.Count == 0)
        {
            Console.WriteLine("[FAIL] [VISUALCFG] No Oryn OS projects found in: " + Candidate);
            return 1;
        }

        Console.WriteLine("[ OK ] [VISUALCFG] The selected directory contains multiple OS projects.");
        for (int Index = 0; Index < ChildProjects.Count; Index++)
        {
            Console.WriteLine($"  {Index + 1}. {Path.GetFileName(ChildProjects[Index])} - {ChildProjects[Index]}");
        }

        Console.Write("Choose project: ");
        if (!int.TryParse(Console.ReadLine(), out int Choice) || Choice < 1 || Choice > ChildProjects.Count)
        {
            Console.WriteLine("[WARN] [VISUALCFG] No project selected.");
            return 0;
        }

        return ConfigureExisting(ProjectRoot, Questions, ChildProjects[Choice - 1]);
    }

    private static Dictionary<string, object> AskQuestions(List<Question> Questions, Dictionary<string, object>? ExistingAnswers)
    {
        Dictionary<string, object> Answers = new(StringComparer.OrdinalIgnoreCase);
        foreach (Question Question in Questions.OrderBy(Question => Question.Order))
        {
            object? ExistingValue = GetExistingValue(ExistingAnswers, Question.AnswerKey);
            string Default = ResolveDefault(Question.Default, Answers, ExistingValue);
            Console.WriteLine();
            Console.WriteLine(Colour(Question.Prompt, Green));
            Console.WriteLine("Answer key: " + Question.AnswerKey);
            Console.WriteLine("Expected: " + Colour(Question.ExpectedAnswer, Cyan));
            if (ExistingValue is not null)
            {
                Console.WriteLine("Current: " + Colour(FormatExistingValue(ExistingValue), Yellow));
            }

            if (Question.Type.Equals("choice", StringComparison.OrdinalIgnoreCase))
            {
                Answers[Question.AnswerKey] = AskChoice(Question, Default);
            }
            else if (Question.Type.Equals("multi-choice", StringComparison.OrdinalIgnoreCase))
            {
                Answers[Question.AnswerKey] = AskMultiChoice(Question, Default);
            }
            else
            {
                Answers[Question.AnswerKey] = AskString(Question, Default);
            }
        }

        ValidateStrictNames(Answers);
        return Answers;
    }

    private static string AskChoice(Question Question, string Default)
    {
        for (int Index = 0; Index < Question.Choices.Length; Index++)
        {
            Console.WriteLine($"  {Index + 1}. {Colour(Question.Choices[Index], Cyan)}");
        }
        Console.Write("Choose [" + Default + "]: ");
        string Input = (Console.ReadLine() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(Input)) return Default;
        if (int.TryParse(Input, out int Number) && Number >= 1 && Number <= Question.Choices.Length) return Question.Choices[Number - 1];
        string? Match = Question.Choices.FirstOrDefault(Choice => Choice.Equals(Input, StringComparison.OrdinalIgnoreCase));
        if (Match is not null) return Match;
        throw new InvalidOperationException("Invalid choice for " + Question.AnswerKey + ": " + Input);
    }

    private static string AskMultiChoice(Question Question, string Default)
    {
        for (int Index = 0; Index < Question.Choices.Length; Index++)
        {
            Console.WriteLine($"  {Index + 1}. {Colour(Question.Choices[Index], Cyan)}");
        }
        Console.Write("Choose comma-separated values [" + Default + "]: ");
        string Input = (Console.ReadLine() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(Input)) return Default;
        string[] Parts = Input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string[] Values = Parts.Select(Part => int.TryParse(Part, out int Number) && Number >= 1 && Number <= Question.Choices.Length ? Question.Choices[Number - 1] : Part).ToArray();
        foreach (string Value in Values)
        {
            if (!Question.Choices.Any(Choice => Choice.Equals(Value, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Invalid choice for " + Question.AnswerKey + ": " + Value);
            }
        }
        if (Values.Any(Value => Value.Equals("None", StringComparison.OrdinalIgnoreCase)) && Values.Length > 1)
        {
            throw new InvalidOperationException("None cannot be combined with other module choices.");
        }
        return string.Join(',', Values);
    }

    private static string AskString(Question Question, string Default)
    {
        Console.Write("Value [" + Default + "]: ");
        string Input = (Console.ReadLine() ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(Input) ? Default : Input;
    }

    private static void ValidateStrictNames(Dictionary<string, object> Answers)
    {
        string OsName = Convert.ToString(Answers.GetValueOrDefault("OsName")) ?? string.Empty;
        string KernelName = Convert.ToString(Answers.GetValueOrDefault("KernelName")) ?? string.Empty;
        if (!Regex.IsMatch(OsName, "^[A-Za-z][A-Za-z0-9]*$"))
        {
            throw new InvalidOperationException("OS Name must start with a letter and contain only letters and numbers. Spaces are not allowed.");
        }

        if (!Regex.IsMatch(KernelName, "^[A-Za-z][A-Za-z0-9]*$"))
        {
            throw new InvalidOperationException("Kernel Name must start with a letter and contain only letters and numbers. Spaces are not allowed.");
        }
    }

    private static int RunGenerator(string ProjectRoot, Dictionary<string, object> Answers)
    {
        string OsTitle = Convert.ToString(Answers["OsTitle"]) ?? "My Oryn OS";
        string OsName = Convert.ToString(Answers["OsName"]) ?? "MyOrynOS";
        string KernelName = Convert.ToString(Answers["KernelName"]) ?? OsName + "Kernel";
        string Target = Convert.ToString(Answers["Target"]) ?? "x64-elf";
        string VmProfile = Convert.ToString(Answers["VmProfile"]) ?? "RunQemu";
        string VmDisplayMode = Convert.ToString(Answers["VmDisplayMode"]) ?? "Headless";
        string BuildMode = Convert.ToString(Answers["BuildMode"]) ?? "Debug";
        string Modules = Convert.ToString(Answers["UserSelectedModules"]) ?? "None";

        string GeneratorProject = Path.Combine(ProjectRoot, "Source", "Core", "Oryn.Generator", "Oryn.Generator.csproj");
        string[] Args =
        {
            "run", "--project", GeneratorProject, "--", "generate", "--non-interactive",
            "--os-title", OsTitle,
            "--os-name", OsName,
            "--kernel-name", KernelName,
            "--target", Target,
            "--vm-profile", VmProfile,
            "--vm-display-mode", VmDisplayMode,
            "--build-mode", BuildMode,
            "--modules", Modules
        };

        Console.WriteLine("[ OK ] [VISUALCFG] Saving configuration through Oryn.Generator.");
        ProcessStartInfo StartInfo = new("dotnet")
        {
            WorkingDirectory = ProjectRoot,
            UseShellExecute = false
        };
        foreach (string Arg in Args) StartInfo.ArgumentList.Add(Arg);
        using Process GeneratorProcess = Process.Start(StartInfo) ?? throw new InvalidOperationException("Could not start dotnet generator process.");
        GeneratorProcess.WaitForExit();
        return GeneratorProcess.ExitCode;
    }

    private static string ResolveDefault(string Default, Dictionary<string, object> CurrentAnswers, object? ExistingValue)
    {
        if (ExistingValue is not null) return FormatExistingValue(ExistingValue);
        if (Default == "<OsName>Kernel" && CurrentAnswers.TryGetValue("OsName", out object? OsName)) return Convert.ToString(OsName) + "Kernel";
        if (Default == "MyOrynOS" && CurrentAnswers.TryGetValue("OsTitle", out object? OsTitle)) return SanitizeStrictName(Convert.ToString(OsTitle) ?? "My Oryn OS");
        return Default;
    }

    private static string SanitizeStrictName(string Name)
    {
        string Sanitized = Regex.Replace(Name.Trim(), "[^A-Za-z0-9]", string.Empty);
        if (string.IsNullOrWhiteSpace(Sanitized)) return "MyOrynOS";
        if (!char.IsLetter(Sanitized[0])) return "Oryn" + Sanitized;
        return Sanitized;
    }

    private static object? GetExistingValue(Dictionary<string, object>? ExistingAnswers, string Key)
    {
        if (ExistingAnswers is null) return null;
        return ExistingAnswers.TryGetValue(Key, out object? Value) ? Value : null;
    }

    private static string FormatExistingValue(object Value)
    {
        if (Value is JsonElement Element)
        {
            if (Element.ValueKind == JsonValueKind.Array)
            {
                return string.Join(',', Element.EnumerateArray().Select(Item => Item.GetString()).Where(Item => !string.IsNullOrWhiteSpace(Item)));
            }
            return Element.ToString();
        }
        return Convert.ToString(Value) ?? string.Empty;
    }

    private static Dictionary<string, object> LoadExistingAnswers(string ProjectPath)
    {
        string AnswersDirectory = Path.Combine(ProjectPath, "Answers");
        string? AnswersFile = Directory.Exists(AnswersDirectory)
            ? Directory.GetFiles(AnswersDirectory, "*.answers.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
        string JsonPath = AnswersFile ?? Path.Combine(ProjectPath, "manifest.json");
        if (!File.Exists(JsonPath)) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        using JsonDocument Document = JsonDocument.Parse(File.ReadAllText(JsonPath));
        Dictionary<string, object> Values = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty Property in Document.RootElement.EnumerateObject())
        {
            Values[Property.Name] = Property.Value.Clone();
        }
        return Values;
    }

    private static string ResolveProjectPath(string ProjectRoot, string? PathOrName)
    {
        if (string.IsNullOrWhiteSpace(PathOrName))
        {
            string? CurrentProject = FindProjectAbove(Directory.GetCurrentDirectory());
            if (CurrentProject is not null) return CurrentProject;
            throw new InvalidOperationException("No Oryn OS project found from the current directory. Use search or load.");
        }

        string Candidate = PathOrName;
        if (!Path.IsPathRooted(Candidate))
        {
            string ByName = Path.Combine(ProjectRoot, "OSes", Candidate);
            string ByRelativePath = Path.Combine(ProjectRoot, Candidate);
            Candidate = Directory.Exists(ByName) ? ByName : ByRelativePath;
        }
        Candidate = Path.GetFullPath(Candidate);
        if (!IsOrynProject(Candidate)) throw new InvalidOperationException("Not an Oryn OS project: " + Candidate);
        return Candidate;
    }

    private static string? FindProjectAbove(string StartPath)
    {
        DirectoryInfo? CurrentDirectory = new(StartPath);
        while (CurrentDirectory is not null)
        {
            if (IsOrynProject(CurrentDirectory.FullName)) return CurrentDirectory.FullName;
            CurrentDirectory = CurrentDirectory.Parent;
        }
        return null;
    }

    private static IEnumerable<string> FindProjects(string ProjectRoot)
    {
        string OsDirectory = Path.Combine(ProjectRoot, "OSes");
        if (!Directory.Exists(OsDirectory)) yield break;
        foreach (string DirectoryPath in Directory.GetDirectories(OsDirectory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (IsOrynProject(DirectoryPath)) yield return DirectoryPath;
        }
    }

    private static bool IsOrynProject(string DirectoryPath)
    {
        return Directory.Exists(DirectoryPath) && File.Exists(Path.Combine(DirectoryPath, "manifest.json")) && Directory.Exists(Path.Combine(DirectoryPath, "Answers"));
    }

    private static string? ReadJsonValue(string FilePath, string Key)
    {
        if (!File.Exists(FilePath)) return null;
        using JsonDocument Document = JsonDocument.Parse(File.ReadAllText(FilePath));
        return Document.RootElement.TryGetProperty(Key, out JsonElement Element) ? Element.ToString() : null;
    }

    private static List<Question> LoadQuestions(string ProjectRoot)
    {
        string QuestionDirectory = Path.Combine(ProjectRoot, "Questions");
        if (!Directory.Exists(QuestionDirectory)) throw new InvalidOperationException("Questions directory not found: " + QuestionDirectory);
        List<Question> Questions = new();
        foreach (string FilePath in Directory.GetFiles(QuestionDirectory, "*.question.json"))
        {
            using JsonDocument Document = JsonDocument.Parse(File.ReadAllText(FilePath));
            JsonElement Root = Document.RootElement;
            string[] Choices = Root.TryGetProperty("Choices", out JsonElement ChoicesElement) && ChoicesElement.ValueKind == JsonValueKind.Array
                ? ChoicesElement.EnumerateArray().Select(Choice => Choice.GetString() ?? string.Empty).Where(Choice => !string.IsNullOrWhiteSpace(Choice)).ToArray()
                : Array.Empty<string>();
            Questions.Add(new Question(
                Root.GetProperty("Id").GetString() ?? Path.GetFileNameWithoutExtension(FilePath),
                Root.TryGetProperty("Order", out JsonElement Order) ? Order.GetInt32() : 0,
                Root.GetProperty("Prompt").GetString() ?? string.Empty,
                Root.GetProperty("AnswerKey").GetString() ?? string.Empty,
                Root.GetProperty("Type").GetString() ?? "string",
                Root.TryGetProperty("Required", out JsonElement Required) && Required.GetBoolean(),
                Root.TryGetProperty("Default", out JsonElement Default) ? Default.GetString() ?? string.Empty : string.Empty,
                Root.TryGetProperty("ExpectedAnswer", out JsonElement ExpectedAnswer) ? ExpectedAnswer.GetString() ?? string.Empty : string.Empty,
                Choices));
        }
        return Questions.OrderBy(Question => Question.Order).ToList();
    }

    private static string LocateProjectRoot()
    {
        DirectoryInfo? CurrentDirectory = new(Directory.GetCurrentDirectory());
        while (CurrentDirectory is not null)
        {
            if (File.Exists(Path.Combine(CurrentDirectory.FullName, "VERSION")) && Directory.Exists(Path.Combine(CurrentDirectory.FullName, "Source"))) return CurrentDirectory.FullName;
            CurrentDirectory = CurrentDirectory.Parent;
        }
        throw new InvalidOperationException("Could not locate Oryn project root.");
    }

    private static Dictionary<string, string> ParseArgs(string[] Args)
    {
        Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase);
        if (Args.Length == 0) return Values;
        string First = Args[0];
        if (First.Equals("new", StringComparison.OrdinalIgnoreCase) || First.Equals("create", StringComparison.OrdinalIgnoreCase)) Values["mode"] = "create";
        else if (First.Equals("configure", StringComparison.OrdinalIgnoreCase)) Values["mode"] = "configure";
        else if (First.Equals("--search", StringComparison.OrdinalIgnoreCase) || First.Equals("search", StringComparison.OrdinalIgnoreCase)) Values["mode"] = "search";
        else if (First.Equals("--load", StringComparison.OrdinalIgnoreCase) || First.Equals("load", StringComparison.OrdinalIgnoreCase)) Values["mode"] = "load";
        if (Args.Length > 1) Values["path"] = Args[1];
        return Values;
    }

    private static string Colour(string Text, string Colour)
    {
        if (Console.IsOutputRedirected || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NO_COLOR")) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ORYN_NO_COLOR"))) return Text;
        return Colour + Text + Reset;
    }
}
