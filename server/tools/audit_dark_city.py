"""Record target-6711 monster profiles and shared-grid Dark City spawn candidates."""
import argparse
import configparser
import hashlib
import io
import json
import struct
from collections import deque
from pathlib import Path

from ClientPatches.prepare_shop_buy_hotfix import transform
from stage_gloomy_jail import index_records

PROFILES = {
    "KidCatcher": (2150, 1500, 100, 0),
    "KidRipper": (2155, 1500, 100, 0),
    "KidHunter": (2151, 5000, 200, 10),
    "KidSiezer": (2152, 10000, 200, 10),
}
CHILDREN = [(150, 200), (160, 200), (170, 200), (150, 210), (170, 210)]
CELLS = [(150, 170), (150, 180), (160, 180), (170, 180),
         (180, 180), (150, 190), (160, 190), (170, 190), (180, 190)]


def profiles(text):
    # Parse only the selected sections: unrelated catalog duplicate keys are
    # irrelevant, but duplicate target sections/fields must not be accepted.
    selected = []
    active = False
    for line in text.splitlines():
        if line.startswith("["):
            active = line.strip() in {"[" + name + "]" for name in PROFILES}
        if active:
            selected.append(line)
    parser = configparser.ConfigParser(interpolation=None, strict=True)
    parser.read_file(io.StringIO("\n".join(selected)))
    if set(parser.sections()) != set(PROFILES):
        raise ValueError("Missing Dark City profile")
    result = []
    for name, expected in PROFILES.items():
        row = parser[name]
        keys = ("TypeID", "MaxLife", "ZoomPercent", "StcType")
        if tuple(row.getint(key) for key in keys) != expected:
            raise ValueError("Changed profile: " + name)
        if row.getint("Level") != 75 or row.getint("Boss") != 0:
            raise ValueError("Changed level/boss classification: " + name)
        result.append(dict(name=name, **{key: row.getint(key) for key in keys},
                           Level=75, Boss=0))
    return result


def audit(client, runtime):
    raw = (client / "ini/monster.dat").read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if digest != "99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585":
        raise ValueError("Unreviewed monster catalog")
    plain = transform(raw)
    if transform(plain, True) != raw:
        raise ValueError("Monster transform failed round trip")
    monsters = profiles(plain.decode("cp1252"))
    server_catalog = (runtime / "database/Monsters.txt").read_bytes()
    if profiles(server_catalog.decode("cp1252")) != monsters:
        raise ValueError("Client/runtime profiles disagree")
    server = (runtime / "database/maps/1780.map").read_bytes()
    native = (client / "map/map/slpk.DMap").read_bytes()
    sh, nh = (hashlib.sha256(data).hexdigest() for data in (server, native))
    if sh != "a8e54974e3127c3235a788269df3f9f662b3c40be93130638469022266fd532b" or nh != "6bf114e344b4b550200f654620da8e024013c440b60ba94005e42defb8c0870c":
        raise ValueError("Unreviewed Dark City terrain")
    for path in (client / "ini/GameMap.dat", runtime / "database/GameMap.dat"):
        if [r for r in index_records(path.read_bytes()) if r[0] == 1780] != [(1780, b"map/map/slpk.7z", 256)]:
            raise ValueError("Changed city binding")
    w, h = struct.unpack_from("<II", server)
    if (w, h) != (320, 320) or struct.unpack_from("<II", native, 268) != (w, h):
        raise ValueError("Changed city dimensions")
    blocked = bytearray(int(server[8+y*w+x] != 0 or
        struct.unpack_from("<H", native, 276+y*(w*6+4)+x*6)[0] != 0)
        for y in range(h) for x in range(w))
    for x, y in [(145, 134), (162, 217)] + CHILDREN:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                blocked[(y+dy)*w+x+dx] = 1
    def free(x, y):
        return 0 < x < w-1 and 0 < y < h-1 and not blocked[y*w+x]
    entry = (150, 150)
    if not free(*entry):
        raise ValueError("Blocked entry")
    queue, distances = deque([entry]), {entry: 0}
    while queue:
        x, y = queue.popleft()
        for cell in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
            if free(*cell) and cell not in distances:
                distances[cell] = distances[(x, y)] + 1
                queue.append(cell)
    if any(cell not in distances for cell in CELLS + [(x-2,y) for x,y in CHILDREN]):
        raise ValueError("Unreachable encounter cell")
    return dict(monsters=monsters, base_map=1780, entry=entry,
        spawn_candidates=[dict(x=x, y=y, walk_steps=distances[(x,y)]) for x,y in CELLS],
        children=[dict(x=x,y=y,stand=[x-2,y],walk_steps=distances[(x-2,y)]) for x,y in CHILDREN],
        placement_policy="Revora placements; MoMo, pillar and five child footprints included.",
        sha256=dict(client_monsters=digest, runtime_monsters=hashlib.sha256(server_catalog).hexdigest(), server_map=sh, native_map=nh),
        native_acceptance=False, combat_statistics_verified=False,
        note="Display profiles do not establish attack, defence, spawn sequence or kill-count semantics.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--runtime", type=Path, default=Path("bin/Debug"))
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = audit(args.client, args.runtime)
    args.output.write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
    print("Verified four matching profiles and nine reachable spawn candidates; native acceptance pending.")
