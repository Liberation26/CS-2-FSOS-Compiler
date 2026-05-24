#pragma once

void Diagnostics_Initialize(void);
void Diagnostics_WriteOk(const char* Message);
void Diagnostics_WriteWarn(const char* Message);
void Diagnostics_WriteFail(const char* Message);
