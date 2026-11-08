"""Audit installed Family Feud metadata without changing client or runtime files."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256
from stage_gloomy_jail import index_records, CLIENT_INDEX, SERVER_INDEX

ITEM_SHA = "52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb"
ITEMS = {710562: "RedToken", 710563: "IndigoToken", 710564: "BlackToken",
         710565: "PitchToken", 710566: "ScarletToken", 710567: "GoldenToken",
         728248: "L97SuperNecklacePack"}
CHANG, FELIX = [8796,1002,473,300], [4705,1015,686,547]
GUIDE, KERRY = [4706,1015,669,780], [4707,1818,56,55]


def checked(path, expected):
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != expected:
        raise ValueError("Unreviewed input: " + str(path))
    return data


def analyze(quest, catalog):
    rows = {}
    for block in re.split(r"(?m)^\[", quest)[1:]:
        pairs = [line.split("=", 1) for line in block.splitlines()[1:] if "=" in line]
        row = dict(pairs)
        if not row.get("MissionId", "").isdigit() or not 1819 <= int(row["MissionId"]) <= 1827:
            continue
        mission = int(row["MissionId"])
        if mission in rows or len(pairs) != len(row) or "ActivityType" in row:
            raise ValueError("Ambiguous Family Feud mission")
        rows[mission] = row
    if set(rows) != set(range(1819, 1828)):
        raise ValueError("Incomplete Family Feud chain")
    stages = []
    for mission, row in sorted(rows.items()):
        if tuple(row.get(k) for k in ("TypeId", "Lv_min", "Lv_max", "Profession", "Prequest")) != (
                "4", "95", "115", "0", str(mission-1 if mission != 1819 else 0)):
            raise ValueError("Changed Family Feud eligibility/predecessor")
        begin = list(map(int, row["BeginNpcId"].split(",")[:4]))
        end = list(map(int, row["FinishNpcId"].split(",")[:4]))
        if begin != (CHANG if mission == 1819 else GUIDE if mission == 1826 else FELIX):
            raise ValueError("Changed mission start")
        if end != (KERRY if mission == 1825 else CHANG if mission == 1827 else FELIX):
            raise ValueError("Changed mission finish")
        materials = [list(map(int, pair)) for pair in re.findall(r"\[chk_item (\d+),(\d+)\]", " ".join(row.values()))]
        if materials != ([[710562 + mission-1820, 1]] if 1820 <= mission <= 1824 else []):
            raise ValueError("Changed token requirements")
        progress = [list(map(int, pair)) for pair in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]", " ".join(row.values()))]
        if progress != ([[1825,0,1,1],[1825,0,2,1]] if mission == 1825 else []):
            raise ValueError("Changed chamber objectives")
        if "[item 728248," not in row.get("Prize", ""):
            raise ValueError("Changed reward pack")
        stages.append(dict(mission=mission,begin=begin,finish=end,materials=materials,progress=progress))
    found, equipment = {}, []
    for line in catalog.splitlines():
        f = line.split("@@")
        if not f[0].isdigit():
            continue
        identity = int(f[0])
        if identity in ITEMS:
            if identity in found or f[1] != ITEMS[identity]:
                raise ValueError("Changed or duplicate quest item")
            found[identity] = f[1]
        if identity in (120189,121189):
            expected = ("DragonNecklace",0) if identity == 120189 else ("GoldBag",190)
            if (f[1],int(f[2])) != expected or int(f[4]) != 97 or any(x["id"] == identity for x in equipment):
                raise ValueError("Changed reward equipment")
            equipment.append(dict(id=identity,name=f[1],required_class=int(f[2]),level=97,quality=9))
    if set(found) != set(ITEMS) or len(equipment) != 2:
        raise ValueError("Missing item evidence")
    return dict(stages=stages,items=found,equipment=equipment,minimum_level=95,client_maximum=115,
                reward_pack=728248,disguise_seconds=90,maze_seconds=10,
                original_drop_odds=None,original_maze_coordinates=None,
                quest_implemented=False,native_acceptance=False,aggregate_10931_proven=False)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client",type=Path,required=True)
    parser.add_argument("--runtime",type=Path,required=True)
    parser.add_argument("--output",type=Path,required=True)
    args = parser.parse_args()
    result = analyze(checked(args.client/"ini/Questinfo.ini",QUEST_SHA256).decode("cp1252"),
                     checked(args.client/"ini/itemtype.txt",ITEM_SHA).decode("latin1"))
    result["sha256"] = dict(quest=QUEST_SHA256,items=ITEM_SHA,client_index=CLIENT_INDEX,server_index=SERVER_INDEX)
    result["chamber"] = dict(journal_map=1818,
        client_registered=any(r[0] == 1818 for r in index_records(checked(args.client/"ini/GameMap.dat",CLIENT_INDEX))),
        server_registered=any(r[0] == 1818 for r in index_records(checked(args.runtime/"GameMap.dat",SERVER_INDEX))),
        server_terrain_present=(args.runtime/"maps/1818.map").is_file())
    args.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print("Verified nine missions, seven quest items, two reward candidates; chamber:",result["chamber"])
