# Oryn

Oryn is a C# to freestanding operating-system compiler and generator project from Oryn Foundry.

Current version: **1.0.6**

## 1.0.6 milestone

Oryn 1.0.6 is the first end-user OS generation milestone.

It proves that a user can:

1. generate a named OS folder,
2. save OS generation answers as JSON,
3. create a generated kernel template and source tree,
4. keep mandatory kernel modules separate from user-selected modules,
5. compose a freestanding-safe C# kernel,
6. validate approved module calls,
7. compile to native freestanding x64 output,
8. link a bootable kernel ISO,
9. boot it in QEMU,
10. see deterministic diagnostics proving the generated OS ran.

## Important module policy

The user-facing selected module list excludes modules needed to get the kernel running.

Mandatory kernel modules are linked automatically:

- Runtime
- Diagnostics
- Panic
- Cpu
- ManifestLoader

Diagnostics and Panic are always enabled. They are not optional user-selected modules.

For 1.0.6, the optional user-selectable choices are:

- None
- Memory

Future modules must not become selectable until they have concrete passing test records.

## Generate your first OS

```bash
./Oryn.sh generate --os-name MyOrynOS --kernel-name MyOrynKernel --modules None
```

This creates:

```text
OSes/MyOrynOS/
  Answers/MyOrynOS.answers.json
  Source/Kernel.cs
  Templates/Kernel.template.cs
  Build/
  README.md
  manifest.json
```

## Build the generated OS

```bash
./Oryn.sh build MyOrynOS
```

## Run the generated OS

```bash
./Oryn.sh run MyOrynOS
```

The generated OS should print diagnostics containing the OS name, kernel name, mandatory modules, user-selected modules, and halt proof.

## Development stages

Stage 9 remains the internal compiler proof for generated kernel template composition. Oryn 1.0.6 uses that proof as the engine behind the user-facing generated OS workflow.

```bash
./Runqemu.sh Stage9
```

## Tests

Generator milestone tests live under:

```text
Tests/Generator/1.0.6/
```

Run them with:

```bash
Tests/Generator/1.0.6/run.sh
```
