"""Validate installed Mystic Ring contract; export derived metadata only."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = tuple(range(1738,1745))
ITEMS = {728240:"Level52UniqueHeadgearPack",721707:"AntiqueRing",721708:"MysticRing",721710:"CastleGate",1000000:"Stancher",1001000:"Agrypnotic"}
CHANG=[8796,1002,473,300]
MINSTREL=[4500,1011,184,282]
SHEN=[4501,1011,779,477]
JUNIOR=[4502,1011,920,552]


def analyze(quest,items):
    rows={}
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        row=dict(pairs)
        if not row.get("MissionId","").isdigit() or int(row["MissionId"]) not in MISSIONS:continue
        mission=int(row["MissionId"])
        if mission in rows or len(row)!=len(pairs):raise ValueError("Ambiguous Mystic Ring section")
        rows[mission]=row
    if set(rows)!=set(MISSIONS):raise ValueError("Incomplete Mystic Ring chain")
    stages=[]
    begins=[CHANG,MINSTREL,SHEN,SHEN,SHEN,SHEN,MINSTREL];ends=[MINSTREL,SHEN,SHEN,SHEN,SHEN,MINSTREL,CHANG]
    for index,mission in enumerate(MISSIONS):
        row=rows[mission]
        if (row.get("TypeId"),row.get("Lv_min"),row.get("Lv_max"),row.get("Profession"))!=("4","48","68","0"):raise ValueError("Mystic Ring eligibility changed")
        if row.get("Prequest")!=str(0 if index==0 else mission-1):raise ValueError("Mystic Ring predecessor changed")
        begin=list(map(int,row["BeginNpcId"].split(",")[:4]));end=list(map(int,row["FinishNpcId"].split(",")[:4]))
        if begin!=begins[index] or end!=ends[index]:raise ValueError("Mystic Ring route changed")
        text=" ".join(row.values())
        counts=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]",text))
        materials=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]",text))
        if counts!={1741:[(1741,1,1,20)],1742:[(1742,0,1,1)],1743:[(1743,1,1,1),(1743,1,2,1)]}.get(mission,[]):raise ValueError("Mystic Ring hunt changed")
        if materials!={1740:[(1000000,12),(1001000,12)]}.get(mission,[]):raise ValueError("Mystic Ring letter changed")
        if "[item 728240," not in row.get("Prize",""):raise ValueError("Mystic Ring reward changed")
        if mission==1742 and "[npc ShenJunior,4502,1011,920,552]" not in text:raise ValueError("Mystic Ring Junior route changed")
        stages.append(dict(mission=mission,predecessor=int(row["Prequest"]),begin=begin,finish=end,objectives=counts,materials=materials))
    found={}
    for line in items.splitlines():
        fields=line.split("@@")
        if fields[0].isdigit() and int(fields[0]) in ITEMS:
            identity=int(fields[0])
            if identity in found or len(fields)<2:raise ValueError("Ambiguous Mystic Ring item")
            found[identity]=fields[1]
    if found!=ITEMS:raise ValueError("Mystic Ring catalog changed")
    return dict(stages=stages,items=sorted(found),minimum_level=48,client_maximum=68,reward_pack=728240,implemented=False,aggregate_10931_proven=False)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    quest=(a.client/"ini/Questinfo.ini").read_bytes();items=(a.client/"ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest()!=QUEST_SHA256:raise SystemExit("Unreviewed journal hash")
    result=analyze(quest.decode("cp1252"),items.decode("latin1"));result["sha256"]={"quest":QUEST_SHA256,"items":hashlib.sha256(items).hexdigest()}
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Mystic Ring: seven stages and six item identities validated")


if __name__=="__main__":main()
