using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.ManifestLoader;
using Oryn.Kernel.Runtime;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage7 kernel entered");
        ManifestLoader.InitializeSelected();
        Diagnostics.WriteOk("Stage7 dependency-resolved modules initialized");
        Runtime.MarkKernelReady();
        Diagnostics.WriteOk("Stage7 kernel is halting forever");
        Cpu.HaltForever();
    }
}
