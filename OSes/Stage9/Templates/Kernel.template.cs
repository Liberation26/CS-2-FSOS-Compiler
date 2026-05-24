__ORYN_GENERATED_USINGS__

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage9 generated kernel entered");
        Diagnostics.WriteOk("Stage9 compiler version __ORYN_COMPILER_VERSION__");
__ORYN_KERNEL_BOOT_PROOF_LINES__
__ORYN_MODULE_INITIALIZATION_CALLS__
        Runtime.MarkKernelReady();
        Diagnostics.WriteOk("Stage9 generated kernel is halting forever");
        Cpu.HaltForever();
    }
}
