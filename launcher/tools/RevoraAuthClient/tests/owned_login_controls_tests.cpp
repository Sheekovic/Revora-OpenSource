#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <array>
#include <cstring>
#include <iostream>
#include "../src/owned_login_controls.h"
extern "C" void __fastcall RevoraAuthOwnedControlsTick(void*,void*);
extern "C" void __stdcall RevoraDirectDiagnostic(const char*,const char*){}
int main(){
    std::array<unsigned char,0xa900> dialog{};
    HWND parent=CreateWindowExW(0,L"STATIC",L"Synthetic owned-control test",WS_OVERLAPPEDWINDOW,
        0,0,600,400,nullptr,nullptr,GetModuleHandleW(nullptr),nullptr);
    if(!parent)return 1;
    *reinterpret_cast<HWND*>(dialog.data()+0x20)=parent;
    auto child=[&](UINT_PTR id,int y){return CreateWindowExW(0,L"BUTTON",L"",WS_CHILD|WS_VISIBLE,30,y,200,25,
        parent,reinterpret_cast<HMENU>(id),GetModuleHandleW(nullptr),nullptr);};
    auto edit=[&](int id,int y){return CreateWindowExW(0,L"EDIT",L"",WS_CHILD,30,y,0,0,parent,
        reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),GetModuleHandleW(nullptr),nullptr);};
    *reinterpret_cast<HWND*>(dialog.data()+0xbd8)=edit(5694,30);
    HWND legacyUser=child(6001,30);
    *reinterpret_cast<HWND*>(dialog.data()+0xed0)=edit(3482,70);
    HWND legacyPassword=child(6002,70);
    child(5680,110);
    RevoraAuthOwnedControlsTick(dialog.data(),nullptr);
    HWND user=GetDlgItem(parent,0x7e01),password=GetDlgItem(parent,0x7e02);
    if(!user||!password||IsWindowEnabled(legacyUser)||IsWindowEnabled(legacyPassword))return 2;
    if(!(GetWindowLongPtrW(password,GWL_STYLE)&ES_PASSWORD))return 3;
    SetWindowTextW(user,L"SyntheticUser");SetWindowTextW(password,L"SyntheticOnly");
    std::array<char,33> u{};std::array<char,1025> p{};
    if(!RevoraOwnedReadPair(dialog.data(),u.data(),u.size(),p.data(),p.size())||strcmp(p.data(),"SyntheticOnly"))return 4;
    if(RevoraOwnedReadPair(dialog.data(),u.data(),u.size(),p.data(),p.size()))return 5;
    RevoraOwnedClearPassword();RevoraOwnedSetBusy(false);
    if(GetWindowTextLengthW(password)||SendMessageW(password,EM_CANUNDO,0,0))return 6;
    SetWindowTextW(password,L"A\x00e9\x4e2d");
    if(!RevoraOwnedReadPair(dialog.data(),u.data(),u.size(),p.data(),p.size())||strcmp(p.data(),"A\xc3\xa9\xe4\xb8\xad"))return 7;
    RevoraOwnedClearPassword();RevoraOwnedSetBusy(false);
    SetWindowTextW(password,L"abc");SendMessageW(password,EM_SETSEL,1,2);
    SendMessageW(password,EM_REPLACESEL,FALSE,reinterpret_cast<LPARAM>(L"Z"));
    if(!RevoraOwnedReadPair(dialog.data(),u.data(),u.size(),p.data(),p.size())||strcmp(p.data(),"aZc"))return 8;
    RevoraOwnedClearPassword();RevoraOwnedSetBusy(false);
    wchar_t malformed[]{0xd800,0};SetWindowTextW(password,malformed);
    if(RevoraOwnedReadPair(dialog.data(),u.data(),u.size(),p.data(),p.size()))return 9;
    if(GetWindowTextLengthW(password))return 10;
    SecureZeroMemory(p.data(),p.size());SecureZeroMemory(u.data(),u.size());
    DestroyWindow(parent);
    std::cout<<"Owned controls: masking, actual text, Unicode, selection, busy isolation and clearing passed.\n";
    return 0;
}
