__ORYN_GENERATED_USINGS__

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("__ORYN_OS_NAME__ generated kernel entered");
        Diagnostics.WriteOk("__ORYN_OS_NAME__ kernel name __ORYN_KERNEL_NAME__");
        Diagnostics.WriteOk("__ORYN_OS_NAME__ compiler version __ORYN_COMPILER_VERSION__");
__ORYN_KERNEL_BOOT_PROOF_LINES__
__ORYN_MODULE_INITIALIZATION_CALLS__
        Runtime.MarkKernelReady();
        Diagnostics.WriteOk("__ORYN_OS_NAME__ generated kernel is halting forever");
        Cpu.HaltForever();
    }
}