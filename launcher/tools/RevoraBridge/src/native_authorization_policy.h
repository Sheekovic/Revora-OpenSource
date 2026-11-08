#pragma once

#include <string_view>

namespace revora_native_auth {

enum class Outcome { accepted, definitive_rejection, indeterminate };

inline Outcome Classify(unsigned long status, std::string_view body,
                        std::string_view correlation,
                        bool successEnvelopeValid) noexcept {
  if (status == 400 && body == R"({"error":"request_rejected"})")
    return Outcome::definitive_rejection;
  if (status == 200 && successEnvelopeValid && !correlation.empty())
    return Outcome::accepted;
  return Outcome::indeterminate;
}

inline bool MayRelease(Outcome outcome) noexcept {
  return outcome == Outcome::accepted ||
         outcome == Outcome::definitive_rejection;
}

} // namespace revora_native_auth
