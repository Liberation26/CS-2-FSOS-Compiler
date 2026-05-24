using Oryn.Kernel.Diagnostics;
using Oryn.Kernel.ManifestLoader;
using Oryn.Kernel.Runtime;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("[ KERNEL   ] Stage6 kernel entered");
        Diagnostics.WriteOk("[ MANIFEST ] Stage6 module manifest loading started");
        ManifestLoader.InitializeSelected();
        Diagnostics.WriteOk("[ MANIFEST ] Stage6 selected modules initialized from manifest metadata");
        Runtime.MarkKernelReady();
        Diagnostics.WriteOk("[ KERNEL   ] Stage6 runtime marked kernel ready");
        Diagnostics.WriteOk("[ KERNEL   ] Stage6 kernel is halting forever");
    }
}
