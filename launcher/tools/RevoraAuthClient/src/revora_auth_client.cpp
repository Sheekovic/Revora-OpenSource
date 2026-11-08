#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winhttp.h>
#include <bcrypt.h>
#include <wincrypt.h>
#include <objbase.h>
#include <array>
#include <cstdint>
#include <cstring>
#include <string>
#include <vector>
#include "direct_response.h"

namespace {
constexpr wchar_t Host[]=L"auth.playrevora.com", Path[]=L"/api/v2/native-login/authorize";
constexpr char Magic[]="RVA1"; constexpr std::uint16_t Version=1, ProofOpcode=0x7F11;
SRWLOCK lock=SRWLOCK_INIT; std::uint32_t worldHandle=0; bool handleUsed=false;
std::array<char,33> capturedUsername{}; std::array<char,257> capturedPassword{};
DWORD captureThread=0; bool captureActive=false, usernameReady=false, passwordReady=false;
HINSTANCE moduleInstance{};
void Zero(void*p,size_t n){SecureZeroMemory(p,n);}
void Diagnostic(const char*event,const char*detail=""){wchar_t module[MAX_PATH]{};if(!moduleInstance||!GetModuleFileNameW(moduleInstance,module,MAX_PATH))return;wchar_t*slash=wcsrchr(module,L'\\');if(!slash)return;*slash=L'\0';slash=wcsrchr(module,L'\\');if(!slash)return;*slash=L'\0';std::wstring directory=std::wstring(module)+L"\\RevoraDiagnostics";CreateDirectoryW(directory.c_str(),nullptr);std::wstring path=directory+L"\\revora-auth-client.jsonl";HANDLE file=CreateFileW(path.c_str(),FILE_APPEND_DATA,FILE_SHARE_READ|FILE_SHARE_WRITE,nullptr,OPEN_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);if(file==INVALID_HANDLE_VALUE)return;SYSTEMTIME now{};GetSystemTime(&now);char line[384]{};int length=_snprintf_s(line,sizeof(line),_TRUNCATE,"{\"schema\":1,\"timestampUtc\":\"%04u-%02u-%02uT%02u:%02u:%02u.%03uZ\",\"event\":\"%s\",\"result\":\"%s\"}\r\n",now.wYear,now.wMonth,now.wDay,now.wHour,now.wMinute,now.wSecond,now.wMilliseconds,event,detail);if(length>0){DWORD written=0;WriteFile(file,line,(DWORD)length,&written,nullptr);FlushFileBuffers(file);}CloseHandle(file);}
std::string Escape(const char*s,size_t maximum){std::string r;if(!s)return r;for(size_t i=0;i<=maximum;++i){unsigned char c=(unsigned char)s[i];if(!c)return r;if(c<0x20||c=='"'||c=='\\')return {};if(i==maximum)return {};r.push_back((char)c);}return {};}
bool NormalizeUsername(std::string&value){for(char&c:value){unsigned char b=(unsigned char)c;if(b>='a'&&b<='z')c=(char)(b-'a'+'A');else if(!((b>='A'&&b<='Z')||(b>='0'&&b<='9')||b=='_'||b=='-'))return false;}return value.size()>=3&&value.size()<=32;}
std::string B64(const unsigned char*p,DWORD n){DWORD z=0;if(!CryptBinaryToStringA(p,n,CRYPT_STRING_BASE64|CRYPT_STRING_NOCRLF,nullptr,&z))return {};std::string r(z,'\0');if(!CryptBinaryToStringA(p,n,CRYPT_STRING_BASE64|CRYPT_STRING_NOCRLF,r.data(),&z))return {};if(!r.empty()&&r.back()=='\0')r.pop_back();return r;}
bool UnB64(const std::string&s,unsigned char*out,DWORD expected){DWORD n=expected;return CryptStringToBinaryA(s.c_str(),(DWORD)s.size(),CRYPT_STRING_BASE64,out,&n,nullptr,nullptr)&&n==expected;}
std::string Field(const std::string&j,const char*n){std::string k="\""+std::string(n)+"\":\"";auto p=j.find(k);if(p==std::string::npos)return {};p+=k.size();auto e=j.find('"',p);return e==std::string::npos?std::string{}:j.substr(p,e-p);}
bool Hmac(const unsigned char*key,const unsigned char*data,DWORD size,unsigned char*out){BCRYPT_ALG_HANDLE a{};BCRYPT_HASH_HANDLE h{};DWORD os=0,cb=0;std::vector<unsigned char>obj;bool ok=false;if(!BCryptOpenAlgorithmProvider(&a,BCRYPT_SHA256_ALGORITHM,nullptr,BCRYPT_ALG_HANDLE_HMAC_FLAG)&&!BCryptGetProperty(a,BCRYPT_OBJECT_LENGTH,(PUCHAR)&os,sizeof(os),&cb,0)){obj.resize(os);if(!BCryptCreateHash(a,&h,obj.data(),os,(PUCHAR)key,32,0)&&!BCryptHashData(h,(PUCHAR)data,size,0)&&!BCryptFinishHash(h,out,32,0))ok=true;}if(h)BCryptDestroyHash(h);if(a)BCryptCloseAlgorithmProvider(a,0);Zero(obj.data(),obj.size());return ok;}
int Post(const std::string& body,std::string& response){
    if(body.empty()||body.size()>4096)return 0;
    struct InternetHandle {
        HINTERNET value;
        ~InternetHandle(){if(value)WinHttpCloseHandle(value);}
    } session{WinHttpOpen(L"RevoraAuthClient/2",WINHTTP_ACCESS_TYPE_NO_PROXY,
                         WINHTTP_NO_PROXY_NAME,WINHTTP_NO_PROXY_BYPASS,0)};
    DWORD protocols=WINHTTP_FLAG_SECURE_PROTOCOL_TLS1_2;
    if(!session.value||!WinHttpSetTimeouts(session.value,2000,2000,2000,2000)||
       !WinHttpSetOption(session.value,WINHTTP_OPTION_SECURE_PROTOCOLS,&protocols,sizeof(protocols)))return 0;
    const ULONGLONG expires=GetTickCount64()+8000;
    InternetHandle connection{WinHttpConnect(session.value,Host,INTERNET_DEFAULT_HTTPS_PORT,0)};
    InternetHandle request{connection.value?WinHttpOpenRequest(connection.value,L"POST",Path,nullptr,
                           WINHTTP_NO_REFERER,WINHTTP_DEFAULT_ACCEPT_TYPES,WINHTTP_FLAG_SECURE):nullptr};
    DWORD disabled=WINHTTP_DISABLE_REDIRECTS|WINHTTP_DISABLE_COOKIES;
    DWORD autoLogon=WINHTTP_AUTOLOGON_SECURITY_LEVEL_HIGH;
    if(!request.value||!WinHttpSetOption(request.value,WINHTTP_OPTION_DISABLE_FEATURE,&disabled,sizeof(disabled))||
       !WinHttpSetOption(request.value,WINHTTP_OPTION_AUTOLOGON_POLICY,&autoLogon,sizeof(autoLogon))||
       !WinHttpSendRequest(request.value,L"Content-Type: application/json\r\n",DWORD(-1L),
                          const_cast<char*>(body.data()),static_cast<DWORD>(body.size()),static_cast<DWORD>(body.size()),0)||
       GetTickCount64()>=expires||!WinHttpReceiveResponse(request.value,nullptr))return 0;
    DWORD status=0,bytes=sizeof(status);
    if(GetTickCount64()>=expires||!WinHttpQueryHeaders(request.value,WINHTTP_QUERY_STATUS_CODE|WINHTTP_QUERY_FLAG_NUMBER,
                         nullptr,&status,&bytes,nullptr))return 0;
    response.reserve(4096);
    struct Chunk { std::array<char,512> data{}; ~Chunk(){Zero(data.data(),data.size());} } chunk;
    for(;;){
        if(GetTickCount64()>=expires)return 0;
        int remaining=static_cast<int>(expires-GetTickCount64());
        if(remaining<=0||!WinHttpSetTimeouts(request.value,2000,2000,2000,remaining>2000?2000:remaining))return 0;
        DWORD read=0;
        if(!WinHttpReadData(request.value,chunk.data.data(),static_cast<DWORD>(chunk.data.size()),&read))return 0;
        if(GetTickCount64()>=expires||response.size()+read>4096)return 0;
        if(!read)break;
        response.append(chunk.data.data(),read);Zero(chunk.data.data(),chunk.data.size());
    }
    if(status==200)return 1;
    return status==400&&response=="{\"error\":\"request_rejected\"}"?-1:0;
}
void Put16(unsigned char*p,std::uint16_t v){p[0]=(unsigned char)v;p[1]=(unsigned char)(v>>8);} 
bool ReadClientString(const void*object,char*out,size_t capacity){if(!object||!out||capacity<2)return false;__try{const auto*b=static_cast<const unsigned char*>(object);size_t length=*reinterpret_cast<const unsigned*>(b+0x10),allocated=*reinterpret_cast<const unsigned*>(b+0x14);if(!length||length>=capacity||allocated<length)return false;const char*source=allocated<0x10?reinterpret_cast<const char*>(b):*reinterpret_cast<char*const*>(b);if(!source||source[length]!='\0'||strnlen_s(source,length+1)!=length)return false;memcpy(out,source,length);out[length]='\0';return true;}__except(EXCEPTION_EXECUTE_HANDLER){return false;}}
bool ReadCString(const char*source,char*out,size_t capacity){if(!source||!out||capacity<2)return false;__try{size_t length=strnlen_s(source,capacity);if(!length||length>=capacity)return false;memcpy(out,source,length);out[length]='\0';return true;}__except(EXCEPTION_EXECUTE_HANDLER){return false;}}
void ClearCapture(){Zero(capturedUsername.data(),capturedUsername.size());Zero(capturedPassword.data(),capturedPassword.size());captureThread=0;captureActive=usernameReady=passwordReady=false;}
int RejectionPacket(unsigned char*out,int capacity,const std::string&candidate){std::string username=candidate;if(!NormalizeUsername(username))username="INVALID";const size_t size=92+username.size();if(!out||capacity<(int)size)return 0;GUID id{};std::array<unsigned char,32>nonce{},proof{};Put16(out,(std::uint16_t)size);Put16(out+2,ProofOpcode);memcpy(out+4,Magic,4);Put16(out+8,Version);Put16(out+10,(std::uint16_t)username.size());memcpy(out+12,&id,16);memcpy(out+28,nonce.data(),nonce.size());memcpy(out+60,proof.data(),proof.size());memcpy(out+92,username.data(),username.size());return (int)size;}
}
extern "C" __declspec(dllexport) unsigned long __stdcall RevoraAuthClientAbiVersion(){return 0x00010000;}
extern "C" void __stdcall RevoraAuthControlValidationResult(unsigned result,unsigned usernameLength,unsigned passwordLength){char detail[128]{};_snprintf_s(detail,sizeof(detail),_TRUNCATE,"stage_%u_username_utf16_units_%u_password_utf16_units_%u",result,usernameLength,passwordLength);Diagnostic(result==0?"direct_control_validation_success":"direct_control_validation_failure",detail);}
extern "C" __declspec(dllexport) void __stdcall RevoraAuthClientBeginLogin(void*){AcquireSRWLockExclusive(&lock);ClearCapture();captureThread=GetCurrentThreadId();captureActive=true;ReleaseSRWLockExclusive(&lock);Diagnostic("credential_capture_armed","explicit_login");}
extern "C" __declspec(dllexport) int __stdcall RevoraAuthClientCaptureUsernameRaw(const char*username,unsigned long length){int ok=0;AcquireSRWLockExclusive(&lock);if(captureActive&&captureThread==GetCurrentThreadId()&&!usernameReady&&length>0&&length<capturedUsername.size()){__try{memcpy(capturedUsername.data(),username,length);capturedUsername[length]='\0';usernameReady=true;ok=1;}__except(EXCEPTION_EXECUTE_HANDLER){ClearCapture();}}else ClearCapture();ReleaseSRWLockExclusive(&lock);Diagnostic(ok?"username_accessor_observed":"capture_pair_failure",ok?"bounded":"username_raw_invalid");return ok;}
extern "C" __declspec(dllexport) int __stdcall RevoraAuthClientCaptureObjects(const void*usernameObject,const void*passwordObject){int ok=0;size_t usernameLength=0,passwordLength=0;AcquireSRWLockExclusive(&lock);if(captureActive&&captureThread==GetCurrentThreadId()&&!usernameReady&&!passwordReady&&ReadClientString(usernameObject,capturedUsername.data(),capturedUsername.size())&&ReadClientString(passwordObject,capturedPassword.data(),capturedPassword.size())){usernameReady=passwordReady=true;usernameLength=strnlen_s(capturedUsername.data(),capturedUsername.size());passwordLength=strnlen_s(capturedPassword.data(),capturedPassword.size());ok=1;}else ClearCapture();ReleaseSRWLockExclusive(&lock);if(ok){Diagnostic("username_accessor_observed","bounded");Diagnostic("password_accessor_observed","bounded");char detail[96]{};_snprintf_s(detail,sizeof(detail),_TRUNCATE,"username_length_%zu_password_length_%zu",usernameLength,passwordLength);Diagnostic("capture_pair_success",detail);}else Diagnostic("capture_pair_failure","submit_objects_invalid");return ok;}
extern "C" __declspec(dllexport) int __stdcall RevoraAuthClientCaptureUsername(const void*object){int ok=0;AcquireSRWLockExclusive(&lock);if(captureActive&&captureThread==GetCurrentThreadId()&&!usernameReady&&ReadClientString(object,capturedUsername.data(),capturedUsername.size())){usernameReady=true;ok=1;}else{ClearCapture();}ReleaseSRWLockExclusive(&lock);return ok;}
extern "C" __declspec(dllexport) int __stdcall RevoraAuthClientCapturePassword(const void*object){int ok=0,complete=0;size_t usernameLength=0,passwordLength=0;AcquireSRWLockExclusive(&lock);if(captureActive&&captureThread==GetCurrentThreadId()&&!passwordReady&&ReadClientString(object,capturedPassword.data(),capturedPassword.size())){passwordReady=true;ok=1;complete=usernameReady?1:0;if(complete){usernameLength=strnlen_s(capturedUsername.data(),capturedUsername.size());passwordLength=strnlen_s(capturedPassword.data(),capturedPassword.size());}}else ClearCapture();ReleaseSRWLockExclusive(&lock);Diagnostic(ok?"password_accessor_observed":"capture_pair_failure",ok?"bounded":"password_object_invalid");if(complete){char detail[96]{};_snprintf_s(detail,sizeof(detail),_TRUNCATE,"username_length_%zu_password_length_%zu",usernameLength,passwordLength);Diagnostic("capture_pair_success",detail);}return ok;}
extern "C" __declspec(dllexport) int __stdcall RevoraAuthClientCapturePair(const char*username,const char*password){int ok=0;size_t usernameLength=0,passwordLength=0;AcquireSRWLockExclusive(&lock);if(captureActive&&captureThread==GetCurrentThreadId()&&usernameReady&&passwordReady){ok=1;usernameLength=strnlen_s(capturedUsername.data(),capturedUsername.size());passwordLength=strnlen_s(capturedPassword.data(),capturedPassword.size());}else if(captureActive&&captureThread==GetCurrentThreadId()&&!usernameReady&&!passwordReady&&ReadCString(username,capturedUsername.data(),capturedUsername.size())&&ReadCString(password,capturedPassword.data(),capturedPassword.size())){usernameReady=passwordReady=true;usernameLength=strnlen_s(capturedUsername.data(),capturedUsername.size());passwordLength=strnlen_s(capturedPassword.data(),capturedPassword.size());ok=1;}else{ClearCapture();}ReleaseSRWLockExclusive(&lock);char detail[96]{};if(ok){_snprintf_s(detail,sizeof(detail),_TRUNCATE,"username_length_%zu_password_length_%zu",usernameLength,passwordLength);Diagnostic("capture_pair_success",detail);}else Diagnostic("capture_pair_failure","validation_failed");return ok;}
extern "C" __declspec(dllexport) int __cdecl RevoraAuthClientBuildProof(int,const char*username,const char*password,void*,void*,int,int,int,void*,int,void*,void*,unsigned char*out,int capacity){
 (void)username;(void)password;if(!out||capacity<160)return 0;memset(out,0,(size_t)capacity);std::array<char,33>rawUser{};std::array<char,257>rawPassword{};AcquireSRWLockExclusive(&lock);bool complete=captureActive&&usernameReady&&passwordReady&&captureThread==GetCurrentThreadId();if(complete){memcpy(rawUser.data(),capturedUsername.data(),rawUser.size());memcpy(rawPassword.data(),capturedPassword.data(),rawPassword.size());}ClearCapture();ReleaseSRWLockExclusive(&lock);std::string u=complete?Escape(rawUser.data(),32):std::string{},p=complete?Escape(rawPassword.data(),256):std::string{};Zero(rawUser.data(),rawUser.size());Zero(rawPassword.data(),rawPassword.size());if(!NormalizeUsername(u)||p.empty()){Zero(p.data(),p.size());Diagnostic("fallback_proof_sent","capture_or_validation_failure");return RejectionPacket(out,capacity,u);}std::array<unsigned char,32>cn{};if(BCryptGenRandom(nullptr,cn.data(),(ULONG)cn.size(),BCRYPT_USE_SYSTEM_PREFERRED_RNG)){Zero(p.data(),p.size());Diagnostic("fallback_proof_sent","local_crypto_failure");return RejectionPacket(out,capacity,u);}std::string body="{\"username\":\""+u+"\",\"password\":\""+p+"\",\"clientNonce\":\""+B64(cn.data(),32)+"\",\"realm\":\"Revora\",\"profile\":\"dx9\",\"clientContract\":\"patch-7630-revora-auth-v1\"}";std::string response;int policy=Post(body,response);Zero(p.data(),p.size());Zero(body.data(),body.size());if(policy>0)Diagnostic("https_accepted","validated_200");else if(policy<0)Diagnostic("https_rejected","generic_400");else Diagnostic("https_rejected","indeterminate_failure");std::string id=Field(response,"challengeId"),snText=Field(response,"serverNonce"),bindText=Field(response,"connectionBinding"),secretText=Field(response,"sessionSecret");GUID guid{};std::array<unsigned char,32>sn{},bind{},secret{},proof{};bool authorized=policy>0&&!id.empty()&&CLSIDFromString(std::wstring(id.begin(),id.end()).c_str(),&guid)==S_OK&&UnB64(snText,sn.data(),32)&&UnB64(bindText,bind.data(),32)&&UnB64(secretText,secret.data(),32);if(!authorized){Zero(response.data(),response.size());Diagnostic("fallback_proof_sent","authorization_unavailable");return RejectionPacket(out,capacity,u);}std::vector<unsigned char>t(16+32+32+32+2+u.size()+6);size_t at=0;memcpy(t.data()+at,&guid,16);at+=16;memcpy(t.data()+at,sn.data(),32);at+=32;memcpy(t.data()+at,cn.data(),32);at+=32;memcpy(t.data()+at,bind.data(),32);at+=32;t[at++]=1;t[at++]=0;memcpy(t.data()+at,u.data(),u.size());at+=u.size();memcpy(t.data()+at,"Revora",6);at+=6;bool ok=Hmac(secret.data(),t.data(),(DWORD)at,proof.data());const size_t size=92+u.size();if(ok&&size<=(size_t)capacity){Put16(out,(std::uint16_t)size);Put16(out+2,ProofOpcode);memcpy(out+4,Magic,4);Put16(out+8,Version);Put16(out+10,(std::uint16_t)u.size());memcpy(out+12,&guid,16);memcpy(out+28,cn.data(),32);memcpy(out+60,proof.data(),32);memcpy(out+92,u.data(),u.size());Diagnostic("real_proof_sent","authorized");}Zero(secret.data(),32);Zero(proof.data(),32);Zero(t.data(),t.size());Zero(response.data(),response.size());if(!ok)Diagnostic("fallback_proof_sent","proof_construction_failure");return ok?(int)size:RejectionPacket(out,capacity,u);
}
extern "C" __declspec(dllexport) void __stdcall RevoraAuthClientProcess1637(const unsigned char*p,unsigned long n){AcquireSRWLockExclusive(&lock);worldHandle=0;handleUsed=false;if(p&&n==52&&p[0]==52&&p[1]==0&&p[2]==0x65&&p[3]==0x06&&p[12]==2&&p[13]==0&&p[14]==0&&p[15]==0){memcpy(&worldHandle,p+4,4);if(!worldHandle)handleUsed=true;}ReleaseSRWLockExclusive(&lock);}
extern "C" __declspec(dllexport) int __stdcall RevoraAuthClientPatch2429(unsigned char*p,unsigned long n){int result=0;AcquireSRWLockExclusive(&lock);if(p&&n>=8&&p[2]==0x7D&&p[3]==0x09&&worldHandle&&!handleUsed){memcpy(p+4,&worldHandle,4);handleUsed=true;worldHandle=0;result=1;}ReleaseSRWLockExclusive(&lock);return result;}
extern "C" void __stdcall RevoraAuthDirectEraseOnDetach();
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH)moduleInstance=instance;if(reason==DLL_PROCESS_DETACH){RevoraAuthDirectEraseOnDetach();worldHandle=0;handleUsed=true;ClearCapture();moduleInstance=nullptr;}return TRUE;}

#include "direct_proof.inl"

extern "C" void __stdcall RevoraDirectDiagnostic(const char* event,const char* category){Diagnostic(event,category);}
