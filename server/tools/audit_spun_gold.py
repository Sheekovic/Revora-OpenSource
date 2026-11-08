"""Validate installed Spun Gold Armor contract; export derived metadata only."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = tuple(range(1723,1727))
ITEMS = {728237:"Level32UniqueArmorPack",729971:"Invitation",729972:"Offering",729973:"WoodenBox",729974:"SpunGoldArmor"}
CHANG=[8796,1002,473,300]
HONG=[4464,1011,194,260]
ZHAO=[4465,1011,200,325]


def analyze(quest,items):
    rows={}
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        row=dict(pairs)
        if not row.get("MissionId","").isdigit() or int(row["MissionId"]) not in MISSIONS:continue
        mission=int(row["MissionId"])
        if mission in rows or len(row)!=len(pairs):raise ValueError("Ambiguous Spun Gold Armor section")
        rows[mission]=row
    if set(rows)!=set(MISSIONS):raise ValueError("Incomplete Spun Gold Armor chain")
    stages=[]
    begins=[CHANG,HONG,HONG,HONG];ends=[HONG,HONG,HONG,CHANG]
    for index,mission in enumerate(MISSIONS):
        row=rows[mission]
        if (row.get("TypeId"),row.get("Lv_min"),row.get("Lv_max"),row.get("Profession"))!=("4","30","50","0"):raise ValueError("Spun Gold Armor eligibility changed")
        if row.get("Prequest")!=str(0 if index==0 else mission-1):raise ValueError("Spun Gold Armor predecessor changed")
        begin=list(map(int,row["BeginNpcId"].split(",")[:4]));end=list(map(int,row["FinishNpcId"].split(",")[:4]))
        if begin!=begins[index] or end!=ends[index]:raise ValueError("Spun Gold Armor route changed")
        text=" ".join(row.values())
        counts=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]",text))
        materials=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]",text))
        if counts!=([(1725,0,1,1),(1725,1,2,1)] if mission==1725 else []):raise ValueError("Spun Gold Armor hunt changed")
        if materials!={1724:[(729971,1)],1725:[(729974,1)]}.get(mission,[]):raise ValueError("Spun Gold Armor letter changed")
        if "[item 728237," not in row.get("Prize",""):raise ValueError("Spun Gold Armor reward changed")
        stages.append(dict(mission=mission,predecessor=int(row["Prequest"]),begin=begin,finish=end,objectives=counts,materials=materials))
    found={}
    for line in items.splitlines():
        fields=line.split("@@")
        if fields[0].isdigit() and int(fields[0]) in ITEMS:
            identity=int(fields[0])
            if identity in found or len(fields)<2:raise ValueError("Ambiguous Spun Gold Armor item")
            found[identity]=fields[1]
    if found!=ITEMS:raise ValueError("Spun Gold Armor catalog changed")
    return dict(stages=stages,items=sorted(found),minimum_level=30,client_maximum=50,reward_pack=728237,implemented=False,aggregate_10931_proven=False)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    quest=(a.client/"ini/Questinfo.ini").read_bytes();items=(a.client/"ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest()!=QUEST_SHA256:raise SystemExit("Unreviewed journal hash")
    result=analyze(quest.decode("cp1252"),items.decode("latin1"));result["sha256"]={"quest":QUEST_SHA256,"items":hashlib.sha256(items).hexdigest()}
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Spun Gold Armor: four stages and five item identities validated")


if __name__=="__main__":main()
