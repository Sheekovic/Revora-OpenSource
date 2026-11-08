#include "bridge_contract.h"
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <windows.h>

namespace { using BridgeCall = DWORD(WINAPI *)(void *); }

int wmain(int argc, wchar_t **argv) {
  if (argc != 3 || std::wstring(argv[1]) != L"--self-test") return 2;
  const std::filesystem::path report = std::filesystem::absolute(argv[2]);
  HMODULE bridge = LoadLibraryW((std::filesystem::path(argv[0]).parent_path() /
                                 L"RevoraNativeLogin.dll").c_str());
  if (!bridge) return 4;
  auto initialize = reinterpret_cast<BridgeCall>(GetProcAddress(bridge, "RevoraBridgeInitialize"));
  auto exercise = reinterpret_cast<BridgeCall>(GetProcAddress(bridge, "RevoraBridgeSyntheticExercise"));
  auto credentialBoundary = reinterpret_cast<BridgeCall>(GetProcAddress(bridge, "RevoraBridgeSyntheticCredentialBoundaryValidation"));
  auto hookValidation = reinterpret_cast<BridgeCall>(GetProcAddress(bridge, "RevoraBridgeSyntheticHookValidation"));
  auto cleanup = reinterpret_cast<BridgeCall>(GetProcAddress(bridge, "RevoraBridgeCleanup"));
  RevoraObserverInit init{}; init.size = sizeof(init);
  init.version = kRevoraObserverContractVersion; init.flags = kRevoraObserverSynthetic;
  wcscpy_s(init.reportPath, report.c_str());
  const std::wstring name=report.filename().wstring();
  if(name.size()!=64 || name.rfind(L"native-login-single-entry-",0)!=0) return 11;
  wcscpy_s(init.correlationId, name.substr(26,32).c_str());
  const DWORD initialized = initialize ? initialize(&init) : ERROR_PROC_NOT_FOUND;
  if (initialized != ERROR_SUCCESS) { std::cerr << "initialize=" << initialized << "\n"; return 5; }
  std::ifstream activeReader(report, std::ios::binary);
  const std::string before((std::istreambuf_iterator<char>(activeReader)), {});
  if (before.find("login_bridge_ready") == std::string::npos) return 6;
  std::cout << "diagnostic_concurrency PASS\n";
  if (!exercise || exercise(nullptr) != ERROR_SUCCESS) return 7;
  std::cout << "login_state PASS\n";
  if (!credentialBoundary || credentialBoundary(nullptr) != ERROR_SUCCESS) return 12;
  std::cout << "credential_boundary_matrix PASS\n";
  if (!hookValidation || hookValidation(nullptr) != ERROR_SUCCESS) return 10;
  std::cout << "hook_trampoline_multi_base PASS\n";
  if (!cleanup || cleanup(nullptr) != ERROR_SUCCESS) return 8;
  FreeLibrary(bridge);
  std::ifstream finalReader(report, std::ios::binary);
  const std::string finalText((std::istreambuf_iterator<char>(finalReader)), {});
  const bool passed = finalText.find("login_submit_entered") != std::string::npos &&
         finalText.find("https_started") != std::string::npos &&
         finalText.find("synthetic_hook_validation") != std::string::npos &&
         finalText.find("process_shutdown") != std::string::npos;
  if (passed) std::cout << "REVORA_NATIVE_SYNTHETIC_RUNTIME_VALIDATION PASSED\n";
  return passed ? 0 : 9;
}
