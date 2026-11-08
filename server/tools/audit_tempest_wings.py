"""Extract hash-bound Tempest Wings identities and explicit edition conflicts."""
import argparse
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256
from audit_family_feud import checked, ITEM_SHA

ITEMS={204008:("TempestWing",100),3006392:("Roc`sFeather",0),720027:("MeteorScroll",0)}
EXPECTED={"TypeId":"4","MissionId":"200","Lv_min":"120","Lv_max":"200","Prequest":"0","Profession":"0",
          "BeginNpcId":"8796,1004,51,49,TaskmasterChang,JobCenter",
          "FinishNpcId":"10850,1011,212,196,Mr.Wonder,PhoenixCastle"}

def analyze(quest,catalog):
    selected=[];ids=[]
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line]
        missions=[v for k,v in pairs if k=="MissionId"]
        ids.extend(int(v) for v in missions if v.isdigit())
        if "200" not in missions:continue
        fields=dict(pairs)
        for key,value in EXPECTED.items():
            if [v for k,v in pairs if k==key]!=[value]:raise ValueError("Changed or ambiguous Tempest field: "+key)
        if fields.get("Prize","").count("[item 204008,")!=1:raise ValueError("Changed Tempest reward identity")
        checks=re.findall(r"\[chk_quest (\d+),([^]]+)\]"," ".join(fields.values()))
        material=re.findall(r"\[chk_item (\d+),(\d+)\]"," ".join(fields.values()))
        if checks!=[("3429","1,1,1")] or material!=[("3006392","1")]:raise ValueError("Changed Tempest objectives")
        selected.append(fields)
    if len(selected)!=1:raise ValueError("Missing or duplicate Tempest mission")
    items={}
    for line in catalog.splitlines():
        fields=line.split("@@")
        if not fields[0].isdigit() or int(fields[0]) not in ITEMS:continue
        item=int(fields[0]);name,level=ITEMS[item]
        if item in items or len(fields)<54 or fields[1]!=name or fields[2]!="0" or fields[4]!=str(level):raise ValueError("Changed Tempest item identity")
        items[item]=dict(name=name,required_class=0,level=level)
    if set(items)!=set(ITEMS):raise ValueError("Missing Tempest item")
    return dict(source_sha256=dict(quest=QUEST_SHA256,items=ITEM_SHA),mission=200,category=4,
                begin=EXPECTED["BeginNpcId"],finish=EXPECTED["FinishNpcId"],client_levels=[120,200],
                client_checked_quest=3429,checked_quest_present=3429 in ids,items=items,
                official_entry=dict(meteor_scroll_id=720027,count=5,level=100,reborn_alternative=True),
                conflicts=["Client level120 versus official level100-or-reborn", "Legacy NPC600065 level130/second-reborn menu versus journal actors8796/10850", "Legacy option3 charges50000CP; official entry consumes five Meteor Scrolls", "Legacy option2 randomizes204009 or3006594; journal promises bound204008", "Client quest3429 check is not a verified server receipt"],
                original_drop_odds=None,original_monster_stats=None,rainbow_sky_identity_verified=False,
                aggregate_10931_proven=False,implemented=False,native_acceptance=False)

if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    result=analyze(checked(a.client/"ini/Questinfo.ini",QUEST_SHA256).decode("cp1252"),checked(a.client/"ini/itemtype.txt",ITEM_SHA).decode("latin1"))
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Verified Tempest mission, item identities and edition conflicts.")
