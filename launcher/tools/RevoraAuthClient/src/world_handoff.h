#pragma once
#include <windows.h>
#include <cstdint>
#include <cstring>

// One process-local attempt, not one thread. No credential or packet retention.
class WorldHandoff final {
    SRWLOCK lock_ = SRWLOCK_INIT;
    uint64_t generation_=0, expires_=0;
    DWORD pid_=0;
    uintptr_t transport_=0, socket_=0;
    uint32_t value_=0;
    bool captured_=false, bound_=false, consumed_=false;
    void EraseValue(){SecureZeroMemory(&value_,sizeof(value_));}
public:
    // Process termination only: other threads are gone; never wait under loader lock.
    void EraseAtProcessExit(){EraseValue();pid_=0;captured_=bound_=false;consumed_=true;}
    uint64_t Begin(DWORD pid){
        AcquireSRWLockExclusive(&lock_);
        EraseValue();++generation_;pid_=pid;expires_=0;transport_=socket_=0;
        captured_=bound_=consumed_=false;
        const auto result=generation_;ReleaseSRWLockExclusive(&lock_);return result;
    }
    void Clear(){AcquireSRWLockExclusive(&lock_);EraseValue();captured_=false;bound_=false;consumed_=true;ReleaseSRWLockExclusive(&lock_);}
    bool Capture(uint64_t generation,DWORD pid,uint32_t value,uint64_t now,uint64_t expiry){
        AcquireSRWLockExclusive(&lock_);
        const bool valid=generation==generation_&&pid==pid_&&value&&!captured_&&!consumed_&&expiry>now&&expiry-now<=30000;
        if(valid){value_=value;expires_=expiry;captured_=true;}else{EraseValue();consumed_=true;}
        ReleaseSRWLockExclusive(&lock_);return valid;
    }
    bool Bind(uint64_t generation,DWORD pid,uintptr_t transport,uint64_t now){
        AcquireSRWLockExclusive(&lock_);
        const bool valid=generation==generation_&&pid==pid_&&transport&&captured_&&!bound_&&!consumed_&&now<expires_;
        if(valid){transport_=transport;bound_=true;}else{EraseValue();consumed_=true;}
        ReleaseSRWLockExclusive(&lock_);return valid;
    }
    // Called only for a proven 2429, with the exact native transport and a
    // verified live Game peer. The packet write and consumption are one lock.
    bool Inject(uint64_t generation,DWORD pid,uintptr_t transport,uintptr_t socket,uint64_t now,unsigned char* packet,size_t length){
        bool accepted=false;
        AcquireSRWLockExclusive(&lock_);
        __try {
            if(generation==generation_&&pid==pid_&&transport==transport_&&socket&&
               captured_&&bound_&&!consumed_&&value_&&now<expires_&&packet&&length>=8&&length<=1024&&
               static_cast<size_t>(packet[0]|packet[1]<<8)==length&&packet[2]==0x7d&&packet[3]==9){
                socket_=socket;memcpy(packet+4,&value_,4);accepted=true;
            }
            EraseValue();consumed_=true;
        } __finally {ReleaseSRWLockExclusive(&lock_);}
        return accepted;
    }
};
