namespace Oryn.Compiler;

internal sealed record CompilerCommand(string SourcePath, string Target, string OutputPath)
{
    public static CompilerCommand Parse(string[] Args)
    {
        if (Args.Length == 0)
        {
            throw new OrynCompileException("Missing source file.");
        }

        string SourcePath = Args[0];
        string Target = ReadOption(Args, "--target") ?? "x64-elf";
        string? OutputPath = ReadOption(Args, "--output");

        if (!Target.Equals("x64-elf", StringComparison.OrdinalIgnoreCase))
        {
            throw new OrynCompileException($"Unsupported Stage 2 target: {Target}. Supported target: x64-elf");
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            throw new OrynCompileException("Missing --output <output.o>.");
        }

        return new CompilerCommand(SourcePath, Target, OutputPath);
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
}
