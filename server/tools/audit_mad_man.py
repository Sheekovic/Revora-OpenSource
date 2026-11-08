"""Validate installed The Mad Man contract; export derived metadata only."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = tuple(range(1781,1787))
ITEMS = {728245:"Level67EliteHeadgearPack",729978:"VenomousFang",729979:"RedToxin",729980:"GreenToxin",729981:"BlueToxin",729982:"RainbowPowder",729983:"RedPowder",729984:"OrangePowder",729985:"YellowPowder",729986:"GreenPowder",729987:"BluePowder",729988:"IndigoPowder",729989:"PurplePowder"}
CHANG=[8796,1002,473,300]
DU=[4468,1020,552,601]
JUDD=[1611,1002,345,251]
DONG=[4470,1002,431,331]



def analyze(quest,items):
    rows={}
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        row=dict(pairs)
        if not row.get("MissionId","").isdigit() or int(row["MissionId"]) not in MISSIONS:continue
        mission=int(row["MissionId"])
        activity=[v for k,v in pairs if k=="ActivityType"]
        expected=2
        if activity!=["0"]*expected or len(pairs)-len(row)!=expected-1 or mission in rows:raise ValueError("Ambiguous The Mad Man section")
        rows[mission]=row
    if set(rows)!=set(MISSIONS):raise ValueError("Incomplete The Mad Man chain")
    stages=[]
    begins=[CHANG,DU,JUDD,JUDD,DONG,DU];ends=[DU,JUDD,JUDD,DONG,DU,CHANG]
    for index,mission in enumerate(MISSIONS):
        row=rows[mission]
        if (row.get("TypeId"),row.get("Lv_min"),row.get("Lv_max"),row.get("Profession"))!=("4","60","80","0"):raise ValueError("The Mad Man eligibility changed")
        if row.get("Prequest")!=str(0 if index==0 else mission-1):raise ValueError("The Mad Man predecessor changed")
        begin=list(map(int,row["BeginNpcId"].split(",")[:4]));end=list(map(int,row["FinishNpcId"].split(",")[:4]))
        if begin!=begins[index] or end!=ends[index]:raise ValueError("The Mad Man route changed")
        text=" ".join(row.values())
        counts=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]",text))
        materials=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]",text))
        if counts!={1785:[(1785,1,n,1) for n in range(1,8)]}.get(mission,[]):raise ValueError("The Mad Man hunt changed")
        if materials!={1783:[(729978,1)],1785:[(729982,1)]}.get(mission,[]):raise ValueError("The Mad Man letter changed")
        if "[item 728245," not in row.get("Prize",""):raise ValueError("The Mad Man reward changed")
        stages.append(dict(mission=mission,predecessor=int(row["Prequest"]),begin=begin,finish=end,objectives=counts,materials=materials))
    found={}
    for line in items.splitlines():
        fields=line.split("@@")
        if fields[0].isdigit() and int(fields[0]) in ITEMS:
            identity=int(fields[0])
            if identity in found or len(fields)<2:raise ValueError("Ambiguous The Mad Man item")
            found[identity]=fields[1]
    if found!=ITEMS:raise ValueError("The Mad Man catalog changed")
    return dict(stages=stages,items=sorted(found),minimum_level=60,client_maximum=80,reward_pack=728245,implemented=False,aggregate_10931_proven=False)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    quest=(a.client/"ini/Questinfo.ini").read_bytes();items=(a.client/"ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest()!=QUEST_SHA256:raise SystemExit("Unreviewed journal hash")
    result=analyze(quest.decode("cp1252"),items.decode("latin1"));result["sha256"]={"quest":QUEST_SHA256,"items":hashlib.sha256(items).hexdigest()}
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("The Mad Man: six stages and thirteen item identities validated")


if __name__=="__main__":main()
