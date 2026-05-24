#include "Panic.Native.h"
#include "../Diagnostics/Diagnostics.Native.h"
#include "../Cpu/Cpu.Native.h"

void Panic_Initialize(void)
{
}

void Panic_Halt(const char* Reason)
{
    Diagnostics_WriteFail("Stage5 panic entered");
    Diagnostics_WriteFail(Reason);
    Cpu_HaltForever();
}
