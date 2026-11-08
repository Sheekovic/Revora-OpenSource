#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <array>
#include <cstring>
#include <atomic>
#include "owned_login_controls.h"
#include "world_handoff.h"
#include "game_entry_watchdog.h"
#include "native_world_ui.h"

extern "C" int __stdcall RevoraCreateDirectProof(const char*,const char*,unsigned char*,unsigned long);
extern "C" void __stdcall RevoraDirectDiagnostic(const char*,const char*);

namespace {
volatile LONG stage=0; // 0 idle; 1 connect/seed; 2 HTTPS; 3 queued; 4 handoff; 5 sent.
DWORD threadId=0;
void* activeDialog=nullptr;
UINT_PTR timer=0;
ULONGLONG deadline=0,handleDeadline=0;
WorldHandoff handoff;
std::atomic<uint64_t> attempt{0};
LONG handoffFailed=0;
LONG gamePacketEvents=0;
std::atomic<ULONGLONG> worldDeadline{0};
NativeWorldUiGate worldUi;
std::atomic<void*> gameTransport{nullptr};
bool ownsConnection=false;
std::array<char,33> username{};
std::array<char,1025> password{};
unsigned char* Base(){return reinterpret_cast<unsigned char*>(GetModuleHandleW(nullptr));}
void EraseCredentials(){SecureZeroMemory(username.data(),username.size());SecureZeroMemory(password.data(),password.size());}
void CancelTimer(){if(timer){KillTimer(nullptr,timer);timer=0;}}
void Event(const char* name,const char* result){RevoraDirectDiagnostic(name,result);}
LONG CurrentStage(){return InterlockedCompareExchange(&stage,0,0);}

bool NativeWorldUiTick(){
    // Called only by the original Login UI thread's timer, never a socket worker.
    if(GetCurrentThreadId()!=threadId)return false;
    auto* main=*reinterpret_cast<unsigned char**>(Base()+0x10b3080);
    auto* manager=*reinterpret_cast<unsigned char**>(Base()+0x10afbf4);
    if(!main||!manager)return false;
    DWORD pid=0;HWND window=*reinterpret_cast<HWND*>(main+0x20);
    if(GetWindowThreadProcessId(window,&pid)!=threadId||pid!=GetCurrentProcessId())return false;
    auto* state=*reinterpret_cast<unsigned char**>(manager+4);
    if(!state)return false;
    auto table=*reinterpret_cast<uintptr_t*>(state);
    unsigned stateId=table==reinterpret_cast<uintptr_t>(Base()+0xe1effc)?1013:
        table==reinterpret_cast<uintptr_t>(Base()+0xe1f014)?1010:0;
    auto* loading=main+0x260d70;
    if(*reinterpret_cast<uintptr_t*>(loading)!=reinterpret_cast<uintptr_t>(Base()+0xd6b198))return false;
    if(worldUi.Start(GetCurrentThreadId(),stateId)){
        const unsigned char setup[]{0x55,0x8b,0xec,0x8b,0x45,0x08};
        const unsigned char flag[]{0x55,0x8b,0xec,0x8a,0x45,0x08};
        const unsigned char show[]{0x55,0x8b,0xec,0x83,0x7d,0x08,0x00};
        if(memcmp(Base()+0x255046,setup,sizeof(setup))||memcmp(Base()+0xceac6,flag,sizeof(flag))||memcmp(Base()+0x57fbdb,show,sizeof(show))){
            worldUi.Fail(threadId);Event("world_ui_rejected","preimage_mismatch");return false;
        }
        // UI setup copied semantically from native Login, not its credential branch.
        using Setup=void(__thiscall*)(void*,int,int);
        using Flag=void(__thiscall*)(void*,bool);
        using Show=void(__thiscall*)(void*,int);
        reinterpret_cast<Setup>(Base()+0x255046)(loading,300,30);
        reinterpret_cast<Flag>(Base()+0xceac6)(loading,true);
        reinterpret_cast<Show>(Base()+0x57fbdb)(loading,5);
        Event("world_ui_native_loading_started","ui_thread_map_state");
    }
    HWND loadingWindow=*reinterpret_cast<HWND*>(loading+0x20);
    return worldUi.Confirm(GetCurrentThreadId(),stateId,RevoraOwnedLoginVisible(),
        loadingWindow&&IsWindowVisible(loadingWindow));
}

bool NativePreimages(){
    const unsigned char connect[]{0x6a,0x50,0xb8,0x29,0x59,0x0a,0x01};
    const unsigned char queue[]{0x55,0x8b,0xec,0x57,0x8b,0xf9};
    const unsigned char update[]{0x6a,0x08,0xb8,0x77,0x59,0x0a,0x01};
    return !memcmp(Base()+0x900dec,connect,sizeof(connect)) &&
           !memcmp(Base()+0x901168,queue,sizeof(queue)) &&
           !memcmp(Base()+0x900f9b,update,sizeof(update));
}

bool ReadPair(void* dialog){
    return RevoraOwnedReadPair(dialog,username.data(),username.size(),password.data(),password.size());
}

void Fail(const char* reason){
    CancelTimer();EraseCredentials();handoff.Clear();
    RevoraOwnedClearPassword();RevoraOwnedSetBusy(false);
    // Own only this explicit attempt's native Account manager; never a Game
    // connection. Native recovery must remain on the original UI thread.
    if(GetCurrentThreadId()!=threadId){Event("direct_failure","wrong_thread");return;}
    __try {
        auto* manager=Base()+0x10ce508;
        if(ownsConnection && *reinterpret_cast<unsigned*>(manager+0x70)==1 && stage>0&&stage<4){
            using Close=void(__thiscall*)(void*);
            reinterpret_cast<Close>(Base()+0x900944)(manager);
        }
    } __except(EXCEPTION_EXECUTE_HANDLER){Event("direct_failure","native_close_exception");}
    ownsConnection=false;
    InterlockedExchange(&stage,0);
    Event("direct_failure",reason);
    MessageBoxW(nullptr,L"Unable to contact the Revora login service. Please try again later.",L"Revora",MB_OK|MB_ICONWARNING);
}

void CALLBACK Pump(HWND,UINT,UINT_PTR,DWORD){
    if(GetCurrentThreadId()!=threadId)return;
    if(CurrentStage()==0){CancelTimer();return;}
    if(CurrentStage()==6){
        // Observe only our established Game transport; do not dispatch it twice.
        bool connected=false;
        __try {
            auto* transport=static_cast<unsigned char*>(gameTransport.load());
            auto* manager=Base()+0x10ce508;
            if(transport&&*reinterpret_cast<void**>(manager+0x10)==transport&&*reinterpret_cast<unsigned*>(manager+0x70)==2){
                SOCKET s=*reinterpret_cast<SOCKET*>(transport+4);
                sockaddr_in peer{};int size=sizeof(peer);
                connected=s!=INVALID_SOCKET&&getpeername(s,reinterpret_cast<sockaddr*>(&peer),&size)==0;
            }
        } __except(EXCEPTION_EXECUTE_HANDLER){}
        const auto expires=worldDeadline.load();
        if(!connected||(expires&&GetTickCount64()>=expires)||InterlockedCompareExchange(&handoffFailed,0,0)){
            CancelTimer();handoff.Clear();EraseCredentials();InterlockedExchange(&stage,0);
            Event("world_connection_failed","visible_recovery");
            MessageBoxW(nullptr,L"The Revora game connection ended. Your saved character is preserved. Close this client and reconnect.",L"Revora",MB_OK|MB_ICONWARNING);
            PostQuitMessage(0);
        }
        return;
    }
    if(InterlockedCompareExchange(&handoffFailed,0,0)){Fail("world_handoff_rejected");return;}
    if(CurrentStage()==4||CurrentStage()==5){
        if(GetTickCount64()>=handleDeadline){
            handoff.Clear();InterlockedExchange(&stage,0);
            CancelTimer();RevoraOwnedSetBusy(false);Event("world_handle_cleared","handoff_timeout");
            MessageBoxW(nullptr,L"Unable to enter the game service. Please try again later.",L"Revora",MB_OK|MB_ICONWARNING);
            return;
        }
        // The native Game connect completion, transport handshake and receive
        // queue need the same UI-thread pump after Account success. A retained
        // handle is not a completed Game connection.
    }else if(GetTickCount64()>=deadline){Fail("attempt_timeout");return;}
    __try {
        using Update=void(__thiscall*)(void*);
        reinterpret_cast<Update>(Base()+0x900f9b)(Base()+0x10ce508);
        if(CurrentStage()==5){
            if(NativeWorldUiTick()){
                RevoraOwnedClearPassword();
                handoff.Clear();InterlockedExchange(&stage,6);worldDeadline=0;
                Event("world_ui_native_transition_complete","play_state_dialogs_closed");return;
            }
            if(!worldUi.started&&!RevoraOwnedLoginVisible()){
                handoff.Clear();InterlockedExchange(&stage,6);
                // Creation UI may await owner choice. Its submit narrows this
                // bound to 30 seconds; established world traffic cancels it.
                worldDeadline=GetTickCount64()+600000;
                Event("world_handoff_completed","native_login_dialog_closed");return;
            }
            auto* transport=static_cast<unsigned char*>(gameTransport.load());
            if(!transport||*reinterpret_cast<SOCKET*>(transport+4)==INVALID_SOCKET){Fail("game_disconnected");return;}
        }
    } __except(EXCEPTION_EXECUTE_HANDLER){Fail("native_dispatch_exception");}
}
}

extern "C" __declspec(dllexport) void __stdcall RevoraAuthDirectSubmit(void* dialog){
    if(InterlockedCompareExchange(&stage,1,0)!=0){Event("direct_submit_rejected","attempt_busy");return;}
    threadId=GetCurrentThreadId();activeDialog=dialog;ownsConnection=false;attempt=handoff.Begin(GetCurrentProcessId());
    worldUi.Begin(threadId);
    gameTransport=nullptr;InterlockedExchange(&handoffFailed,0);EraseCredentials();
    worldDeadline=0;InterlockedExchange(&gamePacketEvents,0);
    __try {
        if(!dialog||!NativePreimages()){Fail("preimage_mismatch");return;}
        DWORD pid=0;GetWindowThreadProcessId(*reinterpret_cast<HWND*>(static_cast<unsigned char*>(dialog)+0x20),&pid);
        if(pid!=GetCurrentProcessId()){Fail("dialog_identity");return;}
        if(!ReadPair(dialog)){Event("capture_pair_failure","owned_controls");Fail("owned_controls");return;}
        Event("capture_pair_success","revora_owned_controls");
        auto* manager=Base()+0x10ce508;
        if(!*reinterpret_cast<void**>(manager+0x10)||*reinterpret_cast<void**>(manager+8)){Fail("account_session_busy");return;}
        // Preserve the native username's non-secret account/display context.
        using SetUsername=void(__thiscall*)(void*,const char*);
        reinterpret_cast<SetUsername>(Base()+0x25f171)(dialog,username.data());
        using Connect=bool(__thiscall*)(void*,int,const char*,int,int);
        if(!reinterpret_cast<Connect>(Base()+0x900dec)(manager,1,"79.110.49.34",9958,1)){Fail("account_connect");return;}
        ownsConnection=true;
        deadline=GetTickCount64()+15000;timer=SetTimer(nullptr,0,100,Pump);
        if(!timer){Fail("attempt_timer");return;}
        Event("direct_account_connect","native_type_1");
    } __except(EXCEPTION_EXECUTE_HANDLER){Fail("submit_exception");}
}

extern "C" __declspec(dllexport) void __stdcall RevoraAuthDirectSeedReady(){
    if(GetCurrentThreadId()!=threadId||InterlockedCompareExchange(&stage,2,1)!=1){Event("direct_seed_rejected","no_current_attempt");return;}
    Event("direct_seed_ready","native_1059_processed");
    std::array<unsigned char,1028> object{};
    int count=RevoraCreateDirectProof(username.data(),password.data(),object.data()+4,1024);
    EraseCredentials();RevoraOwnedClearPassword();
    const bool definitiveRejection=count==-1;
    if(definitiveRejection){
        // A distinct rejection-only control: no username, challenge, proof or
        // authorization claim. Requires the paired AccountHost implementation.
        const unsigned char control[]{10,0,0x12,0x7f,'R','V','A','1',1,0};
        memcpy(object.data()+4,control,sizeof(control));count=sizeof(control);
    }
    if(count<=0){SecureZeroMemory(object.data(),object.size());Fail("https_indeterminate");return;}
    __try {
        if(GetTickCount64()>=deadline||!NativePreimages()){SecureZeroMemory(object.data(),object.size());Fail("proof_expired_before_queue");return;}
        using Queue=int(__thiscall*)(void*,void*);
        int accepted=reinterpret_cast<Queue>(Base()+0x901168)(Base()+0x10ce508,object.data());
        SecureZeroMemory(object.data(),object.size());
        if(accepted!=1){Fail("native_queue_rejected");return;}
        InterlockedExchange(&stage,3);
        Event(definitiveRejection?"definitive_rejection_control_queued":"real_proof_queued","native_queue_accepted");
    } __except(EXCEPTION_EXECUTE_HANDLER){SecureZeroMemory(object.data(),object.size());Fail("native_queue_exception");}
}

extern "C" __declspec(dllexport) void __stdcall RevoraAuthDirectProcess1637(const unsigned char* packet,unsigned long length){
    if(GetCurrentThreadId()!=threadId||stage!=3){handoff.Clear();return;}
    __try {
        if(!packet||length!=52||packet[0]!=52||packet[1]!=0||packet[2]!=0x65||packet[3]!=6){Fail("invalid_1637");return;}
        unsigned status=0,value=0;memcpy(&status,packet+12,4);memcpy(&value,packet+4,4);
        EraseCredentials();ownsConnection=false;
        if(status==1){CancelTimer();handoff.Clear();InterlockedExchange(&stage,0);RevoraOwnedSetBusy(false);Event("native_rejection_received","status_1");return;}
        if(status!=2||!value){Fail("invalid_success_1637");return;}
        handleDeadline=GetTickCount64()+30000;
        bool captured=handoff.Capture(attempt.load(),GetCurrentProcessId(),value,GetTickCount64(),handleDeadline);
        SecureZeroMemory(&value,sizeof(value));
        if(!captured){Fail("world_capture_state");return;}
        InterlockedExchange(&stage,4);Event("world_handle_captured","nonzero_status_2");
    } __except(EXCEPTION_EXECUTE_HANDLER){Fail("response_exception");}
}

extern "C" __declspec(dllexport) bool __fastcall RevoraAuthGameConnect(void* manager,void*,int type,const char* address,int port,int retries){
    using Connect=bool(__thiscall*)(void*,int,const char*,int,int);
    const bool busy=*reinterpret_cast<void**>(static_cast<unsigned char*>(manager)+8)!=nullptr;
    Event("game_connect_requested",busy?"native_worker_present":"native_worker_absent");
    if(GetCurrentThreadId()!=threadId||stage!=4||manager!=Base()+0x10ce508||type!=2||port!=9959||!address||strcmp(address,"79.110.49.34")||busy){
        handoff.Clear();InterlockedExchange(&handoffFailed,1);return false;
    }
    gameTransport=*reinterpret_cast<void**>(static_cast<unsigned char*>(manager)+0x10);
    if(!handoff.Bind(attempt.load(),GetCurrentProcessId(),reinterpret_cast<uintptr_t>(gameTransport.load()),GetTickCount64())){
        InterlockedExchange(&handoffFailed,1);return false;
    }
#ifdef REVORA_GAME_HANDSHAKE_FIXTURE
    if(type!=2||port!=9959||strcmp(address,"79.110.49.34"))return false;
    address="127.0.0.1";port=19959;
    Event("game_handshake_fixture","loopback_research_only");
#endif
    bool accepted=reinterpret_cast<Connect>(Base()+0x900dec)(manager,type,address,port,retries);
    if(!accepted){handoff.Clear();InterlockedExchange(&handoffFailed,1);}
    Event("game_connect_result",accepted?"native_accepted":"native_rejected");
    return accepted;
}

static int Patch2429(unsigned char* packet,unsigned long length,void* transport){
    __try {
        // This native send boundary also carries the provider prelude and
        // gameplay packets. Those must not consume or clear a world handle.
        // The 1024-byte handoff bound belongs to 2429 only. This seam is
        // shared by character creation and larger native gameplay messages;
        // leave their framing, sizes and validation to the original sender.
        if(packet&&length>=4&&(packet[2]!=0x7d||packet[3]!=9)){
            if(CurrentStage()==6&&transport==gameTransport.load()){
                if((packet[2]|packet[3]<<8)==2205)worldDeadline=GetTickCount64()+30000;
                if(RevoraLocationRequested(packet,length)){worldDeadline=0;Event("world_location_requested","native_action_74_unchanged");}
            }
            if(InterlockedIncrement(&gamePacketEvents)<=64){
                char category[64]{};wsprintfA(category,"opcode_%u_unchanged",static_cast<unsigned>(packet[2]|packet[3]<<8));
                Event("game_packet_forwarded",category);
            }
            return 1;
        }
        auto* manager=Base()+0x10ce508;
        if(CurrentStage()!=4||!transport||transport!=gameTransport.load()||*reinterpret_cast<void**>(manager+0x10)!=transport||
           *reinterpret_cast<unsigned*>(manager+0x70)!=2)goto rejected;
        {
            const SOCKET socket=*reinterpret_cast<SOCKET*>(static_cast<unsigned char*>(transport)+4);
            sockaddr_in peer{};int size=sizeof(peer);
            const int saved=WSAGetLastError();
            const bool connected=getpeername(socket,reinterpret_cast<sockaddr*>(&peer),&size)==0&&
                size==sizeof(peer)&&peer.sin_family==AF_INET&&peer.sin_port==htons(9959)&&peer.sin_addr.s_addr==htonl(0x4f6e3122);
            WSASetLastError(saved);
            if(!connected||!handoff.Inject(attempt.load(),GetCurrentProcessId(),reinterpret_cast<uintptr_t>(transport),socket,GetTickCount64(),packet,length))goto rejected;
        }
        InterlockedExchange(&stage,5); // UI thread alone owns its dispatch timer.
        Event("world_handle_injected","opcode_2429_offset_4_width_4");return 1;
    } __except(EXCEPTION_EXECUTE_HANDLER){}
rejected:
    handoff.Clear();InterlockedExchange(&handoffFailed,1);Event("world_handle_bridge_rejected","session_or_packet_invalid");return 0;
}

extern "C" __declspec(dllexport) int __stdcall RevoraAuthDirectPatch2429V2(unsigned char* packet,unsigned long length,void* transport){
    const DWORD error=GetLastError();const int socketError=WSAGetLastError();
    const int result=Patch2429(packet,length,transport);
    WSASetLastError(socketError);SetLastError(error);return result;
}

extern "C" __declspec(dllexport) void __stdcall RevoraAuthGameDisconnectObserved(void* manager){
    const DWORD error=GetLastError();const int socketError=WSAGetLastError();
    __try {
        if(manager==Base()+0x10ce508&&(CurrentStage()==4||CurrentStage()==5||CurrentStage()==6)&&
           *reinterpret_cast<unsigned*>(static_cast<unsigned char*>(manager)+0x70)==2){
            handoff.Clear();InterlockedExchange(&handoffFailed,1);
            Event("world_handle_cleared","game_disconnect");
        }
    } __except(EXCEPTION_EXECUTE_HANDLER){handoff.Clear();InterlockedExchange(&handoffFailed,1);}
    WSASetLastError(socketError);SetLastError(error);
}

extern "C" __declspec(dllexport) void __stdcall RevoraAuthOfficialReportSuppressed(unsigned category){
    Event(category==1?"pingtest_upload_suppressed":"dumperror_upload_suppressed","local_diagnostics_retained");
}

// Loader-lock safe: no native dispatch, waits, file I/O or thread creation.
extern "C" void __stdcall RevoraAuthDirectEraseOnDetach(){
    SecureZeroMemory(username.data(),username.size());
    SecureZeroMemory(password.data(),password.size());
    handoff.EraseAtProcessExit();InterlockedExchange(&stage,0);
}
