// Included in revora_auth_client.cpp: reuses the owned HTTPS/HMAC primitives,
// never the legacy capture state or the zero-proof rejection constructor.
#include "direct_guid.h"
namespace {
struct ErasedText {
    std::string value;
    ~ErasedText(){ if(!value.empty()) Zero(value.data(),value.size()); }
};
bool AppendJsonString(std::string& json,const char* input,size_t maximum) {
    if(!input) return false;
    json.push_back('"');
    for(size_t i=0;i<=maximum;++i) {
        const unsigned char ch=static_cast<unsigned char>(input[i]);
        if(!ch){json.push_back('"');return i!=0;}
        if(i==maximum || ch<0x20) return false;
        if(ch=='"'||ch=='\\') json.push_back('\\');
        json.push_back(static_cast<char>(ch));
    }
    return false;
}
bool DirectExpiry(const std::string& text) {
    // Only the serializer's explicit UTC representation is accepted.
    if(text.size()<20 || text.size()>35)return false;
    auto digits=[&](size_t at,size_t count,unsigned& value){
        value=0;if(at+count>text.size())return false;
        for(size_t i=at;i<at+count;++i){if(text[i]<'0'||text[i]>'9')return false;value=value*10+text[i]-'0';}return true;};
    unsigned y,m,d,h,min,s;
    if(!digits(0,4,y)||!digits(5,2,m)||!digits(8,2,d)||!digits(11,2,h)||!digits(14,2,min)||!digits(17,2,s)
        ||text[4]!='-'||text[7]!='-'||text[10]!='T'||text[13]!=':'||text[16]!=':')return false;
    size_t end=19;unsigned milliseconds=0;
    if(text[end]=='.'){
        ++end;size_t start=end;
        while(end<text.size()&&text[end]>='0'&&text[end]<='9'){
            if(end-start<3)milliseconds=milliseconds*10+text[end]-'0';++end;}
        if(end==start||end-start>7)return false;
        for(size_t n=end-start;n<3;++n)milliseconds*=10;
    }
    if(text.substr(end)!="Z"&&text.substr(end)!="+00:00")return false;
    SYSTEMTIME utc{};utc.wYear=static_cast<WORD>(y);utc.wMonth=static_cast<WORD>(m);utc.wDay=static_cast<WORD>(d);
    utc.wHour=static_cast<WORD>(h);utc.wMinute=static_cast<WORD>(min);utc.wSecond=static_cast<WORD>(s);utc.wMilliseconds=static_cast<WORD>(milliseconds);
    FILETIME expiry{},now{};if(!SystemTimeToFileTime(&utc,&expiry))return false;GetSystemTimeAsFileTime(&now);
    ULARGE_INTEGER a{},b{};a.LowPart=expiry.dwLowDateTime;a.HighPart=expiry.dwHighDateTime;
    b.LowPart=now.dwLowDateTime;b.HighPart=now.dwHighDateTime;
    return a.QuadPart>b.QuadPart && a.QuadPart-b.QuadPart<=120000000ULL;
}
int DirectProof(const char* user,const char* password,unsigned char* packet,size_t capacity) {
    if(!user||!password||!packet||capacity<124)return 0;
    Zero(packet,capacity);
    ErasedText username,body,response;
    username.value.assign(user,strnlen_s(user,33));
    if(!NormalizeUsername(username.value)){Diagnostic("proof_input_rejected","username_policy");return 0;}
    std::array<unsigned char,32> clientNonce{},serverNonce{},binding{},secret{},proof{};
    struct EraseArrays {
        std::array<unsigned char,32> &a,&b,&c,&d,&e;
        ~EraseArrays(){Zero(a.data(),a.size());Zero(b.data(),b.size());Zero(c.data(),c.size());Zero(d.data(),d.size());Zero(e.data(),e.size());}
    } erase{clientNonce,serverNonce,binding,secret,proof};
    if(BCryptGenRandom(nullptr,clientNonce.data(),32,BCRYPT_USE_SYSTEM_PREFERRED_RNG)){Diagnostic("proof_input_rejected","rng_failure");return 0;}
    body.value.reserve(4096);body.value="{\"username\":";
    if(!AppendJsonString(body.value,username.value.c_str(),32)){Diagnostic("proof_input_rejected","username_encoding");return 0;}
    body.value+=",\"password\":";
    if(!AppendJsonString(body.value,password,1024)){Diagnostic("proof_input_rejected","password_encoding");return 0;}
    body.value+=",\"clientNonce\":\""+B64(clientNonce.data(),32)+"\",\"realm\":\"Revora\",\"profile\":\"dx9\",\"clientContract\":\"patch-7630-revora-auth-v1\"}";
    int policy=Post(body.value,response.value);
    Zero(body.value.data(),body.value.size());body.value.clear();
    if(policy!=1){Diagnostic(policy==-1?"https_rejected":"https_indeterminate","direct_path");return policy==-1?-1:0;}
    revora::direct::Response decoded;
    if(!revora::direct::ParseResponse(response.value,decoded)){Diagnostic("https_response_rejected","schema");return 0;}
    if(!DirectExpiry(decoded.fields[4])){Diagnostic("https_response_rejected","expiry");return 0;}
    const auto& id=decoded.fields[0];
    GUID guid{};
    if(!RevoraParseChallengeGuid(id,guid)){Diagnostic("https_response_rejected","challenge_identifier");return 0;}
    auto decode=[&](size_t index,unsigned char* bytes){
        const auto& value=decoded.fields[index];
        if(value.size()!=44||!UnB64(value,bytes,32))return false;
        ErasedText canonical;canonical.value=B64(bytes,32);return canonical.value==value;};
    if(!decode(1,serverNonce.data())||!decode(2,binding.data())||!decode(3,secret.data())){Diagnostic("https_response_rejected","encoding");return 0;}
    std::array<unsigned char,152> transcript{};
    size_t at=0;
    memcpy(transcript.data()+at,&guid,16);at+=16;
    for(const auto* part:{&serverNonce,&clientNonce,&binding}){memcpy(transcript.data()+at,part->data(),32);at+=32;}
    transcript[at++]=1;transcript[at++]=0;
    memcpy(transcript.data()+at,username.value.data(),username.value.size());at+=username.value.size();
    memcpy(transcript.data()+at,"Revora",6);at+=6;
    const bool ok=Hmac(secret.data(),transcript.data(),static_cast<DWORD>(at),proof.data());
    Zero(transcript.data(),transcript.size());
    if(!ok||!DirectExpiry(decoded.fields[4]))return 0;
    const auto count=92+username.value.size();
    Put16(packet,static_cast<std::uint16_t>(count));Put16(packet+2,ProofOpcode);
    memcpy(packet+4,Magic,4);Put16(packet+8,Version);Put16(packet+10,static_cast<std::uint16_t>(username.value.size()));
    memcpy(packet+12,&guid,16);memcpy(packet+28,clientNonce.data(),32);memcpy(packet+60,proof.data(),32);
    memcpy(packet+92,username.value.data(),username.value.size());
    Diagnostic("https_accepted","direct_proof_constructed_not_sent");
    return static_cast<int>(count);
}
}
extern "C" int __stdcall RevoraCreateDirectProof(const char* user,const char* password,unsigned char* packet,unsigned long capacity) {
    try { return DirectProof(user,password,packet,capacity); }
    catch(...) { if(packet&&capacity<=1024)Zero(packet,capacity);Diagnostic("https_indeterminate","local_exception");return 0; }
}
