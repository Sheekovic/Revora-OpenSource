#pragma once
#include <winsock2.h>
#include <ws2tcpip.h>

namespace revora_redirect {
inline bool TryRewrite(const sockaddr *name,int length,sockaddr_in &copy) noexcept {
  if(!name||length<static_cast<int>(sizeof(sockaddr_in))||name->sa_family!=AF_INET)return false;
  const auto *v4=reinterpret_cast<const sockaddr_in*>(name);IN_ADDR expected{};
  if(InetPtonA(AF_INET,"170.33.9.35",&expected)!=1||v4->sin_addr.S_un.S_addr!=expected.S_un.S_addr||ntohs(v4->sin_port)!=12000)return false;
  copy=*v4;if(InetPtonA(AF_INET,"79.110.49.34",&copy.sin_addr)!=1)return false;copy.sin_port=htons(9958);return true;
}
}
