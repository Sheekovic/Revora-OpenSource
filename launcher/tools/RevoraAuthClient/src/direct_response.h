#pragma once
#include <windows.h>
#include <array>
#include <string>
#include <string_view>

namespace revora::direct {
// The v2 route currently returns exactly these five string-valued properties.
// Own and erase every decoded copy, including Base64 session-secret text.
struct Response final {
    std::array<std::string, 5> fields;
    Response() = default;
    Response(const Response&) = delete;
    Response& operator=(const Response&) = delete;
    ~Response() { Clear(); }
    void Clear() noexcept {
        for (auto& field : fields) {
            if (!field.empty()) SecureZeroMemory(field.data(), field.size());
            field.clear();
        }
    }
};

inline bool ParseResponse(std::string_view input, Response& output) {
    output.Clear();
    if (input.empty() || input.size() > 4096) return false;
    constexpr std::array<std::string_view, 5> names{
        "challengeId", "serverNonce", "connectionBinding", "sessionSecret", "expiresUtc"};
    size_t pos = 0;
    auto whitespace = [&] {
        while (pos < input.size() && (input[pos] == ' ' || input[pos] == '\t' ||
               input[pos] == '\r' || input[pos] == '\n')) ++pos;
    };
    auto quoted = [&](std::string& value) {
        if (pos >= input.size() || input[pos++] != '"') return false;
        while (pos < input.size()) {
            unsigned char ch = static_cast<unsigned char>(input[pos++]);
            if (ch == '"') return true;
            if (ch == '\\') {
                if (pos >= input.size()) return false;
                ch = static_cast<unsigned char>(input[pos++]);
                if (ch == 'u') {
                    if (input.size() - pos < 4) return false;
                    unsigned code = 0;
                    for (unsigned n = 0; n != 4; ++n) {
                        const char h = input[pos++];
                        unsigned digit;
                        if (h >= '0' && h <= '9') digit = h - '0';
                        else if (h >= 'a' && h <= 'f') digit = h - 'a' + 10;
                        else if (h >= 'A' && h <= 'F') digit = h - 'A' + 10;
                        else return false;
                        code = (code << 4) | digit;
                    }
                    if (code < 0x20 || code > 0x7e) return false;
                    ch = static_cast<unsigned char>(code);
                } else if (ch != '/' && ch != '\\' && ch != '"') return false;
            }
            if (ch < 0x20 || ch > 0x7e || value.size() >= 128) return false;
            value.push_back(static_cast<char>(ch));
        }
        return false;
    };
    whitespace();
    if (pos >= input.size() || input[pos++] != '{') return false;
    unsigned seen = 0;
    for (unsigned member = 0; member < names.size(); ++member) {
        whitespace();
        std::string key;
        if (!quoted(key)) { output.Clear(); return false; }
        size_t index = 0;
        while (index < names.size() && names[index] != key) ++index;
        if (index == names.size() || (seen & (1u << index))) {
            output.Clear(); return false;
        }
        whitespace();
        if (pos >= input.size() || input[pos++] != ':') { output.Clear(); return false; }
        whitespace();
        if (!quoted(output.fields[index]) || output.fields[index].empty()) {
            output.Clear(); return false;
        }
        seen |= 1u << index;
        whitespace();
        const char expected = member + 1 == names.size() ? '}' : ',';
        if (pos >= input.size() || input[pos++] != expected) { output.Clear(); return false; }
    }
    whitespace();
    if (pos != input.size() || seen != 31) { output.Clear(); return false; }
    // Value syntax (GUID/Base64/UTC expiry) is validated by the consumer.
    return true;
}
}
