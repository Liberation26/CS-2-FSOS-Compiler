using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Memory;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage2 phase3 kernel entered");
        Memory.Initialize();

        int Counter = 0;
        while (Counter < 2)
        {
            Counter = Counter + 1;
        }

        if (Counter == 2)
        {
            Diagnostics.WriteOk("Stage2 phase3 real Oryn IR loop completed");
        }
        else
        {
            Diagnostics.WriteFail("Stage2 phase3 real Oryn IR loop failed");
        }

        Diagnostics.WriteOk("Stage2 phase3 kernel is halting forever");
        Cpu.HaltForever();
    }
}
