#pragma once

#include <cstddef>
#include <string_view>

namespace revora_ipc {
enum class LineResult { ok, missing_lf, multiple_lines, embedded_cr, empty, oversized };

inline LineResult NormalizeFrame(std::string_view frame, char* output, std::size_t capacity,
                                 std::size_t& length) noexcept {
  length = 0;
  if (frame.empty() || frame.back() != '\n') return LineResult::missing_lf;
  if (frame.find('\n') != frame.size() - 1) return LineResult::multiple_lines;
  frame.remove_suffix(1);
  if (!frame.empty() && frame.back() == '\r') frame.remove_suffix(1);
  if (frame.find('\r') != std::string_view::npos) return LineResult::embedded_cr;
  if (frame.empty()) return LineResult::empty;
  if (frame.size() + 1 > capacity) return LineResult::oversized;
  for (std::size_t index = 0; index < frame.size(); ++index) output[index] = frame[index];
  output[frame.size()] = 0;
  length = frame.size();
  return LineResult::ok;
}

inline bool IsUpperHex(std::string_view value) noexcept {
  for (const char character : value)
    if (!((character >= '0' && character <= '9') || (character >= 'A' && character <= 'F'))) return false;
  return true;
}

inline bool IsChallenge(std::string_view value) noexcept {
  constexpr std::string_view prefix = "CHALLENGE ";
  return value.size() == prefix.size() + 64 && value.starts_with(prefix)
      && IsUpperHex(value.substr(prefix.size()));
}

template <typename Close>
inline void CloseAndReset(void*& handle, void* invalid, Close close) noexcept {
  if (handle != invalid) close(handle);
  handle = invalid;
}
}
