#pragma once
#include <cstdint>
// UI-only once gate. Native state 1013 is entered by the genuine map reply;
// 1010 is the runtime-confirmed original play state. Neither is an authentication claim.
struct NativeWorldUiGate {
    uint32_t owner=0; bool started=false,completed=false,failed=false;
    void Begin(uint32_t thread){owner=thread;started=completed=failed=false;}
    bool Start(uint32_t thread,unsigned state){
        if(!owner||thread!=owner||started||completed||failed||state!=1013)return false;
        started=true;return true;
    }
    bool Confirm(uint32_t thread,unsigned state,bool loginVisible,bool loadingVisible){
        if(thread!=owner||!started||completed||failed||state!=1010||loginVisible||loadingVisible)return false;
        completed=true;return true;
    }
    bool Fail(uint32_t thread){if(thread!=owner||failed||completed)return false;failed=true;return true;}
};
