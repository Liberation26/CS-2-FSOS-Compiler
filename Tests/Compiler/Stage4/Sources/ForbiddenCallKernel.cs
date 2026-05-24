using Oryn.Kernel.Diagnostics;

public static class Kernel
{
    public static void Main()
    {
        Diagnostics.WriteOk("Stage4 forbidden test entered");
        Console.WriteLine("This is not an approved Oryn module API");
    }
}
