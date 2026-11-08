#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <commctrl.h>
#include <array>
#include "owned_login_controls.h"

extern "C" void __stdcall RevoraDirectDiagnostic(const char*,const char*);
namespace {
constexpr int UserId=0x7e01,PasswordId=0x7e02;
struct Controls {void* dialog{};HWND parent{},user{},password{},login{};DWORD thread{};bool busy{};};
Controls state;
unsigned reportedStage=0;
void Once(unsigned stage,const char* result){const unsigned bit=1u<<stage;if(!(reportedStage&bit)){reportedStage|=bit;RevoraDirectDiagnostic("owned_controls_initialization",result);}}
struct Find {UINT_PTR id;HWND result{};unsigned count{};};
BOOL CALLBACK FindId(HWND window,LPARAM parameter){
    auto& find=*reinterpret_cast<Find*>(parameter);
    DWORD pid=0;GetWindowThreadProcessId(window,&pid);
    if(pid==GetCurrentProcessId()&&static_cast<UINT_PTR>(GetDlgCtrlID(window))==find.id){find.result=window;++find.count;}
    return TRUE;
}
HWND Unique(HWND root,UINT_PTR id){Find find{id};EnumChildWindows(root,FindId,reinterpret_cast<LPARAM>(&find));return find.count==1?find.result:nullptr;}
bool Current(HWND window){DWORD pid=0;return window&&IsWindow(window)&&GetWindowThreadProcessId(window,&pid)==GetCurrentThreadId()&&pid==GetCurrentProcessId();}
HWND DisplayForEdit(HWND edit){
    // Runtime layout: native zero-size EDIT immediately precedes its drawn
    // BUTTON field, at the same screen anchor and under the same parent.
    // Verify the complete relationship; never infer a credential-object ID.
    if(!Current(edit))return nullptr;
    wchar_t type[16]{};GetClassNameW(edit,type,16);
    if(lstrcmpiW(type,L"Edit"))return nullptr;
    HWND display=GetWindow(edit,GW_HWNDNEXT);
    if(!Current(display)||GetParent(display)!=GetParent(edit))return nullptr;
    GetClassNameW(display,type,16);if(lstrcmpiW(type,L"Button"))return nullptr;
    RECT a{},b{};if(!GetWindowRect(edit,&a)||!GetWindowRect(display,&b))return nullptr;
    return a.left==b.left&&a.top==b.top&&a.left==a.right&&a.top==a.bottom?display:nullptr;
}
LRESULT CALLBACK EditProcedure(HWND window,UINT message,WPARAM w,LPARAM l,UINT_PTR,DWORD_PTR){
    if(message==WM_GETDLGCODE)return DefSubclassProc(window,message,w,l)|DLGC_WANTTAB|DLGC_WANTALLKEYS;
    if(message==WM_KEYDOWN&&(w==VK_TAB||w==VK_RETURN)){
        if(state.busy)return 0;
        if(w==VK_RETURN){PostMessageW(state.login,BM_CLICK,0,0);return 0;}
        bool reverse=(GetKeyState(VK_SHIFT)&0x8000)!=0;
        HWND next=window==state.user?(reverse?state.login:state.password):(reverse?state.user:state.login);
        if(Current(next))SetFocus(next);
        return 0;
    }
    if(message==WM_CHAR&&(w==L'\t'||w==L'\r'))return 0;
    if(message==WM_NCDESTROY){RemoveWindowSubclass(window,EditProcedure,1);return DefSubclassProc(window,message,w,l);}
    return DefSubclassProc(window,message,w,l);
}
bool Bounds(HWND source,HWND parent,RECT& rect){
    if(!Current(source)||!GetWindowRect(source,&rect))return false;
    if(rect.right-rect.left<30||rect.right-rect.left>1000||rect.bottom-rect.top<10||rect.bottom-rect.top>150)return false;
    MapWindowPoints(nullptr,parent,reinterpret_cast<POINT*>(&rect),2);return true;
}
HWND Make(HWND parent,HWND source,int id,bool password){
    RECT rect{};if(!Bounds(source,parent,rect))return nullptr;
    DWORD style=WS_CHILD|WS_VISIBLE|WS_TABSTOP|ES_AUTOHSCROLL|(password?ES_PASSWORD:0);
    HWND edit=CreateWindowExW(WS_EX_NOPARENTNOTIFY,L"EDIT",L"",style,rect.left,rect.top,
        rect.right-rect.left,rect.bottom-rect.top,parent,reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),GetModuleHandleW(nullptr),nullptr);
    if(!edit)return nullptr;
    SendMessageW(edit,EM_SETLIMITTEXT,password?256:32,0);
    HFONT font=reinterpret_cast<HFONT>(SendMessageW(source,WM_GETFONT,0,0));
    if(!font)font=static_cast<HFONT>(GetStockObject(DEFAULT_GUI_FONT));
    SendMessageW(edit,WM_SETFONT,reinterpret_cast<WPARAM>(font),TRUE);
    if(password)SendMessageW(edit,EM_SETPASSWORDCHAR,L'\x2022',0);
    if(!SetWindowSubclass(edit,EditProcedure,1,0)){DestroyWindow(edit);return nullptr;}
    return edit;
}
bool Read(HWND edit,char* output,size_t capacity,size_t maximum){
    std::array<wchar_t,257> temporary{};
    SecureZeroMemory(output,capacity);
    int length=GetWindowTextLengthW(edit);
    bool ok=length>0&&static_cast<size_t>(length)<=maximum;
    if(ok){
        int copied=GetWindowTextW(edit,temporary.data(),static_cast<int>(temporary.size()));
        ok=copied==length&&WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,temporary.data(),copied,
            output,static_cast<int>(capacity-1),nullptr,nullptr)>0;
    }
    SecureZeroMemory(temporary.data(),sizeof(temporary));
    if(!ok)SecureZeroMemory(output,capacity);
    return ok;
}
}

// Called instead of CDlgLogin's legacy per-frame credential-input update.
// ECX is the dialog, no stack arguments. No keyboard polling or native getter.
extern "C" __declspec(dllexport) void __fastcall RevoraAuthOwnedControlsTick(void* dialog,void*){
    __try {
        if(state.dialog&&(!IsWindow(state.parent)||state.dialog!=dialog)){
            RevoraOwnedClearPassword();
            if(Current(state.user))DestroyWindow(state.user);
            if(Current(state.password))DestroyWindow(state.password);
            state={};
        }
        if(!dialog)return;
        if(state.dialog)return;
        Once(1,"input_update_reached");
        auto* object=static_cast<unsigned char*>(dialog);
        HWND parent=*reinterpret_cast<HWND*>(object+0x20);
        if(!Current(parent)){Once(2,"dialog_window_unavailable");return;}
        HWND oldUser=DisplayForEdit(*reinterpret_cast<HWND*>(object+0xbd8));
        HWND oldPassword=DisplayForEdit(*reinterpret_cast<HWND*>(object+0xed0));
        HWND login=Unique(parent,5680);
        if(!oldUser||!oldPassword||!login){Once(3,"field_windows_unavailable");return;}
        HWND user=Make(parent,oldUser,UserId,false);
        HWND password=Make(parent,oldPassword,PasswordId,true);
        if(!user||!password){if(user)DestroyWindow(user);if(password)DestroyWindow(password);return;}
        state={dialog,parent,user,password,login,GetCurrentThreadId(),false};
        for(HWND old:{oldUser,oldPassword,*reinterpret_cast<HWND*>(object+0xbd8),*reinterpret_cast<HWND*>(object+0xed0)}){
            if(Current(old)){EnableWindow(old,FALSE);ShowWindow(old,SW_HIDE);}
        }
        SetWindowPos(user,HWND_TOP,0,0,0,0,SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE);
        SetWindowPos(password,HWND_TOP,0,0,0,0,SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE);
        SetFocus(user);
        RevoraDirectDiagnostic("owned_login_controls_ready","legacy_input_disabled");
    } __except(EXCEPTION_EXECUTE_HANDLER){RevoraOwnedClearPassword();RevoraDirectDiagnostic("owned_login_controls_failed","initialization_exception");}
}

bool RevoraOwnedReadPair(void* dialog,char* user,size_t userCapacity,char* password,size_t passwordCapacity) noexcept{
    if(!user||!password||userCapacity!=33||passwordCapacity!=1025)return false;
    SecureZeroMemory(user,userCapacity);SecureZeroMemory(password,passwordCapacity);
    if(state.dialog!=dialog){RevoraDirectDiagnostic("owned_pair_rejected","dialog_mismatch");return false;}
    if(state.thread!=GetCurrentThreadId()){RevoraDirectDiagnostic("owned_pair_rejected","thread_mismatch");return false;}
    if(state.busy||!Current(state.user)||!Current(state.password)){RevoraDirectDiagnostic("owned_pair_rejected","controls_unavailable_or_busy");return false;}
    const bool userOk=Read(state.user,user,userCapacity,32);
    const bool passwordOk=userOk&&Read(state.password,password,passwordCapacity,256);
    if(!userOk||!passwordOk){
        RevoraDirectDiagnostic("owned_pair_rejected",userOk?"password_text_invalid":"username_text_invalid");
        SecureZeroMemory(user,userCapacity);SecureZeroMemory(password,passwordCapacity);RevoraOwnedClearPassword();return false;
    }
    RevoraOwnedSetBusy(true);return true;
}
void RevoraOwnedClearPassword() noexcept{
    if(Current(state.password)){SetWindowTextW(state.password,L"");SendMessageW(state.password,EM_EMPTYUNDOBUFFER,0,0);}
}
bool RevoraOwnedLoginVisible() noexcept{return Current(state.parent)&&IsWindowVisible(state.parent);}
void RevoraOwnedSetBusy(bool busy) noexcept{
    if(state.thread!=GetCurrentThreadId())return;
    state.busy=busy;
    for(HWND control:{state.user,state.password})if(Current(control))SendMessageW(control,EM_SETREADONLY,busy,0);
    if(!busy&&Current(state.password))SetFocus(state.password);
}
