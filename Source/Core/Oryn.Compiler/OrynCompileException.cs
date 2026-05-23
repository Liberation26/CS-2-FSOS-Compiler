namespace Oryn.Compiler;

internal sealed class OrynCompileException : Exception
{
    public OrynCompileException(string Message)
        : base(Message)
    {
    }
}
