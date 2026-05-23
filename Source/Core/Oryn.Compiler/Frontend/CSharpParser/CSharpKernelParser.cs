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
        string BodyText = StripComments(SourceText.Substring(BodyStart + 1, BodyEnd - BodyStart - 1));
        StatementParser Parser = new(BodyText);
        return new KernelAst(SourcePath, Parser.ParseStatements());
    }

    private static int FindMatchingBrace(string Text, int OpenBraceIndex)
    {
        int Depth = 0;
        bool InString = false;
        bool Escape = false;

        for (int Index = OpenBraceIndex; Index < Text.Length; Index++)
        {
            char Character = Text[Index];
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

                continue;
            }

            if (Character == '"')
            {
                InString = true;
            }
            else if (Character == '{')
            {
                Depth++;
            }
            else if (Character == '}')
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

    private sealed class StatementParser
    {
        private readonly string Text;
        private int Position;

        public StatementParser(string Text)
        {
            this.Text = Text;
        }

        public IReadOnlyList<KernelStatementAst> ParseStatements(char StopCharacter = '\0')
        {
            List<KernelStatementAst> Statements = new();
            while (true)
            {
                SkipWhitespace();
                if (Position >= Text.Length || (StopCharacter != '\0' && Text[Position] == StopCharacter))
                {
                    break;
                }

                Statements.Add(ParseStatement());
            }

            return Statements;
        }

        private KernelStatementAst ParseStatement()
        {
            SkipWhitespace();
            if (MatchKeyword("if"))
            {
                return ParseIf();
            }

            if (MatchKeyword("while"))
            {
                return ParseWhile();
            }

            if (MatchKeyword("return"))
            {
                Expect(';');
                return new KernelReturnAst();
            }

            string StatementText = ReadUntilTopLevelSemicolon().Trim();
            if (StatementText.Length == 0)
            {
                throw new OrynCompileException("Empty Stage 2 statement is not supported.");
            }

            return ParseSimpleStatement(StatementText);
        }

        private KernelStatementAst ParseIf()
        {
            KernelExpressionAst Condition = ParseParenthesizedExpression();
            IReadOnlyList<KernelStatementAst> ThenStatements = ParseRequiredBlock();
            IReadOnlyList<KernelStatementAst> ElseStatements = Array.Empty<KernelStatementAst>();
            SkipWhitespace();
            if (MatchKeyword("else"))
            {
                ElseStatements = ParseRequiredBlock();
            }

            return new KernelIfAst(Condition, ThenStatements, ElseStatements);
        }

        private KernelStatementAst ParseWhile()
        {
            KernelExpressionAst Condition = ParseParenthesizedExpression();
            return new KernelWhileAst(Condition, ParseRequiredBlock());
        }

        private IReadOnlyList<KernelStatementAst> ParseRequiredBlock()
        {
            SkipWhitespace();
            Expect('{');
            IReadOnlyList<KernelStatementAst> Statements = ParseStatements('}');
            Expect('}');
            return Statements;
        }

        private KernelExpressionAst ParseParenthesizedExpression()
        {
            SkipWhitespace();
            Expect('(');
            int Start = Position;
            int Depth = 1;
            bool InString = false;
            bool Escape = false;

            while (Position < Text.Length)
            {
                char Character = Text[Position++];
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
                else if (Character == '(')
                {
                    Depth++;
                }
                else if (Character == ')')
                {
                    Depth--;
                    if (Depth == 0)
                    {
                        string ExpressionText = Text.Substring(Start, Position - Start - 1);
                        return ExpressionParser.Parse(ExpressionText);
                    }
                }
            }

            throw new OrynCompileException("Could not find the closing parenthesis for a Stage 2 condition.");
        }

        private string ReadUntilTopLevelSemicolon()
        {
            int Start = Position;
            int ParenthesisDepth = 0;
            bool InString = false;
            bool Escape = false;

            while (Position < Text.Length)
            {
                char Character = Text[Position++];
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
                else if (Character == '(')
                {
                    ParenthesisDepth++;
                }
                else if (Character == ')')
                {
                    ParenthesisDepth--;
                }
                else if (Character == ';' && ParenthesisDepth == 0)
                {
                    return Text.Substring(Start, Position - Start);
                }
                else if ((Character == '{' || Character == '}') && ParenthesisDepth == 0)
                {
                    throw new OrynCompileException($"Unexpected block character in Stage 2 statement near: {Text.Substring(Start, Math.Min(Position - Start, 40))}");
                }
            }

            throw new OrynCompileException("Stage 2 statement is missing a semicolon.");
        }

        private static KernelStatementAst ParseSimpleStatement(string StatementText)
        {
            string WithoutSemicolon = StatementText.EndsWith(';') ? StatementText[..^1].Trim() : StatementText;

            Match Declaration = Regex.Match(WithoutSemicolon, @"^int\s+(?<Name>[A-Za-z_][A-Za-z0-9_]*)(?:\s*=\s*(?<Value>.+))?$", RegexOptions.Singleline);
            if (Declaration.Success)
            {
                string Name = Declaration.Groups["Name"].Value;
                KernelExpressionAst? Initializer = Declaration.Groups["Value"].Success
                    ? ExpressionParser.Parse(Declaration.Groups["Value"].Value)
                    : null;
                return new KernelLocalDeclarationAst("Int32", Name, Initializer);
            }

            Match Assignment = Regex.Match(WithoutSemicolon, @"^(?<Name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<Value>.+)$", RegexOptions.Singleline);
            if (Assignment.Success)
            {
                return new KernelAssignmentAst(Assignment.Groups["Name"].Value, ExpressionParser.Parse(Assignment.Groups["Value"].Value));
            }

            Match Call = Regex.Match(WithoutSemicolon, @"^(?<Class>[A-Za-z_][A-Za-z0-9_]*)\.(?<Method>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<Args>.*)\)$", RegexOptions.Singleline);
            if (Call.Success)
            {
                string ManagedName = Call.Groups["Class"].Value + "." + Call.Groups["Method"].Value;
                return new KernelCallStatementAst(ManagedName, ParseArguments(Call.Groups["Args"].Value));
            }

            throw new OrynCompileException($"Unsupported Stage 2 statement: {StatementText}");
        }

        private static IReadOnlyList<KernelExpressionAst> ParseArguments(string ArgumentText)
        {
            List<KernelExpressionAst> Arguments = new();
            foreach (string Argument in SplitArguments(ArgumentText))
            {
                if (!string.IsNullOrWhiteSpace(Argument))
                {
                    Arguments.Add(ExpressionParser.Parse(Argument));
                }
            }

            return Arguments;
        }

        private static IEnumerable<string> SplitArguments(string Text)
        {
            StringBuilder Current = new();
            int ParenthesisDepth = 0;
            bool InString = false;
            bool Escape = false;

            foreach (char Character in Text)
            {
                if (InString)
                {
                    Current.Append(Character);
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

                    continue;
                }

                if (Character == '"')
                {
                    InString = true;
                    Current.Append(Character);
                }
                else if (Character == '(')
                {
                    ParenthesisDepth++;
                    Current.Append(Character);
                }
                else if (Character == ')')
                {
                    ParenthesisDepth--;
                    Current.Append(Character);
                }
                else if (Character == ',' && ParenthesisDepth == 0)
                {
                    yield return Current.ToString();
                    Current.Clear();
                }
                else
                {
                    Current.Append(Character);
                }
            }

            yield return Current.ToString();
        }

        private bool MatchKeyword(string Keyword)
        {
            SkipWhitespace();
            if (!Text.AsSpan(Position).StartsWith(Keyword, StringComparison.Ordinal))
            {
                return false;
            }

            int End = Position + Keyword.Length;
            if (End < Text.Length && (char.IsLetterOrDigit(Text[End]) || Text[End] == '_'))
            {
                return false;
            }

            Position = End;
            return true;
        }

        private void Expect(char Expected)
        {
            SkipWhitespace();
            if (Position >= Text.Length || Text[Position] != Expected)
            {
                throw new OrynCompileException($"Expected '{Expected}' in Stage 2 source.");
            }

            Position++;
        }

        private void SkipWhitespace()
        {
            while (Position < Text.Length && char.IsWhiteSpace(Text[Position]))
            {
                Position++;
            }
        }
    }

    private sealed class ExpressionParser
    {
        private readonly IReadOnlyList<Token> Tokens;
        private int Position;

        private ExpressionParser(string Text)
        {
            Tokens = Tokenize(Text).ToList();
        }

        public static KernelExpressionAst Parse(string Text)
        {
            ExpressionParser Parser = new(Text);
            KernelExpressionAst Expression = Parser.ParseComparison();
            if (!Parser.IsAtEnd)
            {
                throw new OrynCompileException($"Unsupported Stage 2 expression near: {Parser.Peek().Text}");
            }

            return Expression;
        }

        private KernelExpressionAst ParseComparison()
        {
            KernelExpressionAst Left = ParseAdditive();
            while (Match("==") || Match("<"))
            {
                string Operator = Previous().Text;
                KernelExpressionAst Right = ParseAdditive();
                Left = new KernelBinaryExpressionAst(Operator, Left, Right);
            }

            return Left;
        }

        private KernelExpressionAst ParseAdditive()
        {
            KernelExpressionAst Left = ParsePrimary();
            while (Match("+") || Match("-"))
            {
                string Operator = Previous().Text;
                KernelExpressionAst Right = ParsePrimary();
                Left = new KernelBinaryExpressionAst(Operator, Left, Right);
            }

            return Left;
        }

        private KernelExpressionAst ParsePrimary()
        {
            if (MatchKind(TokenKind.Int32))
            {
                return new KernelIntLiteralAst(int.Parse(Previous().Text));
            }

            if (MatchKind(TokenKind.String))
            {
                return new KernelStringLiteralAst(Regex.Unescape(Previous().Text[1..^1]));
            }

            if (MatchKind(TokenKind.Identifier))
            {
                return new KernelLocalReferenceAst(Previous().Text);
            }

            if (Match("("))
            {
                KernelExpressionAst Expression = ParseComparison();
                Consume(")");
                return Expression;
            }

            throw new OrynCompileException($"Unsupported Stage 2 expression near: {Peek().Text}");
        }

        private bool Match(string Text)
        {
            if (!IsAtEnd && Peek().Text == Text)
            {
                Position++;
                return true;
            }

            return false;
        }

        private bool MatchKind(TokenKind Kind)
        {
            if (!IsAtEnd && Peek().Kind == Kind)
            {
                Position++;
                return true;
            }

            return false;
        }

        private void Consume(string Text)
        {
            if (!Match(Text))
            {
                throw new OrynCompileException($"Expected '{Text}' in Stage 2 expression.");
            }
        }

        private Token Peek() => IsAtEnd ? new Token(TokenKind.End, "<end>") : Tokens[Position];

        private Token Previous() => Tokens[Position - 1];

        private bool IsAtEnd => Position >= Tokens.Count;

        private static IEnumerable<Token> Tokenize(string Text)
        {
            int Position = 0;
            while (Position < Text.Length)
            {
                char Character = Text[Position];
                if (char.IsWhiteSpace(Character))
                {
                    Position++;
                    continue;
                }

                if (char.IsDigit(Character))
                {
                    int Start = Position;
                    while (Position < Text.Length && char.IsDigit(Text[Position]))
                    {
                        Position++;
                    }

                    yield return new Token(TokenKind.Int32, Text[Start..Position]);
                    continue;
                }

                if (char.IsLetter(Character) || Character == '_')
                {
                    int Start = Position;
                    while (Position < Text.Length && (char.IsLetterOrDigit(Text[Position]) || Text[Position] == '_'))
                    {
                        Position++;
                    }

                    yield return new Token(TokenKind.Identifier, Text[Start..Position]);
                    continue;
                }

                if (Character == '"')
                {
                    int Start = Position++;
                    bool Escape = false;
                    bool Closed = false;
                    while (Position < Text.Length)
                    {
                        char Current = Text[Position++];
                        if (Escape)
                        {
                            Escape = false;
                        }
                        else if (Current == '\\')
                        {
                            Escape = true;
                        }
                        else if (Current == '"')
                        {
                            Closed = true;
                            break;
                        }
                    }

                    if (!Closed)
                    {
                        throw new OrynCompileException("Unterminated string literal in Stage 2 expression.");
                    }

                    yield return new Token(TokenKind.String, Text[Start..Position]);
                    continue;
                }

                if (Position + 1 < Text.Length && Text.Substring(Position, 2) == "==")
                {
                    Position += 2;
                    yield return new Token(TokenKind.Operator, "==");
                    continue;
                }

                if ("+-<()".IndexOf(Character) >= 0)
                {
                    Position++;
                    yield return new Token(TokenKind.Operator, Character.ToString());
                    continue;
                }

                throw new OrynCompileException($"Unsupported token in Stage 2 expression: {Character}");

            }
        }

        private readonly record struct Token(TokenKind Kind, string Text);

        private enum TokenKind
        {
            Int32,
            String,
            Identifier,
            Operator,
            End
        }
    }
}
