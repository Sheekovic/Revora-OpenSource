"""Validate the installed six-stage Azure Pill contract without exporting client text."""
import argparse
import hashlib
import json
from pathlib import Path
import re

QUEST_SHA256 = "f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14"
STAGES = tuple(range(1727, 1733))
ITEMS = {721695: "RatlingEye", 721696: "RatlingMeat", 721697: "HerbPack", 721698: "AzurePill", 728238: "UniqueRingPack"}
ROUTES = [(8796,4482),(4482,4483),(4483,4483),(4483,4483),(4483,4482),(4482,8796)]

def analyze(quest, items, server):
    rows = {}
    for block in re.split(r"(?m)^\[", quest)[1:]:
        row = dict(line.split("=",1) for line in block.splitlines()[1:] if "=" in line)
        if row.get("MissionId", "").isdigit() and int(row["MissionId"]) in STAGES:
            key=int(row["MissionId"])
            if key in rows: raise ValueError("Duplicate Azure Pill mission")
            rows[key]=row
    if set(rows)!=set(STAGES): raise ValueError("Incomplete Azure Pill chain")
    result=[]
    for index,key in enumerate(STAGES):
        row=rows[key]
        expected=0 if index==0 else STAGES[index-1]
        if row.get("Prequest")!=str(expected): raise ValueError("Azure Pill predecessor changed")
        if (row.get("TypeId"),row.get("Lv_min"),row.get("Lv_max"),row.get("Profession"))!=("4","40","60","0"):
            raise ValueError("Azure Pill eligibility changed")
        begin=row["BeginNpcId"].split(",");end=row["FinishNpcId"].split(",")
        if (int(begin[0]),int(end[0]))!=ROUTES[index]: raise ValueError("Azure Pill NPC route changed")
        checks=sorted((int(a),int(b)) for a,b in re.findall(r"\[chk_item (\d+),(\d+)\]", " ".join(row.values())))
        expected_items={1729:[(721695,1),(721696,3),(721697,2)],1731:[(721698,1)]}.get(key,[])
        if checks!=expected_items: raise ValueError("Azure Pill material quantities changed")
        if "[item 728238," not in row.get("Prize",""): raise ValueError("Azure Pill final pack changed")
        if key==1730 and "2 minutes" not in row.get("IntentionDesp",""): raise ValueError("Azure Pill preparation timer changed")
        result.append({"mission":key,"predecessor":expected,"begin":[int(v) for v in begin[:4]],
                       "finish":[int(v) for v in end[:4]],"materials":[{"item":a,"count":b} for a,b in checks]})
    found={}
    for line in items.splitlines():
        fields=line.split("@@")
        if fields[0].isdigit() and int(fields[0]) in ITEMS:
            key=int(fields[0])
            if key in found: raise ValueError("Duplicate Azure Pill item")
            found[key]=fields[1]
    if set(found)!=set(ITEMS): raise ValueError("Missing Azure Pill catalog item")
    # Exact material names; reward pack naming varies, so its numeric identity is recorded only.
    if any(found[key]!=ITEMS[key] for key in ITEMS if key!=728238): raise ValueError("Azure Pill material identity changed")
    collisions=sorted(set(map(int,re.findall(r"case\s+(4482|4483|4484|8796)\s*:",server))))
    return {"stages":result,"material_ids":sorted(found),"preparation_seconds":120,
            "client_min_level":40,"client_max_level":60,"server_dialogue_id_collisions":collisions,
            "aggregate_10931_proven":False,"implemented":False,
            "limits":["NPC coordinates are client catalog declarations, not installed-world proof",
                      "Server material catalog, drop family and reward-pack opening remain unverified",
                      "Six Azure Pill stages do not establish the entire equipment achievement"]}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client",type=Path,required=True)
    parser.add_argument("--source",type=Path,default=Path("Npcs.cs"))
    parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args()
    quest=(args.client/"ini/Questinfo.ini").read_bytes()
    if hashlib.sha256(quest).hexdigest()!=QUEST_SHA256: raise SystemExit("Unreviewed quest catalog hash")
    items=(args.client/"ini/itemtype.txt").read_bytes();server=args.source.read_bytes()
    report=analyze(quest.decode("cp1252"),items.decode("latin1"),server.decode("utf-8-sig"))
    report["sha256"]={"quest":QUEST_SHA256,"items":hashlib.sha256(items).hexdigest(),"server_dialogue":hashlib.sha256(server).hexdigest()}
    args.output.write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8")
    print("Azure Pill: six stages validated; existing dialogue IDs:", report["server_dialogue_id_collisions"])
if __name__=="__main__": main()
