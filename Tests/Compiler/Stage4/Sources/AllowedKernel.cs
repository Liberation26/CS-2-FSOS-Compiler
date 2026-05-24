using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Memory;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage4 allowed call compiled");
        Memory.Initialize();
        Cpu.HaltForever();
    }
}
