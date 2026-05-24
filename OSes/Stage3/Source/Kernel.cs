using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Memory;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage3 kernel entered");

        Memory.Initialize();
        Diagnostics.WriteOk("Stage3 memory initialized");

        int Counter = 0;
        while (Counter < 3)
        {
            Diagnostics.WriteOk("Stage3 loop tick");
            Counter = Counter + 1;
        }

        if (Counter == 3)
        {
            Diagnostics.WriteOk("Stage3 branch worked");
        }
        else
        {
            Diagnostics.WriteFail("Stage3 branch failed");
        }

        WriteBanner();

        Diagnostics.WriteOk("Stage3 kernel is halting forever");
        Cpu.HaltForever();
    }

    private static void WriteBanner()
    {
        Diagnostics.WriteOk("Stage3 helper method worked");
    }
}
