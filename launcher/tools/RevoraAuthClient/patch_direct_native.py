"""Hash-gated direct Account derivative. No loader, hook, or legacy capture seam.

Research candidate until the native end-to-end acceptance gate passes. Input
is the preserved branded pre-auth derivative, not an installed auth candidate.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import lief

SOURCE = "93E5EF1946B9FE3161135E206DC5D5E2158E8379534E101A42B4127FA0E9092C"
DLL = "RevoraAuthClient.dll"
EXPORTS = ("RevoraAuthDirectSubmit", "RevoraAuthDirectSeedReady",
           "RevoraAuthDirectProcess1637", "RevoraAuthDirectPatch2429V2",
           "RevoraAuthOfficialReportSuppressed", "RevoraAuthOwnedControlsTick", "RevoraAuthGameConnect", "RevoraAuthGameDisconnectObserved")
SITES = {
    0x25969C: ("explicit Login entry; replace legacy construction", "680C010000"),
    0xA26985: ("after genuine 1059 seed initialization; skip legacy command", "6A0068E10C0000E8252BCAFF5959C3"),
    0x9FD0A2: ("processed 1637; preserve original handler", "684C060000"),
    0x9FC095: ("2429 construction before original transport", "558BEC5153"),
    0x65C411: ("PingTest report InternetOpen failure; retain local cleanup", "FF1564E70C01"),
    0x967280: ("DumpError report InternetOpen failure; retain local cleanup", "FF1564E70C01"),
    0x25CA6B: ("replace legacy credential input tick with owned controls", "E80B000000"),
    0x75D2F4: ("native Game connect call with categorical result", "E8F33A1A00"),
    0x900944: ("clear bound Game handoff before native manager close", "6A0CB896580A01"),
}

def digest(data):
    return hashlib.sha256(data).hexdigest().upper()

def generate(source):
    if digest(source) != SOURCE:
        raise ValueError("Unknown source hash")
    pe = lief.PE.parse(list(source))
    if pe.header.machine != lief.PE.Header.MACHINE_TYPES.I386:
        raise ValueError("Not x86")
    # Original image is fixed-base. Do not add unrelocated absolute references
    # to a relocatable image, or silently disable ASLR on such an image.
    if pe.optional_header.imagebase != 0x400000 or pe.optional_header.dll_characteristics & 0x40:
        raise ValueError("Unexpected image-base/ASLR contract")
    if pe.get_import(DLL):
        raise ValueError("Input already imports authentication component")
    for rva, (_, text) in SITES.items():
        pre = bytes.fromhex(text)
        offset = pe.rva_to_offset(rva)
        if source[offset:offset+len(pre)] != pre:
            raise ValueError(f"Preimage mismatch {rva:08X}")
    library = pe.add_import(DLL)
    for name in EXPORTS:
        library.add_entry(name)
    section = lief.PE.Section(".rvdirct")
    section.characteristics = 0x60000020
    section.content = [0xcc] * (len(SITES)*64)
    pe.add_section(section)
    config = lief.PE.Builder.config_t()
    config.imports = True
    builder = lief.PE.Builder(pe, config)
    builder.build()
    result = bytearray(builder.raw_bytes())
    built = lief.PE.parse(list(result))
    base = built.optional_header.imagebase
    section_rva = built.get_section(".rvdirct").virtual_address
    library = built.get_import(DLL)
    def call(index):
        return b"\xff\x15" + struct.pack("<I", base + library.get_entry(EXPORTS[index]).iat_address)
    def jump(source_rva, target):
        return b"\xe9" + struct.pack("<i", target-source_rva-5)
    stubs = [b"\x9c\x60\x51"+call(0)+b"\x61\x9d\xc3",
             b"\x9c\x60"+call(1)+b"\x61\x9d\xc3"]
    processed = b"\x9c\x60\x6a\x34\x8b\x81\x04\x04\x00\x00\x50"+call(2)+b"\x61\x9d"+bytes.fromhex(SITES[0x9FD0A2][1])
    processed += jump(section_rva+128+len(processed), 0x9FD0A7)
    stubs.append(processed)
    # ECX is the native transport, not a thread identity. Pass it as arg3
    # before ECX is reused for the declared packet length. Preserve the saved
    # registers/flags; a rejected 2429 returns false without reaching encryption.
    outgoing = bytes.fromhex(SITES[0x9FC095][1])+b"\x9c\x60\x51\x8b\x45\x08\x83\xc0\x04\x0f\xb7\x08\x51\x50"+call(3)
    outgoing += b"\x85\xc0\x75\x0b\x61\x9d\x5b\x8b\xe5\x5d\x31\xc0\xc2\x04\x00\x61\x9d"
    outgoing += jump(section_rva+192+len(outgoing), 0x9FC09A)
    stubs.append(outgoing)
    for category in (1, 2):
        stubs.append(b"\x9c\x60\x6a"+bytes([category])+call(4)+b"\x61\x31\xc0\x9d\xc2\x14\x00")
    # Native ECX dialog -> fastcall ECX dialog. No arguments on the stack.
    stubs.append(b"\x9c\x60"+call(5)+b"\x61\x9d\xc3")
    stubs.append(b"\xff\x25"+struct.pack("<I",base+library.get_entry(EXPORTS[6]).iat_address))
    closing=b"\x9c\x60\x51"+call(7)+b"\x61\x9d"+bytes.fromhex(SITES[0x900944][1])
    closing+=jump(section_rva+512+len(closing),0x90094B)
    stubs.append(closing)
    patches=[]
    for index, ((rva, (purpose, text)), stub) in enumerate(zip(SITES.items(), stubs)):
        if len(stub)>64:
            raise ValueError("Stub overflow")
        target=section_rva+index*64
        offset=built.rva_to_offset(target)
        result[offset:offset+len(stub)]=stub
        replacement=jump(rva,target)
        if 4<=index<=7:
            replacement=b"\xe8"+replacement[1:]
        pre=bytes.fromhex(text)
        replacement+=b"\x90"*(len(pre)-len(replacement))
        offset=built.rva_to_offset(rva)
        result[offset:offset+len(pre)]=replacement
        patches.append(dict(rva=f"0x{rva:08X}",preimage=text,postimage=replacement.hex().upper(),purpose=purpose))
    manifest=dict(schema=2,researchCandidate=True,directBridgeAbi="2429-v2-stdcall-12",sourceSha256=SOURCE,
                  destinationSha256=digest(result),dll=DLL,imports=EXPORTS,patches=patches)
    return bytes(result),manifest

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("source",type=Path)
    parser.add_argument("destination",type=Path)
    parser.add_argument("manifest",type=Path)
    parser.add_argument("--restore",action="store_true")
    args=parser.parse_args()
    raw=args.source.read_bytes()
    patched,manifest=generate(raw)
    if args.restore:
        if not args.destination.exists() or digest(args.destination.read_bytes())!=manifest["destinationSha256"]:
            raise SystemExit("Unknown destination; refusing restore")
        args.destination.write_bytes(raw)
    else:
        if args.source.resolve()==args.destination.resolve():
            raise SystemExit("Never overwrite source")
        args.destination.write_bytes(patched)
        args.manifest.write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
    print(manifest["destinationSha256"])

if __name__=="__main__":
    main()
