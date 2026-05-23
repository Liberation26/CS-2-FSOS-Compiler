#!/usr/bin/env bash
set -euo pipefail

RUNQEMU_VERSION="0.1.2"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_ROOT="${ORYN_BUILD_ROOT:-$PROJECT_ROOT/Build/Runqemu}"
SOURCE_FILE="${ORYN_KERNEL_SOURCE:-$PROJECT_ROOT/Source/Core/Oryn.Compiler/Tests/Stage0/Kernel.stage0.cs}"
COMPILER_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj"
KERNEL_OBJECT_PLACEHOLDER="$BUILD_ROOT/Kernel.stage1.o"
GENERATED_ASM="$BUILD_ROOT/Kernel.stage1.generated.S"
BOOT_SOURCE="$BUILD_ROOT/Boot.S"
DIAGNOSTICS_SOURCE="$BUILD_ROOT/Diagnostics.Runtime.c"
LINKER_SCRIPT="$BUILD_ROOT/Linker.ld"
KERNEL_ELF="$BUILD_ROOT/OrynKernel.elf"
QEMU_TIMEOUT="${ORYN_QEMU_TIMEOUT:-8}"

info() { printf '[ OK ] [ RUNQEMU  ] %s\n' "$1"; }
warn() { printf '[WARN] [ RUNQEMU  ] %s\n' "$1"; }
fail() { printf '[FAIL] [ RUNQEMU  ] %s\n' "$1"; exit 1; }

RequireTool() {
    command -v "$1" >/dev/null 2>&1 || fail "Required tool not found: $1"
}

info "Runqemu.sh version ${RUNQEMU_VERSION}"
info "Project root: ${PROJECT_ROOT}"
info "Build root: ${BUILD_ROOT}"

RequireTool dotnet
RequireTool clang
RequireTool ld
RequireTool qemu-system-x86_64
RequireTool timeout

[ -f "$COMPILER_PROJECT" ] || fail "Compiler project not found: $COMPILER_PROJECT"
[ -f "$SOURCE_FILE" ] || fail "Kernel source not found: $SOURCE_FILE"

rm -rf "$BUILD_ROOT"
mkdir -p "$BUILD_ROOT"

info "Running Oryn.Compiler Stage 1 backend"
dotnet run --project "$COMPILER_PROJECT" -- compile "$SOURCE_FILE" --target x64-elf --output "$KERNEL_OBJECT_PLACEHOLDER"

[ -f "$GENERATED_ASM" ] || fail "Compiler did not produce expected backend assembly: $GENERATED_ASM"

cat > "$BOOT_SOURCE" <<'EOF_BOOT'
.set MB_MAGIC, 0x1BADB002
.set MB_FLAGS, 0x00000003
.set MB_CHECKSUM, -(MB_MAGIC + MB_FLAGS)

.section .multiboot, "a"
.align 4
.long MB_MAGIC
.long MB_FLAGS
.long MB_CHECKSUM

.section .text
.code32
.global _start
.type _start, @function
_start:
    cli
    mov $BootStackTop32, %esp

    lgdt GdtDescriptor

    mov %cr4, %eax
    or $0x20, %eax
    mov %eax, %cr4

    mov $Pml4Table, %eax
    mov %eax, %cr3

    mov $0xC0000080, %ecx
    rdmsr
    or $0x00000100, %eax
    wrmsr

    mov %cr0, %eax
    or $0x80000001, %eax
    mov %eax, %cr0

    ljmp $0x08, $LongModeEntry

.code64
LongModeEntry:
    mov $0x10, %ax
    mov %ax, %ds
    mov %ax, %es
    mov %ax, %ss
    mov $BootStackTop64, %rsp
    xor %rbp, %rbp
    call Kernel_Main
BootHalt:
    hlt
    jmp BootHalt

.section .rodata
.align 8
Gdt:
    .quad 0x0000000000000000
    .quad 0x00209A0000000000
    .quad 0x0000920000000000
GdtEnd:
GdtDescriptor:
    .word GdtEnd - Gdt - 1
    .quad Gdt

.section .data
.align 4096
Pml4Table:
    .quad PdptTable + 0x003
    .fill 511, 8, 0
.align 4096
PdptTable:
    .quad PageDirectory + 0x003
    .fill 511, 8, 0
.align 4096
PageDirectory:
    .set PageIndex, 0
    .rept 512
    .quad (PageIndex * 0x200000) + 0x083
    .set PageIndex, PageIndex + 1
    .endr

.section .bss
.align 16
BootStack32:
    .skip 16384
BootStackTop32:
.align 16
BootStack64:
    .skip 16384
BootStackTop64:
EOF_BOOT

cat > "$DIAGNOSTICS_SOURCE" <<'EOF_DIAG'
#include <stdint.h>

#if DEBUG
static uint16_t* const VgaBuffer = (uint16_t*)0xB8000;
static uint32_t VgaOffset = 0;

static void OutByte(uint16_t Port, uint8_t Value)
{
    __asm__ volatile ("outb %0, %1" : : "a"(Value), "Nd"(Port));
}

static void SerialWriteChar(char Character)
{
    if (Character == '\n')
    {
        OutByte(0x3F8, '\r');
    }
    OutByte(0x3F8, (uint8_t)Character);
}

static void SerialWriteString(const char* Text)
{
    while (*Text != 0)
    {
        SerialWriteChar(*Text++);
    }
}

static void VgaWriteChar(char Character, uint8_t Attribute)
{
    if (Character == '\n')
    {
        VgaOffset = ((VgaOffset / 80) + 1) * 80;
        return;
    }

    if (VgaOffset >= (80 * 25))
    {
        VgaOffset = 0;
    }

    VgaBuffer[VgaOffset++] = ((uint16_t)Attribute << 8) | (uint8_t)Character;
}

static void VgaWriteString(const char* Text, uint8_t Attribute)
{
    while (*Text != 0)
    {
        VgaWriteChar(*Text++, Attribute);
    }
}

static void WriteLine(const char* Prefix, const char* Message, uint8_t Attribute)
{
    SerialWriteString(Prefix);
    SerialWriteString(Message);
    SerialWriteString("\n");

    VgaWriteString(Prefix, Attribute);
    VgaWriteString(Message, Attribute);
    VgaWriteString("\n", Attribute);
}
#endif

void Diagnostics_WriteOk(const char* Message)
{
#if DEBUG
    WriteLine("[ OK ] [ KERNEL   ] ", Message, 0x0A);
#else
    (void)Message;
#endif
}

void Diagnostics_WriteWarn(const char* Message)
{
#if DEBUG
    WriteLine("[WARN] [ KERNEL   ] ", Message, 0x0E);
#else
    (void)Message;
#endif
}

void Diagnostics_WriteFail(const char* Message)
{
#if DEBUG
    WriteLine("[FAIL] [ KERNEL   ] ", Message, 0x0C);
#else
    (void)Message;
#endif
}
EOF_DIAG

cat > "$LINKER_SCRIPT" <<'EOF_LINK'
ENTRY(_start)
SECTIONS
{
    . = 1M;
    .multiboot ALIGN(4K) : { KEEP(*(.multiboot)) }
    .text ALIGN(4K) : { *(.text*) }
    .rodata ALIGN(4K) : { *(.rodata*) }
    .data ALIGN(4K) : { *(.data*) }
    .bss ALIGN(4K) : { *(COMMON) *(.bss*) }
}
EOF_LINK

info "Compiling freestanding kernel objects"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$BOOT_SOURCE" -o "$BUILD_ROOT/Boot.o"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$GENERATED_ASM" -o "$BUILD_ROOT/Kernel.stage1.o.real"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -DDEBUG=1 -c "$DIAGNOSTICS_SOURCE" -o "$BUILD_ROOT/Diagnostics.Runtime.o"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$PROJECT_ROOT/Source/Native/Modules/Cpu/Cpu.Native.c" -o "$BUILD_ROOT/Cpu.Native.o"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$PROJECT_ROOT/Source/Native/Modules/Memory/Memory.Native.c" -o "$BUILD_ROOT/Memory.Native.o"

info "Linking freestanding ELF64 kernel: $KERNEL_ELF"
ld -nostdlib -T "$LINKER_SCRIPT" -o "$KERNEL_ELF" \
    "$BUILD_ROOT/Boot.o" \
    "$BUILD_ROOT/Kernel.stage1.o.real" \
    "$BUILD_ROOT/Diagnostics.Runtime.o" \
    "$BUILD_ROOT/Cpu.Native.o" \
    "$BUILD_ROOT/Memory.Native.o"

[ -f "$KERNEL_ELF" ] || fail "Kernel ELF was not produced: $KERNEL_ELF"
info "Freestanding kernel created: $KERNEL_ELF"

if [ "${ORYN_SKIP_QEMU:-0}" = "1" ]; then
    info "QEMU run skipped because ORYN_SKIP_QEMU=1."
    exit 0
fi

info "Starting QEMU. The kernel intentionally halts forever; timeout is treated as success."
set +e
timeout "$QEMU_TIMEOUT" qemu-system-x86_64 \
    -kernel "$KERNEL_ELF" \
    -serial stdio \
    -display none \
    -monitor none \
    -no-reboot \
    -no-shutdown ${ORYN_QEMU_EXTRA_FLAGS:-}
QEMU_STATUS=$?
set -e

if [ "$QEMU_STATUS" -eq 124 ]; then
    info "QEMU timeout reached after ${QEMU_TIMEOUT}s; freestanding kernel remained running as expected."
    exit 0
fi

if [ "$QEMU_STATUS" -ne 0 ]; then
    fail "QEMU exited with status $QEMU_STATUS"
fi

info "QEMU exited cleanly."
