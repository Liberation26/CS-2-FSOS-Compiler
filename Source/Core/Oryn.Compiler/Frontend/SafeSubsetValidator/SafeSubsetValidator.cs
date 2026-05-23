using System.Text.RegularExpressions;

namespace Oryn.Compiler.Frontend.SafeSubsetValidator;

internal sealed class SafeSubsetValidator
{
    private static readonly string[] ForbiddenTokens =
    {
        " new ", " throw ", " try ", " catch ", " finally ", " async ", " await ", " delegate ",
        " dynamic ", " foreach ", " lock ", " typeof", " sizeof", " stackalloc", " fixed ", " unsafe "
    };

    public IReadOnlyList<string> Validate(string SourceText)
    {
        List<string> Failures = new();
        string SearchText = " " + Regex.Replace(SourceText, @"\s+", " ") + " ";

        foreach (string Token in ForbiddenTokens)
        {
            if (SearchText.Contains(Token, StringComparison.Ordinal))
            {
                Failures.Add($"Forbidden Stage 2 construct detected: {Token.Trim()}");
            }
        }

        if (SourceText.Contains("=>", StringComparison.Ordinal))
        {
            Failures.Add("Forbidden Stage 2 construct detected: lambda/expression body.");
        }

        if (!SourceText.Contains("public static void Main", StringComparison.Ordinal))
        {
            Failures.Add("Stage 2 requires public static void Main().");
        }

        return Failures;
    }
}
