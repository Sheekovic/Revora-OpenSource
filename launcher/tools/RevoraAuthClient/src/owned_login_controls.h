#pragma once
bool RevoraOwnedLoginVisible() noexcept;
#include <windows.h>
#include <cstddef>
bool RevoraOwnedReadPair(void* dialog,char* username,size_t userCapacity,char* password,size_t passwordCapacity) noexcept;
void RevoraOwnedClearPassword() noexcept;
void RevoraOwnedSetBusy(bool busy) noexcept;
