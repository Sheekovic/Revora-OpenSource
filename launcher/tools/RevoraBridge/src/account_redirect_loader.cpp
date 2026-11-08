#include "bridge_contract.h"
#include <windows.h>
#include <filesystem>
#include <string>
#include <vector>

namespace { struct Handle{HANDLE h{nullptr};~Handle(){if(h&&h!=INVALID_HANDLE_VALUE)CloseHandle(h);}operator HANDLE()const{return h;}};
DWORD Call(HANDLE process,uintptr_t address,void *argument){Handle thread{CreateRemoteThread(process,nullptr,0,reinterpret_cast<LPTHREAD_START_ROUTINE>(address),argument,0,nullptr)};if(!thread.h)return ERROR_ACCESS_DENIED;if(WaitForSingleObject(thread,10000)!=WAIT_OBJECT_0)return ERROR_TIMEOUT;DWORD value=0;return GetExitCodeThread(thread,&value)?value:ERROR_READ_FAULT;}
}
int wmain(int argc,wchar_t **argv){
  if(argc!=9||std::wstring(argv[1])!=L"--pid"||std::wstring(argv[3])!=L"--target"||std::wstring(argv[5])!=L"--pipe"||std::wstring(argv[7])!=L"--report")return 2;
  DWORD pid=std::stoul(argv[2]);Handle process{OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION|PROCESS_CREATE_THREAD|PROCESS_VM_OPERATION|PROCESS_VM_WRITE|PROCESS_VM_READ,FALSE,pid)};if(!process.h)return 3;
  wchar_t actual[32768]{};DWORD count=static_cast<DWORD>(std::size(actual));if(!QueryFullProcessImageNameW(process,0,actual,&count)||_wcsicmp(std::filesystem::path(actual).lexically_normal().c_str(),std::filesystem::path(argv[4]).lexically_normal().c_str()))return 4;
  std::filesystem::path dll=std::filesystem::path(argv[0]).parent_path()/L"RevoraAccountRedirect.dll";std::wstring dllText=std::filesystem::absolute(dll).wstring();SIZE_T bytes=(dllText.size()+1)*sizeof(wchar_t);void *remote=VirtualAllocEx(process,nullptr,bytes,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);if(!remote||!WriteProcessMemory(process,remote,dllText.c_str(),bytes,nullptr))return 5;
  auto load=reinterpret_cast<uintptr_t>(GetProcAddress(GetModuleHandleW(L"kernel32.dll"),"LoadLibraryW"));DWORD remoteBase=Call(process,load,remote);VirtualFreeEx(process,remote,0,MEM_RELEASE);if(!remoteBase)return 6;
  HMODULE local=LoadLibraryExW(dllText.c_str(),nullptr,DONT_RESOLVE_DLL_REFERENCES);if(!local)return 7;auto init=GetProcAddress(local,"RevoraAccountRedirectInitialize");uintptr_t rva=reinterpret_cast<uintptr_t>(init)-reinterpret_cast<uintptr_t>(local);FreeLibrary(local);if(!init)return 8;
  RevoraObserverInit contract{};contract.size=sizeof(contract);contract.version=kRevoraObserverContractVersion;if(wcscpy_s(contract.channel,argv[6])||wcscpy_s(contract.reportPath,argv[8]))return 9;
  void *remoteContract=VirtualAllocEx(process,nullptr,sizeof(contract),MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);if(!remoteContract||!WriteProcessMemory(process,remoteContract,&contract,sizeof(contract),nullptr))return 9;DWORD result=Call(process,remoteBase+rva,remoteContract);VirtualFreeEx(process,remoteContract,0,MEM_RELEASE);return result==ERROR_SUCCESS?0:10;
}
