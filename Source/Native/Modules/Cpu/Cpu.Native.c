#include "Cpu.Native.h"

void Cpu_Initialize(void)
{
}

void Cpu_HaltForever(void)
{
    for (;;)
    {
#if defined(__x86_64__) || defined(__i386__)
        __asm__ volatile ("hlt");
#endif
    }
}
