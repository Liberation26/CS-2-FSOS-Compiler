# Stage 5 OS

Stage 5 is the runtime-contract kernel.

It proves that a safe C# kernel can use the approved Oryn module catalogue to enter a freestanding runtime path, initialize diagnostics and memory, use Stage 2 control flow, expose an approved panic route, mark the kernel ready, and halt through the approved CPU module.

Expected serial proof lines include:

```text
[ OK ] [ KERNEL   ] Stage5 kernel entered
[ OK ] [ KERNEL   ] Stage5 runtime contract initialized
[ OK ] [ KERNEL   ] Stage5 memory module initialized
[ OK ] [ KERNEL   ] Stage5 loop and branch proof worked
[ OK ] [ KERNEL   ] Stage5 runtime marked kernel ready
[ OK ] [ KERNEL   ] Stage5 kernel is halting forever
```
