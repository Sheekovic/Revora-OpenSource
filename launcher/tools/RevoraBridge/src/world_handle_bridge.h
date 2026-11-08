#pragma once
#include <chrono>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <mutex>

namespace revora_world {
enum class Result { captured, injected, rejected_shape, rejected_state, rejected_expired, rejected_replay };
class HandleBridge final {
 public:
  explicit HandleBridge(std::chrono::milliseconds lifetime=std::chrono::seconds(30)) noexcept:lifetime_(lifetime){}
  void Begin(std::uint64_t session) noexcept{std::lock_guard guard(lock_);ClearLocked();session_=session;}
  Result Capture(const std::byte*packet,std::size_t size,std::uint64_t session) noexcept{std::lock_guard guard(lock_);if(session_!=session){ClearLocked();return Result::rejected_state;}if(!packet||size!=52||Read16(packet)!=52||Read16(packet+2)!=1637){ClearLocked();return Result::rejected_shape;}const auto identifier=Read32(packet+4),status=Read32(packet+12);if(!identifier||status!=2){ClearLocked();return Result::rejected_shape;}ClearValue();handle_=identifier;captured_=std::chrono::steady_clock::now();present_=true;used_=false;return Result::captured;}
  Result Inject(std::byte*packet,std::size_t size,std::uint64_t session) noexcept{std::lock_guard guard(lock_);if(session_!=session||!present_)return used_?Result::rejected_replay:Result::rejected_state;if(std::chrono::steady_clock::now()-captured_>lifetime_){ClearLocked();return Result::rejected_expired;}if(!packet||size<8||Read16(packet)!=size||Read16(packet+2)!=2429){ClearLocked();return Result::rejected_shape;}Write32(packet+4,handle_);ClearValue();present_=false;used_=true;return Result::injected;}
  void Clear() noexcept{std::lock_guard guard(lock_);ClearLocked();}
  void ProcessDetachClear() noexcept{ClearValue();present_=false;used_=false;}
  bool Present()const noexcept{std::lock_guard guard(lock_);return present_;}
 private:
  static std::uint16_t Read16(const std::byte*p)noexcept{std::uint16_t v{};std::memcpy(&v,p,2);return v;}
  static std::uint32_t Read32(const std::byte*p)noexcept{std::uint32_t v{};std::memcpy(&v,p,4);return v;}
  static void Write32(std::byte*p,std::uint32_t v)noexcept{std::memcpy(p,&v,4);}
  void ClearValue()noexcept{volatile std::uint32_t*p=&handle_;*p=0;}
  void ClearLocked()noexcept{ClearValue();present_=false;used_=false;captured_={};}
  mutable std::mutex lock_;std::chrono::milliseconds lifetime_;std::chrono::steady_clock::time_point captured_{};std::uint64_t session_{};std::uint32_t handle_{};bool present_{};bool used_{};
};
}
