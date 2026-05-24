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
        Builder.AppendLine("This file intentionally is not a direct-writer ELF64 object yet.");
        Builder.AppendLine("Stage 2 uses clang/as to assemble the generated .S file into a real object; local variables are lowered to simple rbp-relative 64-bit stack slots and static helper methods are lowered to Kernel_* symbols.");
        return Builder.ToString();
    }
}
