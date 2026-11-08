"""Hash-gated, read-only Boost price/request evidence; no client execution.

Requires pefile and capstone. This checks static instruction anchors, not native
acceptance, currency priority, or server payment correctness.
"""
import hashlib
import json
import struct
from pathlib import Path

import capstone
import pefile


def audit():
    root = Path(__file__).resolve().parents[1]
    client = root.parents[1] / "6711"
    binary = (client / "Env_DX9/Conquer.exe").read_bytes()
    config = (client / "ini/info.ini").read_bytes()
    labels = (client / "ini/Cn_Res.ini").read_bytes()
    if hashlib.sha256(binary).hexdigest() != "3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60":
        raise ValueError("Approved client hash mismatch")
    if hashlib.sha256(config).hexdigest() != "f698abcfc6595a6aba333b5b1c87545dd3b4891fffa284a2a6372139b5997337":
        raise ValueError("Price configuration hash mismatch")
    if hashlib.sha256(labels).hexdigest() != "de05f60546fe635b9d3a37c5134761ddf27d5c21851058cac2e65e585ef76599":
        raise ValueError("Localization hash mismatch")
    if b"STR_MONOPOLYPRE=<color=0xffff0000>(B)</color>" not in labels:
        raise ValueError("Bound item label mismatch")
    section = config.split(b"[EquipRefine]", 1)[1].split(b"[", 1)[0]
    settings = dict(line.strip().split(b"=", 1) for line in section.splitlines() if b"=" in line)
    if settings.get(b"Emoney") != b"8" or settings.get(b"RefineExp") != b"10":
        raise ValueError("Unexpected Boost conversion")
    pe = pefile.PE(data=binary, fast_load=True)
    if pe.FILE_HEADER.Machine != 0x14c or pe.OPTIONAL_HEADER.ImageBase != 0x400000:
        raise ValueError("Unexpected architecture/base")
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
    checks = 0

    def check(start, length, expected):
        nonlocal checks
        decoded = {i.address: (i.mnemonic, i.op_str) for i in decoder.disasm(
            pe.get_data(start - 0x400000, length), start)}
        for address, instruction in expected.items():
            if decoded.get(address) != instruction:
                raise ValueError("Instruction mismatch at %08x" % address)
            checks += 1

    for address, value in {
        0xb86478: b"EMoney\0", 0xb8646c: b"RefineExp\0",
        0xb84bd8: b"EquipRefine\0", 0xb75e7c: b"ini/info.Ini\0",
        0xb6e4d0: b"STR_MONOPOLYPRE\0",
    }.items():
        if pe.get_data(address - 0x400000, len(value)) != value:
            raise ValueError("Configuration key mismatch")
    check(0x8792ce, 7, {
        0x8792ce: ("mov", "al, byte ptr [ecx + 0x274]"),
    })
    check(0x8794f4, 0x10, {
        0x8794f4: ("call", "0x8792ce"),
        0x8794f9: ("dec", "al"),
        0x8794fb: ("mov", "cl, 2"),
        0x8794fd: ("cmp", "cl, al"),
        0x8794ff: ("sbb", "al, al"),
        0x879501: ("inc", "al"),
    })
    check(0x428982, 0x35, {
        0x428985: ("call", "0x8792ce"),
        0x42898a: ("cmp", "al, 1"),
        0x42898e: ("push", "0xb6e4d0"),
        0x4289b3: ("push", "eax"),
    })
    check(0x49fd40, 0x19f, {
        0x49fd40: ("push", "0xb86478"),
        0x49fd52: ("mov", "edi, 0xb84bd8"),
        0x49fd65: ("mov", "esi, 0xb75e7c"),
        0x49fd8d: ("call", "0x759b00"),
        0x49fd92: ("mov", "dword ptr [ebp + 0x38], eax"),
        0x49fdbc: ("push", "0xb8646c"),
        0x49fdff: ("call", "0x759b00"),
        0x49fe04: ("mov", "dword ptr [ebp + 0x3c], eax"),
        0x49fe43: ("call", "0x8794f4"),
        0x49fe5a: ("call", "0x904e6f"),
        0x49fe63: ("call", "0x7f0407"),
        0x49fe68: ("add", "esi, eax"),
        0x49fe73: ("call", "0x7f0407"),
        0x49fe7a: ("mov", "edi, dword ptr [ebp + 0x38]"),
        0x49fec2: ("div", "edi"),
        0x49fec6: ("imul", "eax, dword ptr [ebp + 0x3c]"),
        0x49feca: ("imul", "ecx, edi"),
        0x49fecf: ("mov", "dword ptr [ebx + 0x20c], ecx"),
    })
    check(0x49c5ef, 0x62, {
        0x49c606: ("call", "0x8c031a"),
        0x49c617: ("add", "esi, 0x20c"),
        0x49c61d: ("push", "esi"),
        0x49c61e: ("push", "1"),
        0x49c620: ("call", "dword ptr [eax + 0x68]"),
        0x49c623: ("push", "eax"),
        0x49c624: ("push", "3"),
        0x49c62c: ("call", "0x8c0788"),
        0x49c637: ("call", "0x7772cc"),
    })
    check(0x8c0788, 0x90, {
        0x8c079e: ("mov", "ecx, dword ptr [ebp + 8]"),
        0x8c07a4: ("mov", "dword ptr [eax + 0x28], ecx"),
        0x8c07b1: ("mov", "ecx, dword ptr [ebp + 0xc]"),
        0x8c07b4: ("mov", "dword ptr [eax + 0x2c], ecx"),
        0x8c07a1: ("mov", "ebx, dword ptr [ebp + 0x10]"),
        0x8c07bc: ("mov", "edi, dword ptr [ebp + 0x14]"),
        0x8c07ce: ("add", "ecx, 0x34"),
        0x8c07d1: ("call", "0x8c052d"),
        0x8c07ec: ("call", "0xab4e70"),
        0x8c07fb: ("mov", "eax, 0xcb3"),
        0x8c0800: ("mov", "word ptr [esi + 6], ax"),
    })
    if struct.unpack("<I", pe.get_data(0xc0642c - 0x400000, 4))[0] != 0x8c01f5:
        raise ValueError("Protobuf writer vtable mismatch")
    check(0x8c01f5, 0x95, {
        0x8c020b: ("push", "dword ptr [esi + 0x28]"),
        0x8c020e: ("push", "1"),
        0x8c0210: ("call", "0xab7cc0"),
        0x8c0224: ("push", "dword ptr [esi + 0x2c]"),
        0x8c0227: ("push", "2"),
        0x8c0229: ("call", "0xab7cc0"),
        0x8c0253: ("mov", "eax, dword ptr [esi + 0x34]"),
        0x8c0256: ("push", "dword ptr [eax + edi*4]"),
        0x8c0259: ("push", "4"),
        0x8c025b: ("call", "0xab7cc0"),
    })
    check(0xab7cc0, 0x27, {
        0xab7cc7: ("push", "0"),
        0xab7ccd: ("call", "0xab8790"),
        0xab7cdd: ("call", "0xab8820"),
    })
    check(0x8c089a, 0x197, {
        0x8c08f2: ("cmp", "ecx, 0x20"),
        0x8c08fb: ("cmp", "ecx, 0x22"),
        0x8c0900: ("lea", "eax, [esi + 0x34]"),
        0x8c095f: ("cmp", "ecx, 0x10"),
        0x8c0966: ("cmp", "ecx, 8"),
        0x8c096b: ("lea", "eax, [esi + 0x28]"),
        0x8c098f: ("lea", "eax, [esi + 0x2c]"),
        0x8c09dc: ("lea", "eax, [esi + 0x34]"),
    })
    return {"instruction_checks": checks, "price_cps": 8, "progress": 10,
            "packet": 3251, "operation": 3, "native_acceptance": False,
            "currency_priority_confirmed": False}


if __name__ == "__main__":
    print(json.dumps(audit(), sort_keys=True))
