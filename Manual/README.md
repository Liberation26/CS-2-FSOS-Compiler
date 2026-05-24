# Oryn Manual

Oryn 1.0.8 introduces the first end-user OS generation workflow.

## What Oryn 1.0.8 does

Oryn can now create a named OS folder from JSON-backed answers, generate a kernel template and source tree, compose the final kernel from approved modules, build a bootable freestanding x64 kernel, and run it through QEMU.

## User-selected modules versus mandatory modules

User-selected modules are optional modules chosen by the generated OS owner.

They do not include modules needed to get the kernel running.

Mandatory modules are linked automatically:

- Runtime
- Diagnostics
- Panic
- Cpu
- ManifestLoader

Diagnostics and Panic are always enabled and are never optional user choices.

For 1.0.8, choose `None` for no optional modules or `Memory` to include the optional Memory module.

## Generate

```bash
./Oryn.sh generate --os-name MyOrynOS --kernel-name MyOrynKernel --modules None
```

## Build

```bash
./Oryn.sh build MyOrynOS
```

## Run

```bash
./Oryn.sh run MyOrynOS
```

## Files created for a generated OS

```text
OSes/<OsName>/
  Answers/<OsName>.answers.json
  Source/Kernel.cs
  Templates/Kernel.template.cs
  Build/
  README.md
  manifest.json
```

The manifest separates `MandatoryKernelModules` from `UserSelectedModules` so the user's selections are not polluted by kernel boot requirements.

## 1.0.8 generated OS proof

Generated kernels now print `Hello from <OsName>` during boot. The generation questions also ask whether QEMU should run in `Headless` mode or `Visual` mode, and the generated manifest records that choice as `VmDisplayMode`.

## VM display modes

`Headless` mode is for automated proof runs. It closes automatically after the QEMU proof timeout when the generated kernel remains running/halted as expected.

`Visual` mode is for end users who want to see the VM window. Oryn does not close QEMU in visual mode; the VM stays open until the user closes the window.

## Question expectations

Every generator question shows the expected answer and the default. Press Enter to accept the default.

Generator prompts show the accepted options on a separate `OPTIONS` line. In interactive terminals, those options are displayed in a different colour so the expected answers are easier to see.
