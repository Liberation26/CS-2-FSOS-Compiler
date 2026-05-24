#!/usr/bin/env bash
set -euo pipefail

RUNQEMU_VERSION="0.8.0"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPILER_PROJECT="$PROJECT_ROOT/Source/Core/Oryn.Compiler/Oryn.Compiler.csproj"
COMPILER_CONFIGURATION="${ORYN_COMPILER_CONFIGURATION:-Debug}"
COMPILER_FRAMEWORK="${ORYN_COMPILER_FRAMEWORK:-net8.0}"
COMPILER_DLL="$PROJECT_ROOT/Source/Core/Oryn.Compiler/bin/${COMPILER_CONFIGURATION}/${COMPILER_FRAMEWORK}/Oryn.Compiler.dll"
REQUESTED_STAGE="${1:-${ORYN_STAGE:-Stage8}}"

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

EnsureCompilerAvailable() {
    RequireTool dotnet
    RequireTool tee

    local SelectedCompilerDll="${ORYN_COMPILER_DLL:-$COMPILER_DLL}"
    local BuildRequested="${ORYN_BUILD_COMPILER:-0}"

    case "$BuildRequested" in
        1|true|TRUE|yes|YES|on|ON)
            [ -f "$COMPILER_PROJECT" ] || fail "Compiler project not found: $COMPILER_PROJECT"
            RunDotnetBuild
            SelectedCompilerDll="$COMPILER_DLL"
            ;;
        0|false|FALSE|no|NO|off|OFF)
            ;;
        *)
            fail "Unsupported ORYN_BUILD_COMPILER value: $BuildRequested. Use 1 to build or 0 to use the existing compiler."
            ;;
    esac

    [ -f "$SelectedCompilerDll" ] || fail "Compiler DLL not found: $SelectedCompilerDll. Build it explicitly with: ORYN_BUILD_COMPILER=1 ./Runqemu.sh $STAGE_NAME"
    export ORYN_COMPILER_DLL="$SelectedCompilerDll"
}

RunOneStage() {
    local SELECTED_STAGE="$1"
    ORYN_STAGE="$SELECTED_STAGE" "$0" "$SELECTED_STAGE"
}

case "$REQUESTED_STAGE" in
    all|All|ALL)
        info "Runqemu.sh version ${RUNQEMU_VERSION}"
        info "Selected stage set: All"
        info "Stage 8 development mode is active; running the module API contract proof kernel."
        RunOneStage Stage8
        info "Requested Stage 8 kernel completed."
        exit 0
        ;;
    1|stage1|Stage1|STAGE1)
        printf '[FAIL] [ RUNQEMU  ] Stage 8 development mode is active; Stage1 is not run by this script. Use Stage2, Stage3, Stage4, Stage5, Stage6, Stage7, or Stage8.\n'
        exit 1
        ;;
    2|stage2|Stage2|STAGE2)
        STAGE_NAME="Stage2"
        STAGE_LABEL="stage2"
        DIRECT_OBJECT_LINK=0
        ;;
    3|stage3|Stage3|STAGE3)
        STAGE_NAME="Stage3"
        STAGE_LABEL="stage3"
        DIRECT_OBJECT_LINK=1
        ;;
    4|stage4|Stage4|STAGE4)
        STAGE_NAME="Stage4"
        STAGE_LABEL="stage4"
        DIRECT_OBJECT_LINK=1
        ;;
    5|stage5|Stage5|STAGE5)
        STAGE_NAME="Stage5"
        STAGE_LABEL="stage5"
        DIRECT_OBJECT_LINK=1
        ;;
    6|stage6|Stage6|STAGE6)
        STAGE_NAME="Stage6"
        STAGE_LABEL="stage6"
        DIRECT_OBJECT_LINK=1
        ;;
    7|stage7|Stage7|STAGE7)
        STAGE_NAME="Stage7"
        STAGE_LABEL="stage7"
        DIRECT_OBJECT_LINK=1
        ;;
    8|stage8|Stage8|STAGE8)
        STAGE_NAME="Stage8"
        STAGE_LABEL="stage8"
        DIRECT_OBJECT_LINK=1
        ;;
    *)
        printf '[FAIL] [ RUNQEMU  ] Unsupported stage: %s. Use All, Stage2, Stage3, Stage4, Stage5, Stage6, Stage7, or Stage8.\n' "$REQUESTED_STAGE"
        exit 1
        ;;
esac
BUILD_ROOT="${ORYN_BUILD_ROOT:-$PROJECT_ROOT/OSes/$STAGE_NAME/Build/Runqemu}"
SOURCE_FILE="${ORYN_KERNEL_SOURCE:-$PROJECT_ROOT/OSes/$STAGE_NAME/Source/Kernel.cs}"
KERNEL_OBJECT="$BUILD_ROOT/Kernel.${STAGE_LABEL}.o"
GENERATED_ASM="$BUILD_ROOT/Kernel.${STAGE_LABEL}.generated.S"
BOOT_SOURCE="$BUILD_ROOT/Boot.S"
DIAGNOSTICS_SOURCE="$PROJECT_ROOT/Source/Native/Modules/Diagnostics/Diagnostics.Native.c"
RUNTIME_SOURCE="$PROJECT_ROOT/Source/Native/Modules/Runtime/Runtime.Native.c"
PANIC_SOURCE="$PROJECT_ROOT/Source/Native/Modules/Panic/Panic.Native.c"
COMPILER_DIAGNOSTICS_LOG="$BUILD_ROOT/Kernel.${STAGE_LABEL}.diagnostics.log"
LINKER_SCRIPT="$BUILD_ROOT/Linker.ld"
KERNEL_ELF="$BUILD_ROOT/OrynKernel.elf"
ISO_ROOT="$BUILD_ROOT/IsoRoot"
GRUB_CFG="$ISO_ROOT/boot/grub/grub.cfg"
KERNEL_ISO="$BUILD_ROOT/OrynKernel.iso"
SERIAL_LOG="$BUILD_ROOT/Qemu.serial.log"
DEBUGCON_LOG="$BUILD_ROOT/Qemu.debugcon.log"
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

[ -f "$SOURCE_FILE" ] || fail "Kernel source not found: $SOURCE_FILE"

rm -rf "$BUILD_ROOT"
mkdir -p "$BUILD_ROOT"

info "Running Oryn.Compiler backend for ${STAGE_NAME}"
EnsureCompilerAvailable
dotnet "$ORYN_COMPILER_DLL" compile "$SOURCE_FILE" --target x64-elf --output "$KERNEL_OBJECT"
info "Oryn.Compiler backend completed for ${STAGE_NAME}"

[ -f "$KERNEL_OBJECT" ] || fail "Compiler did not produce expected direct ELF64 object: $KERNEL_OBJECT"
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

BootDebugConWriteChar32:
    push %edx
    mov $0x00E9, %dx
    outb %al, %dx
    pop %edx
    ret

BootSerialWriteChar32:
    push %eax
    call BootDebugConWriteChar32
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
    lea BootPreKernelMessage(%rip), %rsi
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

BootDebugConWriteChar:
    push %rdx
    mov $0x00E9, %dx
    outb %al, %dx
    pop %rdx
    ret

BootSerialWriteChar:
    push %rax
    call BootDebugConWriteChar
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
BootPreKernelMessage:
    .asciz "[ OK ] [ KERNEL   ] __ORYN_STAGE_NAME__ native pre-kernel handoff reached\n"
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
sed -i "s/__ORYN_STAGE_NAME__/${STAGE_NAME}/g" "$BOOT_SOURCE"

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

GenerateManifestGlue() {
    local StageLimit="$1"
    local GlueSource="$BUILD_ROOT/ModuleManifest.Generated.c"
    local ManifestDir="$PROJECT_ROOT/Source/Sdk/ModuleManifests"
    [ -d "$ManifestDir" ] || fail "Module manifest directory not found: $ManifestDir"

    python3 - "$PROJECT_ROOT" "$ManifestDir" "$GlueSource" "$StageLimit" "$STAGE_NAME" <<'PY_MANIFEST'
import json
import pathlib
import sys

project_root = pathlib.Path(sys.argv[1])
manifest_dir = pathlib.Path(sys.argv[2])
glue = pathlib.Path(sys.argv[3])
stage_limit = int(sys.argv[4])
stage_name = sys.argv[5]

class ManifestError(Exception):
    pass

def read_records():
    loaded = []
    for path in sorted(manifest_dir.glob('*.module.json')):
        with path.open('r', encoding='utf-8') as handle:
            item = json.load(handle)
        if not item.get('allowedInKernel', False) or not item.get('linkByDefault', False):
            continue
        if int(item.get('stage', 0)) > stage_limit:
            continue
        if stage_name in ('Stage7', 'Stage8') and item.get('module') == 'ManifestLoader':
            continue
        item['_path'] = str(path)
        item['dependsOn'] = list(item.get('dependsOn') or [])
        loaded.append(item)
    if not loaded:
        raise ManifestError('no selected module manifests were loaded')
    return loaded

def resolve(records):
    by_name = {}
    for item in records:
        name = item.get('module', '')
        if not name:
            raise ManifestError('module manifest missing module name: ' + item.get('_path', '<unknown>'))
        if name in by_name:
            raise ManifestError('duplicate module manifest: ' + name)
        by_name[name] = item

    for item in records:
        name = item['module']
        for dep in item.get('dependsOn', []):
            if dep not in by_name:
                raise ManifestError(f'module {name} requires missing dependency {dep}')

    result = []
    state = {}
    stack = []

    def visit(name):
        current = state.get(name, 0)
        if current == 2:
            return
        if current == 1:
            cycle = stack[stack.index(name):] + [name] if name in stack else stack + [name]
            raise ManifestError('circular module dependency detected: ' + ' -> '.join(cycle))
        state[name] = 1
        stack.append(name)
        item = by_name[name]
        for dep in sorted(item.get('dependsOn', []), key=lambda d: (int(by_name[d].get('initializeOrder', 0)), d)):
            visit(dep)
        stack.pop()
        state[name] = 2
        result.append(item)

    for item in sorted(records, key=lambda i: (int(i.get('initializeOrder', 0)), i.get('module', ''))):
        visit(item['module'])
    return result

try:
    records = resolve(read_records())
except ManifestError as exc:
    print('[FAIL] [ MANIFEST ] ' + str(exc))
    sys.exit(2)

stage_number = '8' if stage_name == 'Stage8' else ('7' if stage_name == 'Stage7' else '6')
lines = []
lines.append('#include "Diagnostics.Native.h"')
lines.append('#include "Runtime.Native.h"')
lines.append('#include "Memory.Native.h"')
lines.append('')
for item in records:
    name = item.get('module', 'Unknown')
    symbol = item.get('initializerNativeSymbol') or ''
    if symbol and name != 'ManifestLoader':
        lines.append(f'extern void {symbol}(void);')
lines.append('')
lines.append('static int ModuleManifestAlreadyInitialized = 0;')
lines.append('')
lines.append('void ModuleManifest_InitializeSelected(void)')
lines.append('{')
lines.append('    if (ModuleManifestAlreadyInitialized != 0)')
lines.append('    {')
lines.append('        Diagnostics_WriteOk("[ MANIFEST ] selected manifest modules already initialized");')
lines.append('        return;')
lines.append('    }')
lines.append('')
lines.append('    ModuleManifestAlreadyInitialized = 1;')
if stage_name in ('Stage7', 'Stage8'):
    if stage_name == 'Stage8':
        lines.append('    Diagnostics_WriteOk("[ CONTRACT ] Stage8 module API contract runtime proof started");')
        lines.append('    Diagnostics_WriteOk("[ CONTRACT ] approved C# call Diagnostics.WriteOk -> Diagnostics_WriteOk");')
        lines.append('    Diagnostics_WriteOk("[ CONTRACT ] approved C# call Runtime.MarkKernelReady -> Runtime_MarkKernelReady");')
        lines.append('    Diagnostics_WriteOk("[ CONTRACT ] approved C# call Cpu.HaltForever -> Cpu_HaltForever");')
    lines.append(f'    Diagnostics_WriteOk("[ MANIFEST ] {stage_name} dependency graph loading started");')
    for item in records:
        deps = item.get('dependsOn', [])
        dep_text = ', '.join(deps) if deps else '<none>'
        lines.append(f'    Diagnostics_WriteOk("[ MANIFEST ] dependency {item.get("module", "Unknown")} -> {dep_text}");')
    order = ', '.join(item.get('module', 'Unknown') for item in records)
    lines.append(f'    Diagnostics_WriteOk("[ MANIFEST ] resolved initialization order: {order}");')
else:
    lines.append('    Diagnostics_WriteOk("[ MANIFEST ] generated Stage 6 manifest runtime started");')

for item in records:
    name = item.get('module', 'Unknown')
    source = item.get('nativeSource', '')
    symbol = item.get('initializerNativeSymbol') or ''
    if stage_name not in ('Stage7', 'Stage8'):
        lines.append(f'    Diagnostics_WriteOk("[ MANIFEST ] selected {name}: {source}");')
    if symbol and name != 'ManifestLoader':
        lines.append(f'    Diagnostics_WriteOk("[ MANIFEST ] initializing {name}");')
        lines.append(f'    {symbol}();')
    elif name == 'ManifestLoader' and stage_name != 'Stage7':
        lines.append('    Diagnostics_WriteOk("[ MANIFEST ] ManifestLoader glue is active");')
if stage_name in ('Stage7', 'Stage8'):
    lines.append(f'    Diagnostics_WriteOk("[ MANIFEST ] {stage_name} dependency graph runtime completed");')
    if stage_name == 'Stage8':
        lines.append('    Diagnostics_WriteOk("[ CONTRACT ] Stage8 module API contract runtime proof completed");')
else:
    lines.append('    Diagnostics_WriteOk("[ MANIFEST ] generated Stage 6 manifest runtime completed");')
lines.append('}')
lines.append('')
glue.write_text('\n'.join(lines), encoding='utf-8')
print(f'[ OK ] [ MANIFEST ] Generated {stage_name} manifest glue: {glue}')
for item in records:
    deps = item.get('dependsOn', [])
    print('[ OK ] [ MANIFEST ] resolved module={module} dependsOn={deps} source={nativeSource} order={initializeOrder}'.format(
        module=item.get('module', 'Unknown'),
        deps=(','.join(deps) if deps else '<none>'),
        nativeSource=item.get('nativeSource', ''),
        initializeOrder=item.get('initializeOrder', 0)))
print('[ OK ] [ MANIFEST ] resolved initialization order: ' + ', '.join(item.get('module', 'Unknown') for item in records))
PY_MANIFEST
}

CompileManifestModules() {
    local StageLimit="$1"
    GenerateManifestGlue "$StageLimit"
    local ManifestDir="$PROJECT_ROOT/Source/Sdk/ModuleManifests"
    python3 - "$PROJECT_ROOT" "$ManifestDir" "$BUILD_ROOT/${STAGE_NAME}.manifest.sources" "$StageLimit" <<'PY_SOURCES'
import json
import pathlib
import sys
root = pathlib.Path(sys.argv[1])
manifest_dir = pathlib.Path(sys.argv[2])
out = pathlib.Path(sys.argv[3])
stage_limit = int(sys.argv[4])
records = []
for path in sorted(manifest_dir.glob('*.module.json')):
    item = json.loads(path.read_text(encoding='utf-8'))
    if item.get('allowedInKernel') and item.get('linkByDefault') and int(item.get('stage', 0)) <= stage_limit:
        item['dependsOn'] = list(item.get('dependsOn') or [])
        records.append(item)
by_name = {item['module']: item for item in records}
state = {}
resolved = []
def visit(name):
    if state.get(name) == 2:
        return
    if state.get(name) == 1:
        raise SystemExit('[FAIL] circular module dependency detected while writing sources')
    state[name] = 1
    item = by_name[name]
    for dep in sorted(item.get('dependsOn', []), key=lambda d: (int(by_name[d].get('initializeOrder', 0)), d)):
        if dep not in by_name:
            raise SystemExit('[FAIL] missing dependency while writing sources: ' + dep)
        visit(dep)
    state[name] = 2
    resolved.append(item)
for item in sorted(records, key=lambda item: (int(item.get('initializeOrder', 0)), item.get('module', ''))):
    visit(item['module'])
with out.open('w', encoding='utf-8') as handle:
    if stage_limit >= 8:
        handle.write(str(root / 'OSes' / 'Stage8' / 'Build' / 'Runqemu' / 'ModuleManifest.Generated.c') + '\n')
    elif stage_limit >= 7:
        handle.write(str(root / 'OSes' / 'Stage7' / 'Build' / 'Runqemu' / 'ModuleManifest.Generated.c') + '\n')
    for item in resolved:
        source = item.get('nativeSource', '')
        if source == 'Build/Generated/ModuleManifest.Generated.c':
            if stage_limit < 7:
                handle.write(str(root / 'OSes' / 'Stage6' / 'Build' / 'Runqemu' / 'ModuleManifest.Generated.c') + '\n')
        else:
            handle.write(str(root / source) + '\n')
PY_SOURCES
    STAGE_NATIVE_OBJECTS=()
    local Index=0
    while IFS= read -r NativeSource; do
        [ -f "$NativeSource" ] || fail "${STAGE_NAME} selected native module source not found: $NativeSource"
        local ObjectPath="$BUILD_ROOT/${STAGE_NAME}.Module.${Index}.o"
        info "Compiling ${STAGE_NAME} manifest-selected native module: $NativeSource"
        clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -DDEBUG=1 -I"$PROJECT_ROOT/Source/Native/Modules/Diagnostics" -I"$PROJECT_ROOT/Source/Native/Modules/Runtime" -I"$PROJECT_ROOT/Source/Native/Modules/Memory" -I"$PROJECT_ROOT/Source/Native/Modules/Panic" -I"$PROJECT_ROOT/Source/Native/Modules/Cpu" -c "$NativeSource" -o "$ObjectPath"
        STAGE_NATIVE_OBJECTS+=("$ObjectPath")
        Index=$((Index + 1))
    done < "$BUILD_ROOT/${STAGE_NAME}.manifest.sources"
}

info "Compiling freestanding x86_64 kernel objects"
clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$BOOT_SOURCE" -o "$BUILD_ROOT/Boot.o"
if [ "${DIRECT_OBJECT_LINK:-0}" = "1" ]; then
    info "Using direct Oryn ELF64 object writer output: $KERNEL_OBJECT"
else
    clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$GENERATED_ASM" -o "$BUILD_ROOT/Kernel.${STAGE_LABEL}.o.real"
fi
if [ "$STAGE_NAME" = "Stage6" ]; then
    CompileManifestModules 6
elif [ "$STAGE_NAME" = "Stage7" ]; then
    CompileManifestModules 7
elif [ "$STAGE_NAME" = "Stage8" ]; then
    CompileManifestModules 8
else
    clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -DDEBUG=1 -c "$DIAGNOSTICS_SOURCE" -o "$BUILD_ROOT/Diagnostics.Runtime.o"
    clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -DDEBUG=1 -c "$RUNTIME_SOURCE" -o "$BUILD_ROOT/Runtime.Native.o"
    clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -DDEBUG=1 -c "$PANIC_SOURCE" -o "$BUILD_ROOT/Panic.Native.o"
    clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$PROJECT_ROOT/Source/Native/Modules/Cpu/Cpu.Native.c" -o "$BUILD_ROOT/Cpu.Native.o"
    clang -m64 -ffreestanding -fno-stack-protector -fno-pic -fno-pie -mno-red-zone -c "$PROJECT_ROOT/Source/Native/Modules/Memory/Memory.Native.c" -o "$BUILD_ROOT/Memory.Native.o"
fi

info "Linking x86_64 freestanding ELF kernel: $KERNEL_ELF"
KERNEL_LINK_OBJECT="$BUILD_ROOT/Kernel.${STAGE_LABEL}.o.real"
if [ "${DIRECT_OBJECT_LINK:-0}" = "1" ]; then
    KERNEL_LINK_OBJECT="$KERNEL_OBJECT"
fi

if [ "$STAGE_NAME" = "Stage6" ] || [ "$STAGE_NAME" = "Stage7" ] || [ "$STAGE_NAME" = "Stage8" ]; then
    ld -nostdlib -T "$LINKER_SCRIPT" -o "$KERNEL_ELF" \
        "$BUILD_ROOT/Boot.o" \
        "$KERNEL_LINK_OBJECT" \
        "${STAGE_NATIVE_OBJECTS[@]}"
else
    ld -nostdlib -T "$LINKER_SCRIPT" -o "$KERNEL_ELF" \
        "$BUILD_ROOT/Boot.o" \
        "$KERNEL_LINK_OBJECT" \
        "$BUILD_ROOT/Diagnostics.Runtime.o" \
        "$BUILD_ROOT/Runtime.Native.o" \
        "$BUILD_ROOT/Panic.Native.o" \
        "$BUILD_ROOT/Cpu.Native.o" \
        "$BUILD_ROOT/Memory.Native.o"
fi

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
serial --unit=0 --speed=115200 --word=8 --parity=no --stop=1
terminal_input serial
terminal_output serial
set timeout=0
set default=0

menuentry "Oryn ${STAGE_NAME} x86_64 Kernel" {
    echo "Loading Oryn ${STAGE_NAME} x86_64 Kernel"
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
        -debugcon "file:$DEBUGCON_LOG"
        -global isa-debugcon.iobase=0xe9
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
        -debugcon "file:$DEBUGCON_LOG"
        -global isa-debugcon.iobase=0xe9
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
rm -f "$SERIAL_LOG" "$DEBUGCON_LOG"

info "Starting QEMU in ${QEMU_DISPLAY_MODE} mode using ${QEMU_BOOT_MODE} boot. The kernel intentionally halts forever; timeout is treated as success."
set +e
timeout "${TIMEOUT_ARGS[@]}" qemu-system-x86_64 "${QEMU_ARGS[@]}"
QEMU_STATUS=$?
set -e

if [ -s "$SERIAL_LOG" ]; then
    info "QEMU serial output follows from: $SERIAL_LOG"
    sed 's/^/[SERIAL] /' "$SERIAL_LOG"
    PROOF_LOG="$SERIAL_LOG"
elif [ -s "$DEBUGCON_LOG" ]; then
    warn "QEMU serial log was empty, but debugcon output was captured: $DEBUGCON_LOG"
    sed 's/^/[DEBUGCON] /' "$DEBUGCON_LOG"
    PROOF_LOG="$DEBUGCON_LOG"
else
    fail "QEMU serial and debugcon logs were empty even after GRUB serial output was enabled: $SERIAL_LOG ; $DEBUGCON_LOG"
fi

if ! grep -q '\[ OK \] \[ BOOT32   \]' "$PROOF_LOG"; then
    fail "Expected BOOT32 proof was not found in: $PROOF_LOG"
fi

if ! grep -q '\[ OK \] \[ BOOT     \]' "$PROOF_LOG"; then
    fail "Expected long-mode boot proof was not found in: $PROOF_LOG"
fi

if [ "$STAGE_NAME" = "Stage4" ]; then
    if ! grep -q "Stage4 kernel entered" "$PROOF_LOG" && ! grep -q "Stage4 approved module boundary entered" "$PROOF_LOG"; then
        fail "Expected Stage4 approved module boundary entry proof was not found in: $PROOF_LOG"
    fi

    if ! grep -q "Stage4 kernel is halting forever" "$PROOF_LOG" && ! grep -q "Stage4 approved Diagnostics.WriteOk call worked" "$PROOF_LOG"; then
        fail "Expected Stage4 approved module boundary completion proof was not found in: $PROOF_LOG"
    fi
elif [ "$STAGE_NAME" = "Stage8" ]; then
    for Required in         "Stage8 native pre-kernel handoff reached"         "Stage8 kernel entered"         "[ CONTRACT ] Stage8 module API contract runtime proof started"         "[ CONTRACT ] approved C# call Diagnostics.WriteOk -> Diagnostics_WriteOk"         "[ CONTRACT ] approved C# call Runtime.MarkKernelReady -> Runtime_MarkKernelReady"         "[ CONTRACT ] approved C# call Cpu.HaltForever -> Cpu_HaltForever"         "[ MANIFEST ] Stage8 dependency graph loading started"         "[ MANIFEST ] dependency Runtime -> <none>"         "[ MANIFEST ] dependency Diagnostics -> Runtime"         "[ MANIFEST ] dependency Memory -> Runtime, Diagnostics"         "[ MANIFEST ] dependency Panic -> Runtime, Diagnostics"         "[ MANIFEST ] dependency Cpu -> Runtime, Diagnostics"         "[ MANIFEST ] resolved initialization order: Runtime, Diagnostics, Memory, Panic, Cpu"         "[ MANIFEST ] initializing Runtime"         "[ MANIFEST ] initializing Diagnostics"         "[ MANIFEST ] initializing Memory"         "[ MANIFEST ] initializing Panic"         "[ MANIFEST ] initializing Cpu"         "[ CONTRACT ] Stage8 module API contract runtime proof completed"         "Stage8 approved module API contracts initialized"         "Stage8 kernel is halting forever"; do
        if ! grep -Fq "$Required" "$PROOF_LOG"; then
            fail "Expected Stage8 module API contract proof was not found: $Required in $PROOF_LOG"
        fi
    done
elif [ "$STAGE_NAME" = "Stage7" ]; then
    for Required in         "Stage7 native pre-kernel handoff reached"         "Stage7 kernel entered"         "Stage7 dependency graph loading started"         "[ MANIFEST ] dependency Runtime -> <none>"         "[ MANIFEST ] dependency Diagnostics -> Runtime"         "[ MANIFEST ] dependency Memory -> Runtime, Diagnostics"         "[ MANIFEST ] dependency Panic -> Runtime, Diagnostics"         "[ MANIFEST ] dependency Cpu -> Runtime, Diagnostics"         "[ MANIFEST ] resolved initialization order: Runtime, Diagnostics, Memory, Panic, Cpu"         "[ MANIFEST ] initializing Runtime"         "[ MANIFEST ] initializing Diagnostics"         "[ MANIFEST ] initializing Memory"         "[ MANIFEST ] initializing Panic"         "[ MANIFEST ] initializing Cpu"         "Stage7 dependency-resolved modules initialized"         "Stage7 kernel is halting forever"; do
        if ! grep -Fq "$Required" "$PROOF_LOG"; then
            fail "Expected Stage7 dependency proof was not found: $Required in $PROOF_LOG"
        fi
    done
elif [ "$STAGE_NAME" = "Stage6" ]; then
    for Required in         "Stage6 native pre-kernel handoff reached"         "Stage6 kernel entered"         "Stage6 module manifest loading started"         "[ MANIFEST ] ManifestLoader glue is active"         "[ MANIFEST ] initializing Runtime"         "[ MANIFEST ] initializing Memory"         "[ MANIFEST ] generated Stage 6 manifest runtime completed"         "Stage6 selected modules initialized from manifest metadata"         "Stage6 kernel is halting forever"; do
        if ! grep -Fq "$Required" "$PROOF_LOG"; then
            fail "Expected Stage6 manifest proof was not found: $Required in $PROOF_LOG"
        fi
    done
else
    if ! grep -q "${STAGE_NAME} kernel entered" "$PROOF_LOG"; then
        fail "Expected ${STAGE_NAME} kernel entry proof was not found in: $PROOF_LOG"
    fi

    if ! grep -q "${STAGE_NAME} kernel is halting forever" "$PROOF_LOG"; then
        fail "Expected ${STAGE_NAME} halt proof was not found in: $PROOF_LOG"
    fi
fi

if [ "$QEMU_STATUS" -eq 124 ]; then
    info "QEMU timeout reached after ${QEMU_TIMEOUT}s; x86_64 freestanding kernel remained running as expected."
    exit 0
fi

if [ "$QEMU_STATUS" -ne 0 ]; then
    fail "QEMU exited with status $QEMU_STATUS"
fi

info "QEMU exited cleanly."
