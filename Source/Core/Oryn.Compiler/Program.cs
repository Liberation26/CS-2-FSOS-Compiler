namespace Oryn.Compiler;

internal static class Program
{
    private const string Version = "0.4.2";

    private static int Main(string[] Args)
    {
        Console.WriteLine("[ OK ] Oryn.Compiler started.");
        Console.WriteLine($"[ OK ] Version: {Version}");

        CompilerCli Cli = new(Version, Console.Out);
        return Cli.Run(Args);
    }
}
