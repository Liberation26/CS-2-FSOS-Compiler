namespace Oryn.Compiler;

internal static class Program
{
    private static int Main(string[] Args)
    {
        Console.WriteLine("[ OK ] Oryn.Compiler started.");
        Console.WriteLine("[ OK ] Version: 0.0.0");

        if (Args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        if (Args[0].Equals("modules", StringComparison.OrdinalIgnoreCase))
        {
            PrintModules();
            return 0;
        }

        if (Args[0].Equals("compile", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[FAIL] Native compilation is not implemented in 0.0.0.");
            Console.WriteLine("[ OK ] This release creates the compiler filesystem and command surface.");
            return 2;
        }

        Console.WriteLine($"[FAIL] Unknown command: {Args[0]}");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  oryn compiler modules");
        Console.WriteLine("  oryn compiler compile <source.cs> --target x64-elf --output <file.o>");
    }

    private static void PrintModules()
    {
        Console.WriteLine("[ OK ] Available starter modules:");
        Console.WriteLine("  Diagnostics  namespace=Oryn.Kernel.Diagnostics");
        Console.WriteLine("  Cpu          namespace=Oryn.Kernel.Cpu");
        Console.WriteLine("  Memory       namespace=Oryn.Kernel.Memory");
    }
}
