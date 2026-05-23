#include "Diagnostics.Native.h"

#include <stdint.h>

#if DEBUG
static uint16_t* const VgaBuffer = (uint16_t*)0xB8000;
static uint32_t VgaOffset = 0;
static int DiagnosticsReady = 0;

static void OutByte(uint16_t Port, uint8_t Value)
{
    __asm__ volatile ("outb %0, %1" : : "a"(Value), "Nd"(Port));
}

static uint8_t InByte(uint16_t Port)
{
    uint8_t Value;
    __asm__ volatile ("inb %1, %0" : "=a"(Value) : "Nd"(Port));
    return Value;
}

static void SerialWaitForTransmitReady(void)
{
    while ((InByte(0x3F8 + 5) & 0x20) == 0)
    {
    }
}

static void SerialInitialize(void)
{
    OutByte(0x3F8 + 1, 0x00);
    OutByte(0x3F8 + 3, 0x80);
    OutByte(0x3F8 + 0, 0x03);
    OutByte(0x3F8 + 1, 0x00);
    OutByte(0x3F8 + 3, 0x03);
    OutByte(0x3F8 + 2, 0xC7);
    OutByte(0x3F8 + 4, 0x0B);
}

static void DiagnosticsEnsureReady(void)
{
    if (DiagnosticsReady != 0)
    {
        return;
    }

    SerialInitialize();
    DiagnosticsReady = 1;
}

static void SerialWriteChar(char Character)
{
    if (Character == '\n')
    {
        SerialWaitForTransmitReady();
        OutByte(0x3F8, '\r');
    }

    SerialWaitForTransmitReady();
    OutByte(0x3F8, (uint8_t)Character);
}

static void SerialWriteString(const char* Text)
{
    while (*Text != 0)
    {
        SerialWriteChar(*Text++);
    }
}

static void VgaWriteChar(char Character, uint8_t Attribute)
{
    if (Character == '\n')
    {
        VgaOffset = ((VgaOffset / 80) + 1) * 80;
        return;
    }

    if (VgaOffset >= (80 * 25))
    {
        VgaOffset = 0;
    }

    VgaBuffer[VgaOffset++] = ((uint16_t)Attribute << 8) | (uint8_t)Character;
}

static void VgaWriteString(const char* Text, uint8_t Attribute)
{
    while (*Text != 0)
    {
        VgaWriteChar(*Text++, Attribute);
    }
}

static void WriteLine(const char* Prefix, const char* Message, uint8_t Attribute)
{
    DiagnosticsEnsureReady();

    SerialWriteString(Prefix);
    SerialWriteString(Message);
    SerialWriteString("\n");

    VgaWriteString(Prefix, Attribute);
    VgaWriteString(Message, Attribute);
    VgaWriteString("\n", Attribute);
}
#endif

void Diagnostics_WriteOk(const char* Message)
{
#if DEBUG
    WriteLine("[ OK ] [ KERNEL   ] ", Message, 0x0A);
#else
    (void)Message;
#endif
}

void Diagnostics_WriteWarn(const char* Message)
{
#if DEBUG
    WriteLine("[WARN] [ KERNEL   ] ", Message, 0x0E);
#else
    (void)Message;
#endif
}

void Diagnostics_WriteFail(const char* Message)
{
#if DEBUG
    WriteLine("[FAIL] [ KERNEL   ] ", Message, 0x0C);
#else
    (void)Message;
#endif
}
