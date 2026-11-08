"""Check Tomboy NPC/hunt routes in the shared server/native Desert City grid."""
import argparse
import hashlib
import json
import struct
import subprocess
from collections import deque
from pathlib import Path
from stage_gloomy_jail import index_records


def audit(client, runtime, sevenzip):
    server = (runtime / "database/maps/1000.map").read_bytes()
    archive = client / "map/map/desert.7z"
    sh, ah = hashlib.sha256(server).hexdigest(), hashlib.sha256(archive.read_bytes()).hexdigest()
    if sh != "c874aa5659420dfcf03545f1bfad67bea0197ae55fbc8140fcd7e85ffbae33f1" or ah != "22a6b93a38be33b3fd1cb736b2bdb091d4a87fc1bf78d431227051515e964d10":
        raise ValueError("Unreviewed Desert City terrain")
    native = subprocess.check_output([str(sevenzip), "e", "-so", str(archive), "desert.DMap"], timeout=30)
    nh = hashlib.sha256(native).hexdigest()
    if nh != "baedafd532acc86d81cdc08ed49b7ef0197e67c5e5d62ae0e8655265ccdd35f7":
        raise ValueError("Unreviewed native terrain")
    for path in (client / "ini/GameMap.dat", runtime / "database/GameMap.dat"):
        if [r for r in index_records(path.read_bytes()) if r[0] == 1000] != [(1000, b"map/map/desert.7z", 256)]:
            raise ValueError("Desert City binding changed")
    w, h = struct.unpack_from("<II", server)
    if (w, h) != (984, 984) or struct.unpack_from("<II", native, 268) != (w, h):
        raise ValueError("Terrain dimensions changed")
    blocked = bytearray(int(server[8+y*w+x] != 0 or struct.unpack_from("<H", native, 276+y*(w*6+4)+x*6)[0] != 0)
                        for y in range(h) for x in range(w))
    npcs = [(461, 661), (460, 667)]
    for x, y in npcs:
        if blocked[y*w+x]:
            raise ValueError("Blocked NPC center")
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                blocked[(y+dy)*w+x+dx] = 1
    start, targets = (459, 661), [(458, 667), (356, 358), (265, 264)]
    if any(blocked[y*w+x] for x, y in [start]+targets):
        raise ValueError("Blocked stand/hunt target")
    queue, distances = deque([start]), {start: 0}
    remaining = set(targets)
    while queue and remaining:
        x, y = queue.popleft()
        remaining.discard((x, y))
        for cell in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
            cx, cy = cell
            if 0 < cx < w-1 and 0 < cy < h-1 and not blocked[cy*w+cx] and cell not in distances:
                distances[cell] = distances[(x, y)] + 1
                queue.append(cell)
    return dict(map=1000, start=start, npc_footprints=npcs,
                targets=[dict(x=x, y=y, walk_steps=distances.get((x, y))) for x, y in targets],
                walk_route_verified=not remaining, sha256=dict(server=sh, archive=ah, native=nh),
                scope="Static four-direction walking with the two quest NPC footprints; other live objects and inter-city travel not validated.",
                native_acceptance=False)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--runtime", type=Path, default=Path("bin/Debug"))
    parser.add_argument("--sevenzip", type=Path, default=Path("C:/Program Files/7-Zip/7z.exe"))
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = audit(args.client, args.runtime, args.sevenzip)
    args.output.write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
    print(json.dumps(result))
