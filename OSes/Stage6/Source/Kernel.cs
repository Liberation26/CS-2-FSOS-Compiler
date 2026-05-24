using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.ManifestLoader;
using Oryn.Kernel.Runtime;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage6 kernel entered");
        Diagnostics.WriteOk("Stage6 module manifest loading started");
        ManifestLoader.InitializeSelected();
        Diagnostics.WriteOk("Stage6 selected modules initialized from manifest metadata");
        Runtime.MarkKernelReady();
        Diagnostics.WriteOk("Stage6 runtime marked kernel ready");
        Diagnostics.WriteOk("Stage6 kernel is halting forever");
        Cpu.HaltForever();
    }
}
