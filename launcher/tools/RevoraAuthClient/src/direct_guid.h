#pragma once
#include <windows.h>
#include <objbase.h>
#include <string>
inline bool RevoraParseChallengeGuid(const std::string& text,GUID& value){
    value={};
    if(text.size()!=36)return false;
    for(size_t i=0;i<text.size();++i){
        if(i==8||i==13||i==18||i==23){if(text[i]!='-')return false;}
        else if(!((text[i]>='0'&&text[i]<='9')||(text[i]>='a'&&text[i]<='f')||(text[i]>='A'&&text[i]<='F')))return false;
    }
    // Identity JSON uses Guid "D". CLSIDFromString requires the braced form;
    // passing D directly yields CO_E_CLASSSTRING, even for a valid identifier.
    std::wstring braced=L"{"+std::wstring(text.begin(),text.end())+L"}";
    return CLSIDFromString(braced.c_str(),&value)==S_OK&&value!=GUID{};
}
