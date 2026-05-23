using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Memory;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage2 part1 kernel entered");
        Diagnostics.WriteOk("Stage2 part1 uses the Stage1 native call backend as its baseline");
        Memory.Initialize();
        Diagnostics.WriteOk("Stage2 part1 memory module initialized");
        Diagnostics.WriteOk("Stage2 part1 is ready for variables, branches, loops, and helper methods next");
        Diagnostics.WriteOk("Stage2 part1 kernel is halting forever");
        Cpu.HaltForever();
    }
}
