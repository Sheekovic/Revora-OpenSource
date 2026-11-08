"""Validate installed Artisan Feather contract; export derived metadata only."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = tuple(range(1733,1738))
ITEMS = {728239:"Level50UniqueWeaponPack",729976:"VultureFeather"}
CHANG=[8796,1002,473,300]
APPRENTICE=[4466,1002,459,331]
ARTISAN=[4467,1002,342,640]


def analyze(quest,items):
    rows={}
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        row=dict(pairs)
        if not row.get("MissionId","").isdigit() or int(row["MissionId"]) not in MISSIONS:continue
        mission=int(row["MissionId"])
        if mission in rows:raise ValueError("Ambiguous Artisan Feather section")
        # The installed rows repeat ActivityType=0; reject every other duplicate.
        for key in row:
            values=[v for k,v in pairs if k==key]
            if len(values)>1 and (key!="ActivityType" or values!=["0","0"]):raise ValueError("Ambiguous Artisan Feather field")
        rows[mission]=row
    if set(rows)!=set(MISSIONS):raise ValueError("Incomplete Artisan Feather chain")
    stages=[]
    begins=[CHANG,APPRENTICE,ARTISAN,ARTISAN,ARTISAN];ends=[APPRENTICE,ARTISAN,ARTISAN,ARTISAN,CHANG]
    for index,mission in enumerate(MISSIONS):
        row=rows[mission]
        if (row.get("TypeId"),row.get("Lv_min"),row.get("Lv_max"),row.get("Profession"))!=("4","45","65","0"):raise ValueError("Artisan Feather eligibility changed")
        if row.get("Prequest")!=str(0 if index==0 else mission-1):raise ValueError("Artisan Feather predecessor changed")
        begin=list(map(int,row["BeginNpcId"].split(",")[:4]));end=list(map(int,row["FinishNpcId"].split(",")[:4]))
        if begin!=begins[index] or end!=ends[index]:raise ValueError("Artisan Feather route changed")
        text=" ".join(row.values())
        counts=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]",text))
        materials=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]",text))
        if counts!=([(1735,1,1,100)] if mission==1735 else []):raise ValueError("Artisan Feather hunt changed")
        if materials!={1736:[(729976,1)]}.get(mission,[]):raise ValueError("Artisan Feather letter changed")
        if "[item 728239," not in row.get("Prize",""):raise ValueError("Artisan Feather reward changed")
        stages.append(dict(mission=mission,predecessor=int(row["Prequest"]),begin=begin,finish=end,objectives=counts,materials=materials))
    found={}
    for line in items.splitlines():
        fields=line.split("@@")
        if fields[0].isdigit() and int(fields[0]) in ITEMS:
            identity=int(fields[0])
            if identity in found or len(fields)<2:raise ValueError("Ambiguous Artisan Feather item")
            found[identity]=fields[1]
    if found!=ITEMS:raise ValueError("Artisan Feather catalog changed")
    return dict(stages=stages,items=sorted(found),minimum_level=45,client_maximum=65,reward_pack=728239,implemented=False,aggregate_10931_proven=False)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    quest=(a.client/"ini/Questinfo.ini").read_bytes();items=(a.client/"ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest()!=QUEST_SHA256:raise SystemExit("Unreviewed journal hash")
    result=analyze(quest.decode("cp1252"),items.decode("latin1"));result["sha256"]={"quest":QUEST_SHA256,"items":hashlib.sha256(items).hexdigest()}
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Artisan Feather: five stages and two item identities validated")


if __name__=="__main__":main()
