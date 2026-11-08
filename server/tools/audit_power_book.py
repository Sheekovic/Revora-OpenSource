"""Validate the Power Book stage contract; export numeric evidence, not client text."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256

MISSIONS = tuple(range(1840,1853))
COUNTS = {1840:[(1840,0,1,1)],1841:[(1841,1,1,20)],1842:[(1842,1,1,10)],
          1843:[(1843,1,1,15),(1843,1,2,15)],1844:[(1844,1,1,2)],
          1845:[(1845,1,1,1)],1846:[(1846,1,1,10),(1846,1,2,10)],
          1847:[(1847,1,1,2)],1848:[(1848,1,1,1)],1849:[(1849,1,1,5),(1849,1,2,5)],
          1850:[(1850,1,1,10)],1851:[],1852:[]}
ITEMS = {721715:"TrainingBadge",721924:"PowerBook",721926:"AgileRatFang",721928:"FiendBatWing",
         721929:"RoastFiendBatWing",721931:"BlueBirdPlume",728251:"L120SuperClothingPack"}


def analyze(quest,items):
    rows={}
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        row=dict(pairs)
        if not row.get("MissionId","").isdigit() or int(row["MissionId"]) not in MISSIONS:continue
        mission=int(row["MissionId"])
        if mission in rows or len(row)!=len(pairs):raise ValueError("Ambiguous Power Book section")
        rows[mission]=row
    if set(rows)!=set(MISSIONS):raise ValueError("Incomplete Power Book chain")
    stages=[]
    for mission in MISSIONS:
        row=rows[mission]
        if (row.get("TypeId"),row.get("Lv_min"),row.get("Lv_max"),row.get("Profession"))!=("4","115","135","0"):
            raise ValueError("Power Book eligibility changed")
        if row.get("Prequest")!=str(0 if mission==1840 else mission-1):raise ValueError("Power Book predecessor changed")
        begin=list(map(int,row["BeginNpcId"].split(",")[:4]));end=list(map(int,row["FinishNpcId"].split(",")[:4]))
        if begin!=([8796,1002,473,300] if mission==1840 else [4704,1002,466,331]):raise ValueError("Power Book issuer changed")
        if end!=([8796,1002,473,300] if mission==1852 else [4704,1002,466,331]):raise ValueError("Power Book destination changed")
        text=" ".join(row.values())
        counts=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_quest (\d+),(\d+),(\d+),(\d+)\]",text))
        if counts!=COUNTS[mission]:raise ValueError("Power Book objective changed")
        materials=sorted(tuple(map(int,m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]",text))
        if materials!={1840:[(1088001,1)],1850:[(721931,1)]}.get(mission,[]):raise ValueError("Power Book material changed")
        if "[item 728251," not in row.get("Prize",""):raise ValueError("Power Book prize changed")
        seconds={1844:10,1847:20}.get(mission)
        if seconds and str(seconds)+" seconds" not in text:raise ValueError("Power Book timer changed")
        stages.append(dict(mission=mission,predecessor=int(row["Prequest"]),begin=begin,finish=end,objectives=counts,materials=materials,timer_seconds=seconds))
    found={}
    for line in items.splitlines():
        fields=line.split("@@")
        if fields[0].isdigit() and int(fields[0]) in ITEMS:
            identity=int(fields[0])
            if identity in found or len(fields)<2:raise ValueError("Ambiguous Power Book item")
            found[identity]=fields[1]
    if found!=ITEMS:raise ValueError("Power Book catalog changed")
    return dict(stages=stages,items=sorted(found),minimum_level=115,client_maximum=135,
                reward_pack=728251,implemented=False,aggregate_10931_proven=False)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    quest=(a.client/"ini/Questinfo.ini").read_bytes();items=(a.client/"ini/itemtype.txt").read_bytes()
    if hashlib.sha256(quest).hexdigest()!=QUEST_SHA256:raise SystemExit("Unreviewed journal hash")
    result=analyze(quest.decode("cp1252"),items.decode("latin1"));result["sha256"]={"quest":QUEST_SHA256,"items":hashlib.sha256(items).hexdigest()}
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Power Book: 13 stages and seven item identities validated")


if __name__=="__main__":main()
