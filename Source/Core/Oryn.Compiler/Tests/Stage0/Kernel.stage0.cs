using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Kernel entered");
        Cpu.HaltForever();
    }
}
