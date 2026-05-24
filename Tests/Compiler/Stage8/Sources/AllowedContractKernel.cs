using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Runtime;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage8 allowed contract call");
        Runtime.MarkKernelReady();
        Cpu.HaltForever();
    }
}
