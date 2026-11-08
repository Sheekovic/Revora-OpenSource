"""Read-only, hash-bound checks of approved DX9 gem-composition boundaries.

Requires pefile and capstone. Does not execute the client or emit client bytes.
These are static checks, not evidence of an accepted live client transaction.
"""
import hashlib
import json
from pathlib import Path

import capstone
import pefile


EXPECTED = "3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60"


def audit():
    root = Path(__file__).resolve().parents[1]
    path = root.parents[1] / "6711/Env_DX9/Conquer.exe"
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED:
        raise ValueError("Approved DX9 executable hash mismatch")
    pe = pefile.PE(data=data, fast_load=True)
    if pe.FILE_HEADER.Machine != 0x14C or pe.OPTIONAL_HEADER.ImageBase != 0x400000:
        raise ValueError("Unexpected architecture or image base")
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
    checked = 0

    def check(start, length, expected):
        nonlocal checked
        decoded = {i.address: (i.mnemonic, i.op_str) for i in decoder.disasm(
            pe.get_data(start - pe.OPTIONAL_HEADER.ImageBase, length), start)}
        for address, instruction in expected.items():
            if decoded.get(address) != instruction:
                raise ValueError("Instruction boundary mismatch at %08X" % address)
            checked += 1

    check(0x8990F2, 0x7E, {
        0x899101: ("push", "0x5f"),
        0x89910D: ("mov", "ecx, 0x3f1"),
        0x89912E: ("mov", "dword ptr [eax + 8], ecx"),
        0x89913B: ("mov", "dword ptr [eax + 0xc], ecx"),
        0x899149: ("mov", "word ptr [eax + 0x14], cx"),
        0x899165: ("mov", "dword ptr [ecx + 0x16], eax"),
    })
    check(0x4DF502, 0x79, {
        0x4DF555: ("mov", "esi, dword ptr [ecx + 0x1c]"),
        0x4DF558: ("push", "0x27"),
        0x4DF55A: ("call", "0x8794f4"),
        0x4DF563: ("dec", "esi"),
        0x4DF56B: ("call", "0x8990f2"),
        0x4DF576: ("call", "0x7772cc"),
    })
    check(0x8792CE, 7, {0x8792CE: ("mov", "al, byte ptr [ecx + 0x274]")})
    check(0x4DFCD9, 0x108, {
        0x4DFCF5: ("lea", "esi, [ecx + 0x1f4]"),
        0x4DFD01: ("mov", "ecx, dword ptr [edx + 0x1c]"),
        0x4DFD07: ("inc", "ecx"),
        0x4DFD08: ("cmp", "eax, ecx"),
        0x4DFD6A: ("mov", "eax, dword ptr [ecx + 0x1c]"),
        0x4DFD6D: ("inc", "eax"),
        0x4DFD77: ("call", "0x87a717"),
    })
    check(0x8794F4, 16, {
        0x8794F4: ("call", "0x8792ce"),
        0x8794F9: ("dec", "al"),
        0x8794FB: ("mov", "cl, 2"),
        0x8794FD: ("cmp", "cl, al"),
        0x8794FF: ("sbb", "al, al"),
        0x879501: ("inc", "al"),
    })
    check(0x48EF24, 0x13F, {
        0x48EF4B: ("sub", "eax, dword ptr [esi + 0xc]"),
        0x48EF4E: ("and", "eax, 0xfffffffc"),
        0x48EF51: ("cmp", "eax, 0x1c"),
        0x48EF90: ("cmp", "eax, 0x186a0"),
        0x48EFEE: ("push", "0x33"),
        0x48EFF8: ("call", "0x8990f2"),
        0x48F028: ("call", "0x898ff5"),
        0x48F058: ("call", "0x7772cc"),
    })
    check(0x898FF5, 0x42, {
        0x899012: ("mov", "edx, dword ptr [eax + 0x1a]"),
        0x89901A: ("mov", "dword ptr [eax + edx*4 + 0x5b], esi"),
        0x899024: ("add", "word ptr [eax], 4"),
        0x89902E: ("inc", "dword ptr [ecx + 0x1a]"),
    })
    check(0x89AFFA, 13, {
        0x89AFFA: ("push", "dword ptr [eax + 0xc]"),
        0x89AFFD: ("push", "0xcfe"),
    })
    check(0x89B7E7, 0x38, {
        0x89B7EB: ("cmp", "dword ptr [eax + 0x1a], ebx"),
        0x89B7F0: ("push", "dword ptr [eax + edi*4 + 0x5b]"),
        0x89B7F4: ("push", "0xddb"),
        0x89B812: ("push", "dword ptr [eax + 0xc]"),
        0x89B815: ("push", "0xddc"),
    })
    return {"client_sha256": digest, "checked_instructions": checked,
            "evidence": "static only", "basic_record_length": 95,
            "tortoise_record_length": 123, "tortoise_selected_uids": 7,
            "native_acceptance": "not tested"}


if __name__ == "__main__":
    print(json.dumps(audit(), indent=2))
