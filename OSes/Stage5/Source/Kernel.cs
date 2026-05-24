using Oryn.Kernel.Runtime;
using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Memory;
using Oryn.Kernel.Panic;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Runtime.Initialize();
        Diagnostics.WriteOk("Stage5 kernel entered");
        Diagnostics.WriteOk("Stage5 runtime contract initialized");
        Memory.Initialize();
        Diagnostics.WriteOk("Stage5 memory module initialized");

        int Counter = 0;
        while (Counter < 3)
        {
            Counter = Counter + 1;
        }

        if (Counter == 3)
        {
            Diagnostics.WriteOk("Stage5 loop and branch proof worked");
        }
        else
        {
            Panic.Halt("Stage5 loop and branch proof failed");
        }

        Runtime.MarkKernelReady();
        Diagnostics.WriteOk("Stage5 runtime marked kernel ready");
        Diagnostics.WriteOk("Stage5 kernel is halting forever");
        Cpu.HaltForever();
    }
}
