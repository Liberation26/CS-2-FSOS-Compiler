using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OrynVisualConfigurator;

internal sealed record Question(string Id, int Order, string Prompt, string AnswerKey, string Type, bool Required, string Default, string ExpectedAnswer, string[] Choices);

internal static class Program
{
    private const string Version = "2.0.3";
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

            if (Arguments.ContainsKey("terminal") || Environment.GetEnvironmentVariable("ORYN_VISUALCFG_TERMINAL") == "1")
            {
                return RunTerminalMode(ProjectRoot, Questions, Arguments);
            }

            return RunBrowserMode(ProjectRoot, Questions, Arguments);
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
        Console.WriteLine("[ OK ] [VISUALCFG] Visual mode uses a local browser window. Set ORYN_VISUALCFG_TERMINAL=1 for terminal fallback.");
    }

    private static int RunBrowserMode(string ProjectRoot, List<Question> Questions, Dictionary<string, string> Arguments)
    {
        int Port = FindFreePort();
        string Prefix = $"http://127.0.0.1:{Port}/";
        using HttpListener Listener = new();
        Listener.Prefixes.Add(Prefix);
        Listener.Start();

        BrowserSession Session = new(ProjectRoot, Questions, Arguments);
        string StartUrl = Prefix + InitialRoute(Arguments);
        Console.WriteLine("[ OK ] [VISUALCFG] Opening visual configurator: " + StartUrl);
        OpenBrowser(StartUrl);
        Console.WriteLine("[ OK ] [VISUALCFG] If the browser did not open, copy the URL above into your browser.");

        while (!Session.Done)
        {
            HttpListenerContext Context = Listener.GetContext();
            try
            {
                HandleRequest(Context, Session);
            }
            catch (Exception Exception)
            {
                SendHtml(Context, RenderPage("Oryn Visual Configurator - Error", HtmlEscape(Exception.Message), Navigation()), 500);
            }
        }

        return Session.ExitCode;
    }

    private static string InitialRoute(Dictionary<string, string> Arguments)
    {
        string Mode = Arguments.TryGetValue("mode", out string? ModeValue) ? ModeValue : string.Empty;
        if (Mode.Equals("create", StringComparison.OrdinalIgnoreCase)) return "new";
        if (Mode.Equals("search", StringComparison.OrdinalIgnoreCase)) return "search";
        if (Mode.Equals("load", StringComparison.OrdinalIgnoreCase)) return "load";
        if (Mode.Equals("configure", StringComparison.OrdinalIgnoreCase))
        {
            if (Arguments.TryGetValue("path", out string? PathValue) && !string.IsNullOrWhiteSpace(PathValue))
            {
                return "configure?path=" + Uri.EscapeDataString(PathValue);
            }
            return "configure";
        }
        return string.Empty;
    }

    private static void HandleRequest(HttpListenerContext Context, BrowserSession Session)
    {
        string Path = Context.Request.Url?.AbsolutePath.Trim('/') ?? string.Empty;
        if (Context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            if (Path.Equals("save", StringComparison.OrdinalIgnoreCase))
            {
                HandleSave(Context, Session);
                return;
            }
            if (Path.Equals("load", StringComparison.OrdinalIgnoreCase))
            {
                HandleLoadPost(Context, Session);
                return;
            }
        }

        if (Path.Length == 0)
        {
            SendHtml(Context, RenderHome(Session));
            return;
        }
        if (Path.Equals("new", StringComparison.OrdinalIgnoreCase))
        {
            SendHtml(Context, RenderQuestionForm(Session, null));
            return;
        }
        if (Path.Equals("configure", StringComparison.OrdinalIgnoreCase))
        {
            string? PathOrName = Context.Request.QueryString["path"];
            string ProjectPath = ResolveProjectPath(Session.ProjectRoot, PathOrName);
            Dictionary<string, object> ExistingAnswers = LoadExistingAnswers(ProjectPath);
            SendHtml(Context, RenderQuestionForm(Session, ExistingAnswers, ProjectPath));
            return;
        }
        if (Path.Equals("search", StringComparison.OrdinalIgnoreCase))
        {
            SendHtml(Context, RenderSearch(Session));
            return;
        }
        if (Path.Equals("load", StringComparison.OrdinalIgnoreCase))
        {
            SendHtml(Context, RenderLoad(Session));
            return;
        }
        if (Path.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            Session.Done = true;
            Session.ExitCode = 0;
            SendHtml(Context, RenderPage("Oryn Visual Configurator", "<p>Configurator closed. You can close this browser tab.</p>", string.Empty));
            return;
        }

        SendHtml(Context, RenderPage("Oryn Visual Configurator", "<p>Unknown route.</p>", Navigation()), 404);
    }

    private static void HandleSave(HttpListenerContext Context, BrowserSession Session)
    {
        Dictionary<string, string> Form = ReadForm(Context.Request);
        Dictionary<string, object> Answers = BuildAnswersFromForm(Session.Questions, Form);
        ValidateStrictNames(Answers);
        int ExitCode = RunGenerator(Session.ProjectRoot, Answers);
        Session.Done = true;
        Session.ExitCode = ExitCode;
        string OsName = Convert.ToString(Answers["OsName"]) ?? "generated OS";
        string Body = ExitCode == 0
            ? $"<p class='ok'>Saved and generated <strong>{HtmlEscape(OsName)}</strong>.</p><p>You can close this tab. The command will continue from the terminal.</p>"
            : $"<p class='fail'>Oryn.Generator exited with status {ExitCode}.</p><p>Check the terminal output for details.</p>";
        SendHtml(Context, RenderPage("Oryn Visual Configurator - Saved", Body, string.Empty));
    }

    private static void HandleLoadPost(HttpListenerContext Context, BrowserSession Session)
    {
        Dictionary<string, string> Form = ReadForm(Context.Request);
        string DirectoryPath = Form.TryGetValue("directory", out string? Value) ? Value.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(DirectoryPath))
        {
            SendHtml(Context, RenderPage("Load OS Directory", "<p class='warn'>No directory entered.</p>" + RenderLoadForm(), Navigation()));
            return;
        }
        string Candidate = Path.GetFullPath(DirectoryPath);
        if (IsOrynProject(Candidate))
        {
            Redirect(Context, "/configure?path=" + Uri.EscapeDataString(Candidate));
            return;
        }
        if (Directory.Exists(Candidate))
        {
            List<string> ChildProjects = Directory.GetDirectories(Candidate).Where(IsOrynProject).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();
            if (ChildProjects.Count > 0)
            {
                string Items = string.Join("", ChildProjects.Select(Project => $"<li><a href='/configure?path={Uri.EscapeDataString(Project)}'>{HtmlEscape(Path.GetFileName(Project))}</a><br><code>{HtmlEscape(Project)}</code></li>"));
                SendHtml(Context, RenderPage("Choose OS Project", $"<p>The selected directory contains multiple Oryn OS projects.</p><ul>{Items}</ul>", Navigation()));
                return;
            }
        }
        SendHtml(Context, RenderPage("Load OS Directory", $"<p class='fail'>No Oryn OS project found at: <code>{HtmlEscape(Candidate)}</code></p>" + RenderLoadForm(), Navigation()));
    }

    private static string RenderHome(BrowserSession Session)
    {
        string Detected = string.Empty;
        string? CurrentProject = FindProjectAbove(Directory.GetCurrentDirectory());
        if (CurrentProject is not null)
        {
            Detected = $"<section><h2>Detected current project</h2><p><strong>{HtmlEscape(Path.GetFileName(CurrentProject))}</strong><br><code>{HtmlEscape(CurrentProject)}</code></p><p><a class='button' href='/configure?path={Uri.EscapeDataString(CurrentProject)}'>Open detected project</a></p></section>";
        }
        string Body = $@"
<section class='hero'>
  <h1>Oryn Visual Configurator</h1>
  <p>Configure an Oryn OS using the current version's question files. Known-choice questions are shown as drop-downs or check boxes.</p>
</section>
<section class='grid'>
  <a class='card' href='/new'><strong>Create New OS</strong><span>Answer the versioned questions and generate a new OS project.</span></a>
  <a class='card' href='/search'><strong>Search Existing OS Projects</strong><span>Search under this Oryn tree's OSes directory.</span></a>
  <a class='card' href='/load'><strong>Load OS Directory</strong><span>Open a specific OS project path or a directory containing projects.</span></a>
  <a class='card' href='/exit'><strong>Exit</strong><span>Close the configurator server.</span></a>
</section>
{Detected}";
        return RenderPage("Oryn Visual Configurator", Body, string.Empty);
    }

    private static string RenderQuestionForm(BrowserSession Session, Dictionary<string, object>? ExistingAnswers, string? ProjectPath = null)
    {
        Dictionary<string, object> CurrentAnswers = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object>? SeedAnswers = ExistingAnswers ?? GuessInitialAnswers(Session.ProjectRoot);
        StringBuilder Fields = new();
        foreach (Question Question in Session.Questions.OrderBy(Question => Question.Order))
        {
            object? ExistingValue = GetExistingValue(SeedAnswers, Question.AnswerKey);
            string Default = ResolveDefault(Question.Default, CurrentAnswers, ExistingValue);
            CurrentAnswers[Question.AnswerKey] = Default;
            Fields.Append(RenderQuestionField(Question, Default, ExistingValue));
        }

        string Heading = ProjectPath is null ? "Create New OS" : "Configure OS Project";
        string ProjectInfo = ProjectPath is null ? string.Empty : $"<p>Project path: <code>{HtmlEscape(ProjectPath)}</code></p>";
        string Lead = ProjectPath is null
            ? "<p>This is a form. Oryn has guessed safe defaults from the current folder and the current question files. Change only what you want, then save.</p>"
            : "<p>Saved answers are loaded into the form. Change only what you want; unchanged answers are kept.</p>";
        string Body = $@"
<h1>{Heading}</h1>
{ProjectInfo}
{Lead}
<p>Only OS Title is free display text. OS Name and Kernel Name are strict identifiers and cannot contain spaces.</p>
<form method='post' action='/save' id='oryn-config-form'>
{Fields}
<div class='actions'>
  <button type='submit'>Save and Generate</button>
  <a class='button secondary' href='/'>Cancel</a>
</div>
</form>";
        return RenderPage("Oryn Visual Configurator - " + Heading, Body, Navigation());
    }

    private static string RenderQuestionField(Question Question, string Default, object? ExistingValue)
    {
        string Current = ExistingValue is not null ? FormatExistingValue(ExistingValue) : Default;
        string Help = string.IsNullOrWhiteSpace(Question.ExpectedAnswer) ? string.Empty : $"<p class='help'>{HtmlEscape(Question.ExpectedAnswer)}</p>";
        string Required = Question.Required ? " required" : string.Empty;
        string FieldId = "field-" + HtmlAttribute(Question.AnswerKey);
        string Input;
        if (Question.Type.Equals("choice", StringComparison.OrdinalIgnoreCase))
        {
            string Options = string.Join("", Question.Choices.Select(Choice => $"<option value='{HtmlAttribute(Choice)}'{(Choice.Equals(Current, StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty)}>{HtmlEscape(Choice)}</option>"));
            Input = $"<select id='{FieldId}' name='{HtmlAttribute(Question.AnswerKey)}'{Required}>{Options}</select>";
        }
        else if (Question.Type.Equals("multi-choice", StringComparison.OrdinalIgnoreCase))
        {
            string[] CurrentValues = Current.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string Boxes = string.Join("", Question.Choices.Select(Choice => $"<label class='choice'><input type='checkbox' name='{HtmlAttribute(Question.AnswerKey)}' value='{HtmlAttribute(Choice)}'{(CurrentValues.Any(Value => Value.Equals(Choice, StringComparison.OrdinalIgnoreCase)) ? " checked" : string.Empty)}> {HtmlEscape(Choice)}</label>"));
            Input = $"<div class='choices' id='{FieldId}'>{Boxes}</div>";
        }
        else
        {
            string Pattern = Question.AnswerKey.Equals("OsName", StringComparison.OrdinalIgnoreCase) || Question.AnswerKey.Equals("KernelName", StringComparison.OrdinalIgnoreCase)
                ? " pattern='[A-Za-z][A-Za-z0-9]*'"
                : string.Empty;
            string ReadOnlyHint = Question.AnswerKey.Equals("OsTitle", StringComparison.OrdinalIgnoreCase) || Question.AnswerKey.Equals("OsName", StringComparison.OrdinalIgnoreCase) || Question.AnswerKey.Equals("KernelName", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : " readonly";
            Input = $"<input type='text' id='{FieldId}' name='{HtmlAttribute(Question.AnswerKey)}' value='{HtmlAttribute(Current)}'{Required}{Pattern}{ReadOnlyHint}>";
        }

        return $@"
<section class='question' data-question-key='{HtmlAttribute(Question.AnswerKey)}'>
  <label for='{FieldId}'><span>{HtmlEscape(Question.Prompt)}</span>{Input}</label>
  <p class='key'>Answer key: <code>{HtmlEscape(Question.AnswerKey)}</code></p>
  {Help}
</section>";
    }

    private static string RenderSearch(BrowserSession Session)
    {
        List<string> Projects = FindProjects(Session.ProjectRoot).ToList();
        string Body;
        if (Projects.Count == 0)
        {
            Body = "<p class='warn'>No OS projects found under <code>OSes/</code>.</p>";
        }
        else
        {
            string Items = string.Join("", Projects.Select(Project =>
            {
                string Manifest = Path.Combine(Project, "manifest.json");
                string VersionText = ReadJsonValue(Manifest, "OrynVersion") ?? "unknown";
                string Title = ReadJsonValue(Manifest, "OsTitle") ?? Path.GetFileName(Project);
                return $"<li><a href='/configure?path={Uri.EscapeDataString(Project)}'>{HtmlEscape(Title)}</a><br><code>{HtmlEscape(Project)}</code><br><span>Oryn {HtmlEscape(VersionText)}</span></li>";
            }));
            Body = $"<h1>Existing Oryn OS Projects</h1><ul class='project-list'>{Items}</ul>";
        }
        return RenderPage("Search OS Projects", Body, Navigation());
    }

    private static string RenderLoad(BrowserSession Session)
    {
        return RenderPage("Load OS Directory", "<h1>Load OS Directory</h1>" + RenderLoadForm(), Navigation());
    }

    private static string RenderLoadForm()
    {
        return @"
<form method='post' action='/load'>
  <section class='question'>
    <label><span>Directory path</span><input type='text' name='directory' placeholder='/home/dave/Dev/OrynFoundry/OSes/DES' required></label>
    <p class='help'>Choose an OS project directory, or a parent directory containing generated OS projects.</p>
  </section>
  <button type='submit'>Load Directory</button>
</form>";
    }

    private static string Navigation()
    {
        return "<nav><a href='/'>Home</a><a href='/new'>Create New OS</a><a href='/search'>Search</a><a href='/load'>Load Directory</a><a href='/exit'>Exit</a></nav>";
    }

    private static string RenderPage(string Title, string Body, string NavigationHtml)
    {
        return $@"<!doctype html>
<html lang='en'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<title>{HtmlEscape(Title)}</title>
<style>
:root {{ color-scheme: light dark; font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; }}
body {{ margin: 0; background: #10131a; color: #eef2ff; }}
main {{ max-width: 1050px; margin: 0 auto; padding: 32px; }}
nav {{ display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 24px; }}
nav a, .button, button {{ background: #4f7cff; color: white; border: 0; border-radius: 12px; padding: 10px 14px; text-decoration: none; font-weight: 700; cursor: pointer; display: inline-block; }}
button {{ font-size: 1rem; }}
.secondary {{ background: #30384c; }}
.hero, .question, .card, section {{ background: #171c28; border: 1px solid #2b3447; border-radius: 18px; padding: 20px; margin-bottom: 18px; box-shadow: 0 12px 30px rgba(0,0,0,.22); }}
.grid {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; background: transparent; border: 0; box-shadow: none; padding: 0; }}
.card {{ color: #eef2ff; text-decoration: none; display: flex; flex-direction: column; gap: 8px; }}
.card span, .help, .key, .project-list span {{ color: #aab5cf; }}
label span {{ display: block; font-weight: 750; margin-bottom: 10px; }}
input[type=text], select {{ width: 100%; box-sizing: border-box; padding: 12px; border-radius: 12px; border: 1px solid #3a4560; background: #0d111a; color: #eef2ff; font-size: 1rem; }}
.choices {{ display: flex; gap: 10px; flex-wrap: wrap; }}
.choice {{ background: #0d111a; border: 1px solid #3a4560; border-radius: 12px; padding: 10px 12px; }}
.actions {{ display: flex; gap: 12px; align-items: center; margin-top: 18px; }}
.ok {{ color: #7ef0a1; }}
.warn {{ color: #ffd36b; }}
.fail {{ color: #ff8d8d; }}
code {{ color: #9ee7ff; }}
li {{ margin-bottom: 14px; }}
</style>
</head>
<body>
<main>
{NavigationHtml}
{Body}
</main>
<script>
(function () {{
  const title = document.querySelector('[name="OsTitle"]');
  const osName = document.querySelector('[name="OsName"]');
  const kernelName = document.querySelector('[name="KernelName"]');
  function sanitize(value) {{
    let cleaned = (value || '').replace(/[^A-Za-z0-9]/g, '');
    if (!cleaned) cleaned = 'MyOrynOS';
    if (!/^[A-Za-z]/.test(cleaned)) cleaned = 'Oryn' + cleaned;
    return cleaned;
  }}
  function autoFillFromTitle() {{
    if (!title || !osName || !kernelName) return;
    const generated = sanitize(title.value);
    if (!osName.dataset.userEdited || osName.value === '' || osName.value === 'MyOrynOS') {{
      osName.value = generated;
    }}
    if (!kernelName.dataset.userEdited || kernelName.value === '' || kernelName.value === 'MyOrynOSKernel' || kernelName.value.endsWith('Kernel')) {{
      kernelName.value = osName.value + 'Kernel';
    }}
  }}
  if (title) title.addEventListener('input', autoFillFromTitle);
  if (osName) {{
    osName.addEventListener('input', function () {{
      osName.dataset.userEdited = '1';
      osName.value = sanitize(osName.value);
      if (kernelName && !kernelName.dataset.userEdited) kernelName.value = osName.value + 'Kernel';
    }});
  }}
  if (kernelName) {{
    kernelName.addEventListener('input', function () {{
      kernelName.dataset.userEdited = '1';
      kernelName.value = sanitize(kernelName.value);
    }});
  }}
}})();
</script>
</body>
</html>";
    }

    private static Dictionary<string, string> ReadForm(HttpListenerRequest Request)
    {
        using StreamReader Reader = new(Request.InputStream, Request.ContentEncoding);
        string Body = Reader.ReadToEnd();
        Dictionary<string, List<string>> Values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string Pair in Body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] Parts = Pair.Split('=', 2);
            string Key = WebUtility.UrlDecode(Parts[0]) ?? string.Empty;
            string Value = Parts.Length > 1 ? WebUtility.UrlDecode(Parts[1]) ?? string.Empty : string.Empty;
            if (!Values.TryGetValue(Key, out List<string>? Existing))
            {
                Existing = new List<string>();
                Values[Key] = Existing;
            }
            Existing.Add(Value);
        }
        return Values.ToDictionary(Item => Item.Key, Item => string.Join(',', Item.Value), StringComparer.OrdinalIgnoreCase);
    }

    private static void SendHtml(HttpListenerContext Context, string Html, int StatusCode = 200)
    {
        byte[] Bytes = Encoding.UTF8.GetBytes(Html);
        Context.Response.StatusCode = StatusCode;
        Context.Response.ContentType = "text/html; charset=utf-8";
        Context.Response.ContentLength64 = Bytes.Length;
        Context.Response.OutputStream.Write(Bytes, 0, Bytes.Length);
        Context.Response.OutputStream.Close();
    }

    private static void Redirect(HttpListenerContext Context, string Location)
    {
        Context.Response.StatusCode = 302;
        Context.Response.RedirectLocation = Location;
        Context.Response.OutputStream.Close();
    }

    private static int FindFreePort()
    {
        System.Net.Sockets.TcpListener Listener = new(IPAddress.Loopback, 0);
        Listener.Start();
        int Port = ((IPEndPoint)Listener.LocalEndpoint).Port;
        Listener.Stop();
        return Port;
    }

    private static void OpenBrowser(string Url)
    {
        string? Browser = Environment.GetEnvironmentVariable("BROWSER");
        try
        {
            if (!string.IsNullOrWhiteSpace(Browser))
            {
                Process.Start(new ProcessStartInfo(Browser, Url) { UseShellExecute = false });
                return;
            }
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("cmd", "/c start " + Url) { CreateNoWindow = true });
                return;
            }
            if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", Url);
                return;
            }
            Process.Start("xdg-open", Url);
        }
        catch
        {
            Console.WriteLine("[WARN] [VISUALCFG] Could not open the browser automatically.");
        }
    }

    private static Dictionary<string, object> BuildAnswersFromForm(List<Question> Questions, Dictionary<string, string> Form)
    {
        Dictionary<string, object> Answers = new(StringComparer.OrdinalIgnoreCase);
        foreach (Question Question in Questions.OrderBy(Question => Question.Order))
        {
            string Value = Form.TryGetValue(Question.AnswerKey, out string? FormValue) ? FormValue : Question.Default;
            if (Question.Type.Equals("multi-choice", StringComparison.OrdinalIgnoreCase))
            {
                Value = string.IsNullOrWhiteSpace(Value) ? Question.Default : Value;
                string[] Parts = Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (Parts.Length == 0 && Question.Required) Parts = new[] { Question.Default };
                if (Parts.Any(Part => Part.Equals("None", StringComparison.OrdinalIgnoreCase)) && Parts.Length > 1)
                {
                    throw new InvalidOperationException("None cannot be combined with other module choices.");
                }
                foreach (string Part in Parts)
                {
                    if (!Question.Choices.Any(Choice => Choice.Equals(Part, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException("Invalid choice for " + Question.AnswerKey + ": " + Part);
                    }
                }
                Answers[Question.AnswerKey] = string.Join(',', Parts);
            }
            else if (Question.Type.Equals("choice", StringComparison.OrdinalIgnoreCase))
            {
                if (!Question.Choices.Any(Choice => Choice.Equals(Value, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Invalid choice for " + Question.AnswerKey + ": " + Value);
                }
                Answers[Question.AnswerKey] = Value;
            }
            else
            {
                Answers[Question.AnswerKey] = string.IsNullOrWhiteSpace(Value) ? Question.Default : Value.Trim();
            }
        }
        return Answers;
    }

    private static int RunTerminalMode(string ProjectRoot, List<Question> Questions, Dictionary<string, string> Arguments)
    {
        if (Arguments.TryGetValue("mode", out string? Mode))
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
        if (IsOrynProject(Candidate)) return ConfigureExisting(ProjectRoot, Questions, Candidate);
        List<string> ChildProjects = Directory.GetDirectories(Candidate).Where(IsOrynProject).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();
        if (ChildProjects.Count == 0)
        {
            Console.WriteLine("[FAIL] [VISUALCFG] No Oryn OS projects found in: " + Candidate);
            return 1;
        }
        for (int Index = 0; Index < ChildProjects.Count; Index++) Console.WriteLine($"  {Index + 1}. {Path.GetFileName(ChildProjects[Index])} - {ChildProjects[Index]}");
        Console.Write("Choose project: ");
        if (!int.TryParse(Console.ReadLine(), out int Choice) || Choice < 1 || Choice > ChildProjects.Count) return 0;
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
            if (ExistingValue is not null) Console.WriteLine("Current: " + Colour(FormatExistingValue(ExistingValue), Yellow));
            if (Question.Type.Equals("choice", StringComparison.OrdinalIgnoreCase)) Answers[Question.AnswerKey] = AskChoice(Question, Default);
            else if (Question.Type.Equals("multi-choice", StringComparison.OrdinalIgnoreCase)) Answers[Question.AnswerKey] = AskMultiChoice(Question, Default);
            else Answers[Question.AnswerKey] = AskString(Question, Default);
        }
        ValidateStrictNames(Answers);
        return Answers;
    }

    private static string AskChoice(Question Question, string Default)
    {
        for (int Index = 0; Index < Question.Choices.Length; Index++) Console.WriteLine($"  {Index + 1}. {Colour(Question.Choices[Index], Cyan)}");
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
        for (int Index = 0; Index < Question.Choices.Length; Index++) Console.WriteLine($"  {Index + 1}. {Colour(Question.Choices[Index], Cyan)}");
        Console.Write("Choose comma-separated values [" + Default + "]: ");
        string Input = (Console.ReadLine() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(Input)) return Default;
        string[] Parts = Input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string[] Values = Parts.Select(Part => int.TryParse(Part, out int Number) && Number >= 1 && Number <= Question.Choices.Length ? Question.Choices[Number - 1] : Part).ToArray();
        foreach (string Value in Values)
        {
            if (!Question.Choices.Any(Choice => Choice.Equals(Value, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Invalid choice for " + Question.AnswerKey + ": " + Value);
        }
        if (Values.Any(Value => Value.Equals("None", StringComparison.OrdinalIgnoreCase)) && Values.Length > 1) throw new InvalidOperationException("None cannot be combined with other module choices.");
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
        if (!Regex.IsMatch(OsName, "^[A-Za-z][A-Za-z0-9]*$")) throw new InvalidOperationException("OS Name must start with a letter and contain only letters and numbers. Spaces are not allowed.");
        if (!Regex.IsMatch(KernelName, "^[A-Za-z][A-Za-z0-9]*$")) throw new InvalidOperationException("Kernel Name must start with a letter and contain only letters and numbers. Spaces are not allowed.");
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
        string[] Args = { "run", "--project", GeneratorProject, "--", "generate", "--non-interactive", "--os-title", OsTitle, "--os-name", OsName, "--kernel-name", KernelName, "--target", Target, "--vm-profile", VmProfile, "--vm-display-mode", VmDisplayMode, "--build-mode", BuildMode, "--modules", Modules };
        Console.WriteLine("[ OK ] [VISUALCFG] Saving configuration through Oryn.Generator.");
        ProcessStartInfo StartInfo = new("dotnet") { WorkingDirectory = ProjectRoot, UseShellExecute = false };
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

    private static Dictionary<string, object> GuessInitialAnswers(string ProjectRoot)
    {
        Dictionary<string, object> Answers = new(StringComparer.OrdinalIgnoreCase);
        string BaseName = Path.GetFileName(Directory.GetCurrentDirectory());
        if (string.IsNullOrWhiteSpace(BaseName) || BaseName.Equals("FullSource", StringComparison.OrdinalIgnoreCase) || BaseName.Equals("ChangedFiles", StringComparison.OrdinalIgnoreCase))
        {
            BaseName = "My Oryn OS";
        }

        string GuessedOsName = SanitizeStrictName(BaseName);
        Answers["OsTitle"] = BaseName.Replace('-', ' ').Replace('_', ' ');
        Answers["OsName"] = GuessedOsName;
        Answers["KernelName"] = GuessedOsName + "Kernel";
        Answers["Target"] = "x64-elf";
        Answers["VmProfile"] = "RunQemu";
        Answers["VmDisplayMode"] = "Headless";
        Answers["UserSelectedModules"] = "None";
        Answers["BuildMode"] = "Debug";
        return Answers;
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
            if (Element.ValueKind == JsonValueKind.Array) return string.Join(',', Element.EnumerateArray().Select(Item => Item.GetString()).Where(Item => !string.IsNullOrWhiteSpace(Item)));
            return Element.ToString();
        }
        return Convert.ToString(Value) ?? string.Empty;
    }

    private static Dictionary<string, object> LoadExistingAnswers(string ProjectPath)
    {
        string AnswersDirectory = Path.Combine(ProjectPath, "Answers");
        string? AnswersFile = Directory.Exists(AnswersDirectory) ? Directory.GetFiles(AnswersDirectory, "*.answers.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
        string JsonPath = AnswersFile ?? Path.Combine(ProjectPath, "manifest.json");
        if (!File.Exists(JsonPath)) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        using JsonDocument Document = JsonDocument.Parse(File.ReadAllText(JsonPath));
        Dictionary<string, object> Values = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty Property in Document.RootElement.EnumerateObject())
        {
            Values[Property.Name] = Property.Value.Clone();
        }
        if (Document.RootElement.TryGetProperty("Answers", out JsonElement NestedAnswers) && NestedAnswers.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty Property in NestedAnswers.EnumerateObject())
            {
                if (Property.Value.ValueKind == JsonValueKind.Object && Property.Value.TryGetProperty("Value", out JsonElement NestedValue))
                {
                    Values[Property.Name] = NestedValue.Clone();
                }
                else
                {
                    Values[Property.Name] = Property.Value.Clone();
                }
            }
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
        foreach (string Arg in Args)
        {
            if (Arg.Equals("--terminal", StringComparison.OrdinalIgnoreCase)) Values["terminal"] = "true";
        }
        string[] Positional = Args.Where(Arg => !Arg.Equals("--terminal", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (Positional.Length == 0) return Values;
        string First = Positional[0];
        if (First.Equals("new", StringComparison.OrdinalIgnoreCase) || First.Equals("create", StringComparison.OrdinalIgnoreCase)) Values["mode"] = "create";
        else if (First.Equals("configure", StringComparison.OrdinalIgnoreCase)) Values["mode"] = "configure";
        else if (First.Equals("--search", StringComparison.OrdinalIgnoreCase) || First.Equals("search", StringComparison.OrdinalIgnoreCase)) Values["mode"] = "search";
        else if (First.Equals("--load", StringComparison.OrdinalIgnoreCase) || First.Equals("load", StringComparison.OrdinalIgnoreCase)) Values["mode"] = "load";
        if (Positional.Length > 1) Values["path"] = Positional[1];
        return Values;
    }

    private static string Colour(string Text, string Colour)
    {
        if (Console.IsOutputRedirected || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NO_COLOR")) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ORYN_NO_COLOR"))) return Text;
        return Colour + Text + Reset;
    }

    private static string HtmlEscape(string? Text) => WebUtility.HtmlEncode(Text ?? string.Empty);
    private static string HtmlAttribute(string? Text) => WebUtility.HtmlEncode(Text ?? string.Empty);

    private sealed class BrowserSession
    {
        public BrowserSession(string projectRoot, List<Question> questions, Dictionary<string, string> arguments)
        {
            ProjectRoot = projectRoot;
            Questions = questions;
            Arguments = arguments;
        }

        public string ProjectRoot { get; }
        public List<Question> Questions { get; }
        public Dictionary<string, string> Arguments { get; }
        public bool Done { get; set; }
        public int ExitCode { get; set; }
    }
}
