"""Read-only catalog and showroom-route evidence for level-5 installments.

No database, listener, client execution, client mutation or proprietary output.
"""
from collections import deque
import argparse
import json
from pathlib import Path
import struct
from audit_warrior_epic import digest, item_rows, require, sections


def audit():
    root = Path(__file__).resolve().parents[1]
    require('ItemBaseInfosPath = "database\\\\itemtype-new.txt"' in (root / "ServerBase/Constants.cs").read_text(), "Active catalog path changed")
    client = root.parents[1] / "6711"
    runtime = root / "bin/Debug"
    names = {1088000: "DragonBall", 723087: "LuckyAmulet",
             1072031: "EuxeniteOre", 720027: "MeteorScroll"}
    for family, name in ((700000, "PhoenixGem"), (700010, "DragonGem"),
                         (700020, "FuryGem"), (700030, "RainbowGem"),
                         (700040, "KylinGem"), (700050, "VioletGem"),
                         (700060, "MoonGem")):
        names[family + 3] = name
    native_data = (client / "ini/itemtype.dat").read_bytes()
    server_data = (runtime / "database/itemtype-new.txt").read_bytes()
    require(digest(native_data) == "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a", "Client catalog changed")
    require(digest(server_data) == "1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef", "Server catalog changed")
    native = item_rows(native_data, True, names)
    server = item_rows(server_data, False, names)
    for identity, name in names.items():
        require(server[identity][1].decode("ascii") == name, "Unexpected installment material name")
        require(native[identity][:8] == server[identity][:8], "Client/server material identity differs")

    npc_data = (client / "ini/npc.ini").read_bytes()
    actors = [values for header, values in sections(npc_data) if header == b"[NpcType1297]"]
    require(len(actors) == 1 and actors[0].get(b"Name") == b"SeniorHouseManager", "Manager appearance changed")
    require(actors[0].get(b"SimpleObjID") == b"266", "Manager model changed")
    for path in (client / "ini/Shop.dat", runtime / "database/shops/Shop.dat"):
        shops = [v for _, v in sections(path.read_bytes()) if v.get(b"ID") == b"91008"]
        require(len(shops) == 1 and shops[0].get(b"MoneyType") == b"1", "Subclass CP shop changed")
        require(any(v == b"723087" for k, v in shops[0].items() if k.startswith(b"Item") and k != b"ItemAmount"), "Lucky Amulet shop stock missing")

    floor = (runtime / "database/maps/3024.map").read_bytes()
    require(digest(floor) == "bbb2abb9cf4f023af233c4a3df078de59e3e3505dd5b655b061977011172bae8", "Display-home terrain changed")
    width, height = struct.unpack_from("<ii", floor)
    require((width, height) == (170, 170) and len(floor) == 8 + width * height, "Invalid display-home floor")
    grid = floor[8:]
    require(set(grid) <= {0, 1}, "Unknown floor encoding")
    actor, landing = (101, 102), (90, 70)
    blocked = {(actor[0] + dx, actor[1] + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)}

    def valid(point):
        x, y = point
        return 0 <= x < width and 0 <= y < height and grid[y * width + x] == 0

    require(valid(actor) and valid(landing) and landing not in blocked, "Blocked showroom actor or landing")
    distances, pending = {landing: 0}, deque([landing])
    while pending:
        x, y = pending.popleft()
        for point in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
            if point not in distances and point not in blocked and valid(point):
                distances[point] = distances[(x, y)] + 1
                pending.append(point)
    approaches = [(steps, point) for point, steps in distances.items()
                  if (point[0]-actor[0])**2 + (point[1]-actor[1])**2 <= 36]
    require(approaches, "No ordinary walking approach to manager")
    steps, approach = min(approaches)
    return {"materials": len(names), "confirmed_standard_super_gems": sorted(i for i in names if 700000 < i < 700100),
            "lucky_amulet_shop": 91008,
            "lucky_amulet_client_cps": int(native[723087][36]), "lucky_amulet_server_cps": int(server[723087][36]),
            "lucky_amulet_price_parity": native[723087][36] == server[723087][36],
            "manager_mesh": 12970, "npc_catalog_sha256": digest(npc_data),
            "showroom_terrain": 3024, "terrain_sha256": digest(floor),
            "manager_position": actor, "landing": landing, "approach": approach, "walking_steps": steps,
            "confidence": "static catalogs and server floor only; no native acceptance"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--require-price-parity", action="store_true")
    args = parser.parse_args()
    result = audit()
    print(json.dumps(result, indent=2))
    if args.require_price_parity and not result["lucky_amulet_price_parity"]:
        raise SystemExit(2)
