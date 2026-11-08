#define WIN32_LEAN_AND_MEAN
#include "../src/world_handoff.h"
#include <array>
#include <thread>
#include <atomic>
#include <cstdio>
#include <cstdlib>
void require(bool value){if(!value)std::abort();}
using Packet=std::array<unsigned char,32>;
Packet packet(){Packet p{};for(size_t i=0;i<p.size();++i)p[i]=static_cast<unsigned char>(i);p[0]=32;p[1]=0;p[2]=0x7d;p[3]=9;return p;}
int main(){
    WorldHandoff state;
    auto start=[&](){auto a=state.Begin(123);require(state.Capture(a,123,0x12345678,100,200));require(state.Bind(a,123,456,100));return a;};
    auto a=start();auto p=packet();const auto before=p;
    std::thread worker([&](){require(state.Inject(a,123,456,789,101,p.data(),p.size()));});worker.join();
    uint32_t value=0;memcpy(&value,p.data()+4,4);require(value==0x12345678);
    for(size_t i=0;i<p.size();++i)if(i<4||i>7)require(p[i]==before[i]);
    auto replay=packet();require(!state.Inject(a,123,456,789,102,replay.data(),replay.size()));require(replay==before);
    for(int test=0;test<7;++test){a=start();p=packet();if(test==6)state.Clear();
        require(!state.Inject(test==0?a+1:a,test==1?124:123,test==2?457:456,test==3?0:789,test==4?200:101,p.data(),test==5?7:p.size()));require(p==before);}
    a=start();std::atomic<int> accepted=0;std::array<std::thread,16> threads;
    for(auto& t:threads)t=std::thread([&](){auto local=packet();if(state.Inject(a,123,456,789,101,local.data(),local.size()))++accepted;});
    for(auto& t:threads)t.join();require(accepted==1);
    a=state.Begin(123);require(!state.Capture(a,123,0,100,200));
    a=start();require(!state.Bind(a,123,456,101));p=packet();require(!state.Inject(a,123,456,789,102,p.data(),p.size()));
    a=start();auto old=a;a=state.Begin(123);p=packet();require(!state.Inject(old,123,456,789,101,p.data(),p.size()));
    puts("PASS cross-thread exact-byte handoff, replay, expiry, PID/attempt/transport binding, malformed bounds, reconnect, cleanup, 16-worker single consumption");
}
