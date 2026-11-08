"""Check the installed eight-mission Desert Blood contract; export derived metadata only."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = set(range(1800, 1808))
ITEMS = {728242: "AmbitionPack", 721841: "Harvey`sLetter", 721842: "PeachBag",
         721843: "StoneSpell", 721844: "Note", 721845: "BluntSword", 721846: "BladeGhostFang"}
CHANG = [8796, 1002, 473, 300]
HARVEY = [4621, 1000, 495, 579]
COLUMN = [4622, 1000, 386, 103]
PEACH = [4633, 1015, 765, 602]


def analyze(quest, catalog):
    rows = {}
    for block in re.split(r"(?m)^\[", quest)[1:]:
        pairs = [line.split("=", 1) for line in block.splitlines()[1:] if "=" in line]
        row = dict(pairs)
        if not row.get("MissionId", "").isdigit() or int(row["MissionId"]) not in MISSIONS:
            continue
        mission = int(row["MissionId"])
        activity = [value for key,value in pairs if key == "ActivityType"]
        expected = 2 if mission in (1800,1801) else 1
        if activity != ["0"]*expected or len(pairs)-len(row) != expected-1 or mission in rows:
            raise ValueError("Ambiguous Desert Blood section")
        rows[mission] = row
    if set(rows) != MISSIONS:
        raise ValueError("Incomplete Desert Blood chain")
    stages = []
    for i, mission in enumerate(sorted(MISSIONS)):
        row = rows[mission]
        if (row.get("TypeId"), row.get("Lv_min"), row.get("Lv_max"), row.get("Profession")) != ("4", "85", "105", "0"):
            raise ValueError("Desert Blood eligibility changed")
        if row.get("Prequest") != str(mission-1 if i else 0):
            raise ValueError("Desert Blood predecessor changed")
        begin = list(map(int, row["BeginNpcId"].split(",")[:4]))
        end = list(map(int, row["FinishNpcId"].split(",")[:4]))
        if begin != [CHANG,HARVEY,COLUMN,COLUMN,COLUMN,HARVEY,COLUMN,PEACH][i] or end != [HARVEY,COLUMN,COLUMN,COLUMN,HARVEY,COLUMN,PEACH,CHANG][i]:
            raise ValueError("Desert Blood endpoint changed")
        goals = sorted(tuple(map(int, m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]", " ".join(row.values())))
        if goals != {1801: [(721843,1)], 1802: [(721845,1)], 1803: [(721846,5)]}.get(mission, []):
            raise ValueError("Desert Blood materials changed")
        if "[item 728242," not in row.get("Prize", ""):
            raise ValueError("Desert Blood reward changed")
        kills = [tuple(map(int, m)) for m in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]", " ".join(row.values()))]
        if kills != ([(1802,1,1,200)] if mission == 1802 else []):
            raise ValueError("Desert Blood kill target changed")
        stages.append(dict(kills=kills,mission=mission, predecessor=mission-1 if i else 0, begin=begin, finish=end, materials=goals))
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
        if item in (150178, 152168):
            name, job, level = ("CrystalRing", 0, 90) if item == 150178 else ("JadeBracelet", 190, 85)
            if item in equipment or (fields[1], int(fields[2]), int(fields[4])) != (name, job, level):
                raise ValueError("Desert Blood reward equipment changed")
            equipment[item] = dict(id=item, name=name, required_class=job, level=level, quality=8)
    if set(found) != set(ITEMS) or set(equipment) != {150178, 152168}:
        raise ValueError("Missing Desert Blood item evidence")
    return dict(stages=stages, items=found, equipment=list(equipment.values()),
        minimum_level=85, client_maximum=105, reward_pack=728242,
        original_drop_odds=None, original_crypt_return_xp_limit=None,
        note_role="721844 Note is a candidate for Aunt Peach's readable letter; journal does not bind that item ID to her.",
        quest_implemented=False, native_acceptance=False, aggregate_10931_proven=False)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    quest = (args.client / "ini/Questinfo.ini").read_bytes()
    items = (args.client / "ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest() != QUEST_SHA256 or hashlib.sha256(items).hexdigest() != "52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb":
        raise ValueError("Unreviewed Desert Blood inputs")
    result = analyze(quest.decode("cp1252"), items.decode("latin1"))
    result["sha256"] = dict(quest=QUEST_SHA256, items=hashlib.sha256(items).hexdigest())
    args.output.write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
    print("Verified eight missions, seven item identities and ring/bracelet rewards; quest implementation pending.")
