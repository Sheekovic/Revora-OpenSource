"""Verify Crypt entry and actor approaches against the installed client/server terrain.

No client execution or archive extraction to disk. This is static placement evidence,
not native rendering or private-scene ownership acceptance.
"""
import argparse
import hashlib
import json
import struct
import subprocess
from collections import deque
from pathlib import Path
from stage_gloomy_jail import index_records

HASHES = {
    "server": "27915507e6db3153afc7fa11b29c85b60fa51cbfc260afa93f023d241ed53d06",
    "archive": "dc43ec825a1fb11781ac2fe1f1e6da868948463457e02a643a9442478b49e562",
    "native": "6233285a2139c76cb14db29487820f039d032a4ecbd4423b2f7e3653f111a692",
}
ENTRY = (50, 55)
ACTORS = [(48, 51), (40, 50)]
APPROACHES = [(46, 51), (42, 50)]


def routes(server, native):
    w, h = struct.unpack_from("<II", server)
    if (w, h) != (126, 126) or struct.unpack_from("<II", native, 268) != (w, h):
        raise ValueError("Crypt dimensions changed")
    blocked = bytearray(int(server[8+y*w+x] != 0 or
                           struct.unpack_from("<H", native, 276+y*(w*6+4)+x*6)[0] != 0)
                        for y in range(h) for x in range(w))
    for x, y in ACTORS:
        if blocked[y*w+x]:
            raise ValueError("Crypt actor center blocked")
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                blocked[(y+dy)*w+x+dx] = 1
    if any(blocked[y*w+x] for x, y in [ENTRY]+APPROACHES):
        raise ValueError("Crypt entry/approach blocked")
    queue, distances = deque([ENTRY]), {ENTRY: 0}
    while queue:
        x, y = queue.popleft()
        for point in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
            nx, ny = point
            if 0 < nx < w-1 and 0 < ny < h-1 and not blocked[ny*w+nx] and point not in distances:
                distances[point] = distances[(x, y)]+1
                queue.append(point)
    if any(point not in distances for point in APPROACHES):
        raise ValueError("Crypt actor approach disconnected")
    return [{"x": x, "y": y, "walk_steps": distances[(x, y)]} for x, y in APPROACHES]


def audit(client, runtime, sevenzip):
    for path in (client / "ini/GameMap.dat", runtime / "database/GameMap.dat"):
        if [r for r in index_records(path.read_bytes()) if r[0] == 1801] != [(1801, b"map/map/mission.7z", 128)]:
            raise ValueError("Crypt map binding changed")
    server = (runtime / "database/maps/1801.map").read_bytes()
    archive = client / "map/map/mission.7z"
    if hashlib.sha256(archive.read_bytes()).hexdigest() != HASHES["archive"]:
        raise ValueError("Unreviewed Crypt archive")
    native = subprocess.check_output([str(sevenzip), "e", "-so", str(archive), "mission.DMap"], timeout=30)
    for key, data in (("server", server), ("native", native)):
        if hashlib.sha256(data).hexdigest() != HASHES[key]:
            raise ValueError("Unreviewed Crypt " + key)
    return dict(map=1801, dimensions=[126,126], entry=ENTRY,
                actors=[dict(name="ObscureWarrior", position=ACTORS[0], placement="original journal"),
                        dict(name="StoneColumn", position=ACTORS[1], placement="Revora return placement")],
                approaches=routes(server,native), sha256=HASHES, native_acceptance=False,
                scope="Shared static four-direction walking with both 3x3 NPC footprints; no live actors, ownership, teleport or rendering acceptance.")


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
