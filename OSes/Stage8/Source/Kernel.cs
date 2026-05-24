using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.ManifestLoader;
using Oryn.Kernel.Runtime;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage8 kernel entered");
        Diagnostics.WriteOk("Stage8 approved C# module API contracts reached kernel code");
        ManifestLoader.InitializeSelected();
        Diagnostics.WriteOk("Stage8 approved module API contracts initialized");
        Runtime.MarkKernelReady();
        Diagnostics.WriteOk("Stage8 kernel is halting forever");
        Cpu.HaltForever();
    }
}
