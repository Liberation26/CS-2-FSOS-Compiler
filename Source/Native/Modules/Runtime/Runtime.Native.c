#include "Runtime.Native.h"

static int RuntimeInitialized = 0;
static int KernelReady = 0;

void Runtime_Initialize(void)
{
    RuntimeInitialized = 1;
}

void Runtime_MarkKernelReady(void)
{
    if (RuntimeInitialized == 0)
    {
        Runtime_Initialize();
    }

    KernelReady = 1;
}

int Runtime_IsInitialized(void)
{
    return RuntimeInitialized;
}

int Runtime_IsKernelReady(void)
{
    return KernelReady;
}
