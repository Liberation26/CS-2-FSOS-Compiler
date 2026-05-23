using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Memory;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage1 kernel entered");
        Memory.Initialize();
        Diagnostics.WriteOk("Stage1 memory module initialized");
        Diagnostics.WriteOk("Stage1 kernel is halting forever");
        Cpu.HaltForever();
    }
}
