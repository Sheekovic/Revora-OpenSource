"""Read-only approved-6711 NPC placement builder audit; no native execution.

Records semantic instruction assertions, never exports proprietary binary data.
The three-argument preview path is a furniture candidate, not live acceptance.
"""
from pathlib import Path
import hashlib
import json
import capstone
import pefile


def audit():
    client = Path(__file__).resolve().parents[3] / "6711/Env_DX9/Conquer.exe"
    data = client.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != "3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60":
        raise ValueError("Unapproved executable")
    pe = pefile.PE(data=data)
    if pe.FILE_HEADER.Machine != 0x14c or pe.OPTIONAL_HEADER.ImageBase != 0x400000:
        raise ValueError("Unexpected architecture or base")
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
    checks = 0

    def instruction(address, mnemonic, operands):
        nonlocal checks
        actual = next(decoder.disasm(pe.get_data(address - 0x400000, 16), address))
        if (actual.mnemonic, actual.op_str) != (mnemonic, operands):
            raise ValueError("Instruction evidence changed at %x" % address)
        checks += 1

    # Serializer binds storage offsets to numeric protobuf fields.
    for address, offset, tag in ((0x96db93,0x34,4),(0x96dbab,0x38,5),
                                 (0x96dbc3,0x3c,6),(0x96dbdb,0x40,7),(0x96dbf3,0x44,8)):
        instruction(address,"push","dword ptr [esi + 0x%x]" % offset)
        instruction(address+3,"push",str(tag))
        instruction(address+5,"call","0xab7cc0")
    # Three-argument builder emits fields 4, 5 and 6, not the actor UID.
    instruction(0x96dfd3,"mov","ecx, dword ptr [esp + 0x10]")
    instruction(0x96dfd7,"mov","dword ptr [eax + 0x3c], ecx")
    instruction(0x96dfe4,"mov","ecx, dword ptr [esp + 8]")
    instruction(0x96dfe8,"mov","dword ptr [eax + 0x34], ecx")
    instruction(0x96dff5,"mov","ecx, dword ptr [esp + 0xc]")
    instruction(0x96dff9,"mov","dword ptr [eax + 0x38], ecx")
    instruction(0x96e01a,"mov","eax, 0x7ee")
    instruction(0x96e01f,"mov","word ptr [esi + 6], ax")
    instruction(0x96e032,"ret","0xc")
    for caller, send in ((0x792e6b,0x792e7a),(0x792eea,0x792ef9)):
        instruction(caller,"call","0x96dfbe")
        instruction(send,"call","0x7772cc")
    instruction(0x792e18,"push","0xee6b2801")
    instruction(0x792eaf,"push","esi")
    instruction(0x792eaa,"mov","esi, 0xee6b2801")
    instruction(0x8b9777,"call","0x96df20")
    instruction(0x96dfa2,"mov","eax, 0x7ee")
    # Inbound CMsgNpc action 5 activates the preview using field 4.
    instruction(0x92aeed,"cmp","ecx, 2")
    instruction(0x92aef7,"cmp","ecx, 5")
    instruction(0x92aefc,"mov","ecx, dword ptr [eax + 0x30]")
    instruction(0x92af17,"mov","eax, dword ptr [eax + 0x34]")
    instruction(0x92af22,"call","0x792c01")
    instruction(0x92a7a6,"push","dword ptr [esi + 0x30]")
    instruction(0x92a7a9,"push","3")
    instruction(0x92a7be,"push","dword ptr [esi + 0x34]")
    instruction(0x92a7c1,"push","4")
    instruction(0x724a26,"push","6")
    instruction(0x724a2e,"call","0x92aa7b")
    instruction(0x724a3d,"call","0x7772cc")
    instruction(0x72522d,"push","6")
    instruction(0x725235,"call","0x92aa7b")
    instruction(0x725244,"call","0x7772cc")
    instruction(0x9bec57,"imul","eax, eax, 0xa")
    instruction(0x9bec5a,"add","eax, dword ptr [ecx + 0x1c]")
    # Direction selects a scene path; the native reader visits row-major cells.
    instruction(0x9bf2dc,"push","edi")
    instruction(0x9bf2dd,"lea","ecx, [ebx + 0x20]")
    instruction(0x9bf2e0,"call","0x40d4d4")
    instruction(0x9bf2f2,"call","0x9a430d")
    instruction(0x9a4419,"push","0x40")
    instruction(0x9a4411,"lea","eax, [ebp]")
    instruction(0x9a461a,"cmp","dword ptr [ebp - 0x20], edi")
    instruction(0x9a4624,"cmp","dword ptr [ebp - 0x24], edi")
    instruction(0x9a4659,"call","0x9bff9a")
    instruction(0x9a4662,"mov","cx, word ptr [ebp - 0x54]")
    instruction(0x9a4666,"mov","word ptr [eax + 4], cx")
    instruction(0x9a466a,"mov","cx, word ptr [ebp - 0x50]")
    instruction(0x9a466e,"mov","word ptr [eax + 6], cx")
    instruction(0x9a4672,"mov","cx, word ptr [ebp - 0x74]")
    instruction(0x9a4676,"mov","word ptr [eax + 8], cx")
    # Overlay world lookup subtracts column/row from the resolved part anchor.
    instruction(0x9bc7ac,"call","0x9bff9a")
    instruction(0x9bc7c2,"call","0x9bc297")
    instruction(0x9bc7ca,"sub","dword ptr [ebp - 0x28], eax")
    instruction(0x9bc7cd,"sub","dword ptr [ebp - 0x24], ebx")
    instruction(0x9bc7dd,"call","0x99d133")
    instruction(0x9bc819,"mov","ax, word ptr [edi + 6]")
    instruction(0x9bc81d,"mov","word ptr [esi + 2], ax")
    instruction(0x9bc835,"add","ax, word ptr [edi + 8]")
    instruction(0x9bc84a,"call","0x99d3e2")
    instruction(0x99d3ff,"mov","dword ptr [eax + 8], edx")
    instruction(0x99d51a,"movzx","eax, word ptr [eax + 2]")
    instruction(0x994812,"call","0x99d500")
    instruction(0x994817,"cmp","eax, 1")
    instruction(0x99481a,"je","0x9948d5")
    instruction(0x9958c6,"call","0x99d500")
    instruction(0x9958cb,"cmp","eax, 1")
    instruction(0x9958ce,"je","0x99595a")
    instruction(0x9da749,"shl","edx, 6")
    instruction(0x9da74c,"add","edx, 0x20")
    instruction(0x9dbdff,"mov","eax, dword ptr [eax]")
    instruction(0x9dbe01,"add","eax, dword ptr [ebp + 8]")
    instruction(0x9bc2a6,"add","eax, dword ptr [esi + 4]")
    instruction(0x9bc2b0,"sar","eax, 6")
    return dict(approved_dx9=digest, instruction_checks=checks, packet=2030,
                preview_builder="0x96dfbe", emitted_fields=[4,5,6],
                other_builder="0x96df20", other_emitted_fields=[4,5,6,7,8],
                confidence="static preview, removal and scene-reader evidence; gameplay service is not evaluated by this audit; native acceptance remains open",
                native_acceptance=False)


if __name__ == "__main__":
    print(json.dumps(audit(), indent=2))
