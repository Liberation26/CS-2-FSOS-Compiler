using System.Text;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.Object;

internal sealed class NativeObjectPlaceholderEmitter
{
    public string Emit(CompilerManifest Manifest)
    {
        StringBuilder Builder = new();
        Builder.AppendLine("Oryn Stage 2 object placeholder");
        Builder.AppendLine($"Version: {Manifest.CompilerVersion}");
        Builder.AppendLine($"Target: {Manifest.Target}");
        Builder.AppendLine($"EntrySymbol: {Manifest.EntrySymbol}");
        Builder.AppendLine();
        Builder.AppendLine("This file intentionally is not a linkable ELF64 object yet.");
        Builder.AppendLine("Use the generated .c or .S backend output for the current backend proof.");
        return Builder.ToString();
    }
}
