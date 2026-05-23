using System.Text;
using System.Text.RegularExpressions;
using Oryn.Compiler;

namespace Oryn.Compiler.Frontend.CSharpParser;

internal sealed class CSharpKernelParser
{
    public KernelAst Parse(string SourcePath, string SourceText)
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
        List<KernelCallAst> Calls = new();

        foreach (string Statement in SplitStatements(CleanBody))
        {
            if (string.IsNullOrWhiteSpace(Statement))
            {
                continue;
            }

            Calls.Add(ParseStatement(Statement.Trim()));
        }

        return new KernelAst(SourcePath, Calls);
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

    private static KernelCallAst ParseStatement(string Statement)
    {
        Match CallMatch = Regex.Match(Statement, @"^(?<Class>[A-Za-z_][A-Za-z0-9_]*)\.(?<Method>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<Args>.*)\)\s*;$", RegexOptions.Singleline);
        if (!CallMatch.Success)
        {
            throw new OrynCompileException($"Unsupported Stage 2 statement: {Statement}");
        }

        string ManagedName = CallMatch.Groups["Class"].Value + "." + CallMatch.Groups["Method"].Value;
        string ArgumentText = CallMatch.Groups["Args"].Value.Trim();
        return new KernelCallAst(ManagedName, ParseArguments(ManagedName, ArgumentText));
    }

    private static IReadOnlyList<string> ParseArguments(string ManagedName, string ArgumentText)
    {
        if (string.IsNullOrWhiteSpace(ArgumentText))
        {
            return Array.Empty<string>();
        }

        Match StringMatch = Regex.Match(ArgumentText, "^\\\"(?<Value>(?:\\\\.|[^\\\"])*)\\\"$");
        if (!StringMatch.Success)
        {
            throw new OrynCompileException($"Call {ManagedName} currently accepts string literal arguments only.");
        }

        return new[] { Regex.Unescape(StringMatch.Groups["Value"].Value) };
    }
}
