"""Create a private, fixed-profile loopback derivative; never launch or publish it.

Uses the existing signed Revora server-list pair. The installed tree and its
RSA validation code are unchanged. This is not a complete playable client tree.
"""
import hashlib
import json
from pathlib import Path
import struct

SOURCE = Path(r"C:\Users\Administrator\Desktop\Revora\6711\Env_DX9")
TARGET = Path(r"C:\Revora\DisposablePostgres\moon-box-candidate\client-profile")
GENUINE = "3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60"
DERIVATIVE = "B6D6091CD026757BB9E6400557A44F72BA995D92586618D610861700F362D130"
DAT = "E08C555D34B7ABF7341EDE94EC8867EAAEBE5A6F0347A64ECEC1E0EEEE616E2F"
ENTRY, CAVE = 0x11CEB6, 0x7662E4


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def checked_read(name, expected):
    path = SOURCE / name
    if path.resolve() != path or path.is_symlink():
        raise ValueError("Unexpected source path")
    data = path.read_bytes()
    if digest(data) != expected:
        raise ValueError("Unexpected source hash: " + name)
    return data


def patch(source):
    pe = struct.unpack_from("<I", source, 0x3C)[0]
    if (source[:2] != b"MZ" or source[pe:pe + 4] != b"PE\0\0"
            or struct.unpack_from("<H", source, pe + 4)[0] != 0x14C
            or struct.unpack_from("<H", source, pe + 24)[0] != 0x10B):
        raise ValueError("Expected x86 PE32")
    prologue = bytes.fromhex("5581eca8000000")
    if source[ENTRY:ENTRY + 7] != prologue or source[CAVE:CAVE + 62] != bytes(62):
        raise ValueError("Hook preimage mismatch")
    # GetServerInfo(type=2, address, ..., port): preserve all other types.
    address = b"127.0.0.1\0\0\0"
    stub = (bytes.fromhex("837c240402752b8b442408c700") + address[:4]
            + bytes.fromhex("c74004") + address[4:8]
            + bytes.fromhex("c74008") + address[8:12]
            + bytes.fromhex("c6400c008b4c2410c701") + struct.pack("<I", 19558)
            + bytes.fromhex("b001c21000") + prologue
            + b"\xe9" + struct.pack("<i", ENTRY + 7 - (CAVE + 62)))
    branch = b"\xe9" + struct.pack("<i", CAVE - (ENTRY + 5)) + b"\x90\x90"
    if len(stub) != 62:
        raise ValueError("Invalid stub length")
    output = bytearray(source)
    output[ENTRY:ENTRY + 7] = branch
    output[CAVE:CAVE + 62] = stub
    changes = []
    for offset, replacement in ((ENTRY, branch), (CAVE, stub)):
        changes.append({"offset": offset,
                        "before": source[offset:offset + len(replacement)].hex(),
                        "after": replacement.hex()})
    # Restore both spans and require byte-for-byte source equality.
    restored = bytearray(output)
    for change in changes:
        before = bytes.fromhex(change["before"])
        restored[change["offset"]:change["offset"] + len(before)] = before
    if restored != source or len(output) != len(source):
        raise ValueError("Unexpected mutation outside endpoint hook")
    return bytes(output), changes


def main():
    if TARGET.exists() or TARGET.parent.resolve() != TARGET.parent:
        raise ValueError("Target exists or parent is redirected; inspect instead of overwriting")
    checked_read("Conquer.exe", GENUINE)
    source = checked_read("Conquer-Revora.exe", DERIVATIVE)
    dat = checked_read("RevoraServer.dat", DAT)
    output, changes = patch(source)
    TARGET.mkdir()
    (TARGET / "Conquer-Revora.exe").write_bytes(output)
    (TARGET / "RevoraServer.dat").write_bytes(dat)
    manifest = {
        "schema": "revora.moon-box.private-endpoint/1", "genuine_sha256": GENUINE,
        "source_sha256": DERIVATIVE, "output_sha256": digest(output),
        "server_dat_sha256": DAT, "account_address": "127.0.0.1",
        "account_port": 19558, "changes": changes,
        "native_validated": False, "complete_client_tree": False,
        "publication_approved": False,
        "rollback": "Discard this separate profile; installed source files are untouched."
    }
    (TARGET / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print("Private endpoint profile prepared; native validation remains pending.")
    print("Output SHA256: " + digest(output))


if __name__ == "__main__":
    main()
