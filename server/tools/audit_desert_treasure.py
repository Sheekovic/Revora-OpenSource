"""Hash-gated Desert Treasure client contract; no runtime/client writes."""
import argparse
import json
import re
from audit_azure_pill import QUEST_SHA256
from audit_family_feud import checked, ITEM_SHA
from pathlib import Path

ITEMS = {721870:"ZenScripture",721871:"VictoryPortrait",721872:"BuddhaRelic",
         721873:"RevealVial",721874:"PureBottle",721875:"ScriptureBox",
         721876:"BoxKey",721877:"BatEssence",721878:"VictoryPortrait",
         728249:"L100SuperBootsPack",160199:"KylinBoots"}

def analyze(quest, catalog):
    stages=[]
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        row=dict(pairs)
        if not row.get("MissionId","").isdigit() or not 1828<=int(row["MissionId"])<=1834:continue
        mission=int(row["MissionId"])
        duplicates={k for k,v in pairs if sum(a==k for a,b in pairs)>1}
        if duplicates!={"ActivityType"} or any(v!="0" for k,v in pairs if k=="ActivityType"):
            raise ValueError("Changed duplicate metadata")
        levels=("45","65") if mission==1834 else ("98","118")
        if (row["Lv_min"],row["Lv_max"])!=levels or row["TypeId"]!="4" or row["Profession"]!="0" or row["Prequest"]!=str(0 if mission==1828 else mission-1):
            raise ValueError("Changed eligibility or predecessor")
        if "[item 728249," not in row["Prize"]:raise ValueError("Changed pack")
        stages.append(dict(mission=mission,minimum=int(levels[0]),maximum=int(levels[1]),
            predecessor=int(row["Prequest"]),begin=list(map(int,row["BeginNpcId"].split(",")[:4])),
            finish=list(map(int,row["FinishNpcId"].split(",")[:4])),
            materials=[list(map(int,m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]"," ".join(row.values()))]))
    if [s["mission"] for s in stages]!=list(range(1828,1835)):raise ValueError("Incomplete or duplicate chain")
    found={}
    for line in catalog.splitlines():
        f=line.split("@@")
        if not f[0].isdigit() or int(f[0]) not in ITEMS:continue
        key=int(f[0])
        if key in found or f[1]!=ITEMS[key]:raise ValueError("Changed item identity")
        found[key]=dict(name=f[1],required_class=int(f[2]),level=int(f[4]))
    if set(found)!=set(ITEMS) or found[160199]!={"name":"KylinBoots","required_class":0,"level":100}:raise ValueError("Missing item or changed boots")
    return dict(stages=stages,items=found,sha256=dict(quest=QUEST_SHA256,items=ITEM_SHA),
        anomalies=["All seven sections repeat ActivityType=0", "Final mission has incompatible level 45-65", "Louis prose alternates Y=649 and NPC destination Y=658"],
        original_drop_odds=None,original_bargain_odds=None,implemented=False,native_acceptance=False)

if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client",type=Path,required=True)
    parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args()
    result=analyze(checked(args.client/"ini/Questinfo.ini",QUEST_SHA256).decode("cp1252"),checked(args.client/"ini/itemtype.txt",ITEM_SHA).decode("latin1"))
    args.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print("Verified seven missions and eleven item identities; anomalies explicitly recorded.")
