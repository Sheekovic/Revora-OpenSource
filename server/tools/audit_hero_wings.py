"""Check the installed eleven-mission Hero Wings contract; export derived metadata only."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = set(range(1808, 1819))
ITEMS = {728247: "SuperHeadgearPack", 1088001: "Meteor", 721906: "BirdBlood",
         721908: "BitterGinseng", 721909: "Gallbladderr", 721910: "HawkClaw",
         721911: "PeelPill", 721912: "GinsengMap"}
CHANG = [8796,1002,473,300]
XIAO = [4700,1015,725,541]
DOCTOR = [4701,1015,701,595]



def analyze(quest, catalog):
    rows = {}
    for block in re.split(r"(?m)^\[", quest)[1:]:
        pairs = [line.split("=", 1) for line in block.splitlines()[1:] if "=" in line]
        row = dict(pairs)
        if not row.get("MissionId", "").isdigit() or int(row["MissionId"]) not in MISSIONS:
            continue
        mission = int(row["MissionId"])
        activity = [value for key,value in pairs if key == "ActivityType"]
        expected = 2
        if activity != ["0"]*expected or len(pairs)-len(row) != expected-1 or mission in rows:
            raise ValueError("Ambiguous Hero Wings section")
        rows[mission] = row
    if set(rows) != MISSIONS:
        raise ValueError("Incomplete Hero Wings chain")
    stages = []
    for i, mission in enumerate(sorted(MISSIONS)):
        row = rows[mission]
        if (row.get("TypeId"), row.get("Lv_min"), row.get("Lv_max"), row.get("Profession")) != ("4", "90", "110", "0"):
            raise ValueError("Hero Wings eligibility changed")
        if row.get("Prequest") != str(mission-1 if i else 0):
            raise ValueError("Hero Wings predecessor changed")
        begin = list(map(int, row["BeginNpcId"].split(",")[:4]))
        end = list(map(int, row["FinishNpcId"].split(",")[:4]))
        if begin != [CHANG,XIAO,DOCTOR,XIAO,DOCTOR,DOCTOR,DOCTOR,DOCTOR,XIAO,DOCTOR,XIAO][i] or end != [XIAO,DOCTOR,XIAO,DOCTOR,DOCTOR,DOCTOR,DOCTOR,XIAO,DOCTOR,XIAO,CHANG][i]:
            raise ValueError("Hero Wings endpoint changed")
        goals = sorted(tuple(map(int, m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]", " ".join(row.values())))
        if goals != {1811: [(1088001,1)], 1812: [(721906,10)], 1813: [(721908,1)], 1814: [(721910,5)]}.get(mission, []):
            raise ValueError("Hero Wings materials changed")
        if "[item 728247," not in row.get("Prize", ""):
            raise ValueError("Hero Wings reward changed")
        kills = [tuple(map(int, m)) for m in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]", " ".join(row.values()))]
        if kills != []:
            raise ValueError("Hero Wings kill target changed")
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
        if item//1000 in (111,112,113,114,117,118,143,144,148,170) and item%10==9 and fields[4] in ("92","97"):
            if item in equipment:
                raise ValueError("Duplicate reward candidate")
            equipment[item] = dict(id=item,name=fields[1],required_class=int(fields[2]),level=int(fields[4]),quality=9)
    if set(found) != set(ITEMS) or not equipment:
        raise ValueError("Missing Hero Wings item evidence")
    return dict(stages=stages,items=found,equipment_candidates=list(equipment.values()),
        minimum_level=90,client_maximum=110,reward_pack=728247,
        guide="https://co.99.com/guide/quests/herowings.shtml",
        guide_ginseng_points=[[450,720],[296,290],[255,228],[243,193],[229,142]],
        timed_claws=dict(family="HawkL93",kills_per_claw=2,seconds=60,required_claws=5),
        original_drop_odds=None,original_ginseng_selection_rule=None,
        quest_implemented=False,native_acceptance=False,aggregate_10931_proven=False)



if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    quest = (args.client / "ini/Questinfo.ini").read_bytes()
    items = (args.client / "ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest() != QUEST_SHA256 or hashlib.sha256(items).hexdigest() != "52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb":
        raise ValueError("Unreviewed Hero Wings inputs")
    result = analyze(quest.decode("cp1252"), items.decode("latin1"))
    result["sha256"] = dict(quest=QUEST_SHA256, items=hashlib.sha256(items).hexdigest())
    args.output.write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
    print("Verified eleven missions, eight item identities and Super headgear candidates; implementation pending.")
