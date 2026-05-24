# Oryn Manual

Oryn 1.0.3 introduces the first end-user OS generation workflow.

## What Oryn 1.0.3 does

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

For 1.0.3, Memory is the only user-selectable module.

## Generate

```bash
./Oryn.sh generate --os-name MyOrynOS --kernel-name MyOrynKernel --modules Memory
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
