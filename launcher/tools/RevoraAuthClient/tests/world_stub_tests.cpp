#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <array>
#include <fstream>
#include <vector>
#include <cstdio>
#include <cstdlib>
#include <cstring>
void require(bool b){if(!b)std::abort();}
int decision=0,calls=0;
unsigned char* expected=nullptr;
int __stdcall Mock(unsigned char* packet,unsigned long length,void* transport){
    require(packet==expected&&length==32&&transport==reinterpret_cast<void*>(0x123456));++calls;return decision;
}
int main(int argc,char** argv){
    require(argc==2);
    std::ifstream file(argv[1],std::ios::binary|std::ios::ate);require(file.good());
    const auto size=file.tellg();require(size>4096&&size<32*1024*1024);file.seekg(0);
    std::vector<unsigned char> image(static_cast<size_t>(size));file.read(reinterpret_cast<char*>(image.data()),size);require(file.good());
    auto dos=reinterpret_cast<const IMAGE_DOS_HEADER*>(image.data());require(dos->e_magic==IMAGE_DOS_SIGNATURE&&dos->e_lfanew>0&&dos->e_lfanew<2048);
    auto nt=reinterpret_cast<const IMAGE_NT_HEADERS32*>(image.data()+dos->e_lfanew);require(nt->Signature==IMAGE_NT_SIGNATURE&&nt->FileHeader.Machine==IMAGE_FILE_MACHINE_I386);
    auto section=IMAGE_FIRST_SECTION(nt);const unsigned char* source=nullptr;
    for(unsigned i=0;i<nt->FileHeader.NumberOfSections;++i)if(!memcmp(section[i].Name,".rvdirct",8)){
        require(section[i].PointerToRawData+256<image.size());source=image.data()+section[i].PointerToRawData+192;
    }
    require(source&&source[19]==0xff&&source[20]==0x15&&source[42]==0xe9);
    auto* code=static_cast<unsigned char*>(VirtualAlloc(nullptr,128,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE));require(code);
    memcpy(code,source,64);
    auto function=&Mock;auto slot=reinterpret_cast<uintptr_t>(&function);memcpy(code+21,&slot,4);
    const int relative=64-47;memcpy(code+43,&relative,4);
    const unsigned char continuation[]{0xb8,1,0,0,0,0x5b,0x8b,0xe5,0x5d,0xc2,4,0};memcpy(code+64,continuation,sizeof(continuation));
    DWORD previous=0;require(VirtualProtect(code,128,PAGE_EXECUTE_READ,&previous)!=0);FlushInstructionCache(GetCurrentProcess(),code,128);
    std::array<unsigned char,36> object{};object[4]=32;expected=object.data()+4;
    using Send=int(__thiscall*)(void*,void*);auto send=reinterpret_cast<Send>(code);
    for(int i=0;i<1000;++i){decision=i%2;require(send(reinterpret_cast<void*>(0x123456),object.data())==decision);}
    require(calls==1000);VirtualFree(code,0,MEM_RELEASE);
    puts("PASS exact generated x86 IAT stub: 1000 accepted/suppressed calls; ECX, stack, args, displaced instructions and return path preserved");
}
