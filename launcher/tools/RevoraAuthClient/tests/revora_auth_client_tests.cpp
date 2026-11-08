#include <array>
#include <cstdint>
#include <cstring>
#include <iostream>
#include "../src/native_control_text.h"
#include "../src/direct_response.h"
#include "../src/direct_guid.h"
extern "C" unsigned long __stdcall RevoraAuthClientAbiVersion();
extern "C" void __stdcall RevoraAuthClientProcess1637(const unsigned char*,unsigned long);
extern "C" int __stdcall RevoraAuthClientPatch2429(unsigned char*,unsigned long);
extern "C" void __stdcall RevoraAuthClientBeginLogin(void*);
extern "C" int __stdcall RevoraAuthClientCaptureUsername(const void*);
extern "C" int __stdcall RevoraAuthClientCapturePassword(const void*);
extern "C" int __stdcall RevoraAuthClientCapturePair(const char*,const char*);
extern "C" int __stdcall RevoraAuthClientCaptureUsernameRaw(const char*,unsigned long);
extern "C" int __stdcall RevoraAuthClientCaptureObjects(const void*,const void*);
extern "C" int __cdecl RevoraAuthClientBuildProof(int,const char*,const char*,void*,void*,int,int,int,void*,int,void*,void*,unsigned char*,int);
struct ClientString { union { char inlineValue[16]; const char* heapValue; }; std::uint32_t length{}; std::uint32_t capacity{}; ClientString():inlineValue{}{} };
static ClientString MakeInline(const char* value){ClientString result{};result.length=(std::uint32_t)std::strlen(value);result.capacity=15;std::memcpy(result.inlineValue,value,result.length+1);return result;}
int main(int argc,char**argv){
 {
  GUID value{};
  if(!RevoraParseChallengeGuid("01234567-89ab-cdef-0123-456789abcdef",value)||value.Data1!=0x01234567||value.Data2!=0x89ab)return 60;
  for(const auto& invalid:{"00000000-0000-0000-0000-000000000000","{01234567-89ab-cdef-0123-456789abcdef}","01234567-89ab-cdef-0123-456789abcdeg","01234567_89ab-cdef-0123-456789abcdef"})
   if(RevoraParseChallengeGuid(invalid,value))return 61;
 }
 {
  // DISPLAY and owned password have different representations and ownership.
  // Synthetic-only values: never reuse an account password in test sources.
  auto owned=MakeInline("SyntheticOnly");
  std::array<char,1025> text{};
  if(!ReadNativePasswordUtf8(&owned,text.data(),text.size())||strcmp(text.data(),"SyntheticOnly"))return 50;
  SecureZeroMemory(text.data(),text.size());
  owned.inlineValue[2]='\0';
  if(ReadNativePasswordUtf8(&owned,text.data(),text.size()))return 51;
  for(char value:text)if(value)return 52;
  owned=MakeInline("SyntheticOnly");owned.length=256;owned.capacity=256;
  if(ReadNativePasswordUtf8(&owned,text.data(),text.size()))return 53;
  owned=MakeInline("SyntheticOnly");owned.capacity=1;
  if(ReadNativePasswordUtf8(&owned,text.data(),text.size()))return 54;
  if(ReadNativePasswordUtf8(nullptr,text.data(),text.size()))return 55;
  owned=MakeInline("SyntheticOnly");
  if(ReadNativePasswordUtf8(&owned,text.data(),2))return 56;
  for(char value:text)if(value)return 57;
  SecureZeroMemory(&owned,sizeof(owned));
 }
 {
  revora::direct::Response parsed;
  const std::string valid=R"({"challengeId":"synthetic","serverNonce":"a\u002Bb","connectionBinding":"c","sessionSecret":"d","expiresUtc":"e"})";
  if(!revora::direct::ParseResponse(valid,parsed)||parsed.fields[1]!="a+b")return 40;
  const std::string reordered=R"({"expiresUtc":"e","sessionSecret":"d","connectionBinding":"c","serverNonce":"a+b","challengeId":"synthetic"})";
  if(!revora::direct::ParseResponse(reordered,parsed)||parsed.fields[1]!="a+b")return 41;
  for(const auto& bad:std::array<std::string,6>{"{}",valid+"x",valid.substr(0,valid.size()-1),
      std::string(4097,'x'),
      R"({"challengeId":"a","serverNonce":"b","connectionBinding":"c","sessionSecret":"d","sessionSecret":"e"})",
      R"({"challengeId":"a","serverNonce":"b","connectionBinding":"c","sessionSecret":"\u0000","expiresUtc":"e"})"}) {
   if(revora::direct::ParseResponse(bad,parsed))return 42;
   for(const auto& field:parsed.fields)if(!field.empty())return 43;
  }
 }
 // Regression: the real getter returns UTF-16, with SSO capacity 7, not
 // the ANSI publication string previously (incorrectly) used by the DLL.
 struct WideObject { wchar_t text[8]; std::uint32_t count,capacity; };
 static_assert(sizeof(WideObject)==24);
 WideObject wide{{L'A',L'B',L'C',L'\0'},3,7};
 std::array<wchar_t,33> decoded{};size_t units=0;
 if(!ReadNativeControlUtf16(&wide,decoded.data(),decoded.size(),units)||units!=3||decoded[1]!=L'B')return 30;
 wide.count=8;
 if(ReadNativeControlUtf16(&wide,decoded.data(),decoded.size(),units))return 31;
 wide.count=3;wide.text[1]=L'\0';
 if(ReadNativeControlUtf16(&wide,decoded.data(),decoded.size(),units))return 32;
 wide.text[1]=0xD800;
 if(ReadNativeControlUtf16(&wide,decoded.data(),decoded.size(),units))return 33;
 if(argc==2&&std::strcmp(argv[1],"--https-negative")==0){std::array<unsigned char,472> packet{};RevoraAuthClientBeginLogin(nullptr);if(!RevoraAuthClientCapturePair("NoSuchAcct","SyntheticOnly"))return 20;int size=RevoraAuthClientBuildProof(0,nullptr,nullptr,nullptr,nullptr,0,0,0,nullptr,0,nullptr,nullptr,packet.data(),(int)packet.size());return size==102&&packet[2]==0x11&&packet[3]==0x7f?0:21;}
 if(RevoraAuthClientAbiVersion()!=0x00010000)return 1;
 std::array<unsigned char,52> response{};response[0]=52;response[2]=0x65;response[3]=0x06;response[12]=2;std::uint32_t expected=0x78563412;std::memcpy(response.data()+4,&expected,4);
 RevoraAuthClientProcess1637(response.data(),response.size());
 std::array<unsigned char,16> login{};login[0]=16;login[2]=0x7d;login[3]=0x09;auto before=login;
 if(RevoraAuthClientPatch2429(login.data(),login.size())!=1||std::memcmp(login.data()+4,&expected,4))return 2;
 for(size_t i=0;i<login.size();++i)if((i<4||i>7)&&login[i]!=before[i])return 3;
 if(RevoraAuthClientPatch2429(login.data(),login.size())!=0)return 4;
 response[12]=1;RevoraAuthClientProcess1637(response.data(),response.size());
 if(RevoraAuthClientPatch2429(login.data(),login.size())!=0)return 5;
 auto username=MakeInline("TestPlayer"),password=MakeInline("SyntheticOnly");
 RevoraAuthClientBeginLogin(nullptr);
 if(!RevoraAuthClientCaptureUsername(&username)||!RevoraAuthClientCapturePassword(&password))return 6;
 if(RevoraAuthClientCapturePassword(&password))return 7;
 RevoraAuthClientBeginLogin(nullptr);
 ClientString malformed{};malformed.length=20;malformed.capacity=15;
 if(RevoraAuthClientCaptureUsername(&malformed))return 8;
 RevoraAuthClientBeginLogin(nullptr);
 if(!RevoraAuthClientCapturePassword(&password)||!RevoraAuthClientCaptureUsername(&username))return 9;
 RevoraAuthClientBeginLogin(nullptr);
 if(!RevoraAuthClientCaptureObjects(&username,&password))return 15;
 RevoraAuthClientBeginLogin(nullptr);
 if(!RevoraAuthClientCaptureUsernameRaw("TestPlayer",10)||!RevoraAuthClientCapturePassword(&password))return 13;
 RevoraAuthClientBeginLogin(nullptr);
 if(RevoraAuthClientCaptureUsernameRaw("TestPlayer",0))return 14;
 RevoraAuthClientBeginLogin(nullptr);
 if(!RevoraAuthClientCapturePair("TestPlayer","SyntheticOnly"))return 10;
 if(!RevoraAuthClientCapturePair("TestPlayer","SyntheticOnly"))return 11;
 std::array<unsigned char,472> rejected{};
 RevoraAuthClientBeginLogin(nullptr);
 const int rejectedSize=RevoraAuthClientBuildProof(0,nullptr,nullptr,nullptr,nullptr,0,0,0,nullptr,0,nullptr,nullptr,rejected.data(),(int)rejected.size());
 if(rejectedSize!=99||rejected[2]!=0x11||rejected[3]!=0x7f||std::memcmp(rejected.data()+4,"RVA1",4))return 12;
 std::cout<<"RevoraAuthClient tests passed.\n";return 0;
}
