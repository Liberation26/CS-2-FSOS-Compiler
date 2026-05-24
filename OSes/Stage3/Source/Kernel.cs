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
        int Remaining = 3;
        int Score = 0;

        while (Counter < 3)
        {
            Diagnostics.WriteOk("Stage3 loop tick");
            Counter = Counter + 1;
            Remaining = Remaining - 1;
            Score = Score + 2;
        }

        if (Counter == 3)
        {
            Diagnostics.WriteOk("Stage3 branch worked");
        }
        else
        {
            Diagnostics.WriteFail("Stage3 branch failed");
        }

        if (Remaining == 0)
        {
            Diagnostics.WriteOk("Stage3 subtraction worked");
        }
        else
        {
            Diagnostics.WriteFail("Stage3 subtraction failed");
        }

        if (Score == 6)
        {
            Diagnostics.WriteOk("Stage3 integer arithmetic worked");
        }
        else
        {
            Diagnostics.WriteFail("Stage3 integer arithmetic failed");
        }

        WriteBanner();
        WriteReturnProof();

        Diagnostics.WriteOk("Stage3 parity proof complete");
        Diagnostics.WriteOk("Stage3 kernel is halting forever");
        Cpu.HaltForever();
    }

    private static void WriteBanner()
    {
        Diagnostics.WriteOk("Stage3 helper method worked");
    }

    private static void WriteReturnProof()
    {
        Diagnostics.WriteOk("Stage3 explicit return worked");
        return;
    }
}
