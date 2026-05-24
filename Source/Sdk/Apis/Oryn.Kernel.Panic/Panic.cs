namespace Oryn.Kernel.Panic;

public static class Panic
{
    public static void Halt(string Reason)
    {
        throw new NotSupportedException("Oryn SDK API declarations are compile-time only.");
    }
}
