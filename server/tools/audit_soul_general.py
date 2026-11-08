"""Validate installed Soul of the General contract; export derived metadata only."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = tuple(range(1751,1758))
ITEMS = {728244:"Level60EliteBootPack",721703:"Hair",721704:"SilkPack",721705:"Portrait",721706:"PaperFigure"}
CHANG=[8796,1002,473,300]
AMBER=[4487,1020,865,540]
ZHAO=[4488,1002,368,251]
GUARD=[4490,1002,372,251]



def analyze(quest,items):
    rows={}
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        row=dict(pairs)
        if not row.get("MissionId","").isdigit() or int(row["MissionId"]) not in MISSIONS:continue
        mission=int(row["MissionId"])
        if mission in rows or len(row)!=len(pairs):raise ValueError("Ambiguous Soul of the General section")
        rows[mission]=row
    if set(rows)!=set(MISSIONS):raise ValueError("Incomplete Soul of the General chain")
    stages=[]
    begins=[CHANG,AMBER,AMBER,AMBER,AMBER,GUARD,ZHAO];ends=[AMBER,AMBER,AMBER,AMBER,GUARD,ZHAO,CHANG]
    for index,mission in enumerate(MISSIONS):
        row=rows[mission]
        if (row.get("TypeId"),row.get("Lv_min"),row.get("Lv_max"),row.get("Profession"))!=("4","55","75","0"):raise ValueError("Soul of the General eligibility changed")
        if row.get("Prequest")!=str(0 if index==0 else mission-1):raise ValueError("Soul of the General predecessor changed")
        begin=list(map(int,row["BeginNpcId"].split(",")[:4]));end=list(map(int,row["FinishNpcId"].split(",")[:4]))
        if begin!=begins[index] or end!=ends[index]:raise ValueError("Soul of the General route changed")
        text=" ".join(row.values())
        counts=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]",text))
        materials=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]",text))
        if counts!={1753:[(1753,1,1,1),(1753,1,2,10),(1753,1,3,10)],1755:[(1755,0,1,1)]}.get(mission,[]):raise ValueError("Soul of the General hunt changed")
        if materials!={1752:[(721703,1)],1754:[(721705,1)],1756:[(721706,1)]}.get(mission,[]):raise ValueError("Soul of the General letter changed")
        if "[item 728244," not in row.get("Prize",""):raise ValueError("Soul of the General reward changed")
        stages.append(dict(mission=mission,predecessor=int(row["Prequest"]),begin=begin,finish=end,objectives=counts,materials=materials))
    found={}
    for line in items.splitlines():
        fields=line.split("@@")
        if fields[0].isdigit() and int(fields[0]) in ITEMS:
            identity=int(fields[0])
            if identity in found or len(fields)<2:raise ValueError("Ambiguous Soul of the General item")
            found[identity]=fields[1]
    if found!=ITEMS:raise ValueError("Soul of the General catalog changed")
    return dict(stages=stages,items=sorted(found),minimum_level=55,client_maximum=75,reward_pack=728244,implemented=False,aggregate_10931_proven=False)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    quest=(a.client/"ini/Questinfo.ini").read_bytes();items=(a.client/"ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest()!=QUEST_SHA256:raise SystemExit("Unreviewed journal hash")
    result=analyze(quest.decode("cp1252"),items.decode("latin1"));result["sha256"]={"quest":QUEST_SHA256,"items":hashlib.sha256(items).hexdigest()}
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Soul of the General: seven stages and five item identities validated")


if __name__=="__main__":main()
