using System.Text.RegularExpressions;

namespace Oryn.Compiler.Frontend.SafeSubsetValidator;

internal sealed class SafeSubsetValidator
{
    private static readonly string[] ForbiddenTokens =
    {
        " new ", " throw ", " try ", " catch ", " finally ", " async ", " await ", " delegate ",
        " dynamic ", " foreach ", " lock ", " typeof", " sizeof", " stackalloc", " fixed ", " unsafe "
    };

    private readonly IReadOnlyCollection<string> ApprovedNamespaces;

    public SafeSubsetValidator(IReadOnlyCollection<string> ApprovedNamespaces)
    {
        this.ApprovedNamespaces = ApprovedNamespaces;
    }

    public IReadOnlyList<string> Validate(string SourceText)
    {
        List<string> Failures = new();
        string SearchText = " " + Regex.Replace(SourceText, @"\s+", " ") + " ";

        foreach (string Token in ForbiddenTokens)
        {
            if (SearchText.Contains(Token, StringComparison.Ordinal))
            {
                Failures.Add($"Forbidden Stage 4 safe-kernel construct detected: {Token.Trim()}");
            }
        }

        if (SourceText.Contains("=>", StringComparison.Ordinal))
        {
            Failures.Add("Forbidden Stage 4 safe-kernel construct detected: lambda/expression body.");
        }

        if (!SourceText.Contains("public static void Main", StringComparison.Ordinal))
        {
            Failures.Add("Stage 4 requires public static void Main().");
        }

        foreach (Match UsingMatch in Regex.Matches(SourceText, @"^\s*using\s+(?<Namespace>[A-Za-z_][A-Za-z0-9_.]*)\s*;", RegexOptions.Multiline))
        {
            string NamespaceName = UsingMatch.Groups["Namespace"].Value;
            if (NamespaceName.StartsWith("Oryn.Kernel.", StringComparison.Ordinal) && !ApprovedNamespaces.Contains(NamespaceName))
            {
                Failures.Add($"Stage 4 module boundary rejected namespace '{NamespaceName}'. Approved namespaces: {string.Join(", ", ApprovedNamespaces)}");
            }
        }

        return Failures;
    }
}
