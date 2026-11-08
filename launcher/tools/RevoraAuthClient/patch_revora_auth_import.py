import argparse, hashlib, json, pathlib, struct
import lief

EXPECTED = "93E5EF1946B9FE3161135E206DC5D5E2158E8379534E101A42B4127FA0E9092C"
CALL_RVA = 0x009B0342
CALL_PREIMAGE = bytes.fromhex("E8B7CC1700")
DLL = "RevoraAuthClient.dll"
EXPORTS = (
    "RevoraAuthClientBuildProof",
    "RevoraAuthClientProcess1637",
    "RevoraAuthClientPatch2429",
    "RevoraAuthClientBeginLogin",
    "RevoraAuthClientCapturePair",
)
PROCESSED_RVA, PROCESSED_PREIMAGE = 0x009FD0A2, bytes.fromhex("684C060000")
OUTGOING_RVA, OUTGOING_PREIMAGE = 0x009FC095, bytes.fromhex("558BEC5153")
LOGIN_RVA, LOGIN_PREIMAGE = 0x0025969C, bytes.fromhex("680C010000")
PAIR_RVA, PAIR_PREIMAGE = 0x002597E8, bytes.fromhex("51508BCEE893580000")

def sha(data): return hashlib.sha256(data).hexdigest().upper()
def main():
    p=argparse.ArgumentParser();p.add_argument("input");p.add_argument("output");p.add_argument("manifest")
    a=p.parse_args(); source=pathlib.Path(a.input).read_bytes()
    if sha(source)!=EXPECTED: raise SystemExit("unapproved Revora-Conquer input")
    pe=lief.PE.parse(list(source));
    patch_points = ((CALL_RVA,CALL_PREIMAGE),(PROCESSED_RVA,PROCESSED_PREIMAGE),
                    (OUTGOING_RVA,OUTGOING_PREIMAGE),(LOGIN_RVA,LOGIN_PREIMAGE),
                    (PAIR_RVA,PAIR_PREIMAGE))
    for rva,pre in patch_points:
        off=pe.rva_to_offset(rva)
        if source[off:off+len(pre)]!=pre: raise SystemExit(f"call preimage mismatch at {rva:08X}")
    imp=pe.add_import(DLL)
    for name in EXPORTS: imp.add_entry(name)
    section=lief.PE.Section(".revora");section.characteristics=(lief.PE.Section.CHARACTERISTICS.CNT_CODE|lief.PE.Section.CHARACTERISTICS.MEM_EXECUTE|lief.PE.Section.CHARACTERISTICS.MEM_READ);section.content=[0xCC]*320
    section=pe.add_section(section)
    config=lief.PE.Builder.config_t();config.imports=True
    builder=lief.PE.Builder(pe,config);builder.build();result=bytearray(builder.raw_bytes())
    rebuilt=lief.PE.parse(list(result));imports=rebuilt.get_import(DLL)
    iats={name:imports.get_entry(name).iat_address for name in EXPORTS}
    image_base=rebuilt.optional_header.imagebase
    sec=rebuilt.get_section(".revora");base_rva=sec.virtual_address
    def absjmp(iat): return b"\xff\x25"+struct.pack("<I",image_base+iat)
    build=absjmp(iats[EXPORTS[0]])
    processed=(b"\x60\x6a\x34\x8b\x81\x04\x04\x00\x00\x50\xff\x15"+struct.pack("<I",image_base+iats[EXPORTS[1]])+b"\x61"+PROCESSED_PREIMAGE+b"\xe9")
    processed+=struct.pack("<i",PROCESSED_RVA+5-(base_rva+32+len(processed)+4))
    outgoing=OUTGOING_PREIMAGE+b"\x60\x8b\x45\x08\x83\xc0\x04\x0f\xb7\x08\x51\x50\xff\x15"+struct.pack("<I",image_base+iats[EXPORTS[2]])+b"\x61\xe9"
    outgoing+=struct.pack("<i",OUTGOING_RVA+5-(base_rva+80+len(outgoing)+4))
    login=(b"\x9c\x60\x51\xff\x15"+struct.pack("<I",image_base+iats[EXPORTS[3]])+b"\x61\x9d"+LOGIN_PREIMAGE+b"\xe9")
    login+=struct.pack("<i",LOGIN_RVA+5-(base_rva+128+len(login)+4))
    original_pair_call_target=PAIR_RVA+len(PAIR_PREIMAGE)+struct.unpack("<i",PAIR_PREIMAGE[5:9])[0]
    relocated_pair_prefix=b"\x51\x50\x8b\xce\xe8"
    relocated_pair=relocated_pair_prefix+struct.pack("<i",original_pair_call_target-(base_rva+160+12+len(relocated_pair_prefix)+4))
    pair=b"\x9c\x60\x50\x51\xff\x15"+struct.pack("<I",image_base+iats[EXPORTS[4]])+b"\x61\x9d"+relocated_pair+b"\xe9"
    pair+=struct.pack("<i",PAIR_RVA+len(PAIR_PREIMAGE)-(base_rva+160+len(pair)+4))
    stubs=(build.ljust(32,b"\xcc")+processed.ljust(48,b"\xcc")+outgoing.ljust(48,b"\xcc")+
           login.ljust(32,b"\xcc")+pair)
    pair_displacement=struct.unpack("<i",stubs[177:181])[0]
    if base_rva+181+pair_displacement != original_pair_call_target:
        raise SystemExit("relocated pair call target mismatch")
    sec_off=rebuilt.rva_to_offset(base_rva);result[sec_off:sec_off+len(stubs)]=stubs
    targets=((CALL_RVA,base_rva),(PROCESSED_RVA,base_rva+32),(OUTGOING_RVA,base_rva+80),
             (LOGIN_RVA,base_rva+128),(PAIR_RVA,base_rva+160))
    for rva,target in targets:
        off=rebuilt.rva_to_offset(rva);pre=dict((x,y) for x,y in patch_points)[rva]
        result[off:off+len(pre)]=b"\xe9"+struct.pack("<i",target-(rva+5))+b"\x90"*(len(pre)-5)
        if rva==CALL_RVA: result[off:off+5]=b"\xe8"+struct.pack("<i",target-(rva+5))
    pathlib.Path(a.output).write_bytes(result)
    manifest={"schemaVersion":1,"sourceSha256":EXPECTED,"destinationSha256":sha(result),
      "dll":DLL,"imports":{k:f"0x{v:08X}" for k,v in iats.items()},"sectionRva":f"0x{base_rva:08X}",
      "relocatedPairCallTargetRva":f"0x{original_pair_call_target:08X}",
      "patches":[{"rva":f"0x{x:08X}","preimage":y.hex().upper(),"postimage":result[rebuilt.rva_to_offset(x):rebuilt.rva_to_offset(x)+len(y)].hex().upper()} for x,y in patch_points],"size":len(result)}
    pathlib.Path(a.manifest).write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
    print(manifest["destinationSha256"])
if __name__=="__main__": main()
