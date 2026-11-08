"""Check the installed five-mission Tomboy contract; export derived metadata only."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = set(range(1795, 1800))
ITEMS = {728241: "Level82EliteNecklacePack", 710581: "Marble",
         710582: "RuneJade", 710583: "MoonJade", 710584: "SunJade",
         721951: "LeafStone", 721952: "MapleStone", 721953: "OceanStone"}
CHANG = [8796, 1002, 473, 300]
LYNN = [4718, 1000, 461, 661]
CAROLYN = [4719, 1000, 460, 667]


def analyze(quest, catalog):
    rows = {}
    for block in re.split(r"(?m)^\[", quest)[1:]:
        pairs = [line.split("=", 1) for line in block.splitlines()[1:] if "=" in line]
        row = dict(pairs)
        if not row.get("MissionId", "").isdigit() or int(row["MissionId"]) not in MISSIONS:
            continue
        mission = int(row["MissionId"])
        if len(row) != len(pairs) or mission in rows:
            raise ValueError("Ambiguous Tomboy section")
        rows[mission] = row
    if set(rows) != MISSIONS:
        raise ValueError("Incomplete Tomboy chain")
    stages = []
    for i, mission in enumerate(sorted(MISSIONS)):
        row = rows[mission]
        if (row.get("TypeId"), row.get("Lv_min"), row.get("Lv_max"), row.get("Profession")) != ("4", "78", "98", "0"):
            raise ValueError("Tomboy eligibility changed")
        if row.get("Prequest") != str(mission-1 if i else 0):
            raise ValueError("Tomboy predecessor changed")
        begin = list(map(int, row["BeginNpcId"].split(",")[:4]))
        end = list(map(int, row["FinishNpcId"].split(",")[:4]))
        if begin != [CHANG, LYNN, LYNN, LYNN, CAROLYN][i] or end != [LYNN, LYNN, LYNN, CAROLYN, CHANG][i]:
            raise ValueError("Tomboy endpoint changed")
        goals = sorted(tuple(map(int, m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]", " ".join(row.values())))
        if goals != {1796: [(710581, 10)], 1797: [(710582, 1), (710583, 1), (710584, 1)]}.get(mission, []):
            raise ValueError("Tomboy materials changed")
        if "[item 728241," not in row.get("Prize", ""):
            raise ValueError("Tomboy reward changed")
        stages.append(dict(mission=mission, predecessor=mission-1 if i else 0, begin=begin, finish=end, materials=goals))
    found = {}
    equipment = {}
    for line in catalog.splitlines():
        fields = line.split("@@")
        if not fields[0].isdigit():
            continue
        item = int(fields[0])
        if item in ITEMS:
            if item in found or fields[1] != ITEMS[item]:
                raise ValueError("Ambiguous or changed quest item")
            found[item] = fields[1]
        if item in (120158, 121158):
            name, job = ("BasaltNecklace", 0) if item == 120158 else ("JadeBag", 190)
            if item in equipment or (fields[1], int(fields[2]), int(fields[4])) != (name, job, 82):
                raise ValueError("Tomboy reward equipment changed")
            equipment[item] = dict(id=item, name=name, required_class=job, level=82, quality=8)
    if set(found) != set(ITEMS) or set(equipment) != {120158, 121158}:
        raise ValueError("Missing Tomboy item evidence")
    return dict(stages=stages, items=found, equipment=list(equipment.values()),
        recipes=[dict(clicked=721951, partner=721952, result=710582),
                 dict(clicked=721952, partner=721953, result=710583),
                 dict(clicked=721953, partner=721951, result=710584)],
        minimum_level=78, client_maximum=98, reward_pack=728241,
        original_drop_odds=None, original_fusion_odds=None, original_question=None,
        quest_implemented=False, native_acceptance=False, aggregate_10931_proven=False)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    quest = (args.client / "ini/Questinfo.ini").read_bytes()
    items = (args.client / "ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest() != QUEST_SHA256 or hashlib.sha256(items).hexdigest() != "52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb":
        raise ValueError("Unreviewed Tomboy inputs")
    result = analyze(quest.decode("cp1252"), items.decode("latin1"))
    result["sha256"] = dict(quest=QUEST_SHA256, items=hashlib.sha256(items).hexdigest())
    args.output.write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
    print("Verified five missions, eight quest items and two level-82 rewards; quest implementation pending.")
