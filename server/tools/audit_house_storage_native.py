"""Read-only approved-client storage framing audit. No execution or mutation."""
from pathlib import Path
import hashlib
import json
import capstone
import pefile
import struct

def audit():
    data=(Path(__file__).resolve().parents[3]/"6711/Env_DX9/Conquer.exe").read_bytes()
    digest=hashlib.sha256(data).hexdigest()
    if digest!="3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60":
        raise ValueError("Unapproved executable")
    pe=pefile.PE(data=data)
    decoder=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_32)
    expected=((0x97bb6f,"mov","ecx, 0xa4"),(0x97bb74,"mov","word ptr [eax], cx"),
              (0x97bb7d,"mov","ecx, 0x44e"),(0x97bb82,"mov","word ptr [eax + 2], cx"),
              (0x97bb8f,"mov","dword ptr [eax + 4], ecx"),(0x97bb9b,"mov","byte ptr [eax + 0xc], cl"),
              (0x97bba7,"mov","dword ptr [eax + 0x12], ecx"),(0x97bbb3,"mov","byte ptr [eax + 0xd], cl"),
              (0x97bbc0,"mov","word ptr [eax + 0xe], cx"),(0x97bcc0,"movzx","ecx, byte ptr [eax + 0xc]"),
              (0x81616d,"push","0"),(0x816175,"call","0x97bb5e"),
              (0x8161ff,"push","1"),(0x816207,"call","0x97bb5e"),
              (0x8162ab,"push","2"),(0x8162b3,"call","0x97bb5e"),
              (0x872d59,"mov","eax, dword ptr [ecx + 0x2c]"),
              (0x904b53,"mov","eax, dword ptr [ecx + 0x28]"),
              (0x898e0d,"mov","eax, dword ptr [ebp + 0xc]"),
              (0x898e10,"mov","dword ptr [esi + 0x28], eax"),
              (0x898e13,"mov","eax, dword ptr [ebp + 8]"),
              (0x898e1c,"mov","dword ptr [esi + 0x2c], eax"),
              (0x898e24,"mov","dword ptr [esi + 0x30], 0x3c"),
              (0x97bc19,"movzx","ecx, byte ptr [edx + 0xd]"),
              (0x97bc1e,"push","dword ptr [edx + 4]"),
              (0x97bc27,"call","0x816095"),
              (0x8160a5,"mov","eax, dword ptr [ebp + 0xc]"),
              (0x8160a8,"mov","dword ptr [ebp - 0x18], eax"),
              (0x8160ab,"mov","eax, dword ptr [ebp + 0x10]"),
              (0x8160ae,"mov","dword ptr [ebp - 0x14], eax"),
              (0x81615f,"call","0x872d59"),(0x816167,"call","0x904b53"),
              (0x4697a6,"cmp","eax, 3"),(0x4697a9,"setne","cl"),
              (0x4697ac,"dec","ecx"),(0x4697ad,"and","ecx, 0xfffffff6"),
              (0x4697b0,"add","ecx, 0x14"),
              (0x4697be,"push","dword ptr [esi + 0x1e0]"),
              (0x4697c6,"push","dword ptr [esi + 0x1e8]"),
              (0x4697cc,"call","0x81652e"),
              (0x96e44d,"cmp","eax, 0x38"),(0x96e456,"lea","eax, [esi + 0x40]"),
              (0x96e7b4,"mov","eax, dword ptr [eax + 0x40]"),
              (0x96e7ba,"mov","dword ptr [ecx + 0x3cc], eax"),
              (0x96e738,"je","0x96e8b5"),(0x96e73e,"cmp","ecx, 0x18"),
              (0x96e741,"jbe","0x96e74c"),(0x96e743,"cmp","ecx, 0x1a"),
              (0x96e746,"jbe","0x96e8eb"),
              (0x724a56,"mov","ecx, dword ptr [eax + 0x3cc]"),
              (0x724a6b,"dec","ecx"),(0x724a75,"movzx","ecx, byte ptr [ecx + 0x725531]"),
              (0x724a7c,"jmp","dword ptr [ecx*4 + 0x7254dd]"),
              (0x724b4c,"push","dword ptr [eax + 0x140]"),
              (0x724b59,"call","0x8d5931"),
              (0x8d5942,"xor","ebx, ebx"),(0x8d595b,"push","ebx"),
              (0x8d595c,"push","ebx"),(0x8d595d,"push","dword ptr [ebp + 8]"),
              (0x8d5966,"push","ebx"),(0x8d5967,"call","0x92aa7b"),
              (0x8d5976,"call","0x7772cc"),
              (0x92af0b,"cmp","ecx, 0x18"),(0x92af0e,"jbe","0x92af15"),
              (0x92af15,"push","0"))
    for address,mnemonic,operands in expected:
        ins=next(decoder.disasm(pe.get_data(address-0x400000,16),address))
        if (ins.mnemonic,ins.op_str)!=(mnemonic,operands):raise ValueError("Storage evidence changed at %x"%address)
    click_targets={}
    for npc_type in (3,4):
        slot=pe.get_data(0x725531-0x400000+npc_type-1,1)[0]
        target=struct.unpack('<I',pe.get_data(0x7254dd-0x400000+slot*4,4))[0]
        if target!=0x724b34:raise ValueError("NPC click dispatch changed")
        click_targets[npc_type]=hex(target)
    catalog=(Path(__file__).resolve().parents[3]/"6711/ini/npc.ini").read_bytes()
    catalog_hash=hashlib.sha256(catalog).hexdigest()
    if catalog_hash!='b06db1e2484992a1b58eabd7a3d884da07ac728a4a183d0fd18f2dccd5b20497':raise ValueError('NPC catalog changed')
    from audit_warrior_epic import sections
    box=[row for name,row in sections(catalog) if name==b'[NpcType820]']
    if len(box)!=1 or box[0].get(b'Name')!=b'ItemBox' or box[0].get(b'SimpleObjID')!=b'315':raise ValueError('Box asset profile changed')
    return dict(approved_dx9=digest,instruction_checks=len(expected),packet=1102,declared_length=164,framed_length=172,
                npc_catalog=catalog_hash,box_model=820,click_targets=click_targets,
                confidence="static spawn-type, NPC click and storage-kind paths; type-4 box profile is a Revora candidate, native rendered acceptance remains open")

if __name__=="__main__":print(json.dumps(audit(),indent=2))
