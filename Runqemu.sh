#!/usr/bin/env bash
set -euo pipefail

RUNQEMU_VERSION="0.1.6"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_ROOT="${ORYN_BUILD_ROOT:-$PROJECT_ROOT/OSes/Stage1/Build/Runqemu}"
SOURCE_FILE="${ORYN_KERNEL_SOURCE:-$PROJECT_ROOT/OSes/Stage1/Source/Kernel.cs}"
COMPILER_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj"
KERNEL_OBJECT_PLACEHOLDER="$BUILD_ROOT/Kernel.stage1.o"
GENERATED_ASM="$BUILD_ROOT/Kernel.stage1.generated.S"
BOOT_SOURCE="$BUILD_ROOT/Boot.S"
DIAGNOSTICS_SOURCE="$PROJECT_ROOT/Source/Native/Modules/Diagnostics/Diagnostics.Native.c"
COMPILER_DIAGNOSTICS_LOG="$BUILD_ROOT/Kernel.stage1.diagnostics.log"
LINKER_SCRIPT="$BUILD_ROOT/Linker.ld"
KERNEL_ELF="$BUILD_ROOT/OrynKernel.elf"
ISO_ROOT="$BUILD_ROOT/IsoRoot"
GRUB_CFG="$ISO_ROOT/boot/grub/grub.cfg"
KERNEL_ISO="$BUILD_ROOT/OrynKernel.iso"
QEMU_TIMEOUT="${ORYN_QEMU_TIMEOUT:-8}"
QEMU_DISPLAY_MODE="${ORYN_QEMU_DISPLAY:-headed}"
QEMU_BOOT_MODE="${ORYN_QEMU_BOOT:-iso}"
if [ "${ORYN_QEMU_HEADLESS:-0}" = "1" ]; then
    QEMU_DISPLAY_MODE="headless"
fi

info() { printf '[ OK ] [ RUNQEMU  ] %s\n' "$1"; }
warn() { printf '[WARN] [ RUNQEMU  ] %s\n' "$1"; }
fail() { printf '[FAIL] [ RUNQEMU  ] %s\n' "$1"; exit 1; }

RequireTool() {
    command -v "$1" >/dev/null 2>&1 || fail "Required tool not found: $1"
}

info "Runqemu.sh version ${RUNQEMU_VERSION}"
info "Project root: ${PROJECT_ROOT}"
info "Build root: ${BUILD_ROOT}"
info "Kernel source: ${SOURCE_FILE}"

RequireTool dotnet
RequireTool clang
RequireTool ld
RequireTool qemu-system-x86_64
RequireTool timeout

if [ "$QEMU_BOOT_MODE" = "iso" ]; then
    RequireTool grub-mkrescue
fi

[ -f "$COMPILER_PROJECT" ] || fail "Compiler project not found: $COMPILER_PROJECT"
[ -f "$SOURCE_FILE" ] || fail "Kernel source not found: $SOURCE_FILE"

rm -rf "$BUILD_ROOT"
mkdir -p "$BUILD_ROOT"

info "Running Oryn.Compiler Stage 1 backend"
info "The first dotnet run after an update may build the compiler before Oryn.Compiler prints its own lines."
dotnet run --project "$COMPILER_PROJECT" -- compile "$SOURCE_FILE" --target x64-elf --output "$KERNEL_OBJECT_PLACEHOLDER"
info "Oryn.Compiler Stage 1 backend completed"

[ -f "$GENERATED_ASM" ] || fail "Compiler did not produce expected backend assembly: $GENERATED_ASM"
[ -f "$COMPILER_DIAGNOSTICS_LOG" ] || fail "Compiler did not produce expected diagnostics log: $COMPILER_DIAGNOSTICS_LOG"
info "Compiler diagnostics log: $COMPILER_DIAGNOSTICS_LOG"

cat > "$BOOT_SOURCE" <<'EOF_BOOT'
.set MB1_MAGIC, 0x1BADB002
.set MB1_FLAGS, 0x00000003
.set MB1_CHECKSUM, -(MB1_MAGIC + MB1_FLAGS)

.set MB2_MAGIC, 0xE85250D6
.set MB2_ARCHITECTURE, 0
.set MB2_HEADER_LENGTH, Multiboot2HeaderEnd - Multiboot2Header
.set MB2_CHECKSUM, -(MB2_MAGIC + MB2_ARCHITECTURE + MB2_HEADER_LENGTH)

.section .multiboot, "a"
.align 4
.long MB1_MAGIC
.long MB1_FLAGS
.long MB1_CHECKSUM

.section .multiboot2, "a"
.align 8
Multiboot2Header:
.long MB2_MAGIC
.long MB2_ARCHITECTURE
.long MB2_HEADER_LENGTH
.long MB2_CHECKSUM
.align 8
.word 0
.word 0
.long 8
Multiboot2HeaderEnd:

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

cat > "$LINKER_SCRIPT" <<'EOF_LINK'
ENTRY(_start)
SECTIONS
{
    . = 1M;
    .multiboot ALIGN(4K) : { KEEP(*(.multiboot)) }
    .multiboot2 ALIGN(8) : { KEEP(*(.multiboot2)) }
    .text ALIGN(4K) : { *(.text*) }
    .rodata ALIGN(4K) : { *(.rodata*) }
    .data ALIGN(4K) : { *(.data*) }
    .bss ALIGN(4K) : { *(COMMON) *(.bss*) }
}
EOF_LINK

info "Compiling freestanding x86_64 kernel objects"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$BOOT_SOURCE" -o "$BUILD_ROOT/Boot.o"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$GENERATED_ASM" -o "$BUILD_ROOT/Kernel.stage1.o.real"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -DDEBUG=1 -c "$DIAGNOSTICS_SOURCE" -o "$BUILD_ROOT/Diagnostics.Runtime.o"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$PROJECT_ROOT/Source/Native/Modules/Cpu/Cpu.Native.c" -o "$BUILD_ROOT/Cpu.Native.o"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$PROJECT_ROOT/Source/Native/Modules/Memory/Memory.Native.c" -o "$BUILD_ROOT/Memory.Native.o"

info "Linking x86_64 freestanding ELF kernel: $KERNEL_ELF"
ld -nostdlib -T "$LINKER_SCRIPT" -o "$KERNEL_ELF" \
    "$BUILD_ROOT/Boot.o" \
    "$BUILD_ROOT/Kernel.stage1.o.real" \
    "$BUILD_ROOT/Diagnostics.Runtime.o" \
    "$BUILD_ROOT/Cpu.Native.o" \
    "$BUILD_ROOT/Memory.Native.o"

[ -f "$KERNEL_ELF" ] || fail "Kernel ELF was not produced: $KERNEL_ELF"
info "x86_64 freestanding kernel created: $KERNEL_ELF"

if command -v file >/dev/null 2>&1; then
    info "Kernel file type: $(file -b "$KERNEL_ELF")"
fi

BuildIso() {
    rm -rf "$ISO_ROOT"
    mkdir -p "$ISO_ROOT/boot/grub"
    cp "$KERNEL_ELF" "$ISO_ROOT/boot/OrynKernel.elf"
    cat > "$GRUB_CFG" <<'EOF_GRUB'
set timeout=0
set default=0

menuentry "Oryn Stage1 x86_64 Kernel" {
    multiboot2 /boot/OrynKernel.elf
    boot
}
EOF_GRUB

    info "Creating bootable x86_64 GRUB ISO: $KERNEL_ISO"
    grub-mkrescue -o "$KERNEL_ISO" "$ISO_ROOT" >/dev/null 2>&1 || fail "grub-mkrescue failed while creating: $KERNEL_ISO. Your host may also need xorriso installed."
    [ -f "$KERNEL_ISO" ] || fail "Kernel ISO was not produced: $KERNEL_ISO"
    info "Bootable kernel ISO created: $KERNEL_ISO"
}

if [ "$QEMU_BOOT_MODE" = "iso" ]; then
    BuildIso
elif [ "$QEMU_BOOT_MODE" = "direct" ]; then
    warn "ORYN_QEMU_BOOT=direct uses QEMU -kernel and may reject ELF64 images. The supported default is ORYN_QEMU_BOOT=iso."
else
    fail "Unsupported ORYN_QEMU_BOOT value: $QEMU_BOOT_MODE. Use iso or direct."
fi

if [ "${ORYN_SKIP_QEMU:-0}" = "1" ]; then
    info "QEMU run skipped because ORYN_SKIP_QEMU=1."
    exit 0
fi

case "$QEMU_DISPLAY_MODE" in
    headed|head|gui)
        QEMU_DISPLAY_MODE="headed"
        QEMU_DISPLAY_ARGS=()
        ;;
    headless|none|off)
        QEMU_DISPLAY_MODE="headless"
        QEMU_DISPLAY_ARGS=(-display none)
        ;;
    *)
        fail "Unsupported ORYN_QEMU_DISPLAY value: $QEMU_DISPLAY_MODE. Use headed or headless."
        ;;
esac

read -r -a QEMU_EXTRA_ARGS <<< "${ORYN_QEMU_EXTRA_FLAGS:-}"
if [ "$QEMU_BOOT_MODE" = "iso" ]; then
    QEMU_ARGS=(
        -cdrom "$KERNEL_ISO"
        -boot d
        -serial stdio
        -monitor none
        -no-reboot
        -no-shutdown
        "${QEMU_DISPLAY_ARGS[@]}"
        "${QEMU_EXTRA_ARGS[@]}"
    )
else
    QEMU_ARGS=(
        -kernel "$KERNEL_ELF"
        -serial stdio
        -monitor none
        -no-reboot
        -no-shutdown
        "${QEMU_DISPLAY_ARGS[@]}"
        "${QEMU_EXTRA_ARGS[@]}"
    )
fi

TIMEOUT_ARGS=()
if timeout --help 2>/dev/null | grep -q -- '--foreground'; then
    TIMEOUT_ARGS+=(--foreground)
fi
TIMEOUT_ARGS+=("$QEMU_TIMEOUT")

info "Starting QEMU in ${QEMU_DISPLAY_MODE} mode using ${QEMU_BOOT_MODE} boot. The kernel intentionally halts forever; timeout is treated as success."
set +e
timeout "${TIMEOUT_ARGS[@]}" qemu-system-x86_64 "${QEMU_ARGS[@]}"
QEMU_STATUS=$?
set -e

if [ "$QEMU_STATUS" -eq 124 ]; then
    info "QEMU timeout reached after ${QEMU_TIMEOUT}s; x86_64 freestanding kernel remained running as expected."
    exit 0
fi

if [ "$QEMU_STATUS" -ne 0 ]; then
    fail "QEMU exited with status $QEMU_STATUS"
fi

info "QEMU exited cleanly."
