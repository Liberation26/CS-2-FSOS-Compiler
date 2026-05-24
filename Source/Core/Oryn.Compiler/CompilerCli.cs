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

        if (Command.Equals("compose-kernel", StringComparison.OrdinalIgnoreCase))
        {
            return ComposeKernel(Args.Skip(1).ToArray());
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
        Output.WriteLine("  oryn compiler compose-kernel --stage Stage9 --template <template.cs> --output <generated.cs> [--os-name <name>] [--kernel-name <name>] [--modules Memory] [--mandatory-modules Runtime,Diagnostics,Panic,Cpu,ManifestLoader]");
        Output.WriteLine("  oryn compiler compile <source.cs> --target x64-elf --output <output.o>");
        Output.WriteLine();
        Output.WriteLine("2.0.2 compile output:");
        Output.WriteLine("  <output>.stage2.ir.json through <output>.stage9.ir.json lowered Oryn IR and backend manifest");
        Output.WriteLine("  <output>.generated.c     freestanding C backend snippet");
        Output.WriteLine("  <output>.generated.S     readable x64 assembly reference artifact");
        Output.WriteLine("  <output>                 real ELF64 relocatable object written directly by Oryn after approved-module validation");
    }

    private void PrintModules()
    {
        Output.WriteLine("[ OK ] Stage 9 manifest-backed approved module catalogue:");
        ModuleManifestCatalog ManifestCatalog = ModuleManifestCatalog.CreateDefault();
        foreach (ModuleManifestRecord Manifest in ManifestCatalog.ResolveApprovedKernelModules(9, ExcludeManifestLoaderFromGraph: false))
        {
            string Initializer = string.IsNullOrWhiteSpace(Manifest.InitializerNativeSymbol) ? "<none>" : Manifest.InitializerNativeSymbol;
            Output.WriteLine($"  {Manifest.ModuleName,-16} exposed namespace={Manifest.NamespaceName} stage={Manifest.Stage} order={Manifest.InitializeOrder} initializer={Initializer} dependsOn={(Manifest.DependsOn.Count == 0 ? "<none>" : string.Join(",", Manifest.DependsOn))} nativeSource={Manifest.NativeSource}");
        }

        Output.WriteLine("[ OK ] Approved method bindings:");
        foreach (BindingRecord Binding in BindingCatalog.CreateDefault().Bindings)
        {
            string Approval = Binding.AllowedInKernel ? "approved" : "blocked";
            Output.WriteLine($"  {Binding.ModuleName,-16} {Approval,-8} namespace={Binding.NamespaceName} type={Binding.TypeName} method={Binding.MethodName} signature=\"{Binding.Signature}\" native={Binding.NativeSymbol}");
        }
    }


    private int ComposeKernel(string[] Args)
    {
        try
        {
            KernelCompositionCommand Command = KernelTemplateComposer.ParseCommand(Args);
            BindingCatalog Bindings = BindingCatalog.CreateDefault();
            ModuleApiContractCatalog ApiContracts = ModuleApiContractCatalog.CreateDefault();
            KernelTemplateComposer Composer = new(Version, ModuleManifestCatalog.CreateDefault(), Bindings, ApiContracts);
            foreach (string Message in Composer.Compose(Command))
            {
                Output.WriteLine(Message);
            }

            return 0;
        }
        catch (OrynCompileException Exception)
        {
            Output.WriteLine($"[FAIL] {Exception.Message}");
            return 2;
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
