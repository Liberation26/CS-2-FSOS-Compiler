using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.Runtime;
using Oryn.Kernel.Cpu;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("This call is valid");
        Diagnostics.SecretWrite("This call must be blocked before backend compilation");
        Runtime.MarkKernelReady();
        Cpu.HaltForever();
    }
}
