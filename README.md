# Oryn

Oryn is a C# Operating System Development Platform from Oryn Foundry.

The purpose of Oryn is to let a developer write safe, user-facing C# kernel code and turn it into freestanding native output for a bootable operating-system project. The developer should not need to write unsafe kernel-facing code directly. Unsafe, native, boot, CPU, memory, and device details are meant to live behind approved Oryn modules.

Oryn is not trying to be one fixed operating system. It is a generator and compiler platform for creating operating systems from selected, tested modules.

## What the app is about

Oryn is being built so that an end user can:

1. choose the kind of operating system they want to create;
2. answer structured questions about the target OS and target machine;
3. select only modules that Oryn knows are available for that target;
4. write safe C# kernel-facing code against approved Oryn APIs;
5. have Oryn compile that code into freestanding native output;
6. build a bootable kernel image;
7. run and test that generated operating-system project in a supported virtual-machine profile.

The long-term goal is that Oryn gives C# developers a practical route from safe C# OS design to a bootable freestanding kernel without exposing them to the dangerous implementation details unless they are writing approved modules themselves.

## What Stage 3 gives the end user

Stage 3 proves that Oryn can take real safe C# kernel code and turn it into a bootable freestanding kernel proof.

For the end user, Stage 3 gives a working compiler path from:

```text
Kernel.cs
  -> Oryn IR
  -> direct ELF64 relocatable object
  -> linked freestanding kernel ELF
  -> bootable GRUB ISO
  -> QEMU proof run
```

That matters because the generated kernel is no longer only a sketch, a mock-up, or host-dependent output. Stage 3 proves that Oryn can produce native freestanding kernel output that boots.

Stage 3 supports a useful early C# subset for kernel-facing code:

- local `int` variables;
- integer addition and subtraction;
- integer equality checks;
- integer less-than checks;
- `if` and `else` branches;
- `while` loops;
- private static helper methods;
- explicit `return` statements;
- string literals passed to approved module calls;
- approved calls such as diagnostics output, memory initialization, and CPU halt.

Stage 3 also gives the end user proof artifacts they can inspect:

- generated Oryn IR;
- generated control-flow output;
- generated assembly reference output;
- a real ELF64 object written by Oryn;
- a linked freestanding kernel ELF;
- a bootable ISO;
- QEMU serial proof output.

In practical terms, Stage 3 is the point where Oryn proves the compiler pipeline can create and boot a real freestanding kernel from safe C# source.

## Current version

`0.4.2`
