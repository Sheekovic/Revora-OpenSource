#pragma once
#include <cstdint>
#include <cstddef>
// Read-only observation of the original MaTriX ActionQuery protobuf contract.
// It never authorizes, edits or suppresses a packet.
inline bool RevoraLocationRequested(const unsigned char* p,size_t length){
    if(!p||length<6||length>4096||static_cast<size_t>(p[0]|p[1]<<8)!=length||(p[2]|p[3]<<8)!=2099)return false;
    size_t at=4;bool found=false;
    auto varint=[&](uint64_t& value){value=0;for(unsigned n=0;n<10&&at<length;++n){unsigned b=p[at++];if(n==9&&b>1)return false;value|=uint64_t(b&127)<<(7*n);if(!(b&128))return true;}return false;};
    while(at<length){uint64_t tag=0,value=0;if(!varint(tag)||!(tag>>3))return false;
        switch(tag&7){
        case 0:if(!varint(value))return false;if((tag>>3)==12){if(found||value!=74)return false;found=true;}break;
        case 1:if(length-at<8)return false;at+=8;break;
        case 2:if(!varint(value)||value>length-at)return false;at+=static_cast<size_t>(value);break;
        case 5:if(length-at<4)return false;at+=4;break;
        default:return false;
        }
    }
    return found;
}
