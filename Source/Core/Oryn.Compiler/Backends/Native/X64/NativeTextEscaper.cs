using Oryn.Compiler;
namespace Oryn.Compiler.Backends.Native.X64;

internal static class NativeTextEscaper
{
    public static string EscapeCString(string Value)
    {
        return Value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
