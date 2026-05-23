namespace Oryn.Compiler;

internal sealed record CompilerResult(int ExitCode, IReadOnlyList<string> Messages);
