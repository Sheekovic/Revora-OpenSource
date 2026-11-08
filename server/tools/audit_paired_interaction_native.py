"""Read-only numeric checks for the approved DX9 paired-interaction constructor.

No client execution, byte export or native-acceptance claim. Requires pefile and
capstone. Static identity is not native acceptance.
"""
import hashlib
import json
import re
import struct
from pathlib import Path
import capstone
import pefile

EXPECTED = "3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60"


def audit():
    root = Path(__file__).resolve().parents[1]
    path = root.parents[1] / "6711/Env_DX9/Conquer.exe"
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != EXPECTED:
        raise ValueError("Approved client hash mismatch")
    pe = pefile.PE(data=data, fast_load=True)
    if pe.FILE_HEADER.Machine != 0x14c or pe.OPTIONAL_HEADER.ImageBase != 0x400000:
        raise ValueError("Unexpected architecture/base")
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
    checks = 0

    def check(start, length, expected):
        nonlocal checks
        instructions = {i.address: (i.mnemonic, i.op_str) for i in decoder.disasm(
            pe.get_data(start - 0x400000, length), start)}
        for address, fact in expected.items():
            if instructions.get(address) != fact:
                raise ValueError("Instruction mismatch at %08x" % address)
            checks += 1

    check(0x8bb1f7, 0xac, {
        0x8bb273: ("movzx", "ecx, word ptr [ebp + 8]"),
        0x8bb277: ("mov", "dword ptr [eax + 0x58], ecx"),
        0x8bb280: ("or", "dword ptr [eax + 0x20], 0x4000"),
        0x8bb287: ("mov", "ecx, dword ptr [ebp + 0x1c]"),
        0x8bb28a: ("mov", "dword ptr [eax + 0x60], ecx"),
        0x8bb293: ("mov", "ecx, dword ptr [ebp + 0x20]"),
        0x8bb296: ("or", "dword ptr [eax + 0x20], 0x800000"),
        0x8bb29d: ("mov", "dword ptr [eax + 0x84], ecx"),
    })
    check(0x724303, 0x6c, {
        0x724303: ("mov", "eax, dword ptr [ebx + 0x822b60]"),
        0x724309: ("cmp", "eax, 0x1c"),
        0x724310: ("sub", "eax, 0x18"),
        0x724322: ("mov", "ebx, 0x186a3"),
        0x724329: ("mov", "ebx, 0x186a2"),
        0x724330: ("mov", "ebx, 0x186a1"),
        0x724337: ("mov", "ebx, 0x186a0"),
        0x72433e: ("mov", "ebx, 0x186a4"),
        0x724345: ("sub", "eax, 0x1d"),
        0x72434d: ("sub", "eax, 0xd"),
        0x724356: ("mov", "ebx, 0x186aa"),
        0x72435d: ("mov", "ebx, 0x186a7"),
        0x724364: ("mov", "ebx, 0x186a5"),
    })
    check(0x7243fc, 0x33, {
        0x72440d: ("push", "dword ptr [ecx + 0x822b64]"),
        0x724419: ("push", "ebx"),
        0x724428: ("push", "0x2e"),
        0x72442a: ("call", "0x8bb1f7"),
    })
    check(0x7a3dbc, 0x2f, {
        0x7a3dcf: ("push", "0"),
        0x7a3dd1: ("push", "dword ptr [ebp + 0xc]"),
        0x7a3de4: ("push", "0x2f"),
        0x7a3de6: ("call", "0x8bb1f7"),
    })
    check(0x7a3ea6, 0x5b, {
        0x7a3ee5: ("push", "0"),
        0x7a3ee7: ("push", "dword ptr [ebp + 0xc]"),
        0x7a3efa: ("push", "0x30"),
        0x7a3efc: ("call", "0x8bb1f7"),
    })
    check(0x8ba58d, 0xe, {
        0x8ba58e: ("push", "dword ptr [esi + 0x54]"),
        0x8ba591: ("push", "0xc"),
        0x8ba593: ("call", "0xab7cc0"),
    })
    check(0x8ba5d5, 0xe, {
        0x8ba5d6: ("push", "dword ptr [esi + 0x60]"),
        0x8ba5d9: ("push", "0xf"),
        0x8ba5db: ("call", "0xab7cc0"),
    })
    check(0x8ba6ab, 0x11, {
        0x8ba6ac: ("push", "dword ptr [esi + 0x84]"),
        0x8ba6b2: ("push", "0x18"),
        0x8ba6b4: ("call", "0xab7cc0"),
    })
    check(0xab7cc0, 0x27, {
        0xab7cc7: ("push", "0"),
        0xab7cc9: ("mov", "ecx, dword ptr [ebp + 8]"),
        0xab7ccd: ("call", "0xab8790"),
        0xab7cd9: ("mov", "eax, dword ptr [ebp + 0xc]"),
        0xab7cdd: ("call", "0xab8820"),
    })
    check(0xab8790, 0x1e, {0xab879b: ("call", "0xab86b0")})
    check(0xab86b0, 0xe, {
        0xab86b3: ("mov", "eax, dword ptr [ebp + 8]"),
        0xab86b6: ("shl", "eax, 3"),
        0xab86b9: ("or", "eax, dword ptr [ebp + 0xc]"),
    })
    check(0x8bb9b2, 0x14, {
        0x8bb9b2: ("cmp", "eax, 0x78"),
        0x8bb9bb: ("lea", "eax, [esi + 0x60]"),
        0x8bb9c1: ("call", "0x771134"),
    })
    check(0x8bbbc2, 0x28, {
        0x8bbbc2: ("cmp", "eax, 0xc0"),
        0x8bbbcd: ("lea", "eax, [esi + 0x84]"),
        0x8bbbd6: ("call", "0x771134"),
        0x8bbbe3: ("or", "dword ptr [esi + 0x20], 0x800000"),
    })
    check(0x728e19, 0x26, {
        0x728e1c: ("push", "0x2b"),
        0x728e20: ("call", "0x6fb434"),
        0x728e35: ("push", "0xbf2ca4"),
    })
    # Dialog 360's command map binds control 2436 to effect 100004.
    check(0x4ff208, 0x3d, {
        0x4ff224: ("push", "0x168"),
        0x4ff229: ("call", "0x6d4d54"),
        0x4ff23a: ("mov", "dword ptr [esi], 0xb9ac1c"),
    })
    check(0x4ff479, 0xc, {
        0x4ff47a: ("push", "0x984"),
        0x4ff480: ("call", "0xab05d2"),
    })
    check(0x4ff343, 18, {
        0x4ff343: ("push", "0x186a4"),
        0x4ff348: ("push", "0xbf9"),
        0x4ff34d: ("call", "0x76225f"),
    })
    if struct.unpack("<6I", pe.get_data(0xb9ae38 - 0x400000, 24)) != (
            0x111, 0, 2436, 2436, 0x39, 0x4ff343):
        raise ValueError("Hand-holding command map mismatch")
    # Independent effect-indexed icon switch: case (100004 - 100000).
    if struct.unpack("<I", pe.get_data(0x4ff1d1 + 4 * 4 - 0x400000, 4))[0] != 0x4ff148:
        raise ValueError("Hand-holding icon switch mismatch")
    check(0x4ff148, 7, {0x4ff148: ("mov", "ebx, 0xb9aaac")})
    if pe.get_data(0xb9aaac - 0x400000, len(b"Mutual_HoldHandPic") + 1) != b"Mutual_HoldHandPic\0":
        raise ValueError("Hand-holding icon identity mismatch")
    gui = path.parents[1] / "ini/GUI.ini"
    gui_bytes = gui.read_bytes()
    gui_hash = hashlib.sha256(gui_bytes).hexdigest()
    if gui_hash != "8ef1d6cd6109fe4227bf28d9e877d30428e7680abd98093337d23119e252128b":
        raise ValueError("Interaction GUI resource changed")
    section = re.search(r"\[360-2436\]([^\[]*)", gui_bytes.decode("gbk", errors="strict"))
    if section is None or "\u7275\u624b" not in section.group(1):
        raise ValueError("Hand-holding GUI control label mismatch")
    key = b"STR_DOUBLEDANCE_CURSORTIP_MALE"
    if pe.get_data(0xbf2ca4 - 0x400000, len(key) + 1) != key + b"\0":
        raise ValueError("Dance cursor resource key mismatch")
    resource = path.parents[1] / "ini/Cn_Res.ini"
    resource_hash = hashlib.sha256(resource.read_bytes()).hexdigest()
    if resource_hash != "de05f60546fe635b9d3a37c5134761ddf27d5c21851058cac2e65e585ef76599":
        raise ValueError("Interaction localization resource changed")
    if key + b"=Select a lady to dance with." not in resource.read_bytes():
        raise ValueError("Dance cursor localization mismatch")
    return {"sha256": EXPECTED, "instruction_checks": checks,
            "ui_selector_effect_pairs": [[24,100000],[25,100001],[26,100002],
                                         [27,100003],[28,100004],[29,100005],
                                         [30,100007],[43,100010]],
            "verified": "Static constructor/caller and protobuf fields 15/24; selector 43 double-dance cursor; hand-holding control 2436/effect 100004",
            "resource_sha256": resource_hash, "gui_sha256": gui_hash,
            "pending": ["native acceptance"]}


if __name__ == "__main__":
    print(json.dumps(audit(), indent=2))
