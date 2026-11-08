#pragma once
#include <windows.h>
constexpr DWORD kRevoraObserverContractVersion = 4;
constexpr DWORD kRevoraObserverSynthetic = 1;
struct RevoraObserverInit final { DWORD size; DWORD version; DWORD flags; wchar_t channel[192]; wchar_t reportPath[1024]; wchar_t correlationId[33]; };
