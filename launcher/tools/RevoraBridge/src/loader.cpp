#include <windows.h>
#include <bcrypt.h>
#include <tlhelp32.h>
#include <array>
#include <charconv>
#include <filesystem>
#include <iostream>
#include <string>
#include <vector>
#include "bridge_contract.h"

namespace {
struct Handle { HANDLE value{nullptr}; ~Handle(){ if(value && value != INVALID_HANDLE_VALUE) CloseHandle(value); } operator HANDLE() const{return value;} };

std::wstring FullPath(const std::wstring& path) {
    std::vector<wchar_t> buffer(32768);
    DWORD count = GetFullPathNameW(path.c_str(), static_cast<DWORD>(buffer.size()), buffer.data(), nullptr);
    if (!count || count >= buffer.size()) throw std::runtime_error("invalid path");
    return std::wstring(buffer.data(), count);
}

std::string Hex(const std::array<unsigned char, 32>& bytes) {
    constexpr char alphabet[]="0123456789ABCDEF"; std::string result(64, '0');
    for(size_t i=0;i<bytes.size();++i){result[i*2]=alphabet[bytes[i]>>4];result[i*2+1]=alphabet[bytes[i]&15];}
    return result;
}

std::string Sha256(const std::wstring& path) {
    Handle file{CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN, nullptr)};
    if(file.value==INVALID_HANDLE_VALUE) throw std::runtime_error("file open failed");
    BCRYPT_ALG_HANDLE algorithm{}; BCRYPT_HASH_HANDLE hash{};
    if(BCryptOpenAlgorithmProvider(&algorithm,BCRYPT_SHA256_ALGORITHM,nullptr,0)!=0) throw std::runtime_error("sha provider failed");
    DWORD objectSize=0,cb=0; BCryptGetProperty(algorithm,BCRYPT_OBJECT_LENGTH,reinterpret_cast<PUCHAR>(&objectSize),sizeof(objectSize),&cb,0);
    std::vector<unsigned char> object(objectSize), chunk(1024*1024); std::array<unsigned char,32> digest{};
    if(BCryptCreateHash(algorithm,&hash,object.data(),objectSize,nullptr,0,0)!=0){BCryptCloseAlgorithmProvider(algorithm,0);throw std::runtime_error("sha init failed");}
    DWORD read=0; while(ReadFile(file,chunk.data(),static_cast<DWORD>(chunk.size()),&read,nullptr)&&read>0)
        if(BCryptHashData(hash,chunk.data(),read,0)!=0) throw std::runtime_error("sha update failed");
    NTSTATUS status=BCryptFinishHash(hash,digest.data(),static_cast<ULONG>(digest.size()),0);
    BCryptDestroyHash(hash); BCryptCloseAlgorithmProvider(algorithm,0);
    if(status!=0) throw std::runtime_error("sha finish failed"); return Hex(digest);
}

uintptr_t RemoteModule(DWORD pid, const wchar_t* name) {
    Handle snapshot{CreateToolhelp32Snapshot(TH32CS_SNAPMODULE|TH32CS_SNAPMODULE32,pid)};
    MODULEENTRY32W entry{sizeof(entry)}; if(snapshot.value==INVALID_HANDLE_VALUE||!Module32FirstW(snapshot,&entry)) return 0;
    do { if(_wcsicmp(entry.szModule,name)==0) return reinterpret_cast<uintptr_t>(entry.modBaseAddr); } while(Module32NextW(snapshot,&entry));
    return 0;
}

DWORD RemoteCall(HANDLE process, uintptr_t function, void* parameter) {
    Handle thread{CreateRemoteThread(process,nullptr,0,reinterpret_cast<LPTHREAD_START_ROUTINE>(function),parameter,0,nullptr)};
    if(!thread.value) throw std::runtime_error("remote call failed");
    if(WaitForSingleObject(thread,10000)!=WAIT_OBJECT_0) throw std::runtime_error("remote call timeout");
    DWORD result=0; if(!GetExitCodeThread(thread,&result)) throw std::runtime_error("remote result failed"); return result;
}
}

int wmain(int argc, wchar_t** argv) {
    try {
        if(argc!=9 || std::wstring(argv[1])!=L"--pid" || std::wstring(argv[3])!=L"--target"
            || std::wstring(argv[5])!=L"--report" || std::wstring(argv[7])!=L"--correlation") return 2;
        DWORD pid=std::stoul(argv[2]); std::wstring expected=FullPath(argv[4]);
        std::wstring report=FullPath(argv[6]);
        const std::wstring correlation=argv[8];
        const auto filename=std::filesystem::path(report).filename().wstring();
        const auto approvedDiagnostics=std::filesystem::path(expected).parent_path().parent_path()/L"RevoraDiagnostics";
        if(report.size()>=1024 || correlation.size()!=32 ||
           filename!=L"native-login-single-entry-"+correlation+L".jsonl" ||
           correlation.find_first_not_of(L"0123456789ABCDEF")!=std::wstring::npos ||
           _wcsicmp(std::filesystem::path(report).parent_path().c_str(),FullPath(approvedDiagnostics.wstring()).c_str())!=0 ||
           std::filesystem::is_symlink(std::filesystem::symlink_status(std::filesystem::path(report).parent_path()))) return 3;
#ifndef REVORA_SYNTHETIC_TEST
        constexpr wchar_t approvedSuffix[]=LR"(\Env_DX9\Conquer.exe)";
        if(expected.size()<std::size(approvedSuffix)-1
            || _wcsicmp(expected.c_str()+expected.size()-(std::size(approvedSuffix)-1),approvedSuffix)!=0) return 5;
#endif
        Handle process{OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION|PROCESS_CREATE_THREAD|PROCESS_VM_OPERATION|PROCESS_VM_WRITE|PROCESS_VM_READ,false,pid)};
        if(!process.value) return 4;
        std::array<wchar_t,32768> image{}; DWORD imageSize=static_cast<DWORD>(image.size());
        if(!QueryFullProcessImageNameW(process,0,image.data(),&imageSize)||_wcsicmp(FullPath(image.data()).c_str(),expected.c_str())!=0) return 5;
        LARGE_INTEGER size{}; Handle target{CreateFileW(expected.c_str(),GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,0,nullptr)};
        if(target.value==INVALID_HANDLE_VALUE||!GetFileSizeEx(target,&size)) return 6;
#ifdef REVORA_SYNTHETIC_TEST
        wchar_t* testHash=nullptr; size_t testHashLength=0; _wdupenv_s(&testHash,&testHashLength,L"REVORA_BRIDGE_SYNTHETIC_SHA256");
        if(testHash==nullptr || Sha256(expected)!=std::filesystem::path(testHash).string()){free(testHash);return 7;}
        free(testHash);
#else
        constexpr long long approvedSize=18594824;
        constexpr char approvedHash[]="4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE";
        if(size.QuadPart!=approvedSize || Sha256(expected)!=approvedHash) return 7;
#endif

        std::wstring dll=FullPath((std::filesystem::path(argv[0]).parent_path()/L"RevoraNativeLogin.dll").wstring());
        SIZE_T dllBytes=(dll.size()+1)*sizeof(wchar_t); void* remoteDll=VirtualAllocEx(process,nullptr,dllBytes,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);
        if(!remoteDll||!WriteProcessMemory(process,remoteDll,dll.c_str(),dllBytes,nullptr)) return 8;
        auto loadLibrary=reinterpret_cast<uintptr_t>(GetProcAddress(GetModuleHandleW(L"kernel32.dll"),"LoadLibraryW"));
        DWORD remoteBase=RemoteCall(process,loadLibrary,remoteDll); VirtualFreeEx(process,remoteDll,0,MEM_RELEASE); if(!remoteBase)return 9;

        HMODULE local=LoadLibraryExW(dll.c_str(),nullptr,DONT_RESOLVE_DLL_REFERENCES); if(!local)return 10;
        auto initialize=GetProcAddress(local,"RevoraBridgeInitialize");
        auto cleanup=GetProcAddress(local,"RevoraBridgeCleanup");
        auto exercise=GetProcAddress(local,"RevoraBridgeSyntheticExercise");
        uintptr_t initRva=reinterpret_cast<uintptr_t>(initialize)-reinterpret_cast<uintptr_t>(local);
        uintptr_t cleanupRva=reinterpret_cast<uintptr_t>(cleanup)-reinterpret_cast<uintptr_t>(local);
        uintptr_t exerciseRva=reinterpret_cast<uintptr_t>(exercise)-reinterpret_cast<uintptr_t>(local);
        FreeLibrary(local);
        RevoraObserverInit contract{}; contract.size=sizeof(contract);contract.version=kRevoraObserverContractVersion;
#ifdef REVORA_SYNTHETIC_TEST
        contract.flags=kRevoraObserverSynthetic;
#endif
        if(wcscpy_s(contract.reportPath,report.c_str())!=0 || wcscpy_s(contract.correlationId,correlation.c_str())!=0)return 11;
        void* remoteContract=VirtualAllocEx(process,nullptr,sizeof(contract),MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);
        if(!remoteContract||!WriteProcessMemory(process,remoteContract,&contract,sizeof(contract),nullptr))return 11;
        DWORD initResult=RemoteCall(process,remoteBase+initRva,remoteContract);
        if(initResult!=ERROR_SUCCESS) std::cerr << "bridge initialization category=" << initResult << "\n";
        VirtualFreeEx(process,remoteContract,0,MEM_RELEASE);
#ifdef REVORA_SYNTHETIC_TEST
        if(initResult==ERROR_SUCCESS){if(RemoteCall(process,remoteBase+exerciseRva,nullptr)!=ERROR_SUCCESS)return 13;Sleep(250);if(RemoteCall(process,remoteBase+cleanupRva,nullptr)!=ERROR_SUCCESS)return 14;}
#else
        (void)cleanupRva;(void)exerciseRva;
#endif
        return initResult==ERROR_SUCCESS?0:12;
    } catch(...) { return 20; }
}
