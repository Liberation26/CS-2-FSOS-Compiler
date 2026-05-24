#!/usr/bin/env bash
set -euo pipefail

RUNQEMU_VERSION="0.2.14"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPILER_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj"
COMPILER_CONFIGURATION="${ORYN_COMPILER_CONFIGURATION:-Debug}"
COMPILER_FRAMEWORK="${ORYN_COMPILER_FRAMEWORK:-net8.0}"
COMPILER_DLL="$PROJECT_ROOT/Source/Core/Oryn.Compiler/bin/${COMPILER_CONFIGURATION}/${COMPILER_FRAMEWORK}/Oryn.Compiler.dll"
REQUESTED_STAGE="${1:-${ORYN_STAGE:-Stage2}}"

info() { printf '[ OK ] [ RUNQEMU  ] %s\n' "$1"; }
warn() { printf '[WARN] [ RUNQEMU  ] %s\n' "$1"; }
fail() { printf '[FAIL] [ RUNQEMU  ] %s\n' "$1"; exit 1; }

RequireTool() {
    command -v "$1" >/dev/null 2>&1 || fail "Required tool not found: $1"
}

RunDotnetBuild() {
    local BuildLog="$PROJECT_ROOT/Build/Oryn.Compiler.build.log"
    local BuildTimeout="${ORYN_COMPILER_BUILD_TIMEOUT:-240}"
    mkdir -p "$(dirname "$BuildLog")"

    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    export DOTNET_NOLOGO=1
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

    info "Building Oryn.Compiler: $COMPILER_PROJECT"
    info "Compiler build log: $BuildLog"
    info "Compiler build timeout: ${BuildTimeout}s"

    set +e
    if command -v timeout >/dev/null 2>&1; then
        timeout "$BuildTimeout" dotnet build "$COMPILER_PROJECT" -c "$COMPILER_CONFIGURATION" --nologo --disable-build-servers -v:minimal 2>&1 | tee "$BuildLog"
        local BuildStatus=${PIPESTATUS[0]}
    else
        dotnet build "$COMPILER_PROJECT" -c "$COMPILER_CONFIGURATION" --nologo --disable-build-servers -v:minimal 2>&1 | tee "$BuildLog"
        local BuildStatus=${PIPESTATUS[0]}
    fi
    set -e

    if [ "$BuildStatus" -eq 124 ]; then
        fail "Oryn.Compiler build timed out after ${BuildTimeout}s. See: $BuildLog"
    fi

    if [ "$BuildStatus" -ne 0 ]; then
        fail "Oryn.Compiler build failed with status $BuildStatus. See: $BuildLog"
    fi

    info "Oryn.Compiler build completed."
}

BuildCompilerOnce() {
    RequireTool dotnet
    RequireTool tee
    [ -f "$COMPILER_PROJECT" ] || fail "Compiler project not found: $COMPILER_PROJECT"
    RunDotnetBuild
    [ -f "$COMPILER_DLL" ] || fail "Compiler DLL was not produced: $COMPILER_DLL"
    export ORYN_COMPILER_PREBUILT=1
    export ORYN_COMPILER_DLL="$COMPILER_DLL"
}

RunOneStage() {
    local SELECTED_STAGE="$1"
    ORYN_STAGE="$SELECTED_STAGE" "$0" "$SELECTED_STAGE"
}

case "$REQUESTED_STAGE" in
    all|All|ALL)
        info "Runqemu.sh version ${RUNQEMU_VERSION}"
        info "Selected stage set: All"
        info "Stage 2 development mode is active; running only the second kernel."
        BuildCompilerOnce
        RunOneStage Stage2
        info "Requested Stage 2 kernel completed."
        exit 0
        ;;
    1|stage1|Stage1|STAGE1)
        STAGE_NAME="Stage1"
        STAGE_LABEL="stage1"
        ;;
    2|stage2|Stage2|STAGE2)
        STAGE_NAME="Stage2"
        STAGE_LABEL="stage2"
        ;;
    *)
        printf '[FAIL] [ RUNQEMU  ] Unsupported stage: %s. Use All, Stage1, or Stage2.\n' "$REQUESTED_STAGE"
        exit 1
        ;;
esac
BUILD_ROOT="${ORYN_BUILD_ROOT:-$PROJECT_ROOT/OSes/$STAGE_NAME/Build/Runqemu}"
SOURCE_FILE="${ORYN_KERNEL_SOURCE:-$PROJECT_ROOT/OSes/$STAGE_NAME/Source/Kernel.cs}"
KERNEL_OBJECT_PLACEHOLDER="$BUILD_ROOT/Kernel.${STAGE_LABEL}.o"
GENERATED_ASM="$BUILD_ROOT/Kernel.${STAGE_LABEL}.generated.S"
BOOT_SOURCE="$BUILD_ROOT/Boot.S"
DIAGNOSTICS_SOURCE="$PROJECT_ROOT/Source/Native/Modules/Diagnostics/Diagnostics.Native.c"
COMPILER_DIAGNOSTICS_LOG="$BUILD_ROOT/Kernel.${STAGE_LABEL}.diagnostics.log"
LINKER_SCRIPT="$BUILD_ROOT/Linker.ld"
KERNEL_ELF="$BUILD_ROOT/OrynKernel.elf"
ISO_ROOT="$BUILD_ROOT/IsoRoot"
GRUB_CFG="$ISO_ROOT/boot/grub/grub.cfg"
KERNEL_ISO="$BUILD_ROOT/OrynKernel.iso"
SERIAL_LOG="$BUILD_ROOT/Qemu.serial.log"
QEMU_TIMEOUT="${ORYN_QEMU_TIMEOUT:-8}"
QEMU_DISPLAY_MODE="${ORYN_QEMU_DISPLAY:-headless}"
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
info "Selected stage: ${STAGE_NAME}"
info "Build root: ${BUILD_ROOT}"
info "Kernel source: ${SOURCE_FILE}"

RequireTool dotnet
RequireTool tee
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

info "Running Oryn.Compiler backend for ${STAGE_NAME}"
if [ "${ORYN_COMPILER_PREBUILT:-0}" != "1" ] || [ ! -f "${ORYN_COMPILER_DLL:-$COMPILER_DLL}" ]; then
    info "Compiler was not prebuilt for this stage; building it now."
    RunDotnetBuild
    ORYN_COMPILER_DLL="$COMPILER_DLL"
fi
[ -f "${ORYN_COMPILER_DLL:-$COMPILER_DLL}" ] || fail "Compiler DLL not found: ${ORYN_COMPILER_DLL:-$COMPILER_DLL}"
dotnet "${ORYN_COMPILER_DLL:-$COMPILER_DLL}" compile "$SOURCE_FILE" --target x64-elf --output "$KERNEL_OBJECT_PLACEHOLDER"
info "Oryn.Compiler backend completed for ${STAGE_NAME}"

[ -f "$GENERATED_ASM" ] || fail "Compiler did not produce expected backend assembly: $GENERATED_ASM"
[ -f "$COMPILER_DIAGNOSTICS_LOG" ] || fail "Compiler did not produce expected diagnostics log: $COMPILER_DIAGNOSTICS_LOG"
info "Compiler diagnostics log: $COMPILER_DIAGNOSTICS_LOG"
info "Compiler CFG proof lines from diagnostics log:"
grep '\[ CFG      \]' "$COMPILER_DIAGNOSTICS_LOG" || fail "Compiler diagnostics log did not contain CFG proof lines: $COMPILER_DIAGNOSTICS_LOG"

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
    call BootSerialInitialize32
    mov $Boot32SerialMessage, %esi
    call BootSerialWriteString32

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

BootSerialInitialize32:
    mov $0x3F9, %dx
    xor %al, %al
    outb %al, %dx
    mov $0x3FB, %dx
    mov $0x80, %al
    outb %al, %dx
    mov $0x3F8, %dx
    mov $0x03, %al
    outb %al, %dx
    mov $0x3F9, %dx
    xor %al, %al
    outb %al, %dx
    mov $0x3FB, %dx
    mov $0x03, %al
    outb %al, %dx
    mov $0x3FA, %dx
    mov $0xC7, %al
    outb %al, %dx
    mov $0x3FC, %dx
    mov $0x0B, %al
    outb %al, %dx
    ret

BootSerialWait32:
    mov $0x3FD, %dx
4:
    inb %dx, %al
    test $0x20, %al
    jz 4b
    ret

BootSerialWriteChar32:
    push %eax
    call BootSerialWait32
    pop %eax
    mov $0x3F8, %dx
    outb %al, %dx
    ret

BootSerialWriteString32:
    lodsb
    test %al, %al
    jz 5f
    cmp $10, %al
    jne 6f
    mov $13, %al
    call BootSerialWriteChar32
    mov $10, %al
6:
    call BootSerialWriteChar32
    jmp BootSerialWriteString32
5:
    ret

.code64
LongModeEntry:
    mov $0x10, %ax
    mov %ax, %ds
    mov %ax, %es
    mov %ax, %ss
    mov $BootStackTop64, %rsp
    xor %rbp, %rbp
    call BootSerialInitialize
    lea BootSerialMessage(%rip), %rsi
    call BootSerialWriteString
    call Kernel_Main
BootHalt:
    hlt
    jmp BootHalt

BootSerialInitialize:
    mov $0x3F9, %dx
    xor %al, %al
    outb %al, %dx
    mov $0x3FB, %dx
    mov $0x80, %al
    outb %al, %dx
    mov $0x3F8, %dx
    mov $0x03, %al
    outb %al, %dx
    mov $0x3F9, %dx
    xor %al, %al
    outb %al, %dx
    mov $0x3FB, %dx
    mov $0x03, %al
    outb %al, %dx
    mov $0x3FA, %dx
    mov $0xC7, %al
    outb %al, %dx
    mov $0x3FC, %dx
    mov $0x0B, %al
    outb %al, %dx
    ret

BootSerialWait:
    mov $0x3FD, %dx
1:
    inb %dx, %al
    test $0x20, %al
    jz 1b
    ret

BootSerialWriteChar:
    push %rax
    call BootSerialWait
    pop %rax
    mov $0x3F8, %dx
    outb %al, %dx
    ret

BootSerialWriteString:
    lodsb
    test %al, %al
    jz 2f
    cmp $10, %al
    jne 3f
    mov $13, %al
    call BootSerialWriteChar
    mov $10, %al
3:
    call BootSerialWriteChar
    jmp BootSerialWriteString
2:
    ret

.section .rodata
Boot32SerialMessage:
    .asciz "[ OK ] [ BOOT32   ] Multiboot entry reached; preparing long mode\n"
BootSerialMessage:
    .asciz "[ OK ] [ BOOT     ] Long mode entered; calling Kernel_Main\n"
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
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$GENERATED_ASM" -o "$BUILD_ROOT/Kernel.${STAGE_LABEL}.o.real"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -DDEBUG=1 -c "$DIAGNOSTICS_SOURCE" -o "$BUILD_ROOT/Diagnostics.Runtime.o"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$PROJECT_ROOT/Source/Native/Modules/Cpu/Cpu.Native.c" -o "$BUILD_ROOT/Cpu.Native.o"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$PROJECT_ROOT/Source/Native/Modules/Memory/Memory.Native.c" -o "$BUILD_ROOT/Memory.Native.o"

info "Linking x86_64 freestanding ELF kernel: $KERNEL_ELF"
ld -nostdlib -T "$LINKER_SCRIPT" -o "$KERNEL_ELF" \
    "$BUILD_ROOT/Boot.o" \
    "$BUILD_ROOT/Kernel.${STAGE_LABEL}.o.real" \
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
    cat > "$GRUB_CFG" <<EOF_GRUB
set timeout=0
set default=0

menuentry "Oryn ${STAGE_NAME} x86_64 Kernel" {
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
        -serial "file:$SERIAL_LOG"
        -monitor none
        -no-reboot
        -no-shutdown
        "${QEMU_DISPLAY_ARGS[@]}"
        "${QEMU_EXTRA_ARGS[@]}"
    )
else
    QEMU_ARGS=(
        -kernel "$KERNEL_ELF"
        -serial "file:$SERIAL_LOG"
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
rm -f "$SERIAL_LOG"

info "Starting QEMU in ${QEMU_DISPLAY_MODE} mode using ${QEMU_BOOT_MODE} boot. The kernel intentionally halts forever; timeout is treated as success."
set +e
timeout "${TIMEOUT_ARGS[@]}" qemu-system-x86_64 "${QEMU_ARGS[@]}"
QEMU_STATUS=$?
set -e

if [ -s "$SERIAL_LOG" ]; then
    info "QEMU serial output follows from: $SERIAL_LOG"
    sed 's/^/[SERIAL] /' "$SERIAL_LOG"
else
    fail "QEMU serial log was empty: $SERIAL_LOG"
fi

if ! grep -q '\[ OK \] \[ BOOT32   \]' "$SERIAL_LOG"; then
    fail "Expected BOOT32 serial proof was not found in: $SERIAL_LOG"
fi

if [ "$QEMU_STATUS" -eq 124 ]; then
    info "QEMU timeout reached after ${QEMU_TIMEOUT}s; x86_64 freestanding kernel remained running as expected."
    exit 0
fi

if [ "$QEMU_STATUS" -ne 0 ]; then
    fail "QEMU exited with status $QEMU_STATUS"
fi

info "QEMU exited cleanly."
