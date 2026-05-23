namespace Oryn.Compiler;

internal sealed class CompilerCli
{
    private readonly string Version;
    private readonly TextWriter Output;

    public CompilerCli(string Version, TextWriter Output)
    {
        this.Version = Version;
        this.Output = Output;
    }

    public int Run(string[] Args)
    {
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

        Output.WriteLine($"[FAIL] Unknown command: {Command}");
        PrintUsage();
        return 1;
    }

    private void PrintUsage()
    {
        Output.WriteLine("Usage:");
        Output.WriteLine("  oryn compiler modules");
        Output.WriteLine("  oryn compiler compile <source.cs> --target x64-elf --output <output.o>");
        Output.WriteLine();
        Output.WriteLine("Stage 2 compile output:");
        Output.WriteLine("  <output>.stage2.ir.json lowered Oryn IR and backend manifest");
        Output.WriteLine("  <output>.generated.c     freestanding C backend snippet");
        Output.WriteLine("  <output>.generated.S     x64 assembly backend sketch");
        Output.WriteLine("  <output>                 text placeholder for the future ELF64 object");
    }

    private void PrintModules()
    {
        Output.WriteLine("[ OK ] Available starter modules:");
        foreach (BindingRecord Binding in BindingCatalog.CreateDefault().Bindings)
        {
            Output.WriteLine($"  {Binding.ModuleName,-12} namespace=Oryn.Kernel.{Binding.ModuleName,-12} native={Binding.NativeSymbol}");
        }
    }

    private int Compile(string[] Args)
    {
        CompilerCommand Command;
        try
        {
            Command = CompilerCommand.Parse(Args);
        }
        catch (OrynCompileException Exception)
        {
            Output.WriteLine($"[FAIL] {Exception.Message}");
            PrintUsage();
            return 1;
        }

        CompilerPipeline Pipeline = CompilerPipeline.CreateDefault(Version);
        CompilerResult Result = Pipeline.Compile(Command);

        foreach (string Message in Result.Messages)
        {
            Output.WriteLine(Message);
        }

        return Result.ExitCode;
    }
}
