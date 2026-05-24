using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Memory;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage2 kernel entered");

        Memory.Initialize();
        Diagnostics.WriteOk("Stage2 memory initialized");

        int Counter = 0;
        while (Counter < 3)
        {
            Diagnostics.WriteOk("Stage2 loop tick");
            Counter = Counter + 1;
        }

        if (Counter == 3)
        {
            Diagnostics.WriteOk("Stage2 branch worked");
        }
        else
        {
            Diagnostics.WriteFail("Stage2 branch failed");
        }

        WriteBanner();

        Diagnostics.WriteOk("Stage2 kernel is halting forever");
        Cpu.HaltForever();
    }

    private static void WriteBanner()
    {
        Diagnostics.WriteOk("Stage2 helper method worked");
    }
}
