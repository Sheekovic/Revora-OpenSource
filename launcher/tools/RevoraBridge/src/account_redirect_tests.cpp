#include "account_redirect_policy.h"
#include "ipc_line_protocol.h"
#include "world_handle_bridge.h"
#include "native_authorization_policy.h"
#include <array>
#include <atomic>
#include <chrono>
#include <thread>
#include <cstdio>
#include <cstring>
#include <string>

namespace { int failures=0;void Check(bool value,const char *name){if(!value){++failures;std::printf("FAIL %s\n",name);}}
sockaddr_in Address(const char *ip,unsigned short port){sockaddr_in value{};value.sin_family=AF_INET;value.sin_port=htons(port);InetPtonA(AF_INET,ip,&value.sin_addr);return value;}}
int main(){WSADATA data{};if(WSAStartup(MAKEWORD(2,2),&data))return 2;sockaddr_in output{};
 auto exact=Address("170.33.9.35",12000);Check(revora_redirect::TryRewrite(reinterpret_cast<sockaddr*>(&exact),sizeof(exact),output),"exact_redirected");char ip[32]{};InetNtopA(AF_INET,&output.sin_addr,ip,sizeof(ip));Check(!std::strcmp(ip,"79.110.49.34")&&ntohs(output.sin_port)==9958,"target_exact");Check(exact.sin_port==htons(12000),"input_unchanged");
 const unsigned char expectedOriginal[8]{0x02,0x00,0x2E,0xE0,0xAA,0x21,0x09,0x23};const unsigned char expectedRedirect[8]{0x02,0x00,0x26,0xE6,0x4F,0x6E,0x31,0x22};Check(!std::memcmp(&exact,expectedOriginal,8),"original_prefix_exact");Check(!std::memcmp(&output,expectedRedirect,8),"redirect_prefix_exact");
 auto wrongIp=Address("170.33.9.36",12000);Check(!revora_redirect::TryRewrite(reinterpret_cast<sockaddr*>(&wrongIp),sizeof(wrongIp),output),"wrong_ip");auto wrongPort=Address("170.33.9.35",12001);Check(!revora_redirect::TryRewrite(reinterpret_cast<sockaddr*>(&wrongPort),sizeof(wrongPort),output),"wrong_port");
 sockaddr_in6 v6{};v6.sin6_family=AF_INET6;Check(!revora_redirect::TryRewrite(reinterpret_cast<sockaddr*>(&v6),sizeof(v6),output),"ipv6");Check(!revora_redirect::TryRewrite(nullptr,sizeof(exact),output),"null");Check(!revora_redirect::TryRewrite(reinterpret_cast<sockaddr*>(&exact),2,output),"short");
 WSASetLastError(WSAEACCES);int result=SOCKET_ERROR;int error=WSAGetLastError();Check(result==SOCKET_ERROR&&error==WSAEACCES,"return_error_preserved");
 const char*hex="0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";std::string challenge=std::string("CHALLENGE ")+hex;char normalized[80]{};std::size_t length=0;
 Check(revora_ipc::NormalizeFrame(challenge+"\n",normalized,sizeof(normalized),length)==revora_ipc::LineResult::ok&&revora_ipc::IsChallenge(normalized),"lf_challenge");std::string lf(normalized);
 Check(revora_ipc::NormalizeFrame(challenge+"\r\n",normalized,sizeof(normalized),length)==revora_ipc::LineResult::ok&&lf==normalized,"crlf_same");
 Check(revora_ipc::NormalizeFrame(challenge+"\r",normalized,sizeof(normalized),length)==revora_ipc::LineResult::missing_lf,"bare_cr");
 Check(revora_ipc::NormalizeFrame("CHALLENGE 01\r23\n",normalized,sizeof(normalized),length)==revora_ipc::LineResult::embedded_cr,"embedded_cr");
 Check(revora_ipc::NormalizeFrame(challenge+"\nREADY\n",normalized,sizeof(normalized),length)==revora_ipc::LineResult::multiple_lines,"multiple_lines");
 Check(revora_ipc::NormalizeFrame(challenge+"\r\r\n",normalized,sizeof(normalized),length)==revora_ipc::LineResult::embedded_cr,"double_cr");
 Check(revora_ipc::NormalizeFrame(challenge+" \n",normalized,sizeof(normalized),length)==revora_ipc::LineResult::ok&&!revora_ipc::IsChallenge(normalized),"trailing_space");
 Check(revora_ipc::NormalizeFrame(challenge+"\t\n",normalized,sizeof(normalized),length)==revora_ipc::LineResult::ok&&!revora_ipc::IsChallenge(normalized),"trailing_tab");
 Check(!revora_ipc::IsChallenge("CHALLENGE 01"),"short_challenge");Check(!revora_ipc::IsChallenge(challenge+"0"),"long_challenge");Check(!revora_ipc::IsChallenge(std::string("CHALLENGE ")+std::string(64,'G')),"nonhex_challenge");
 std::string oversized(100,'A');oversized.push_back('\n');Check(revora_ipc::NormalizeFrame(oversized,normalized,sizeof(normalized),length)==revora_ipc::LineResult::oversized,"oversized_line");
 void*fake=reinterpret_cast<void*>(1);int closes=0;revora_ipc::CloseAndReset(fake,nullptr,[&](void*){++closes;});Check(fake==nullptr&&closes==1,"handle_closed_reset");revora_ipc::CloseAndReset(fake,nullptr,[&](void*){++closes;});Check(closes==1,"handle_not_double_closed");
 std::array<std::byte,52> response{};response[0]=std::byte{52};response[2]=std::byte{0x65};response[3]=std::byte{0x06};const std::uint32_t known=0x78563412;std::memcpy(response.data()+4,&known,4);const std::uint32_t ready=2;std::memcpy(response.data()+12,&ready,4);revora_world::HandleBridge bridge;bridge.Begin(7);Check(bridge.Capture(response.data(),response.size(),7)==revora_world::Result::captured,"world_capture");
 std::array<std::byte,24> login{};login[0]=std::byte{24};login[2]=std::byte{0x7D};login[3]=std::byte{0x09};auto before=login;Check(bridge.Inject(login.data(),login.size(),7)==revora_world::Result::injected,"world_inject");Check(!std::memcmp(login.data()+4,&known,4),"world_exact_identifier");Check(!std::memcmp(login.data(),before.data(),4)&&!std::memcmp(login.data()+8,before.data()+8,16),"world_other_bytes_unchanged");Check(bridge.Inject(login.data(),login.size(),7)==revora_world::Result::rejected_replay,"world_replay_rejected");
 bridge.Begin(8);response[12]=std::byte{1};Check(bridge.Capture(response.data(),response.size(),8)==revora_world::Result::rejected_shape&&!bridge.Present(),"rejection_clears");response[12]=std::byte{2};std::uint32_t zero=0;std::memcpy(response.data()+4,&zero,4);Check(bridge.Capture(response.data(),response.size(),8)==revora_world::Result::rejected_shape,"zero_rejected");std::memcpy(response.data()+4,&known,4);response[2]=std::byte{0};Check(bridge.Capture(response.data(),response.size(),8)==revora_world::Result::rejected_shape,"wrong_opcode_rejected");response[2]=std::byte{0x65};Check(bridge.Capture(response.data(),51,8)==revora_world::Result::rejected_shape,"truncated_1637_rejected");
 bridge.Begin(9);Check(bridge.Capture(response.data(),response.size(),9)==revora_world::Result::captured,"recapture");Check(bridge.Inject(login.data(),login.size(),10)==revora_world::Result::rejected_state,"cross_session_rejected");revora_world::HandleBridge expiring(std::chrono::milliseconds(1));expiring.Begin(11);Check(expiring.Capture(response.data(),response.size(),11)==revora_world::Result::captured,"expiry_capture");std::this_thread::sleep_for(std::chrono::milliseconds(3));Check(expiring.Inject(login.data(),login.size(),11)==revora_world::Result::rejected_expired,"timeout_clears");
 revora_world::HandleBridge concurrent;concurrent.Begin(12);Check(concurrent.Capture(response.data(),response.size(),12)==revora_world::Result::captured,"concurrent_capture");std::atomic_int successes=0;auto a=before,b2=before;std::thread t1([&]{if(concurrent.Inject(a.data(),a.size(),12)==revora_world::Result::injected)++successes;});std::thread t2([&]{if(concurrent.Inject(b2.data(),b2.size(),12)==revora_world::Result::injected)++successes;});t1.join();t2.join();Check(successes==1,"concurrent_single_use");
 using revora_native_auth::Classify;using revora_native_auth::MayRelease;using revora_native_auth::Outcome;constexpr auto correlation="11111111-2222-3333-4444-555555555555";
 Check(Classify(200,"valid",correlation,true)==Outcome::accepted&&MayRelease(Outcome::accepted),"validated_200_releases");
 Check(Classify(400,R"({"error":"request_rejected"})",correlation,false)==Outcome::definitive_rejection&&MayRelease(Outcome::definitive_rejection),"exact_400_releases");
 for(const auto body:{"","{}",R"({"error":"request_rejected"} )",R"({"error":"other"})","request_rejected"})Check(Classify(400,body,correlation,false)==Outcome::indeterminate,"other_400_suppressed");
 for(const unsigned long status:{0ul,301ul,302ul,401ul,403ul,404ul,408ul,429ul,500ul,503ul})Check(Classify(status,R"({"error":"request_rejected"})",correlation,false)==Outcome::indeterminate,"infrastructure_suppressed");
 Check(Classify(200,"valid",correlation,false)==Outcome::indeterminate,"invalid_success_suppressed");Check(Classify(200,"valid","",true)==Outcome::indeterminate,"missing_correlation_suppressed");Check(!MayRelease(Outcome::indeterminate),"indeterminate_never_releases");
 WSACleanup();if(failures)return 1;std::puts("Revora Account redirect tests passed.");return 0;}
