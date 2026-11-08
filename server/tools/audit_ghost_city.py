"""Validate installed Ghost City contract; export derived metadata only."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = tuple(range(1787,1795))
ITEMS = {728246:"Level87EliteArmorPack",721717:"MagicFirework",721712:"Powder",721713:"Charm",721714:"Bezoar"}
CHANG=[8796,1002,473,300]
AUNT=[4503,1002,454,540]
WANG=[4504,1002,468,309]
SHINE=[4505,1002,440,334]
MOMO=[4526,1780,145,134]
POINTS=[(440,308),(443,344),(465,356)]



def analyze(quest,items):
    rows={}
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        row=dict(pairs)
        if not row.get("MissionId","").isdigit() or int(row["MissionId"]) not in MISSIONS:continue
        mission=int(row["MissionId"])
        activity=[v for k,v in pairs if k=="ActivityType"]
        expected=2
        if activity!=["0"]*expected or len(pairs)-len(row)!=expected-1 or mission in rows:raise ValueError("Ambiguous Ghost City section")
        rows[mission]=row
    if set(rows)!=set(MISSIONS):raise ValueError("Incomplete Ghost City chain")
    stages=[]
    begins=[CHANG,AUNT,WANG,WANG,SHINE,SHINE,MOMO,AUNT];ends=[AUNT,WANG,WANG,SHINE,SHINE,MOMO,AUNT,CHANG]
    for index,mission in enumerate(MISSIONS):
        row=rows[mission]
        if (row.get("TypeId"),row.get("Lv_min"),row.get("Lv_max"),row.get("Profession"))!=("4","75","95","0"):raise ValueError("Ghost City eligibility changed")
        if row.get("Prequest")!=str(0 if index==0 else mission-1):raise ValueError("Ghost City predecessor changed")
        begin=list(map(int,row["BeginNpcId"].split(",")[:4]));end=list(map(int,row["FinishNpcId"].split(",")[:4]))
        if begin!=begins[index] or end!=ends[index]:raise ValueError("Ghost City route changed")
        text=" ".join(row.values())
        counts=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]",text))
        materials=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]",text))
        if counts!={1789:[(1789,1,n,1) for n in range(1,4)]}.get(mission,[]):raise ValueError("Ghost City hunt changed")
        if materials!={1791:[(721712,1)],1792:[(721714,1)]}.get(mission,[]):raise ValueError("Ghost City letter changed")
        if "[item 728246," not in row.get("Prize",""):raise ValueError("Ghost City reward changed")
        if mission==1789:
            locations=[tuple(map(int,m)) for m in re.findall(r"\[mob Fireworks,1002,(\d+),(\d+)\]",text)]
            if locations!=POINTS:raise ValueError("Ghost City firework positions changed")
        stages.append(dict(mission=mission,predecessor=int(row["Prequest"]),begin=begin,finish=end,objectives=counts,materials=materials))
    found={}
    for line in items.splitlines():
        fields=line.split("@@")
        if fields[0].isdigit() and int(fields[0]) in ITEMS:
            identity=int(fields[0])
            if identity in found or len(fields)<2:raise ValueError("Ambiguous Ghost City item")
            found[identity]=fields[1]
    if found!=ITEMS:raise ValueError("Ghost City catalog changed")
    return dict(stages=stages,items=sorted(found),firework_points=POINTS,minimum_level=75,client_maximum=95,reward_pack=728246,implemented=False,aggregate_10931_proven=False)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    quest=(a.client/"ini/Questinfo.ini").read_bytes();items=(a.client/"ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest()!=QUEST_SHA256:raise SystemExit("Unreviewed journal hash")
    result=analyze(quest.decode("cp1252"),items.decode("latin1"));result["sha256"]={"quest":QUEST_SHA256,"items":hashlib.sha256(items).hexdigest()}
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Ghost City: eight stages and five item identities validated")


if __name__=="__main__":main()
