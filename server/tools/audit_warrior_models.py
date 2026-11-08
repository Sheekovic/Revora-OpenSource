"""Read-only Warrior encounter asset evidence. Does not execute or patch the client."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import struct


def require(value, message):
    if not value:
        raise ValueError(message)


def parts(data, magic, width):
    require(len(data) >= 8 and data[:4] == magic, "Unexpected parts header")
    count = struct.unpack_from('<I', data, 4)[0]
    require(count <= (len(data) - 8) // 8, "Invalid record count")
    offset, result = 8, {}
    for _ in range(count):
        require(offset + 8 <= len(data), "Truncated parts record")
        key, size = struct.unpack_from('<II', data, offset)
        offset += 8
        require(size <= (len(data) - offset) // width, "Truncated parts array")
        values = [struct.unpack_from('<' + 'I' * (width // 4), data, offset + i * width)
                  for i in range(size)]
        require(key not in result or result[key] == values, "Conflicting parts identity")
        result[key] = values
        offset += size * width
    require(offset == len(data), "Unexpected parts trailer")
    return result


def resources(data, wide=False):
    require(len(data) >= 8 and data[:4] == b'RSDB', "Unexpected resource header")
    count = struct.unpack_from('<I', data, 4)[0]
    width = 12 if wide else 8
    end = 8 + count * width
    require(end <= len(data), "Truncated resource index")
    result = {}
    for i in range(count):
        key, offset = struct.unpack_from('<QI' if wide else '<II', data, 8 + i * width)
        require(end <= offset < len(data), "Resource path outside string table")
        stop = data.find(b'\0', offset)
        require(stop >= offset and stop - offset <= 512, "Invalid resource path")
        path = data[offset:stop].decode('latin-1').replace('\\', '/')
        # Some unrelated catalog entries conflict. Do not guess engine precedence;
        # mark them ambiguous and reject if a selected encounter references one.
        if key in result and (result[key] is None or result[key].lower() != path.lower()):
            result[key] = None
        else:
            result[key] = path
    return result


def audit(client):
    client = client.resolve()
    hashes = {}

    def read(relative):
        require(relative is not None, "Selected encounter references an ambiguous resource")
        require(relative.isascii(), "Selected encounter needs an unverified path encoding")
        path = PurePosixPath(relative.replace('\\', '/'))
        require(not path.is_absolute() and '..' not in path.parts and ':' not in str(path), "Unsafe resource path")
        resolved = (client / str(path)).resolve()
        require(resolved.is_relative_to(client), "Resource leaves client directory")
        data = resolved.read_bytes()
        hashes[str(path)] = hashlib.sha256(data).hexdigest()
        return data

    simple = parts(read('ini/3DSimpleObj.dbc'), b'SIMO', 8)
    armor = parts(read('ini/armor.dbc'), b'MESH', 16)
    geometry = resources(read('ini/3DObj.dbc'))
    textures = resources(read('ini/3DTexture.dbc'))
    motions = resources(read('ini/3dmotion.dbc'), True)
    require(simple[770] == [(9997700, 9997700)], "Sealing Stele NPC resources changed")
    require(len(armor[916000000]) == 1 and armor[916000000][0][:2] == simple[770][0],
            "Monster 916 no longer shares the Sealing Stele geometry and texture")
    profiles = []
    for body in (916, 917, 918, 919):
        key = body * 1000000
        require(len(armor[key]) == 1, "Encounter armor no longer has one part")
        mesh, texture, _, _ = armor[key][0]
        read(geometry[mesh])
        read(textures[texture])
        actions = {}
        # Indexed action resources only; runtime action selection is not certified.
        for action in (100, 110, 120, 310):
            motion = motions[body * 10000000 + action]
            read(motion)
            actions[action] = motion
        profiles.append(dict(body=body, geometry=geometry[mesh], texture=textures[texture], motions=actions))
    return dict(profiles=profiles, sha256=hashes,
                evidence='Body 916 shares exact Sealing Stele NPC geometry/texture. Bodies 917-919 have complete indexed monster resources.',
                limits='Static resource references only. Adjacency does not prove Mind of Evil identity or native rendering. No encounter or live spawn is performed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', type=Path, default=Path(__file__).resolve().parents[3] / '6711')
    args = parser.parse_args()
    print(json.dumps(audit(args.client), indent=2))
