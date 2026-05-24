using System.Text;
using Oryn.Compiler;

namespace Oryn.Compiler.Backends.Native.Object;

internal sealed class NativeElf64ObjectEmitter
{
    private const ushort MachineX86_64 = 62;
    private const uint SectionTypeNull = 0;
    private const uint SectionTypeProgBits = 1;
    private const uint SectionTypeSymTab = 2;
    private const uint SectionTypeStrTab = 3;
    private const uint SectionTypeRela = 4;
    private const ulong SectionFlagWrite = 0x1;
    private const ulong SectionFlagAlloc = 0x2;
    private const ulong SectionFlagExecInstr = 0x4;
    private const byte SymbolBindLocal = 0;
    private const byte SymbolBindGlobal = 1;
    private const byte SymbolTypeNoType = 0;
    private const byte SymbolTypeObject = 1;
    private const byte SymbolTypeFunc = 2;
    private const int R_X86_64_PC32 = 2;
    private const int R_X86_64_PLT32 = 4;

    private sealed record LocalSlot(string Name, int Offset);
    private sealed record StringLiteral(string Value, string Symbol, int Offset);
    private sealed record Fixup(int Offset, string Symbol, int Type, long Addend);
    private sealed record PendingBranch(int Offset, string Label, bool Conditional);
    private sealed record EncodedMethod(string Symbol, byte[] Bytes, IReadOnlyList<Fixup> Fixups, IReadOnlyDictionary<string, int> Labels, int StartOffset, int Size);
    private sealed record ElfSection(string Name, uint Type, ulong Flags, ulong Align, uint Link, uint Info, ulong EntrySize, byte[] Data);
    private sealed record ElfSymbol(string Name, byte Info, ushort SectionIndex, ulong Value, ulong Size);
    private sealed record RelaEntry(ulong Offset, uint SymbolIndex, uint Type, long Addend);

    private readonly Dictionary<string, string> LabelNames = new(StringComparer.Ordinal);

    public byte[] Emit(CompilerManifest Manifest)
    {
        OrynIrModule Module = new(Manifest.EntrySymbol, Manifest.Instructions, Manifest.Methods);
        return Emit(Module);
    }

    private byte[] Emit(OrynIrModule Module)
    {
        LabelNames.Clear();
        List<IrInstruction> AllInstructions = Module.Methods.SelectMany(Method => Method.Instructions).ToList();
        IReadOnlyList<StringLiteral> StringLiterals = BuildStringLiteralTable(AllInstructions);
        Dictionary<string, StringLiteral> StringByValue = StringLiterals.ToDictionary(Literal => Literal.Value, Literal => Literal, StringComparer.Ordinal);

        List<byte> Text = new();
        List<Fixup> TextFixups = new();
        List<ElfSymbol> FunctionSymbols = new();

        foreach (OrynIrMethod Method in Module.Methods)
        {
            int MethodStart = Text.Count;
            EncodedMethod Encoded = EncodeMethod(Method, MethodStart, StringByValue);
            Text.AddRange(Encoded.Bytes);
            TextFixups.AddRange(Encoded.Fixups);
            FunctionSymbols.Add(new ElfSymbol(Method.NativeSymbol, MakeInfo(SymbolBindGlobal, SymbolTypeFunc), 1, (ulong)MethodStart, (ulong)Encoded.Size));
        }

        byte[] Rodata = BuildRodata(StringLiterals);

        List<ElfSection> Sections = new()
        {
            new ElfSection(string.Empty, SectionTypeNull, 0, 0, 0, 0, 0, Array.Empty<byte>()),
            new ElfSection(".text", SectionTypeProgBits, SectionFlagAlloc | SectionFlagExecInstr, 16, 0, 0, 0, Text.ToArray()),
            new ElfSection(".rela.text", SectionTypeRela, 0, 8, 4, 1, 24, Array.Empty<byte>()),
            new ElfSection(".rodata", SectionTypeProgBits, SectionFlagAlloc, 1, 0, 0, 0, Rodata),
            new ElfSection(".symtab", SectionTypeSymTab, 0, 8, 5, 0, 24, Array.Empty<byte>()),
            new ElfSection(".strtab", SectionTypeStrTab, 0, 1, 0, 0, 0, Array.Empty<byte>()),
            new ElfSection(".shstrtab", SectionTypeStrTab, 0, 1, 0, 0, 0, Array.Empty<byte>()),
            new ElfSection(".note.GNU-stack", SectionTypeProgBits, 0, 1, 0, 0, 0, Array.Empty<byte>())
        };

        List<ElfSymbol> Symbols = new()
        {
            new ElfSymbol(string.Empty, MakeInfo(SymbolBindLocal, SymbolTypeNoType), 0, 0, 0),
            new ElfSymbol(".text", MakeInfo(SymbolBindLocal, SymbolTypeNoType), 1, 0, 0),
            new ElfSymbol(".rodata", MakeInfo(SymbolBindLocal, SymbolTypeNoType), 3, 0, 0)
        };

        foreach (StringLiteral Literal in StringLiterals)
        {
            Symbols.Add(new ElfSymbol(Literal.Symbol, MakeInfo(SymbolBindLocal, SymbolTypeObject), 3, (ulong)Literal.Offset, (ulong)Encoding.UTF8.GetByteCount(Literal.Value) + 1));
        }

        int FirstGlobalSymbolIndex = Symbols.Count;
        Symbols.AddRange(FunctionSymbols);

        Dictionary<string, uint> SymbolIndexByName = new(StringComparer.Ordinal);
        for (int Index = 0; Index < Symbols.Count; Index++)
        {
            if (!string.IsNullOrEmpty(Symbols[Index].Name))
            {
                SymbolIndexByName[Symbols[Index].Name] = (uint)Index;
            }
        }

        foreach (Fixup Fixup in TextFixups)
        {
            if (!SymbolIndexByName.ContainsKey(Fixup.Symbol))
            {
                Symbols.Add(new ElfSymbol(Fixup.Symbol, MakeInfo(SymbolBindGlobal, SymbolTypeNoType), 0, 0, 0));
                SymbolIndexByName[Fixup.Symbol] = (uint)(Symbols.Count - 1);
            }
        }

        byte[] Strtab = BuildStringTable(Symbols.Select(Symbol => Symbol.Name), out Dictionary<string, uint> SymbolNameOffsets);
        byte[] Symtab = BuildSymbolTable(Symbols, SymbolNameOffsets);

        List<RelaEntry> RelaEntries = new();
        foreach (Fixup Fixup in TextFixups)
        {
            RelaEntries.Add(new RelaEntry((ulong)Fixup.Offset, SymbolIndexByName[Fixup.Symbol], (uint)Fixup.Type, Fixup.Addend));
        }

        byte[] Relatext = BuildRelaTable(RelaEntries);
        byte[] Shstrtab = BuildStringTable(Sections.Select(Section => Section.Name), out Dictionary<string, uint> SectionNameOffsets);

        Sections[2] = Sections[2] with { Data = Relatext, Link = 4, Info = 1 };
        Sections[4] = Sections[4] with { Data = Symtab, Link = 5, Info = (uint)FirstGlobalSymbolIndex };
        Sections[5] = Sections[5] with { Data = Strtab };
        Sections[6] = Sections[6] with { Data = Shstrtab };

        return BuildElf(Sections, SectionNameOffsets);
    }

    private EncodedMethod EncodeMethod(OrynIrMethod Method, int MethodStartOffset, IReadOnlyDictionary<string, StringLiteral> StringSymbols)
    {
        IReadOnlyList<IrInstruction> Instructions = Method.Instructions;
        Dictionary<string, LocalSlot> LocalSlots = AllocateLocals(Instructions);
        int LocalFrameSize = AlignTo(LocalSlots.Count * 8, 16);
        int EvaluationStackDepth = 0;
        List<byte> Bytes = new();
        List<Fixup> Fixups = new();
        List<PendingBranch> Branches = new();
        Dictionary<string, int> Labels = new(StringComparer.Ordinal);

        Emit(Bytes, 0x55);
        Emit(Bytes, 0x48, 0x89, 0xE5);
        if (LocalFrameSize > 0)
        {
            EmitSubRsp(Bytes, LocalFrameSize);
        }

        for (int InstructionIndex = 0; InstructionIndex < Instructions.Count; InstructionIndex++)
        {
            IrInstruction Instruction = Instructions[InstructionIndex];

            if (CanFoldStringLiteralIntoFollowingCall(Instructions, InstructionIndex))
            {
                continue;
            }

            switch (Instruction.OpCode)
            {
                case "DeclareLocal":
                    EmitMovQwordLocalImmediateZero(Bytes, RequireLocal(LocalSlots, Instruction.Operand, Instruction.OpCode).Offset);
                    break;

                case "ConstInt32":
                    Emit(Bytes, 0xB8);
                    EmitInt32(Bytes, Instruction.Int32Value ?? 0);
                    Emit(Bytes, 0x50);
                    EvaluationStackDepth++;
                    break;

                case "ConstString":
                    EmitLeaRipRelative(Bytes, Fixups, MethodStartOffset, RequireString(StringSymbols, Instruction.StringValue).Symbol, ToRax: true);
                    Emit(Bytes, 0x50);
                    EvaluationStackDepth++;
                    break;

                case "LoadLocal":
                    EmitMovRaxFromLocal(Bytes, RequireLocal(LocalSlots, Instruction.Operand, Instruction.OpCode).Offset);
                    Emit(Bytes, 0x50);
                    EvaluationStackDepth++;
                    break;

                case "StoreLocal":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode);
                    Emit(Bytes, 0x58);
                    EmitMovLocalFromRax(Bytes, RequireLocal(LocalSlots, Instruction.Operand, Instruction.OpCode).Offset);
                    EvaluationStackDepth--;
                    break;

                case "AddInt32":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode, 2);
                    Emit(Bytes, 0x5B, 0x58, 0x01, 0xD8, 0x50);
                    EvaluationStackDepth--;
                    break;

                case "SubInt32":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode, 2);
                    Emit(Bytes, 0x5B, 0x58, 0x29, 0xD8, 0x50);
                    EvaluationStackDepth--;
                    break;

                case "CompareEqualInt32":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode, 2);
                    Emit(Bytes, 0x5B, 0x58, 0x39, 0xD8, 0x0F, 0x94, 0xC0, 0x0F, 0xB6, 0xC0, 0x50);
                    EvaluationStackDepth--;
                    break;

                case "CompareLessThanInt32":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode, 2);
                    Emit(Bytes, 0x5B, 0x58, 0x39, 0xD8, 0x0F, 0x9C, 0xC0, 0x0F, 0xB6, 0xC0, 0x50);
                    EvaluationStackDepth--;
                    break;

                case "Call":
                    bool PreviousFoldedString = InstructionIndex > 0 && CanFoldStringLiteralIntoFollowingCall(Instructions, InstructionIndex - 1);
                    EvaluationStackDepth = EmitCall(Bytes, Fixups, MethodStartOffset, Instruction, EvaluationStackDepth, StringSymbols, PreviousFoldedString);
                    break;

                case "Label":
                    Labels[LabelSymbol(Instruction.Operand)] = Bytes.Count;
                    break;

                case "Jump":
                    Emit(Bytes, 0xE9);
                    Branches.Add(new PendingBranch(Bytes.Count, LabelSymbol(Instruction.Operand), Conditional: false));
                    EmitInt32(Bytes, 0);
                    break;

                case "JumpIfFalse":
                    RequireStack(EvaluationStackDepth, Instruction.OpCode);
                    Emit(Bytes, 0x58, 0x85, 0xC0, 0x0F, 0x84);
                    Branches.Add(new PendingBranch(Bytes.Count, LabelSymbol(Instruction.Operand), Conditional: true));
                    EmitInt32(Bytes, 0);
                    EvaluationStackDepth--;
                    break;

                case "Return":
                    Emit(Bytes, 0xC9, 0xC3);
                    break;

                default:
                    throw new OrynCompileException($"Unsupported IR instruction for Stage 3 ELF64 object backend: {Instruction.OpCode}");
            }
        }

        foreach (PendingBranch Branch in Branches)
        {
            if (!Labels.TryGetValue(Branch.Label, out int TargetOffset))
            {
                throw new OrynCompileException($"Missing label target for Stage 3 branch: {Branch.Label}");
            }

            int Displacement = TargetOffset - (Branch.Offset + 4);
            WriteInt32(Bytes, Branch.Offset, Displacement);
        }

        return new EncodedMethod(Method.NativeSymbol, Bytes.ToArray(), Fixups, Labels, MethodStartOffset, Bytes.Count);
    }

    private static int EmitCall(List<byte> Bytes, List<Fixup> Fixups, int MethodStartOffset, IrInstruction Instruction, int EvaluationStackDepth, IReadOnlyDictionary<string, StringLiteral> StringSymbols, bool PreviousFoldedString)
    {
        string CallName = Instruction.ManagedName ?? Instruction.Operand ?? "<unknown>";
        string NativeSymbol = Instruction.NativeSymbol ?? throw new OrynCompileException($"Call IR instruction is missing native symbol for {CallName}.");
        int ArgumentCount = Instruction.Arguments.Count;

        if (ArgumentCount > 1)
        {
            throw new OrynCompileException($"Stage 3 ELF64 backend currently supports up to one native call argument: {Instruction.ManagedName ?? NativeSymbol}");
        }

        if (ArgumentCount == 1)
        {
            if (PreviousFoldedString)
            {
                EmitLeaRipRelative(Bytes, Fixups, MethodStartOffset, RequireString(StringSymbols, Instruction.Arguments[0]).Symbol, ToRax: false);
            }
            else
            {
                RequireStack(EvaluationStackDepth, Instruction.OpCode);
                Emit(Bytes, 0x5F);
                EvaluationStackDepth--;
            }
        }

        bool NeedsAlignmentSlot = (EvaluationStackDepth % 2) != 0;
        if (NeedsAlignmentSlot)
        {
            Emit(Bytes, 0x48, 0x83, 0xEC, 0x08);
        }

        Emit(Bytes, 0xE8);
        int RelocationOffset = MethodStartOffset + Bytes.Count;
        EmitInt32(Bytes, 0);
        Fixups.Add(new Fixup(RelocationOffset, NativeSymbol, R_X86_64_PLT32, -4));

        if (NeedsAlignmentSlot)
        {
            Emit(Bytes, 0x48, 0x83, 0xC4, 0x08);
        }

        return EvaluationStackDepth;
    }

    private static IReadOnlyList<StringLiteral> BuildStringLiteralTable(IReadOnlyList<IrInstruction> Instructions)
    {
        List<StringLiteral> Literals = new();
        HashSet<string> Seen = new(StringComparer.Ordinal);
        int Offset = 0;
        foreach (IrInstruction Instruction in Instructions)
        {
            if (Instruction.OpCode != "ConstString")
            {
                continue;
            }

            string Value = Instruction.StringValue ?? string.Empty;
            if (Seen.Add(Value))
            {
                Literals.Add(new StringLiteral(Value, $".Lstr{Literals.Count}", Offset));
                Offset += Encoding.UTF8.GetByteCount(Value) + 1;
            }
        }

        return Literals;
    }

    private static byte[] BuildRodata(IReadOnlyList<StringLiteral> Literals)
    {
        List<byte> Data = new();
        foreach (StringLiteral Literal in Literals)
        {
            Data.AddRange(Encoding.UTF8.GetBytes(Literal.Value));
            Data.Add(0);
        }

        return Data.ToArray();
    }

    private static Dictionary<string, LocalSlot> AllocateLocals(IReadOnlyList<IrInstruction> Instructions)
    {
        Dictionary<string, LocalSlot> Slots = new(StringComparer.Ordinal);
        foreach (IrInstruction Instruction in Instructions)
        {
            if (Instruction.OpCode != "DeclareLocal" || string.IsNullOrWhiteSpace(Instruction.Operand))
            {
                continue;
            }

            if (!Slots.ContainsKey(Instruction.Operand))
            {
                int Offset = -8 * (Slots.Count + 1);
                Slots.Add(Instruction.Operand, new LocalSlot(Instruction.Operand, Offset));
            }
        }

        return Slots;
    }

    private string LabelSymbol(string? Label)
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new OrynCompileException("Missing label operand in Stage 3 ELF64 backend.");
        }

        if (!LabelNames.TryGetValue(Label, out string? Symbol))
        {
            Symbol = "Oryn_Label_" + SanitizeSymbol(Label);
            LabelNames.Add(Label, Symbol);
        }

        return Symbol;
    }

    private static StringLiteral RequireString(IReadOnlyDictionary<string, StringLiteral> StringSymbols, string? Value)
    {
        string LiteralValue = Value ?? string.Empty;
        if (!StringSymbols.TryGetValue(LiteralValue, out StringLiteral? Literal))
        {
            throw new OrynCompileException($"String literal was not registered in the Stage 3 ELF64 literal table: {LiteralValue}");
        }

        return Literal;
    }

    private static LocalSlot RequireLocal(Dictionary<string, LocalSlot> LocalSlots, string? Name, string OpCode)
    {
        if (string.IsNullOrWhiteSpace(Name) || !LocalSlots.TryGetValue(Name, out LocalSlot? Slot))
        {
            string LocalName = Name ?? "<null>";
            throw new OrynCompileException($"Unknown local for {OpCode}: {LocalName}");
        }

        return Slot;
    }

    private static void RequireStack(int EvaluationStackDepth, string OpCode, int Required = 1)
    {
        if (EvaluationStackDepth < Required)
        {
            throw new OrynCompileException($"IR stack underflow while emitting Stage 3 ELF64 for {OpCode}.");
        }
    }

    private static bool CanFoldStringLiteralIntoFollowingCall(IReadOnlyList<IrInstruction> Instructions, int InstructionIndex)
    {
        IrInstruction Instruction = Instructions[InstructionIndex];
        if (Instruction.OpCode != "ConstString" || InstructionIndex + 1 >= Instructions.Count)
        {
            return false;
        }

        IrInstruction NextInstruction = Instructions[InstructionIndex + 1];
        return NextInstruction.OpCode == "Call" &&
            NextInstruction.Arguments.Count == 1 &&
            string.Equals(Instruction.StringValue ?? string.Empty, NextInstruction.Arguments[0], StringComparison.Ordinal);
    }

    private static byte MakeInfo(byte Bind, byte Type) => (byte)((Bind << 4) | (Type & 0x0F));

    private static byte[] BuildStringTable(IEnumerable<string> Names, out Dictionary<string, uint> Offsets)
    {
        List<byte> Data = new() { 0 };
        Offsets = new Dictionary<string, uint>(StringComparer.Ordinal) { [string.Empty] = 0 };
        foreach (string Name in Names)
        {
            if (Offsets.ContainsKey(Name))
            {
                continue;
            }

            Offsets[Name] = (uint)Data.Count;
            Data.AddRange(Encoding.UTF8.GetBytes(Name));
            Data.Add(0);
        }

        return Data.ToArray();
    }

    private static byte[] BuildSymbolTable(IReadOnlyList<ElfSymbol> Symbols, IReadOnlyDictionary<string, uint> NameOffsets)
    {
        List<byte> Data = new();
        foreach (ElfSymbol Symbol in Symbols)
        {
            EmitUInt32(Data, NameOffsets[Symbol.Name]);
            Emit(Data, Symbol.Info);
            Emit(Data, 0);
            EmitUInt16(Data, Symbol.SectionIndex);
            EmitUInt64(Data, Symbol.Value);
            EmitUInt64(Data, Symbol.Size);
        }

        return Data.ToArray();
    }

    private static byte[] BuildRelaTable(IReadOnlyList<RelaEntry> Entries)
    {
        List<byte> Data = new();
        foreach (RelaEntry Entry in Entries)
        {
            EmitUInt64(Data, Entry.Offset);
            EmitUInt64(Data, ((ulong)Entry.SymbolIndex << 32) | Entry.Type);
            EmitInt64(Data, Entry.Addend);
        }

        return Data.ToArray();
    }

    private static byte[] BuildElf(IReadOnlyList<ElfSection> Sections, IReadOnlyDictionary<string, uint> SectionNameOffsets)
    {
        const int HeaderSize = 64;
        const int SectionHeaderSize = 64;
        List<byte> File = new(new byte[HeaderSize]);
        List<ulong> SectionOffsets = new();

        foreach (ElfSection Section in Sections)
        {
            if (Section.Type == SectionTypeNull)
            {
                SectionOffsets.Add(0);
                continue;
            }

            AlignFile(File, (int)Math.Max(Section.Align, 1));
            SectionOffsets.Add((ulong)File.Count);
            File.AddRange(Section.Data);
        }

        AlignFile(File, 8);
        ulong SectionHeaderOffset = (ulong)File.Count;

        for (int Index = 0; Index < Sections.Count; Index++)
        {
            ElfSection Section = Sections[Index];
            EmitUInt32(File, SectionNameOffsets[Section.Name]);
            EmitUInt32(File, Section.Type);
            EmitUInt64(File, Section.Flags);
            EmitUInt64(File, 0);
            EmitUInt64(File, SectionOffsets[Index]);
            EmitUInt64(File, (ulong)Section.Data.Length);
            EmitUInt32(File, Section.Link);
            EmitUInt32(File, Section.Info);
            EmitUInt64(File, Section.Align);
            EmitUInt64(File, Section.EntrySize);
        }

        List<byte> Header = new();
        Emit(Header, 0x7F, (byte)'E', (byte)'L', (byte)'F');
        Emit(Header, 2, 1, 1, 0);
        while (Header.Count < 16)
        {
            Emit(Header, 0);
        }
        EmitUInt16(Header, 1);
        EmitUInt16(Header, MachineX86_64);
        EmitUInt32(Header, 1);
        EmitUInt64(Header, 0);
        EmitUInt64(Header, 0);
        EmitUInt64(Header, SectionHeaderOffset);
        EmitUInt32(Header, 0);
        EmitUInt16(Header, HeaderSize);
        EmitUInt16(Header, 0);
        EmitUInt16(Header, 0);
        EmitUInt16(Header, SectionHeaderSize);
        EmitUInt16(Header, (ushort)Sections.Count);
        EmitUInt16(Header, 6);

        if (Header.Count != HeaderSize)
        {
            throw new OrynCompileException($"Internal ELF header size error: {Header.Count}");
        }

        for (int Index = 0; Index < Header.Count; Index++)
        {
            File[Index] = Header[Index];
        }

        return File.ToArray();
    }

    private static void EmitLeaRipRelative(List<byte> Bytes, List<Fixup> Fixups, int MethodStartOffset, string Symbol, bool ToRax)
    {
        if (ToRax)
        {
            Emit(Bytes, 0x48, 0x8D, 0x05);
        }
        else
        {
            Emit(Bytes, 0x48, 0x8D, 0x3D);
        }

        int RelocationOffset = MethodStartOffset + Bytes.Count;
        EmitInt32(Bytes, 0);
        Fixups.Add(new Fixup(RelocationOffset, Symbol, R_X86_64_PC32, -4));
    }

    private static void EmitSubRsp(List<byte> Bytes, int Value)
    {
        if (Value <= sbyte.MaxValue)
        {
            Emit(Bytes, 0x48, 0x83, 0xEC, (byte)Value);
        }
        else
        {
            Emit(Bytes, 0x48, 0x81, 0xEC);
            EmitInt32(Bytes, Value);
        }
    }

    private static void EmitMovQwordLocalImmediateZero(List<byte> Bytes, int Offset)
    {
        Emit(Bytes, 0x48, 0xC7, 0x45, unchecked((byte)Offset));
        EmitInt32(Bytes, 0);
    }

    private static void EmitMovRaxFromLocal(List<byte> Bytes, int Offset) => Emit(Bytes, 0x48, 0x8B, 0x45, unchecked((byte)Offset));

    private static void EmitMovLocalFromRax(List<byte> Bytes, int Offset) => Emit(Bytes, 0x48, 0x89, 0x45, unchecked((byte)Offset));

    private static void Emit(List<byte> Bytes, params byte[] Values) => Bytes.AddRange(Values);

    private static void EmitInt32(List<byte> Bytes, int Value)
    {
        Bytes.Add((byte)(Value & 0xFF));
        Bytes.Add((byte)((Value >> 8) & 0xFF));
        Bytes.Add((byte)((Value >> 16) & 0xFF));
        Bytes.Add((byte)((Value >> 24) & 0xFF));
    }

    private static void WriteInt32(List<byte> Bytes, int Offset, int Value)
    {
        Bytes[Offset] = (byte)(Value & 0xFF);
        Bytes[Offset + 1] = (byte)((Value >> 8) & 0xFF);
        Bytes[Offset + 2] = (byte)((Value >> 16) & 0xFF);
        Bytes[Offset + 3] = (byte)((Value >> 24) & 0xFF);
    }

    private static void EmitInt64(List<byte> Bytes, long Value) => EmitUInt64(Bytes, unchecked((ulong)Value));

    private static void EmitUInt16(List<byte> Bytes, ushort Value)
    {
        Bytes.Add((byte)(Value & 0xFF));
        Bytes.Add((byte)((Value >> 8) & 0xFF));
    }

    private static void EmitUInt32(List<byte> Bytes, uint Value)
    {
        Bytes.Add((byte)(Value & 0xFF));
        Bytes.Add((byte)((Value >> 8) & 0xFF));
        Bytes.Add((byte)((Value >> 16) & 0xFF));
        Bytes.Add((byte)((Value >> 24) & 0xFF));
    }

    private static void EmitUInt64(List<byte> Bytes, ulong Value)
    {
        for (int Index = 0; Index < 8; Index++)
        {
            Bytes.Add((byte)((Value >> (Index * 8)) & 0xFF));
        }
    }

    private static void AlignFile(List<byte> File, int Alignment)
    {
        if (Alignment <= 1)
        {
            return;
        }

        while ((File.Count % Alignment) != 0)
        {
            File.Add(0);
        }
    }

    private static string SanitizeSymbol(string Value)
    {
        StringBuilder Builder = new();
        foreach (char Character in Value)
        {
            if ((Character >= 'A' && Character <= 'Z') ||
                (Character >= 'a' && Character <= 'z') ||
                (Character >= '0' && Character <= '9') ||
                Character == '_')
            {
                Builder.Append(Character);
            }
            else
            {
                Builder.Append('_');
            }
        }

        return Builder.ToString();
    }

    private static int AlignTo(int Value, int Alignment)
    {
        if (Value == 0)
        {
            return 0;
        }

        int Remainder = Value % Alignment;
        return Remainder == 0 ? Value : Value + Alignment - Remainder;
    }
}
