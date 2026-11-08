#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <vector>
#include <cstdio>
#include <cstdlib>
#include "../src/game_entry_watchdog.h"
#include "../src/native_world_ui.h"
extern "C" __declspec(dllimport) int __stdcall RevoraAuthDirectPatch2429V2(unsigned char*,unsigned long,void*);
static void Require(bool ok){if(!ok)std::abort();}
int main(){
    NativeWorldUiGate ui;
    Require(!ui.Start(1,1013));
    ui.Begin(1);
    Require(!ui.Start(2,1013)&&!ui.Start(1,1010));
    Require(!ui.Confirm(1,1010,false,false));
    Require(ui.Start(1,1013)&&!ui.Start(1,1013));
    Require(!ui.Confirm(2,1010,false,false));
    Require(!ui.Confirm(1,1100,false,false));
    Require(!ui.Confirm(1,1010,true,false));
    Require(!ui.Confirm(1,1010,false,true));
    Require(ui.Confirm(1,1010,false,false));
    Require(!ui.Confirm(1,1010,false,false)&&!ui.Fail(1));
    ui.Begin(3);
    Require(ui.Start(3,1013)&&!ui.Fail(2)&&ui.Fail(3));
    Require(!ui.Fail(3)&&!ui.Confirm(3,1010,false,false));
    ui.Begin(4);
    Require(ui.Start(4,1013)&&ui.Confirm(4,1010,false,false));
    unsigned char action[]{6,0,0x33,8,0x60,74};
    Require(RevoraLocationRequested(action,sizeof(action)));
    for(size_t n=0;n<sizeof(action);++n)Require(!RevoraLocationRequested(action,n));
    action[5]=75;Require(!RevoraLocationRequested(action,sizeof(action)));
    // Invoke the real release export with no attempt/handoff state. In
    // particular the old >1024 guard must never classify 2205 as 2429.
    for(unsigned opcode: {2205u,1004u,1010u,2562u}){
        for(unsigned length: {4u,136u,472u,1024u,1025u,2048u,65535u}){
            std::vector<unsigned char> packet(length,0xA5);
            packet[0]=static_cast<unsigned char>(length);packet[1]=static_cast<unsigned char>(length>>8);
            packet[2]=static_cast<unsigned char>(opcode);packet[3]=static_cast<unsigned char>(opcode>>8);
            const auto before=packet;
            SetLastError(123);WSASetLastError(123);
            Require(RevoraAuthDirectPatch2429V2(packet.data(),length,nullptr)==1);
            Require(packet==before&&GetLastError()==123&&WSAGetLastError()==123);
        }
    }
    unsigned char login[8]{8,0,0x7d,9,0,0,0,0};
    Require(RevoraAuthDirectPatch2429V2(login,8,nullptr)==0);
    Require(RevoraAuthDirectPatch2429V2(login,8,nullptr)==0);
    // A rejected login must not poison later unrelated sends either.
    unsigned char creation[4]{4,0,0x9d,8};
    Require(RevoraAuthDirectPatch2429V2(creation,4,nullptr)==1);
    std::puts("PASS real export: 28 unchanged Game packets, missing/replayed login rejection, error preservation and post-rejection transparency");
}
