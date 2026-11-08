"""Read-only 6711 Warrior quest catalog and walking-route audit.

Print identifiers, hashes and route results only. No client assets are copied,
no database is opened and no active client or server file is changed.
"""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import re
import struct


QUESTS = (3678, 3679, 3695, 3680, 3681, 3682, 3683)
ITEMS = {624078: "SkyFlame", 3200206: "MemoryJadeSlips",
         3200207: "WarriorGod`sLetter", 3006347: "Pandora`sCall"}
MAPS = {
    10009: ((80, 80), (50, 55), ((42, 40), (57, 53)),
            "sswq-zs03.7z", "5e07cf1227e433021ba97f573fd453aaf8dacc2ee88115a42d45f692326818af"),
    10010: ((120, 120), (35, 45), ((83, 86), (35, 42)),
            "sswq-zs01b_new.7z", "18e8da52143bda414a91217348d0236508711745665a797233ab3fa0575c3c62"),
    10011: ((80, 80), (50, 55), ((41, 39), (49, 47), (39, 44)),
            "sswq-zs02.7z", "5964a858cf550804344da38aa4be0f70d491696f8bc08bf7661b0160fe2dacc2"),
}


def require(value, message):
    if not value:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def item_rows(data, encrypted=False, identities=None):
    identities = ITEMS if identities is None else identities
    if encrypted:
        seed, key = 0x2537, []
        for _ in range(128):
            seed = (seed * 214013 + 2531011) & 0xffffffff
            key.append((seed >> 16) & 255)
        plain = bytearray(len(data))
        for i, byte in enumerate(data):
            value, shift = byte ^ key[i % 128], i % 8
            plain[i] = ((value >> shift) | (value << (8 - shift))) & 255
            encoded = (((plain[i] << shift) | (plain[i] >> (8 - shift))) & 255) ^ key[i % 128]
            require(encoded == byte, "Client item catalog round trip failed")
        data = bytes(plain)
    found = {}
    for line in data.splitlines():
        fields = line.split(b"@@") if b"@@" in line else line.split()
        if fields and fields[0].isdigit() and int(fields[0]) in identities:
            identity = int(fields[0])
            require(identity not in found, "Duplicate quest item")
            found[identity] = fields
    require(set(found) == set(identities), "Missing requested quest item")
    return found


def sections(data):
    for block in re.split(rb"(?m)(?=^\[)", data):
        values = {}
        for line in block.splitlines():
            if b"=" in line:
                key, value = line.split(b"=", 1)
                values[key] = value
        yield block.splitlines()[0] if block else b"", values


def map_index(data):
    require(len(data) >= 4, "Truncated map index")
    count, = struct.unpack_from("<I", data)
    require(count < 100000, "Unexpected map count")
    cursor, found = 4, {}
    for _ in range(count):
        require(cursor + 8 <= len(data), "Truncated map index entry")
        identity, size = struct.unpack_from("<II", data, cursor)
        cursor += 8
        require(0 < size < 1024 and cursor + size + 4 <= len(data), "Invalid map path length")
        if identity in MAPS:
            require(identity not in found, "Duplicate quest map")
            found[identity] = data[cursor:cursor + size].decode("ascii")
        cursor += size + 4
    require(cursor == len(data) and set(found) == set(MAPS), "Incomplete map index")
    return found


def walking_route(data, identity):
    size, start, targets, _, expected = MAPS[identity]
    require(digest(data) == expected, "Reviewed server terrain changed: " + str(identity))
    width, height = struct.unpack_from("<ii", data)
    require((width, height) == size and len(data) == 8 + width * height, "Invalid terrain dimensions")
    grid = data[8:]
    require(set(grid) <= {0, 1}, "Unexpected terrain cell encoding")

    def valid(x, y):
        return 0 <= x < width and 0 <= y < height and grid[x + y * width] != 1

    # Conservatively reserve all candidate actor cells and their eight neighbors,
    # matching the ordinary talker branch of Map.AddNpc. Monster-only actors
    # may ultimately need less space; this proves the stricter walking route.
    occupied = {(x + dx, y + dy) for x, y in targets
                for dx in (-1, 0, 1) for dy in (-1, 0, 1)}
    require(valid(*start) and start not in occupied, "Blocked proposed landing")
    reached, pending = {start}, deque([start])
    while pending:
        x, y = pending.popleft()
        for point in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if point not in reached and point not in occupied and valid(*point):
                reached.add(point)
                pending.append(point)
    approaches = []
    for target in targets:
        near = [point for point in reached if max(abs(point[0] - target[0]), abs(point[1] - target[1])) <= 4]
        require(near, "No walking approach to quest actor")
        approaches.append({"target": target, "terrain_walkable": valid(*target),
                           "approach": min(near, key=lambda p: (abs(p[0]-target[0])+abs(p[1]-target[1]), p))})
    return {"map": identity, "size": size, "landing": start,
            "reachable_cells": len(reached), "approaches": approaches}


def audit(client, runtime):
    files = {}

    def read(root, relative, label):
        data = (root / relative).read_bytes()
        files[label] = digest(data)
        return data

    server = item_rows(read(runtime, "database/itemtype.txt", "server_items"))
    native = item_rows(read(client, "ini/itemtype.dat", "client_items"), True)
    for identity, name in ITEMS.items():
        require(server[identity][1].decode("ascii") == name, "Unexpected server item name")
        require(native[identity][:8] == server[identity][:8], "Client/server item identity or requirements differ")
    require(server[624078][2] == b"20" and server[624078][4] == b"40", "Fist class/level changed")
    quests = {}
    for _, values in sections(read(client, "ini/Questinfo.ini", "client_quests")):
        identity = int(values.get(b"MissionId", b"0"))
        if identity in QUESTS:
            require(identity not in quests, "Duplicate Warrior mission")
            quests[identity] = values
    require(set(quests) == set(QUESTS), "Missing Warrior journal mission")
    for index, identity in enumerate(QUESTS):
        entry = quests[identity]
        require(entry[b"Lv_min"] == b"40", "Quest level changed")
        require(int(entry[b"Prequest"]) == (QUESTS[index - 1] if index else 0), "Quest order changed")
        require(b"624078" in entry[b"Prize"], "Quest reward identity changed")
    profiles = dict(sections(read(client, "ini/npc.ini", "client_npcs")))
    for number, name in {4131: b"Chest", 4132: b"GuardingStele", 4133: b"SealingStele",
                         4134: b"MindofEvil", 4135: b"MindofEvil", 4136: b"Miracle`sSpirit"}.items():
        require(profiles[("[NpcType%d]" % number).encode()][b"Name"] == name, "Quest NPC profile changed")
    maps = map_index(read(client, "ini/GameMap.dat", "client_map_index"))
    routes = []
    for identity, (_, _, _, archive, _) in MAPS.items():
        relative = "map/map/" + archive
        require(maps[identity].replace("\\", "/").lower() == relative.lower(), "Quest map binding changed")
        read(client, relative, "client_archive_" + str(identity))
        routes.append(walking_route(read(runtime, "database/maps/%d.map" % identity,
                                         "server_terrain_" + str(identity)), identity))
    return {"missions": QUESTS, "reward": {"item": 624078, "level": 40, "quality": "elite"},
            "routes": routes, "sha256": files,
            "limits": "Static catalogs and server-grid reachability only; no runtime actors, combat, earning or native acceptance certified."}


if __name__ == "__main__":
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, default=root.parents[1] / "6711")
    parser.add_argument("--runtime", type=Path, default=root / "bin/Debug")
    args = parser.parse_args()
    print(json.dumps(audit(args.client, args.runtime), indent=2))
