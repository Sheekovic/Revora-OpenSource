"""Validate installed Frantic Monkeys contract; export derived metadata only."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = tuple(range(1745,1751))
ITEMS = {728243:"Level55EliteWeaponPack",721699:"DecayedHeart",721700:"Poison",721701:"MetamorphicVial",721702:"DevilishSword"}
CHANG=[8796,1002,473,300]
BAO=[4485,1020,553,584]
LI=[4486,1002,440,343]


def analyze(quest,items):
    rows={}
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        row=dict(pairs)
        if not row.get("MissionId","").isdigit() or int(row["MissionId"]) not in MISSIONS:continue
        mission=int(row["MissionId"])
        if mission in rows or len(row)!=len(pairs):raise ValueError("Ambiguous Frantic Monkeys section")
        rows[mission]=row
    if set(rows)!=set(MISSIONS):raise ValueError("Incomplete Frantic Monkeys chain")
    stages=[]
    begins=[CHANG,BAO,BAO,LI,LI,BAO];ends=[BAO,BAO,LI,LI,BAO,CHANG]
    for index,mission in enumerate(MISSIONS):
        row=rows[mission]
        if (row.get("TypeId"),row.get("Lv_min"),row.get("Lv_max"),row.get("Profession"))!=("4","50","70","0"):raise ValueError("Frantic Monkeys eligibility changed")
        if row.get("Prequest")!=str(0 if index==0 else mission-1):raise ValueError("Frantic Monkeys predecessor changed")
        begin=list(map(int,row["BeginNpcId"].split(",")[:4]));end=list(map(int,row["FinishNpcId"].split(",")[:4]))
        if begin!=begins[index] or end!=ends[index]:raise ValueError("Frantic Monkeys route changed")
        text=" ".join(row.values())
        counts=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]",text))
        materials=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]",text))
        if counts!={1748:[(1748,1,1,5)]}.get(mission,[]):raise ValueError("Frantic Monkeys hunt changed")
        if materials!={1746:[(721699,1)],1749:[(721702,1)]}.get(mission,[]):raise ValueError("Frantic Monkeys letter changed")
        if "[item 728243," not in row.get("Prize",""):raise ValueError("Frantic Monkeys reward changed")
        stages.append(dict(mission=mission,predecessor=int(row["Prequest"]),begin=begin,finish=end,objectives=counts,materials=materials))
    found={}
    for line in items.splitlines():
        fields=line.split("@@")
        if fields[0].isdigit() and int(fields[0]) in ITEMS:
            identity=int(fields[0])
            if identity in found or len(fields)<2:raise ValueError("Ambiguous Frantic Monkeys item")
            found[identity]=fields[1]
    if found!=ITEMS:raise ValueError("Frantic Monkeys catalog changed")
    return dict(stages=stages,items=sorted(found),minimum_level=50,client_maximum=70,reward_pack=728243,implemented=False,aggregate_10931_proven=False)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    quest=(a.client/"ini/Questinfo.ini").read_bytes();items=(a.client/"ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest()!=QUEST_SHA256:raise SystemExit("Unreviewed journal hash")
    result=analyze(quest.decode("cp1252"),items.decode("latin1"));result["sha256"]={"quest":QUEST_SHA256,"items":hashlib.sha256(items).hexdigest()}
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Frantic Monkeys: six stages and five item identities validated")


if __name__=="__main__":main()
