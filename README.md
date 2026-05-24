# Oryn

Oryn is a C# to freestanding operating-system compiler and OS generation platform.

Oryn lets a developer configure an operating-system project, generate a freestanding kernel, compile the approved Oryn-safe C# subset into native output, link a bootable kernel, and run it in a virtual machine.

Current version: **2.0.1**

## What 2.0.1 means

Oryn 2.0.1 is the first **visual-first OS project configuration** milestone.

The major change is that OS setup is no longer treated as a one-time terminal questionnaire. Oryn now has a project configuration application:

```text
Applications/OrynVisualConfigurator/
```

The configurator reads the current version's JSON question files from:

```text
Questions/*.question.json
```

It shows all questions for the installed Oryn version, presents all known choices as selectable options, and saves the answers into the generated OS project.

## Main commands

Create a new OS, configure it visually, generate it, build it, and run it:

```bash
./Oryn.sh new
```

Open the visual configurator for an existing OS:

```bash
./Oryn.sh configure DES
```

Open the visual configurator from inside an OS project directory:

```bash
./Oryn.sh configure
```

Search for existing generated OS projects:

```bash
./Oryn.sh configure --search
```

Load a project directory manually:

```bash
./Oryn.sh configure --load
```

Build an existing OS without asking questions again:

```bash
./Oryn.sh build DES
```

Run an existing OS without asking questions again:

```bash
./Oryn.sh run DES
```

Show SDK paths:

```bash
./Oryn.sh sdk
```

Show available module policy:

```bash
./Oryn.sh modules
```

## Visual configuration rules

The visual configurator is the normal end-user configuration route.

It is automatically launched when:

```text
- a new OS is created
- an existing OS has not completed visual configuration
- a newer Oryn version introduces a required question that the OS has not answered
- saved answers are missing or invalid
```

It is not automatically launched every time. After an OS has valid saved answers, build and run reuse those answers.

The user can manually reopen it with:

```bash
./Oryn.sh configure <OS name or path>
```

## Questions and choices

Questions are not hardcoded in the configurator. They live as JSON files under `Questions/`.

Choice questions are rendered as selectable known choices. Examples:

```text
Target: x64-elf
VM profile: RunQemu
VM display mode: Headless or Visual
Additional modules: None or Memory
Build mode: Debug
```

Free typing is limited to values that genuinely need to be user-defined:

```text
OS Title
OS Name
Kernel Name
```

`OS Title` is human-facing and may contain spaces.

`OS Name` and `Kernel Name` are technical identifiers. They must:

```text
- start with a letter
- contain only letters and numbers
- contain no spaces
- contain no punctuation
- contain no slashes or dots
```

Example:

```text
OS Title: Dave's Space OS
OS Name: DavesSpaceOS
Kernel Name: DavesSpaceOSKernel
```

## Project discovery

`OrynVisualConfigurator` can open projects in several ways:

```text
- from the current working directory
- by OS name under OSes/
- by explicit relative or absolute path
- by searching known generated OS projects
- by loading a directory manually
```

A valid generated Oryn OS project is a directory containing:

```text
manifest.json
Answers/
```

## Generated OS layout

A generated OS lives under:

```text
OSes/<OsName>/
```

Typical layout:

```text
OSes/<OsName>/
  Answers/
    <OsName>.answers.json
  Source/
    Kernel.cs
  Templates/
    Kernel.template.cs
  Build/
  manifest.json
  README.md
```

The answer file and manifest are the saved project profile. They are reused by build, run, and regeneration.

## Mandatory and user-selected modules

Modules required to get the kernel running are not user-selected modules.

Mandatory modules are linked automatically:

```text
Runtime
Diagnostics
Panic
Cpu
ManifestLoader
```

`Diagnostics` and `Panic` are always enabled.

Additional user-selected modules currently include:

```text
None
Memory
```

Choosing `None` creates the minimal generated OS using only mandatory kernel modules.

## VM display mode

The configurator asks whether the generated OS should run in `Headless` or `Visual` mode.

```text
Headless: automated proof run. QEMU closes after the proof timeout.
Visual: opens a QEMU window and keeps it open until the user closes it.
```

In visual mode, Oryn tails serial output live while QEMU is running.

## SDK layout

Oryn now distinguishes between the host-side SDK and the freestanding Oryn SDK surface.

Host-side .NET SDK:

```text
Source/Core/Oryn.Sdk/
```

Freestanding `.Oryn` SDK declarations:

```text
Source/Sdk/Oryn/
```

Generated kernel code uses approved `.Oryn` namespaces such as:

```csharp
using Oryn.Diagnostics;
using Oryn.Cpu;
using Oryn.Runtime;
```

Those declarations are not a general .NET runtime. They are the approved freestanding API surface that Oryn validates and lowers to native module bindings.

## Current compiler flow

The current flow is:

```text
Oryn Visual Configurator
    ↓
JSON answer file and manifest
    ↓
Generated OS folder
    ↓
Kernel template composition
    ↓
Oryn-safe C# validation
    ↓
Oryn IR
    ↓
x64 backend / direct ELF64 object writer
    ↓
freestanding native modules
    ↓
linked bootable kernel
    ↓
GRUB ISO
    ↓
QEMU
```

## Proof output

A successful generated OS boot prints deterministic proof lines similar to:

```text
[SERIAL] [ OK ] [ BOOT     ] Long mode entered; calling Kernel_Main
[SERIAL] [ OK ] [ KERNEL   ] DES generated kernel entered
[SERIAL] [ OK ] [ KERNEL   ] Hello from DES
[SERIAL] [ OK ] [ KERNEL   ] DES mandatory kernel module count: 5
[SERIAL] [ OK ] [ KERNEL   ] DES user-selected module count: 0
[SERIAL] [ OK ] [ KERNEL   ] DES generated kernel is halting forever
```

## Terminal generator

The visual configurator is the normal path. The old terminal generator remains available for automation and tests:

```bash
./Oryn.sh generate --terminal --os-title "Demo OS" --os-name DemoOS --kernel-name DemoOSKernel --modules None --vm-display-mode Headless
```


## 2.0.1 visual configurator fix

OrynVisualConfigurator now opens a local browser-based visual configuration UI rather than presenting the normal question flow only in the terminal. It still reads the current version's `Questions/*.question.json` files, so the question set remains version-driven. Known-choice questions are rendered as dropdowns or check boxes, while `OS Title`, `OS Name`, and `Kernel Name` remain the only typed fields.

The terminal fallback is still available for automation:

```bash
ORYN_VISUALCFG_TERMINAL=1 ./Oryn.sh new
```

## Version 2.0.1 summary

Oryn 2.0.1 adds:

```text
- Applications/OrynVisualConfigurator
- visual-first OS project configuration
- project search and load support
- current-directory project detection
- OS Title separate from strict OS Name
- strict no-space OS and kernel identifiers
- saved answers as reusable project profiles
- automatic configurator launch only when needed
- README rewritten for the 2.0.1 product flow
```
