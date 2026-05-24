using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Memory;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage4 kernel entered");
        Diagnostics.WriteOk("Stage4 approved module boundary entered");
        Memory.Initialize();
        Diagnostics.WriteOk("Stage4 approved Memory.Initialize call worked");
        Diagnostics.WriteOk("Stage4 approved Diagnostics.WriteOk call worked");
        Diagnostics.WriteOk("Stage4 kernel is halting forever");
        Cpu.HaltForever();
    }
}
