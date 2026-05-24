using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Memory;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        WriteBanner();
        Memory.Initialize();

        int Counter = 0;
        while (Counter < 3)
        {
            Counter = Counter + 1;
        }

        if (Counter == 3)
        {
            Diagnostics.WriteOk("Stage2 helper method control flow graph loop completed");
        }
        else
        {
            Diagnostics.WriteFail("Stage2 helper method control flow graph loop failed");
        }

        Diagnostics.WriteOk("Stage2 helper method kernel is halting forever");
        Cpu.HaltForever();
    }

    private static void WriteBanner()
    {
        Diagnostics.WriteOk("Hello from helper method");
    }
}
