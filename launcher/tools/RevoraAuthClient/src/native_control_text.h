#pragma once
#include <windows.h>
#include <cstdint>
#include <cstring>

// Patch-7630 control getter 0x00584B8D returns a 24-byte MSVC
// basic_string<wchar_t>: 8 UTF-16 inline units; size/capacity at +16/+20.
// This is a DISPLAY getter. It is suitable for the username, never for a
// password control: password display records contain masking characters.
inline bool ReadNativeControlUtf16(const void* object, wchar_t* output,
                                  size_t capacity, size_t& length) noexcept {
    length = 0;
    if (!object || !output || capacity < 2 || capacity > 257) return false;
    SecureZeroMemory(output, capacity * sizeof(wchar_t));
    __try {
        const auto* bytes = static_cast<const unsigned char*>(object);
        const auto count = *reinterpret_cast<const std::uint32_t*>(bytes + 16);
        const auto allocation = *reinterpret_cast<const std::uint32_t*>(bytes + 20);
        if (count == 0 || count >= capacity || allocation < count || allocation > 4096)
            return false;
        const wchar_t* data = allocation < 8
            ? reinterpret_cast<const wchar_t*>(bytes)
            : *reinterpret_cast<const wchar_t* const*>(bytes);
        if (!data || data[count] != L'\0') return false;
        for (size_t i = 0; i < count; ++i) {
            if (data[i] == L'\0') return false;
            if (data[i] >= 0xD800 && data[i] <= 0xDBFF) {
                if (++i >= count || data[i] < 0xDC00 || data[i] > 0xDFFF) return false;
            } else if (data[i] >= 0xDC00 && data[i] <= 0xDFFF) return false;
        }
        memcpy(output, data, count * sizeof(wchar_t));
        length = count;
        return true;
    } __except (EXCEPTION_EXECUTE_HANDLER) {
        SecureZeroMemory(output, capacity * sizeof(wchar_t));
        return false;
    }
}

// Native owned-password getter CFF864 returns basic_string<char> (SSO 15).
// The original UI conversion uses CP_ACP (A54354 called with code page zero).
// Preserve that contract, reject embedded NUL/invalid encoding, then convert
// to the HTTPS UTF-8 representation. No buffer or value is logged.
inline bool ReadNativePasswordUtf8(const void* object, char* output,
                                   size_t capacity) noexcept {
    if (!output || capacity < 2 || capacity > 1025) return false;
    SecureZeroMemory(output, capacity);
    wchar_t wide[256]{};
    bool success = false;
    __try {
        if (object) {
            const auto* bytes = static_cast<const unsigned char*>(object);
            const auto count = *reinterpret_cast<const std::uint32_t*>(bytes + 16);
            const auto allocation = *reinterpret_cast<const std::uint32_t*>(bytes + 20);
            if (count > 0 && count < 256 && allocation >= count && allocation <= 4096) {
                const char* data = allocation < 16 ? reinterpret_cast<const char*>(bytes)
                    : *reinterpret_cast<const char* const*>(bytes);
                if (data && data[count] == '\0' && !memchr(data, '\0', count)) {
                    const int units = MultiByteToWideChar(CP_ACP, MB_ERR_INVALID_CHARS,
                        data, static_cast<int>(count), wide, 255);
                    if (units > 0) {
                        const int size = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS,
                            wide, units, output, static_cast<int>(capacity - 1), nullptr, nullptr);
                        success = size > 0;
                    }
                }
            }
        }
    } __except (EXCEPTION_EXECUTE_HANDLER) { success = false; }
    SecureZeroMemory(wide, sizeof(wide));
    if (!success) SecureZeroMemory(output, capacity);
    return success;
}
