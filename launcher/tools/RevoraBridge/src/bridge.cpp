#include "bridge_contract.h"
#include "account_redirect_policy.h"
#include "world_handle_bridge.h"
#include "native_authorization_policy.h"
#include <array>
#include <atomic>
#include <bcrypt.h>
#include <cstdio>
#include <cstring>
#include <filesystem>
#include <objbase.h>
#include <string>
#include <vector>
#include <windows.h>
#include <winhttp.h>
#include <winsock2.h>
#include <ws2tcpip.h>

namespace {
constexpr uintptr_t kSubmitRva = 0x0025969C,
                    kControlTextRva = 0x00584B8D,
                    kProcessed1637Rva = 0x009FD0A2,
                    kOutgoingPacketRva = 0x009FC095,
                    kSendIatRva = 0x010CE7A0,
                    kConnectIatRva = 0x010CE7C4;
constexpr std::array<unsigned char, 5> kSubmitPreimage{0x68, 0x0C, 0x01, 0x00,
                                                       0x00};
constexpr std::array<unsigned char, 7> kControlTextPreimage{
    0x6A, 0x04, 0xB8, 0xDC, 0x60, 0xF5, 0x00};
constexpr std::array<unsigned char, 5> kProcessed1637Preimage{0x68,0x4C,0x06,0x00,0x00};
constexpr std::array<unsigned char, 5> kOutgoingPacketPreimage{0x55,0x8B,0xEC,0x51,0x53};
constexpr wchar_t kAuthHost[] = L"auth.playrevora.com",
                  kAuthPath[] = L"/api/v1/native-login/authorize";
constexpr char kApprovedDx9Sha256[] =
    "4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE";
constexpr LONGLONG kApprovedDx9Size = 18594824;
using SubmitFn = void(__thiscall *)(void *);
using ControlTextFn = void *(__thiscall *)(void *, void *);
using SendFn = int(WSAAPI *)(SOCKET, const char *, int, int);
using ConnectFn = int(WSAAPI *)(SOCKET, const sockaddr *, int);
wchar_t g_reportPath[1024]{};
SRWLOCK g_reportLock = SRWLOCK_INIT;
struct HookState {
  void *site = nullptr;
  void *trampoline = nullptr;
  std::array<unsigned char, 8> original{};
  size_t length = 5;
};
HookState g_submitHook, g_controlTextHook, g_processedHook, g_outgoingHook;
SubmitFn g_submit = nullptr;
ControlTextFn g_controlText = nullptr;
SendFn g_send = nullptr;
ConnectFn g_connect = nullptr;
void **g_sendIat = nullptr;
void **g_connectIat = nullptr;
std::atomic<bool> g_inFlight{false};
std::atomic<unsigned> g_attemptGeneration{0};
// 0=no classified request, 1=classification in progress, 2=released,
// 3=suppressed. Reset only by a new explicit Login submission.
std::atomic<unsigned> g_requestDisposition{0};
std::atomic<SOCKET> g_accountSocket{INVALID_SOCKET};
bool g_synthetic = false;
using SyntheticHookFn = int(__cdecl *)();
SyntheticHookFn g_syntheticTrampoline = nullptr;
std::atomic<unsigned> g_syntheticInvocations{0};
using ProcessedFn=void(__thiscall*)(void*);
using OutgoingFn=void(__thiscall*)(void*,void*);
ProcessedFn g_processed=nullptr;
OutgoingFn g_outgoing=nullptr;
revora_world::HandleBridge g_worldHandle;
std::uint64_t g_launchSession=0;
char g_correlation[33]{};
DWORD g_targetPid=0;
struct AttemptState {
  SRWLOCK lock = SRWLOCK_INIT;
  bool active = false;
  bool capturing = false;
  bool usernameCaptured = false;
  bool passwordCaptured = false;
  bool terminalReported = false;
  void *dialog = nullptr;
  DWORD threadId = 0;
  ULONGLONG started = 0;
  std::array<char, 129> username{};
  std::array<char, 257> password{};
} g_attempt;

bool WriteLine(const char *line) noexcept {
  if (!g_reportPath[0])
    return false;
  AcquireSRWLockExclusive(&g_reportLock);
  HANDLE report=CreateFileW(g_reportPath,FILE_APPEND_DATA,FILE_SHARE_READ,nullptr,OPEN_EXISTING,
                            FILE_ATTRIBUTE_NORMAL|FILE_FLAG_WRITE_THROUGH|FILE_FLAG_OPEN_REPARSE_POINT,nullptr);
  if(report==INVALID_HANDLE_VALUE){ReleaseSRWLockExclusive(&g_reportLock);return false;}
  FILE_ATTRIBUTE_TAG_INFO tag{};
  if(!GetFileInformationByHandleEx(report,FileAttributeTagInfo,&tag,sizeof(tag)) ||
     (tag.FileAttributes&FILE_ATTRIBUTE_REPARSE_POINT)){CloseHandle(report);ReleaseSRWLockExclusive(&g_reportLock);return false;}
  SYSTEMTIME now{}; GetSystemTime(&now);
  char enriched[1024]{};
  const char* body=(line && line[0]=='{')?line+1:line;
  const int count=sprintf_s(enriched,
      "{\"schema\":4,\"version\":\"2.0.0-owner-test\",\"correlationId\":\"%s\",\"targetPid\":%lu,"
      "\"timestampUtc\":\"%04u-%02u-%02uT%02u:%02u:%02u.%03uZ\",%s",
      g_correlation,static_cast<unsigned long>(g_targetPid),now.wYear,now.wMonth,now.wDay,
      now.wHour,now.wMinute,now.wSecond,now.wMilliseconds,body?body:"\"event\":\"invalid\"}\n");
  if(count<=0 || count>=static_cast<int>(sizeof(enriched))) { CloseHandle(report); ReleaseSRWLockExclusive(&g_reportLock); return false; }
  DWORD n = static_cast<DWORD>(count), w = 0;
  LARGE_INTEGER size{};
  const bool bounded=GetFileSizeEx(report,&size) && size.QuadPart+n<=64*1024;
  const bool ok = bounded && WriteFile(report, enriched, n, &w, nullptr) && w == n && FlushFileBuffers(report);
  CloseHandle(report);
  ReleaseSRWLockExclusive(&g_reportLock);
  return ok;
}
bool ValidReport(const wchar_t *value) noexcept {
  if (!value)
    return false;
  std::filesystem::path p(value);
  const auto name=p.filename().wstring();
  constexpr wchar_t prefix[]=L"native-login-single-entry-";
  return p.is_absolute() && name.size()==64 && name.rfind(prefix,0)==0 &&
         name.substr(26,32).find_first_not_of(L"0123456789ABCDEF")==std::wstring::npos &&
         name.substr(58)==L".jsonl" &&
         _wcsicmp(p.parent_path().filename().c_str(), L"RevoraDiagnostics") ==
             0;
}
void ClearWorld(const char* reason) noexcept {
  const bool present=g_worldHandle.Present(); g_worldHandle.Clear();
  if(present){char line[160]{};sprintf_s(line,"{\"event\":\"world_handle_cleared\",\"reason\":\"%s\"}\n",reason);WriteLine(line);}
}
void __fastcall Processed1637Gate(void* self, void*) noexcept {
  __try { auto* packet=self?*reinterpret_cast<std::byte**>(static_cast<unsigned char*>(self)+0x404):nullptr;
    const bool nativeRejection = packet &&
        *reinterpret_cast<const std::uint16_t*>(packet) == 52 &&
        *reinterpret_cast<const std::uint16_t*>(packet + 2) == 1637 &&
        *reinterpret_cast<const std::uint32_t*>(packet + 4) == 0 &&
        *reinterpret_cast<const std::uint32_t*>(packet + 12) == 1;
    if (nativeRejection) {
      ClearWorld("native_rejection");
      WriteLine("{\"event\":\"native_rejection_received\"}\n");
    } else if(g_worldHandle.Capture(packet,52,g_launchSession)==revora_world::Result::captured) WriteLine("{\"event\":\"world_handle_captured\",\"nonzero\":true,\"sourceOpcode\":1637}\n");
    else { ClearWorld("invalid_1637"); WriteLine("{\"event\":\"world_handle_bridge_rejected\",\"reason\":\"invalid_1637\"}\n"); }
  } __except(EXCEPTION_EXECUTE_HANDLER) { ClearWorld("exception"); }
  g_processed(self);
}
void __fastcall OutgoingPacketGate(void* self, void*, void* object) noexcept {
  bool injected=false; __try { auto* packet=object?reinterpret_cast<std::byte*>(static_cast<unsigned char*>(object)+4):nullptr;
    const auto size=packet?*reinterpret_cast<std::uint16_t*>(packet):0;
    injected=g_worldHandle.Inject(packet,size,g_launchSession)==revora_world::Result::injected;
    if(injected) WriteLine("{\"event\":\"world_handle_injected\",\"destinationOpcode\":2429,\"offset\":4,\"width\":4}\n");
  } __except(EXCEPTION_EXECUTE_HANDLER) { ClearWorld("exception"); }
  g_outgoing(self,object); if(injected) WriteLine("{\"event\":\"world_handle_cleared\",\"reason\":\"consumed\"}\n");
}
void ZeroAttempt() noexcept {
  AcquireSRWLockExclusive(&g_attempt.lock);
  SecureZeroMemory(g_attempt.username.data(), g_attempt.username.size());
  SecureZeroMemory(g_attempt.password.data(), g_attempt.password.size());
  g_attempt.active = false;
  g_attempt.capturing = false;
  g_attempt.usernameCaptured = false;
  g_attempt.passwordCaptured = false;
  g_attempt.terminalReported = false;
  g_attempt.dialog = nullptr;
  g_attempt.threadId = 0;
  g_attempt.started = 0;
  ReleaseSRWLockExclusive(&g_attempt.lock);
  g_inFlight.store(false);
  WriteLine("{\"event\":\"credential_buffer_cleared\",\"result\":\"success\"}\n");
}
DWORD WINAPI AttemptWatchdog(void *parameter) noexcept {
  const unsigned generation = static_cast<unsigned>(
      reinterpret_cast<uintptr_t>(parameter));
  const ULONGLONG deadline = GetTickCount64() + 8000;
  while (GetTickCount64() < deadline) {
    if (!g_inFlight.load() || g_attemptGeneration.load() != generation)
      return ERROR_SUCCESS;
    Sleep(25);
  }
  if (!g_inFlight.load() || g_attemptGeneration.load() != generation)
    return ERROR_SUCCESS;
  bool captureIncomplete = false;
  const char *captureReason = "accessors_not_observed";
  AcquireSRWLockExclusive(&g_attempt.lock);
  if (g_attempt.capturing && !g_attempt.terminalReported) {
    captureIncomplete = true;
    if (g_attempt.usernameCaptured)
      captureReason = "password_accessor_missing";
    else if (g_attempt.passwordCaptured)
      captureReason = "username_accessor_missing";
    g_attempt.terminalReported = true;
  }
  ReleaseSRWLockExclusive(&g_attempt.lock);
  if (captureIncomplete) {
    char line[160]{};
    sprintf_s(line, "{\"event\":\"credential_capture_failed\",\"reason\":\"%s\"}\n",
              captureReason);
    WriteLine(line);
  } else {
    WriteLine("{\"event\":\"account_request_not_observed\",\"reason\":\"bounded_timeout\"}\n");
  }
  const SOCKET account = g_accountSocket.load();
  if (account != INVALID_SOCKET)
    shutdown(account, SD_BOTH);
  ZeroAttempt();
  WriteLine("{\"event\":\"login_ui_restoration_requested\",\"method\":\"native_account_error_path\"}\n");
  WriteLine("{\"event\":\"final_stage\",\"stage\":\"account_request_not_observed\"}\n");
  return ERROR_TIMEOUT;
}
bool CopyBounded(const char *source, char *target, size_t capacity) noexcept {
  if (!source)
    return false;
  size_t length = strnlen_s(source, capacity);
  if (length == 0 || length >= capacity)
    return false;
  memcpy_s(target, capacity, source, length);
  target[length] = 0;
  return true;
}
void AppendJsonEscaped(std::string &out, const char *value) {
  for (const unsigned char *cursor =
           reinterpret_cast<const unsigned char *>(value);
       *cursor != 0; ++cursor) {
    const unsigned char c = *cursor;
    switch (c) {
    case '\\':
      out += "\\\\";
      break;
    case '"':
      out += "\\\"";
      break;
    case '\b':
      out += "\\b";
      break;
    case '\f':
      out += "\\f";
      break;
    case '\n':
      out += "\\n";
      break;
    case '\r':
      out += "\\r";
      break;
    case '\t':
      out += "\\t";
      break;
    default:
      if (c < 0x20)
        throw std::runtime_error("encoding");
      out.push_back(static_cast<char>(c));
    }
  }
}
std::string Hex(const std::array<unsigned char, 32> &bytes) {
  constexpr char a[] = "0123456789ABCDEF";
  std::string result(64, '0');
  for (size_t i = 0; i < bytes.size(); ++i) {
    result[i * 2] = a[bytes[i] >> 4];
    result[i * 2 + 1] = a[bytes[i] & 15];
  }
  return result;
}
std::array<unsigned char, 32> Sha256(const char *data, int length) {
  BCRYPT_ALG_HANDLE algorithm{};
  BCRYPT_HASH_HANDLE hash{};
  DWORD objectSize = 0, cb = 0;
  std::array<unsigned char, 32> digest{};
  if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr,
                                  0) != 0)
    throw std::runtime_error("sha");
  BCryptGetProperty(algorithm, BCRYPT_OBJECT_LENGTH,
                    reinterpret_cast<PUCHAR>(&objectSize), sizeof(objectSize),
                    &cb, 0);
  std::vector<unsigned char> object(objectSize);
  if (BCryptCreateHash(algorithm, &hash, object.data(), objectSize, nullptr, 0,
                       0) != 0) {
    BCryptCloseAlgorithmProvider(algorithm, 0);
    throw std::runtime_error("sha");
  }
  NTSTATUS status =
      BCryptHashData(hash, reinterpret_cast<PUCHAR>(const_cast<char *>(data)),
                     static_cast<ULONG>(length), 0);
  if (status == 0)
    status = BCryptFinishHash(hash, digest.data(),
                              static_cast<ULONG>(digest.size()), 0);
  BCryptDestroyHash(hash);
  BCryptCloseAlgorithmProvider(algorithm, 0);
  if (status != 0)
    throw std::runtime_error("sha");
  return digest;
}
bool ApprovedExecutable(const wchar_t *path) noexcept {
  HANDLE file = CreateFileW(path, GENERIC_READ,
                            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                            nullptr, OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN, nullptr);
  if (file == INVALID_HANDLE_VALUE) return false;
  LARGE_INTEGER size{};
  bool approved = GetFileSizeEx(file, &size) && size.QuadPart == kApprovedDx9Size;
  std::vector<char> bytes;
  if (approved) {
    bytes.resize(static_cast<size_t>(size.QuadPart));
    DWORD total = 0;
    while (total < bytes.size()) {
      DWORD read = 0;
      const DWORD remaining = static_cast<DWORD>(bytes.size() - total);
      if (!ReadFile(file, bytes.data() + total, remaining, &read, nullptr) || read == 0) {
        approved = false;
        break;
      }
      total += read;
    }
  }
  CloseHandle(file);
  if (!approved) return false;
  try {
    const auto digest = Hex(Sha256(bytes.data(), static_cast<int>(bytes.size())));
    SecureZeroMemory(bytes.data(), bytes.size());
    return digest == kApprovedDx9Sha256;
  } catch (...) {
    if (!bytes.empty()) SecureZeroMemory(bytes.data(), bytes.size());
    return false;
  }
}
std::string NewCorrelation() {
  GUID id{};
  if (CoCreateGuid(&id) != S_OK)
    throw std::runtime_error("correlation");
  char value[37]{};
  sprintf_s(value, "%08lx-%04x-%04x-%02x%02x-%02x%02x%02x%02x%02x%02x",
            id.Data1, id.Data2, id.Data3, id.Data4[0], id.Data4[1], id.Data4[2],
            id.Data4[3], id.Data4[4], id.Data4[5], id.Data4[6], id.Data4[7]);
  return value;
}
bool ValidSuccessResponse(const std::string &response,
                          const std::string &correlation) noexcept {
  const std::string prefix = "{\"correlationId\":\"" + correlation +
                             "\",\"expiresUtc\":\"";
  if (!response.starts_with(prefix) || !response.ends_with("\"}"))
    return false;
  const std::string_view expiry(response.data() + prefix.size(),
                                response.size() - prefix.size() - 2);
  if (expiry.size() < 20 || expiry.size() > 35 || expiry[4] != '-' ||
      expiry[7] != '-' || expiry[10] != 'T' || expiry[13] != ':' ||
      expiry[16] != ':' ||
      !(expiry.ends_with("Z") || expiry.ends_with("+00:00")))
    return false;
  SYSTEMTIME parsed{};
  int consumed = 0;
  if (sscanf_s(std::string(expiry).c_str(), "%hu-%hu-%huT%hu:%hu:%hu%n",
               &parsed.wYear, &parsed.wMonth, &parsed.wDay, &parsed.wHour,
               &parsed.wMinute, &parsed.wSecond, &consumed) != 6 ||
      consumed != 19)
    return false;
  FILETIME expires{}, now{};
  if (!SystemTimeToFileTime(&parsed, &expires))
    return false;
  GetSystemTimeAsFileTime(&now);
  ULARGE_INTEGER expiryValue{}, nowValue{};
  expiryValue.LowPart = expires.dwLowDateTime;
  expiryValue.HighPart = expires.dwHighDateTime;
  nowValue.LowPart = now.dwLowDateTime;
  nowValue.HighPart = now.dwHighDateTime;
  constexpr ULONGLONG maximumFuture = 15ull * 10000000ull;
  return expiryValue.QuadPart > nowValue.QuadPart &&
         expiryValue.QuadPart - nowValue.QuadPart <= maximumFuture;
}
revora_native_auth::Outcome Authorize(const std::string &digest,
                                      const std::string &correlation,
                                      const char *username,
                                      const char *password) noexcept {
  try {
    if (g_synthetic)
      return revora_native_auth::Outcome::accepted;
    struct SensitiveBody final {
      std::string value;
      ~SensitiveBody() { if (!value.empty()) SecureZeroMemory(value.data(), value.size()); }
    } body;
    body.value.reserve(768);
    body.value = "{\"username\":\"";
    AppendJsonEscaped(body.value, username);
    body.value += "\",\"password\":\"";
    AppendJsonEscaped(body.value, password);
    body.value += "\",\"requestSha256\":\"";
    body.value += digest;
    body.value += "\",\"realm\":\"Revora\",\"profile\":\"dx9\","
                  "\"clientContract\":\"patch-7630-168.06\","
                  "\"correlationId\":\"";
    body.value += correlation;
    body.value += "\"}";
    HINTERNET session = WinHttpOpen(
        L"RevoraNativeLogin/1.0", WINHTTP_ACCESS_TYPE_NO_PROXY,
        WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
    if (!session)
      return revora_native_auth::Outcome::indeterminate;
    DWORD secureProtocols = 0x00000800u; // TLS 1.2 minimum; no legacy protocol fallback.
    if (!WinHttpSetOption(session, WINHTTP_OPTION_SECURE_PROTOCOLS,
                          &secureProtocols, sizeof(secureProtocols))) {
      WinHttpCloseHandle(session);
      return revora_native_auth::Outcome::indeterminate;
    }
    WinHttpSetTimeouts(session, 2000, 2000, 3000, 8000);
    HINTERNET connection =
        WinHttpConnect(session, kAuthHost, INTERNET_DEFAULT_HTTPS_PORT, 0);
    HINTERNET request = connection
                            ? WinHttpOpenRequest(connection, L"POST", kAuthPath,
                                                 nullptr, WINHTTP_NO_REFERER,
                                                 WINHTTP_DEFAULT_ACCEPT_TYPES,
                                                 WINHTTP_FLAG_SECURE)
                            : nullptr;
    DWORD redirectPolicy = WINHTTP_OPTION_REDIRECT_POLICY_NEVER;
    DWORD maximumHeaders = 16 * 1024;
    if (request &&
        (!WinHttpSetOption(request, WINHTTP_OPTION_REDIRECT_POLICY,
                           &redirectPolicy, sizeof(redirectPolicy)) ||
         !WinHttpSetOption(request, WINHTTP_OPTION_MAX_RESPONSE_HEADER_SIZE,
                           &maximumHeaders, sizeof(maximumHeaders)))) {
      WinHttpCloseHandle(request);
      WinHttpCloseHandle(connection);
      WinHttpCloseHandle(session);
      return revora_native_auth::Outcome::indeterminate;
    }
    constexpr DWORD automaticHeaderLength = static_cast<DWORD>(-1);
    BOOL transportOk = request &&
              WinHttpSendRequest(request, L"Content-Type: application/json\r\n",
                                 automaticHeaderLength, body.value.data(),
                                 static_cast<DWORD>(body.value.size()),
                                 static_cast<DWORD>(body.value.size()), 0) &&
              WinHttpReceiveResponse(request, nullptr);
    DWORD status = 0, size = sizeof(status);
    if (transportOk)
      transportOk = WinHttpQueryHeaders(
               request, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
               WINHTTP_HEADER_NAME_BY_INDEX, &status, &size,
               WINHTTP_NO_HEADER_INDEX);
    SensitiveBody responseBody;
    std::string &response = responseBody.value;
    while (transportOk && response.size() <= 4096) {
      DWORD available = 0;
      if (!WinHttpQueryDataAvailable(request, &available)) { transportOk = FALSE; break; }
      if (available == 0) break;
      if (available > 4096 - response.size()) { transportOk = FALSE; break; }
      const size_t offset = response.size(); response.resize(offset + available);
      DWORD read = 0;
      if (!WinHttpReadData(request, response.data() + offset, available, &read)
          || read == 0 || read > available) { transportOk = FALSE; break; }
      response.resize(offset + read);
    }
    const bool successEnvelopeValid = transportOk && status == 200 &&
                                      ValidSuccessResponse(response, correlation);
    if (request)
      WinHttpCloseHandle(request);
    if (connection)
      WinHttpCloseHandle(connection);
    WinHttpCloseHandle(session);
    if (!transportOk)
      return revora_native_auth::Outcome::indeterminate;
    return revora_native_auth::Classify(status, response, correlation,
                                        successEnvelopeValid);
  } catch (...) {
    return revora_native_auth::Outcome::indeterminate;
  }
}
void RestoreUiAfterTransportFailure() noexcept {
  const SOCKET account = g_accountSocket.exchange(INVALID_SOCKET);
  if (account != INVALID_SOCKET)
    shutdown(account, SD_BOTH);
  ClearWorld("transport_failure");
  MessageBoxW(nullptr,
              L"Revora authentication is temporarily unavailable. Please try again.",
              L"PlayRevora", MB_OK | MB_ICONWARNING | MB_TASKMODAL);
  WriteLine("{\"event\":\"login_ui_restored_after_transport_failure\",\"method\":\"native_account_error_path\"}\n");
}
bool ValidEnvelopeLength(int length) noexcept {
  return length == 440 || length == 472 || length == 480;
}
bool IsAccountSocket(SOCKET socket) noexcept {
  if (g_synthetic)
    return true;
  sockaddr_storage address{};
  int addressLength = sizeof(address);
  if (getpeername(socket, reinterpret_cast<sockaddr *>(&address),
                  &addressLength) != 0)
    return false;
  if (address.ss_family == AF_INET)
    return ntohs(reinterpret_cast<sockaddr_in *>(&address)->sin_port) == 9958;
  if (address.ss_family == AF_INET6)
    return ntohs(reinterpret_cast<sockaddr_in6 *>(&address)->sin6_port) == 9958;
  return false;
}
int WSAAPI ConnectGate(SOCKET socket, const sockaddr *name, int nameLength) {
  sockaddr_in replacement{};
  if (revora_redirect::TryRewrite(name,nameLength,replacement)) {
    ClearWorld("account_reconnect");
    WriteLine("{\"event\":\"account_endpoint_selected\",\"profile\":\"dx9\"}\n");
    const int result =
        g_connect(socket, reinterpret_cast<const sockaddr *>(&replacement),
                  sizeof(replacement));
    if (result == 0 || WSAGetLastError() == WSAEWOULDBLOCK)
      g_accountSocket.store(socket);
    return result;
  }
  return g_connect(socket, name, nameLength);
}
int WSAAPI SendGate(SOCKET socket, const char *buffer, int length, int flags) {
  if (!ValidEnvelopeLength(length) || buffer == nullptr || !IsAccountSocket(socket))
    return g_send(socket, buffer, length, flags);
  std::array<char, 129> username{};
  std::array<char, 257> password{};
  AcquireSRWLockShared(&g_attempt.lock);
  const bool active = g_attempt.active && GetTickCount64() - g_attempt.started <= 8000;
  if (active) {
    memcpy(username.data(), g_attempt.username.data(), username.size());
    memcpy(password.data(), g_attempt.password.data(), password.size());
  }
  ReleaseSRWLockShared(&g_attempt.lock);
  if (!active) {
    if (g_requestDisposition.load() != 0) {
      WSASetLastError(WSAEALREADY);
      return SOCKET_ERROR;
    }
    return g_send(socket, buffer, length, flags);
  }
  unsigned expectedDisposition = 0;
  if (!g_requestDisposition.compare_exchange_strong(expectedDisposition, 1)) {
    WSASetLastError(WSAEALREADY);
    return SOCKET_ERROR;
  }
  WriteLine("{\"event\":\"account_request_candidate\",\"profile\":\"dx9\"}\n");
  WriteLine("{\"event\":\"account_request_classified\",\"result\":\"expected_envelope\"}\n");
  std::string correlation;
  auto outcome = revora_native_auth::Outcome::indeterminate;
  try {
    correlation = NewCorrelation();
    WriteLine("{\"event\":\"https_started\",\"profile\":\"dx9\"}\n");
    outcome = Authorize(Hex(Sha256(buffer, length)), correlation,
                        username.data(), password.data());
  } catch (...) {
    outcome = revora_native_auth::Outcome::indeterminate;
  }
  SecureZeroMemory(username.data(), username.size());
  SecureZeroMemory(password.data(), password.size());
  ZeroAttempt();
  if (!revora_native_auth::MayRelease(outcome)) {
    g_requestDisposition.store(3);
    WriteLine("{\"event\":\"authorization_indeterminate_request_suppressed\"}\n");
    RestoreUiAfterTransportFailure();
    WriteLine("{\"event\":\"final_stage\",\"stage\":\"authorization_indeterminate\"}\n");
    WSASetLastError(WSAECONNABORTED);
    return SOCKET_ERROR;
  }
  WriteLine(outcome == revora_native_auth::Outcome::accepted
                ? "{\"event\":\"authorization_accepted_request_released\"}\n"
                : "{\"event\":\"authorization_rejected_request_released_for_native_rejection\"}\n");
  g_requestDisposition.store(2);
  const int result = g_send(socket, buffer, length, flags);
  const int socketError = WSAGetLastError();
  WriteLine(result == SOCKET_ERROR
                ? "{\"event\":\"native_send_result\",\"result\":\"failed\"}\n"
                : "{\"event\":\"native_send_result\",\"result\":\"success\"}\n");
  WSASetLastError(socketError);
  return result;
}
void FailCredentialCapture(const char *reason) noexcept;
void __fastcall SubmitGate(void *dialog, void *) {
  WriteLine("{\"event\":\"login_submit_entered\",\"profile\":\"dx9\"}\n");
  if (g_inFlight.exchange(true)) {
    WriteLine("{\"event\":\"login_attempt_rejected_duplicate\",\"reason\":\"in_flight\"}\n");
    return;
  }
  g_requestDisposition.store(0);
  WriteLine("{\"event\":\"credential_capture_armed\",\"profile\":\"dx9\"}\n");
  AcquireSRWLockExclusive(&g_attempt.lock);
  SecureZeroMemory(g_attempt.username.data(), g_attempt.username.size());
  SecureZeroMemory(g_attempt.password.data(), g_attempt.password.size());
  g_attempt.active = false;
  g_attempt.capturing = true;
  g_attempt.usernameCaptured = false;
  g_attempt.passwordCaptured = false;
  g_attempt.terminalReported = false;
  g_attempt.dialog = dialog;
  g_attempt.threadId = GetCurrentThreadId();
  g_attempt.started = GetTickCount64();
  ReleaseSRWLockExclusive(&g_attempt.lock);
  WriteLine("{\"event\":\"credential_buffer_created\",\"result\":\"bounded\"}\n");
  const unsigned generation = g_attemptGeneration.fetch_add(1) + 1;
  HANDLE watchdog = CreateThread(
      nullptr, 0, &AttemptWatchdog,
      reinterpret_cast<void *>(static_cast<uintptr_t>(generation)), 0,
      nullptr);
  if (watchdog)
    CloseHandle(watchdog);
  else {
    WriteLine("{\"event\":\"bridge_failure\",\"reason\":\"watchdog_start_failed\"}\n");
    ZeroAttempt();
    return;
  }
  __try {
    g_submit(dialog);
  } __except (EXCEPTION_EXECUTE_HANDLER) {
    FailCredentialCapture("submit_exception");
    return;
  }
  bool incomplete = false;
  AcquireSRWLockExclusive(&g_attempt.lock);
  if (g_attempt.capturing && !g_attempt.terminalReported) {
    g_attempt.terminalReported = true;
    incomplete = true;
  }
  ReleaseSRWLockExclusive(&g_attempt.lock);
  if (incomplete) {
    WriteLine("{\"event\":\"credential_capture_failed\",\"reason\":\"submit_returned_without_pair\"}\n");
    ZeroAttempt();
  }
}
bool ReadClientString(void *object, char *destination, size_t capacity) noexcept {
  if (!object || !destination || capacity < 2)
    return false;
  __try {
    const auto *bytes = static_cast<const unsigned char *>(object);
    const size_t length = *reinterpret_cast<const unsigned *>(bytes + 0x10);
    const size_t allocated = *reinterpret_cast<const unsigned *>(bytes + 0x14);
    if (length == 0 || length >= capacity || allocated < length)
      return false;
    const char *source = allocated < 0x10
                             ? reinterpret_cast<const char *>(bytes)
                             : *reinterpret_cast<char *const *>(bytes);
    if (!source || source[length] != '\0' ||
        strnlen_s(source, length + 1) != length)
      return false;
    memcpy(destination, source, length);
    destination[length] = '\0';
    return true;
  } __except (EXCEPTION_EXECUTE_HANDLER) {
    return false;
  }
}
void FailCredentialCapture(const char *reason) noexcept {
  bool report = false;
  AcquireSRWLockExclusive(&g_attempt.lock);
  if (g_attempt.capturing && !g_attempt.terminalReported) {
    g_attempt.terminalReported = true;
    g_attempt.capturing = false;
    report = true;
  }
  ReleaseSRWLockExclusive(&g_attempt.lock);
  if (report) {
    char line[160]{};
    sprintf_s(line, "{\"event\":\"credential_capture_failed\",\"reason\":\"%s\"}\n",
              reason);
    WriteLine(line);
    ZeroAttempt();
  }
}
void *__fastcall ControlTextGate(void *control, void *, void *output) {
  void *result = nullptr;
  __try {
    result = g_controlText(control, output);
  } __except (EXCEPTION_EXECUTE_HANDLER) {
    FailCredentialCapture("accessor_exception");
    return nullptr;
  }
  enum class Kind { none, username, password } kind = Kind::none;
  AcquireSRWLockShared(&g_attempt.lock);
  const bool capturing = g_attempt.capturing;
  void *dialog = g_attempt.dialog;
  const DWORD threadId = g_attempt.threadId;
  ReleaseSRWLockShared(&g_attempt.lock);
  if (!capturing || !dialog)
    return result;
  if (GetCurrentThreadId() != threadId) {
    FailCredentialCapture("thread_mismatch");
    return result;
  }
  auto *base = static_cast<unsigned char *>(dialog);
  if (control == base + 0x0BB8)
    kind = Kind::username;
  else if (control == base + 0x0EB0)
    kind = Kind::password;
  else
    return result;
  bool completed = false;
  bool failed = false;
  bool duplicate = false;
  AcquireSRWLockExclusive(&g_attempt.lock);
  if (g_attempt.capturing && GetTickCount64() - g_attempt.started <= 8000) {
    const bool already = kind == Kind::username ? g_attempt.usernameCaptured
                                                 : g_attempt.passwordCaptured;
    char *destination = kind == Kind::username ? g_attempt.username.data()
                                                : g_attempt.password.data();
    const size_t destinationSize = kind == Kind::username
                                       ? g_attempt.username.size()
                                       : g_attempt.password.size();
    duplicate = already;
    const bool copied = !already && ReadClientString(output, destination,
                                                      destinationSize);
    if (copied) {
      if (kind == Kind::username)
        g_attempt.usernameCaptured = true;
      else
        g_attempt.passwordCaptured = true;
      completed = g_attempt.usernameCaptured && g_attempt.passwordCaptured;
      g_attempt.active = completed;
      if (completed) {
        g_attempt.capturing = false;
        g_attempt.terminalReported = true;
      }
    } else {
      failed = true;
      g_attempt.capturing = false;
      g_attempt.terminalReported = true;
    }
  }
  ReleaseSRWLockExclusive(&g_attempt.lock);
  if (failed) {
    WriteLine(duplicate
                  ? "{\"event\":\"credential_capture_failed\",\"reason\":\"duplicate_accessor\"}\n"
                  : "{\"event\":\"credential_capture_failed\",\"reason\":\"invalid_bounds\"}\n");
    ZeroAttempt();
  } else {
    WriteLine(kind == Kind::username
                  ? "{\"event\":\"username_accessor_observed\",\"result\":\"bounded\"}\n"
                  : "{\"event\":\"password_accessor_observed\",\"result\":\"bounded\"}\n");
    if (completed)
      WriteLine("{\"event\":\"credential_pair_complete\",\"result\":\"success\"}\n");
  }
  return result;
}
template <size_t N>
bool PatchJump(HookState &hook, void *replacement,
               const std::array<unsigned char, N> &preimage) noexcept {
  static_assert(N >= 5 && N <= 8);
  if (!hook.site || memcmp(hook.site, preimage.data(), N) != 0)
    return false;
  hook.length = N;
  memcpy(hook.original.data(), hook.site, N);
  intptr_t delta = reinterpret_cast<intptr_t>(replacement) -
                   (reinterpret_cast<intptr_t>(hook.site) + 5);
  if (delta < INT32_MIN || delta > INT32_MAX)
    return false;
  std::array<unsigned char, N> patch{};
  patch.fill(0x90);
  patch[0] = 0xE9;
  auto relative = static_cast<int32_t>(delta);
  memcpy(patch.data() + 1, &relative, 4);
  DWORD old = 0;
  if (!VirtualProtect(hook.site, N, PAGE_EXECUTE_READWRITE, &old))
    return false;
  memcpy(hook.site, patch.data(), N);
  const bool flushed =
      FlushInstructionCache(GetCurrentProcess(), hook.site, N) != FALSE;
  DWORD ignored = 0;
  VirtualProtect(hook.site, N, old, &ignored);
  return flushed && memcmp(hook.site, patch.data(), N) == 0;
}
int __cdecl SyntheticHookGate() {
  g_syntheticInvocations.fetch_add(1);
  return g_syntheticTrampoline ? g_syntheticTrampoline() : -1;
}
bool CreateTrampoline(HookState &hook) noexcept {
  const size_t length = hook.length;
  auto *trampoline = static_cast<unsigned char *>(VirtualAlloc(
      nullptr, length + 5, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
  if (!trampoline)
    return false;
  memcpy(trampoline, hook.site, length);
  trampoline[length] = 0xE9;
  const intptr_t delta = reinterpret_cast<unsigned char *>(hook.site) + length -
                         (trampoline + length + 5);
  if (delta < INT32_MIN || delta > INT32_MAX) {
    VirtualFree(trampoline, 0, MEM_RELEASE);
    return false;
  }
  const auto relative = static_cast<int32_t>(delta);
  memcpy(trampoline + length + 1, &relative, sizeof(relative));
  if (!FlushInstructionCache(GetCurrentProcess(), trampoline, length + 5)) {
    VirtualFree(trampoline, 0, MEM_RELEASE);
    return false;
  }
  DWORD old = 0;
  if (!VirtualProtect(trampoline, 10, PAGE_EXECUTE_READ, &old)) {
    VirtualFree(trampoline, 0, MEM_RELEASE);
    return false;
  }
  hook.trampoline = trampoline;
  return true;
}
bool PatchPointer(void **location, void *replacement,
                  void **original) noexcept {
  DWORD old = 0;
  if (!VirtualProtect(location, sizeof(void *), PAGE_READWRITE, &old))
    return false;
  *original = *location;
  *location = replacement;
  DWORD ignored = 0;
  VirtualProtect(location, sizeof(void *), old, &ignored);
  return true;
}
void Restore() noexcept {
  if (g_connectIat && g_connect) {
    void *ignored = nullptr;
    PatchPointer(g_connectIat, reinterpret_cast<void *>(g_connect), &ignored);
  }
  if (g_sendIat && g_send) {
    void *ignored = nullptr;
    PatchPointer(g_sendIat, reinterpret_cast<void *>(g_send), &ignored);
  }
  for (HookState *hook : {&g_outgoingHook,&g_processedHook,&g_controlTextHook, &g_submitHook}) {
    if (!hook->site)
      continue;
    DWORD old = 0;
    if (VirtualProtect(hook->site, hook->length, PAGE_EXECUTE_READWRITE, &old)) {
      memcpy(hook->site, hook->original.data(), hook->length);
      FlushInstructionCache(GetCurrentProcess(), hook->site, hook->length);
      DWORD ignored = 0;
      VirtualProtect(hook->site, hook->length, old, &ignored);
    }
    if (hook->trampoline)
      VirtualFree(hook->trampoline, 0, MEM_RELEASE);
    *hook = {};
  }
}
DWORD Fail(const RevoraObserverInit *init, const char *category,
           DWORD code) noexcept {
  char line[160]{};
  sprintf_s(line, "{\"event\":\"bridge_init_failed\",\"category\":\"%s\"}\n",
            category);
  WriteLine(line);
  (void)init;
  return code;
}
struct SyntheticClientString {
  char inlineValue[16]{};
  unsigned length = 0;
  unsigned capacity = 15;
};
void *g_syntheticDialog = nullptr;
SyntheticClientString g_syntheticUsername{}, g_syntheticPassword{};
enum class SyntheticCaptureOrder { normal, reversed, duplicate, usernameOnly,
                                   passwordOnly, malformed, crossThread,
                                   exception };
SyntheticCaptureOrder g_syntheticCaptureOrder = SyntheticCaptureOrder::normal;
volatile LONG g_syntheticControlException = 0;
DWORD WINAPI SyntheticWrongThread(void *parameter) {
  auto *base = static_cast<unsigned char *>(parameter);
  ControlTextGate(base + 0x0BB8, nullptr, &g_syntheticUsername);
  return ERROR_SUCCESS;
}
void __fastcall SyntheticSubmit(void *, void *) {
  auto *base = static_cast<unsigned char *>(g_syntheticDialog);
  if (g_syntheticCaptureOrder == SyntheticCaptureOrder::reversed) {
    ControlTextGate(base + 0x0EB0, nullptr, &g_syntheticPassword);
    ControlTextGate(base + 0x0BB8, nullptr, &g_syntheticUsername);
  } else if (g_syntheticCaptureOrder == SyntheticCaptureOrder::duplicate) {
    ControlTextGate(base + 0x0BB8, nullptr, &g_syntheticUsername);
    ControlTextGate(base + 0x0BB8, nullptr, &g_syntheticUsername);
  } else if (g_syntheticCaptureOrder == SyntheticCaptureOrder::usernameOnly) {
    ControlTextGate(base + 0x0BB8, nullptr, &g_syntheticUsername);
  } else if (g_syntheticCaptureOrder == SyntheticCaptureOrder::passwordOnly) {
    ControlTextGate(base + 0x0EB0, nullptr, &g_syntheticPassword);
  } else if (g_syntheticCaptureOrder == SyntheticCaptureOrder::malformed) {
    const unsigned saved = g_syntheticUsername.length;
    g_syntheticUsername.length = 300;
    ControlTextGate(base + 0x0BB8, nullptr, &g_syntheticUsername);
    g_syntheticUsername.length = saved;
  } else if (g_syntheticCaptureOrder == SyntheticCaptureOrder::crossThread) {
    HANDLE thread = CreateThread(nullptr, 0, &SyntheticWrongThread, base, 0,
                                 nullptr);
    if (thread) {
      WaitForSingleObject(thread, 2000);
      CloseHandle(thread);
    }
  } else if (g_syntheticCaptureOrder == SyntheticCaptureOrder::exception) {
    InterlockedExchange(&g_syntheticControlException, 1);
    ControlTextGate(base + 0x0BB8, nullptr, &g_syntheticUsername);
    InterlockedExchange(&g_syntheticControlException, 0);
  } else {
    ControlTextGate(base + 0x0BB8, nullptr, &g_syntheticUsername);
    ControlTextGate(base + 0x0EB0, nullptr, &g_syntheticPassword);
  }
}
__declspec(naked) void *SyntheticControlText() {
  __asm {
    cmp dword ptr [g_syntheticControlException], 0
    je normal
    xor eax, eax
    mov eax, dword ptr [eax]
  normal:
    mov eax, dword ptr [esp+4]
    ret 4
  }
}
int WSAAPI SyntheticSend(SOCKET, const char *, int length, int) {
  return length;
}
} // namespace
extern "C" __declspec(dllexport) DWORD WINAPI
RevoraBridgeInitialize(void *parameter) noexcept {
  const auto *init = static_cast<const RevoraObserverInit *>(parameter);
  if (!init || init->size != sizeof(*init) ||
      init->version != kRevoraObserverContractVersion)
    return ERROR_REVISION_MISMATCH;
  if (!ValidReport(init->reportPath))
    return ERROR_BAD_PATHNAME;
  if (wcslen(init->correlationId)!=32 || wcsspn(init->correlationId,L"0123456789ABCDEF")!=32)
    return ERROR_INVALID_DATA;
  const auto expectedName=std::wstring(L"native-login-single-entry-")+init->correlationId+L".jsonl";
  if(std::filesystem::path(init->reportPath).filename().wstring()!=expectedName)
    return ERROR_INVALID_DATA;
  for(size_t i=0;i<32;i++) g_correlation[i]=static_cast<char>(init->correlationId[i]);
  g_correlation[32]='\0'; g_targetPid=GetCurrentProcessId();
  HANDLE created = CreateFileW(
      init->reportPath, GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_NEW,
      FILE_ATTRIBUTE_NORMAL | FILE_FLAG_WRITE_THROUGH, nullptr);
  if (created == INVALID_HANDLE_VALUE)
    return ERROR_WRITE_FAULT;
  CloseHandle(created);
  if(wcscpy_s(g_reportPath,init->reportPath)!=0)return ERROR_BAD_PATHNAME;
  g_synthetic = (init->flags & kRevoraObserverSynthetic) != 0;
  WriteLine("{\"event\":\"login_bridge_loaded\",\"result\":\"success\",\"profile\":\"dx9\"}\n");
  HMODULE module = GetModuleHandleW(nullptr);
  wchar_t image[MAX_PATH]{};
  if (!module || !GetModuleFileNameW(module, image, MAX_PATH))
    return Fail(init, "process_identity", ERROR_BAD_EXE_FORMAT);
  std::filesystem::path executable(image);
  if (g_synthetic) {
    if (_wcsicmp(executable.filename().c_str(), L"RevoraSyntheticTarget.exe"))
      return Fail(init, "process_identity", ERROR_BAD_EXE_FORMAT);
    g_submit = reinterpret_cast<SubmitFn>(&SyntheticSubmit);
    g_controlText = reinterpret_cast<ControlTextFn>(&SyntheticControlText);
    g_send = &SyntheticSend;
  } else {
    if (_wcsicmp(executable.filename().c_str(), L"Conquer.exe") ||
        _wcsicmp(executable.parent_path().filename().c_str(), L"Env_DX9") ||
        !ApprovedExecutable(image))
      return Fail(init, "process_identity", ERROR_BAD_EXE_FORMAT);
    WriteLine("{\"event\":\"executable_verified\",\"profile\":\"dx9\"}\n");
    auto base = reinterpret_cast<unsigned char *>(module);
    g_submitHook.site = base + kSubmitRva;
    g_controlTextHook.site = base + kControlTextRva;
    g_controlTextHook.length = kControlTextPreimage.size();
    g_sendIat = reinterpret_cast<void **>(base + kSendIatRva);
    g_connectIat = reinterpret_cast<void **>(base + kConnectIatRva);
    g_processedHook.site=base+kProcessed1637Rva;
    g_outgoingHook.site=base+kOutgoingPacketRva;
    WriteLine("{\"event\":\"target_resolved\",\"address_kind\":\"submit_control_text_and_world_rvas\"}\n");
    if (memcmp(g_submitHook.site, kSubmitPreimage.data(), 5) ||
        memcmp(g_controlTextHook.site, kControlTextPreimage.data(),
               kControlTextPreimage.size()) ||
        memcmp(g_processedHook.site,kProcessed1637Preimage.data(),5) ||
        memcmp(g_outgoingHook.site,kOutgoingPacketPreimage.data(),5))
      return Fail(init, "preimage_mismatch", ERROR_REVISION_MISMATCH);
    WriteLine("{\"event\":\"target_preimage_verified\",\"result\":\"match\"}\n");
    if (!CreateTrampoline(g_submitHook) || !CreateTrampoline(g_controlTextHook) ||
        !CreateTrampoline(g_processedHook) || !CreateTrampoline(g_outgoingHook))
      return Fail(init, "trampoline_initialization", ERROR_WRITE_FAULT);
    g_submit = reinterpret_cast<SubmitFn>(g_submitHook.trampoline);
    g_controlText = reinterpret_cast<ControlTextFn>(g_controlTextHook.trampoline);
    g_processed=reinterpret_cast<ProcessedFn>(g_processedHook.trampoline);
    g_outgoing=reinterpret_cast<OutgoingFn>(g_outgoingHook.trampoline);
    void *original = nullptr;
    if (!PatchPointer(g_sendIat, reinterpret_cast<void *>(&SendGate),
                      &original))
      return Fail(init, "send_boundary", ERROR_WRITE_FAULT);
    g_send = reinterpret_cast<SendFn>(original);
    void *originalConnect = nullptr;
    if (!PatchPointer(g_connectIat, reinterpret_cast<void *>(&ConnectGate),
                      &originalConnect)) {
      Restore();
      return Fail(init, "connect_boundary", ERROR_WRITE_FAULT);
    }
    g_connect = reinterpret_cast<ConnectFn>(originalConnect);
    WriteLine("{\"event\":\"hook_install_started\",\"profile\":\"dx9\"}\n");
    if (!PatchJump(g_processedHook,reinterpret_cast<void*>(&Processed1637Gate),kProcessed1637Preimage) ||
        !PatchJump(g_outgoingHook,reinterpret_cast<void*>(&OutgoingPacketGate),kOutgoingPacketPreimage) ||
        !PatchJump(g_controlTextHook,
                   reinterpret_cast<void *>(&ControlTextGate),
                   kControlTextPreimage) ||
        !PatchJump(g_submitHook, reinterpret_cast<void *>(&SubmitGate),
                   kSubmitPreimage)) {
      Restore();
      return Fail(init, "active_login_boundary", ERROR_WRITE_FAULT);
    }
    WriteLine("{\"event\":\"hook_install_succeeded\",\"profile\":\"dx9\"}\n");
    WriteLine("{\"event\":\"hook_postimage_verified\",\"result\":\"match\"}\n");
  }
  WriteLine("{\"event\":\"login_bridge_ready\",\"result\":\"success\",\"profile\":\"dx9\"}\n");
  return ERROR_SUCCESS;
}
extern "C" __declspec(dllexport) DWORD WINAPI
RevoraBridgeSyntheticExercise(void *) noexcept {
  if (!g_synthetic)
    return ERROR_INVALID_STATE;
  std::array<char, 440> packet{};
  unsigned char dialog{};
  g_syntheticDialog = &dialog;
  strcpy_s(g_syntheticUsername.inlineValue, "SyntheticUser");
  g_syntheticUsername.length = 13;
  g_syntheticUsername.capacity = 15;
  strcpy_s(g_syntheticPassword.inlineValue, "SyntheticPass");
  g_syntheticPassword.length = 13;
  g_syntheticPassword.capacity = 15;
  SubmitGate(g_syntheticDialog, nullptr);
  SubmitGate(g_syntheticDialog, nullptr);
  SecureZeroMemory(&g_syntheticUsername, sizeof(g_syntheticUsername));
  SecureZeroMemory(&g_syntheticPassword, sizeof(g_syntheticPassword));
  g_syntheticDialog = nullptr;
  return SendGate(INVALID_SOCKET, packet.data(),
                  static_cast<int>(packet.size()),
                  0) == static_cast<int>(packet.size())
             ? ERROR_SUCCESS
             : ERROR_INVALID_DATA;
}
extern "C" __declspec(dllexport) DWORD WINAPI
RevoraBridgeSyntheticCredentialBoundaryValidation(void *) noexcept {
  if (!g_synthetic)
    return ERROR_INVALID_STATE;
  unsigned char dialog{};
  g_syntheticDialog = &dialog;
  strcpy_s(g_syntheticUsername.inlineValue, "SyntheticUser");
  g_syntheticUsername.length = 13;
  g_syntheticUsername.capacity = 15;
  strcpy_s(g_syntheticPassword.inlineValue, "SyntheticPass");
  g_syntheticPassword.length = 13;
  g_syntheticPassword.capacity = 15;
  // A getter call outside an explicit submit cannot arm or retain state.
  ControlTextGate(&dialog + 0x0BB8, nullptr, &g_syntheticUsername);
  if (g_attempt.active || g_attempt.capturing)
    return ERROR_INVALID_STATE;
  for (const auto order : {SyntheticCaptureOrder::normal,
                           SyntheticCaptureOrder::reversed}) {
    g_syntheticCaptureOrder = order;
    SubmitGate(g_syntheticDialog, nullptr);
    if (!g_attempt.active || g_attempt.capturing ||
        !g_attempt.usernameCaptured || !g_attempt.passwordCaptured)
      return ERROR_INVALID_DATA;
    ZeroAttempt();
  }
  for (const auto order : {SyntheticCaptureOrder::duplicate,
                           SyntheticCaptureOrder::usernameOnly,
                           SyntheticCaptureOrder::passwordOnly,
                           SyntheticCaptureOrder::malformed,
                           SyntheticCaptureOrder::crossThread,
                           SyntheticCaptureOrder::exception}) {
    g_syntheticCaptureOrder = order;
    SubmitGate(g_syntheticDialog, nullptr);
    if (g_attempt.active || g_attempt.capturing || g_inFlight.load())
      return ERROR_INVALID_DATA;
  }
  // A new explicit submission succeeds after every fail-closed partial attempt.
  g_syntheticCaptureOrder = SyntheticCaptureOrder::normal;
  SubmitGate(g_syntheticDialog, nullptr);
  const bool recovered = g_attempt.active && !g_attempt.capturing;
  ZeroAttempt();
  SecureZeroMemory(&g_syntheticUsername, sizeof(g_syntheticUsername));
  SecureZeroMemory(&g_syntheticPassword, sizeof(g_syntheticPassword));
  g_syntheticDialog = nullptr;
  if (!recovered)
    return ERROR_INVALID_DATA;
  WriteLine("{\"event\":\"synthetic_credential_boundary_validation\",\"result\":\"success\"}\n");
  return ERROR_SUCCESS;
}
extern "C" __declspec(dllexport) DWORD WINAPI
RevoraBridgeSyntheticHookValidation(void *) noexcept {
  if (!g_synthetic) return ERROR_INVALID_STATE;
  constexpr SIZE_T imageSize = 0x00800000;
  constexpr std::array<unsigned char, 14> functionBytes{
      0x68, 0x0C, 0x01, 0x00, 0x00, // explicit Login command preimage
      0x83, 0xC4, 0x04,             // balance overwritten push
      0xB8, 0x2A, 0x00, 0x00, 0x00, // return 42
      0xC3};
  constexpr std::array<uintptr_t, 2> requestedBases{0x20000000, 0x30000000};
  for (const uintptr_t requested : requestedBases) {
    auto *image = static_cast<unsigned char *>(VirtualAlloc(
        reinterpret_cast<void *>(requested), imageSize,
        MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE));
    if (!image) return ERROR_NOT_ENOUGH_MEMORY;
    auto *site = image + kSubmitRva;
    memcpy(site, functionBytes.data(), functionBytes.size());
    DWORD previous = 0;
    if (!VirtualProtect(image, imageSize, PAGE_EXECUTE_READ, &previous)) {
      VirtualFree(image, 0, MEM_RELEASE); return ERROR_WRITE_FAULT;
    }
    g_submitHook.site = site;
    if (!CreateTrampoline(g_submitHook)) {
      VirtualFree(image, 0, MEM_RELEASE); return ERROR_WRITE_FAULT;
    }
    g_syntheticTrampoline =
        reinterpret_cast<SyntheticHookFn>(g_submitHook.trampoline);
    g_syntheticInvocations.store(0);
    if (!PatchJump(g_submitHook, reinterpret_cast<void *>(&SyntheticHookGate),
                   kSubmitPreimage) || site[0] != 0xE9) {
      Restore(); VirtualFree(image, 0, MEM_RELEASE); return ERROR_WRITE_FAULT;
    }
    MEMORY_BASIC_INFORMATION memory{};
    if (!VirtualQuery(site, &memory, sizeof(memory)) || memory.Protect != PAGE_EXECUTE_READ) {
      Restore(); VirtualFree(image, 0, MEM_RELEASE); return ERROR_INVALID_ACCESS;
    }
    const auto function = reinterpret_cast<SyntheticHookFn>(site);
    uintptr_t stackBefore = 0, stackAfter = 0;
    __asm mov stackBefore, esp
    const int result = function();
    __asm mov stackAfter, esp
    if (result != 42 || stackBefore != stackAfter || g_syntheticInvocations.load() != 1) {
      Restore(); VirtualFree(image, 0, MEM_RELEASE); return ERROR_INVALID_DATA;
    }
    Restore();
    if (memcmp(site, functionBytes.data(), 5) != 0) {
      VirtualFree(image, 0, MEM_RELEASE); return ERROR_INVALID_DATA;
    }
    DWORD writable = 0;
    VirtualProtect(image, imageSize, PAGE_READWRITE, &writable);
    site[0] ^= 1;
    DWORD ignored = 0;
    VirtualProtect(image, imageSize, PAGE_EXECUTE_READ, &ignored);
    g_submitHook.site = site;
    if (PatchJump(g_submitHook, reinterpret_cast<void *>(&SyntheticHookGate),
                  kSubmitPreimage)) {
      VirtualFree(image, 0, MEM_RELEASE); return ERROR_REVISION_MISMATCH;
    }
    VirtualFree(image, 0, MEM_RELEASE);
    g_submitHook = {};
    g_syntheticTrampoline = nullptr;
  }
  constexpr std::array<unsigned char, 16> sevenByteFunction{
      0x6A, 0x04, 0xB8, 0xDC, 0x60, 0xF5, 0x00,
      0x83, 0xC4, 0x04, 0xB8, 0x2A, 0x00, 0x00, 0x00, 0xC3};
  auto *sevenByteImage = static_cast<unsigned char *>(VirtualAlloc(
      reinterpret_cast<void *>(0x31000000), 0x00600000,
      MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE));
  if (!sevenByteImage)
    return ERROR_NOT_ENOUGH_MEMORY;
  auto *sevenByteSite = sevenByteImage + kControlTextRva;
  memcpy(sevenByteSite, sevenByteFunction.data(), sevenByteFunction.size());
  DWORD sevenBytePrevious = 0;
  if (!VirtualProtect(sevenByteImage, 0x00600000, PAGE_EXECUTE_READ,
                      &sevenBytePrevious)) {
    VirtualFree(sevenByteImage, 0, MEM_RELEASE);
    return ERROR_WRITE_FAULT;
  }
  g_controlTextHook.site = sevenByteSite;
  g_controlTextHook.length = kControlTextPreimage.size();
  if (!CreateTrampoline(g_controlTextHook)) {
    VirtualFree(sevenByteImage, 0, MEM_RELEASE);
    return ERROR_WRITE_FAULT;
  }
  g_syntheticTrampoline =
      reinterpret_cast<SyntheticHookFn>(g_controlTextHook.trampoline);
  g_syntheticInvocations.store(0);
  if (!PatchJump(g_controlTextHook, reinterpret_cast<void *>(&SyntheticHookGate),
                 kControlTextPreimage)) {
    Restore();
    VirtualFree(sevenByteImage, 0, MEM_RELEASE);
    return ERROR_WRITE_FAULT;
  }
  const int sevenByteResult =
      reinterpret_cast<SyntheticHookFn>(sevenByteSite)();
  if (sevenByteResult != 42 || g_syntheticInvocations.load() != 1) {
    Restore();
    VirtualFree(sevenByteImage, 0, MEM_RELEASE);
    return ERROR_INVALID_DATA;
  }
  Restore();
  if (memcmp(sevenByteSite, sevenByteFunction.data(),
             kControlTextPreimage.size()) != 0) {
    VirtualFree(sevenByteImage, 0, MEM_RELEASE);
    return ERROR_INVALID_DATA;
  }
  VirtualFree(sevenByteImage, 0, MEM_RELEASE);
  g_controlTextHook = {};
  g_syntheticTrampoline = nullptr;
  WriteLine("{\"event\":\"synthetic_hook_validation\",\"result\":\"success\",\"bases\":2}\n");
  return ERROR_SUCCESS;
}
extern "C" __declspec(dllexport) DWORD WINAPI
RevoraBridgeCleanup(void *) noexcept {
  Restore();
  ZeroAttempt();
  WriteLine("{\"event\":\"process_shutdown\",\"profile\":\"dx9\"}\n");
  SecureZeroMemory(g_reportPath,sizeof(g_reportPath));
  return ERROR_SUCCESS;
}
BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, void *) noexcept {
  if (reason == DLL_PROCESS_ATTACH)
    DisableThreadLibraryCalls(instance);
  return TRUE;
}
