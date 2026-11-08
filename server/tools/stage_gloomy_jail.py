"""Prepare a private, unsigned 1363 map repair from exact reviewed local inputs."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import struct
import tempfile

CLIENT_INDEX = "86a54928a9682617371542cdb5a72707c799b92a5c820e3581183adaa94f32b0"
SERVER_INDEX = "d1f35d6dc3dc0d1958a2aa612c4cdc988ed693636d89ad93078388e81cd23c19"
CLIENT_EXE = "3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60"
ARCHIVE = "5e54b7f276b845120e37bcf1e930315ed4e9008a1f05daf67735e95c5036b2c7"
DMAP = "ddce0fec05ab9d2dd1c6509cfd1867a08ce75f3143875f57ee920b80ca65102b"
RECORD = (1363, b"map/map/maidan-D.7z", 256)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def index_records(data):
    if not 4 <= len(data) <= 4 * 1024 * 1024:
        raise ValueError("Invalid map-index size")
    count = struct.unpack_from("<I", data)[0]
    if not 1 <= count <= 4096:
        raise ValueError("Invalid map-index count")
    offset, rows, seen = 4, [], {}
    for _ in range(count):
        if offset + 8 > len(data):
            raise ValueError("Truncated map-index record")
        identity, length = struct.unpack_from("<II", data, offset)
        offset += 8
        if not 1 <= length <= 260 or offset + length + 4 > len(data):
            raise ValueError("Invalid map-index path length")
        path = data[offset:offset + length]
        text = path.decode("ascii")
        parts = text.replace("\\", "/").split("/")
        if any(not part or part in (".", "..") for part in parts) or ":" in text or "\0" in text:
            raise ValueError("Unsafe map-index path")
        offset += length
        opaque = struct.unpack_from("<I", data, offset)[0]
        offset += 4
        if identity == 0:
            raise ValueError("Empty map identity")
        history = seen.setdefault(identity, [])
        history.append((path, opaque))
        # Both exact source indexes contain this conflicting historical pair.
        # Preserve its bytes and ordering; resolving it is outside this repair.
        if len(history) > 1 and (identity != 1067 or history != [
                (b"map/map/qiling-z.7z", 256), (b"map/map/09Christmas02.7z", 128)]):
            raise ValueError("Unreviewed duplicate map identity")
        rows.append((identity, path, opaque))
    if offset != len(data):
        raise ValueError("Unexpected map-index trailer")
    return rows


def add_map(data):
    rows = index_records(data)
    if any(row[0] == RECORD[0] for row in rows):
        raise ValueError("Map 1363 is already registered")
    for identity, suffix in ((1360, b"A"), (1361, b"B"), (1362, b"C"), (1364, b"E")):
        if (identity, b"map/map/maidan-" + suffix + b".7z", 256) not in rows:
            raise ValueError("Reviewed sibling registration is missing or changed")
    identity, path, opaque = RECORD
    output = (struct.pack("<I", len(rows) + 1) + data[4:]
              + struct.pack("<II", identity, len(path)) + path + struct.pack("<I", opaque))
    if index_records(output) != rows + [RECORD]:
        raise ValueError("Map-index preservation check failed")
    return output


def floor_from_dmap(data):
    if not 280 <= len(data) <= 32 * 1024 * 1024:
        raise ValueError("Invalid DMap size")
    if struct.unpack_from("<II", data) != (1004, 0):
        raise ValueError("Unreviewed DMap version")
    if data[8:268].split(b"\0", 1)[0] != b"map\\puzzle\\maidan-D.pul":
        raise ValueError("Unexpected DMap puzzle")
    width, height = struct.unpack_from("<II", data, 268)
    if not 1 <= width <= 4096 or not 1 <= height <= 4096 or width * height > 4_000_000:
        raise ValueError("Invalid DMap dimensions")
    offset, cells = 276, bytearray()
    for _ in range(height):
        end = offset + width * 6 + 4
        if end > len(data):
            raise ValueError("Truncated DMap row")
        for x in range(width):
            access = struct.unpack_from("<H", data, offset + x * 6)[0]
            if access not in (0, 1):
                raise ValueError("Unreviewed access value")
            cells.append(access)  # Revora .map uses 1 for blocked, 0 for walkable.
        offset = end  # The hash-gated input retains its opaque row checksum.
    if offset + 8 > len(data):
        raise ValueError("Truncated DMap objects")
    portals, objects = struct.unpack_from("<II", data, offset)
    offset += 8
    if portals != 0 or objects > 10000:
        raise ValueError("Unreviewed portal/object layout")
    for _ in range(objects):
        if offset + 420 > len(data) or struct.unpack_from("<I", data, offset)[0] != 4:
            raise ValueError("Unreviewed object or collision scene")
        offset += 420  # Type 4 is decorative in the existing server reader.
    if data[offset:] != b"\0\0\0\0":
        raise ValueError("Unexpected DMap trailer")
    return struct.pack("<II", width, height) + cells


def stage(client, server, extracted_dmap, destination):
    client, server = client.resolve(strict=True), server.resolve(strict=True)
    extracted_dmap = extracted_dmap.resolve(strict=True)
    destination = destination.resolve()
    roots = (client, server, extracted_dmap.parent)
    if destination.exists() or any(destination == root or root in destination.parents or destination in root.parents for root in roots):
        raise ValueError("Use a new separate private destination")
    inputs = {
        client / "Env_DX9/Conquer.exe": CLIENT_EXE,
        client / "ini/GameMap.dat": CLIENT_INDEX,
        client / "map/map/maidan-D.7z": ARCHIVE,
        server / "database/GameMap.dat": SERVER_INDEX,
        extracted_dmap: DMAP,
    }
    source = {}
    for path, expected in inputs.items():
        data = path.read_bytes()
        if digest(data) != expected:
            raise ValueError("Unreviewed input: " + str(path))
        source[path] = data
    floor = floor_from_dmap(source[extracted_dmap])
    if struct.unpack_from("<II", floor) != (560, 560) or floor[8:].count(0) != 134697:
        raise ValueError("Reviewed map dimensions/walkability changed")
    outputs = {
        "client/ini/GameMap.dat": add_map(source[client / "ini/GameMap.dat"]),
        "server/database/GameMap.dat": add_map(source[server / "database/GameMap.dat"]),
        "server/database/maps/1363.map": floor,
    }
    manifest = {
        "purpose": "Private unsigned map-1363 repair; installation and native acceptance pending",
        "inputs": [{"path": str(p), "sha256": h} for p, h in inputs.items()],
        "outputs": [{"path": p, "bytes": len(b), "sha256": digest(b)} for p, b in outputs.items()],
        "registration": {"map_id": 1363, "path": RECORD[1].decode(), "opaque_value": 256,
                         "confidence": "256 follows four installed sibling entries; native acceptance pending"},
        "terrain": {"width": 560, "height": 560, "walkable": 134697, "blocked": 178903, "decorative_objects": 178},
        "admission": "No NPC/quest entry or exit route is supplied by this terrain repair",
        "rollback": "No installed file was changed. Any future installation must preserve exact preimages.",
        "publication": "Not signed, deployed or approved for public redistribution",
    }
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="gloomy-stage-", dir=destination.parent) as temporary:
        work = Path(temporary)
        for relative, data in outputs.items():
            target = work / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        (work / "review.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        for path, expected in inputs.items():
            if digest(path.read_bytes()) != expected:
                raise ValueError("Input changed while staging")
        os.rename(work, destination)
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ("client", "server", "extracted_dmap", "destination"):
        parser.add_argument(argument, type=Path)
    args = parser.parse_args()
    result = stage(args.client, args.server, args.extracted_dmap, args.destination)
    for output in result["outputs"]:
        print(output["path"], output["sha256"])
