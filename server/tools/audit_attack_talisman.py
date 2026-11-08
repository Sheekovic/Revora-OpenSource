"""Extract exact installed 6711 Heaven Fan quest evidence; no runtime writes."""
import argparse
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256
from audit_family_feud import checked, ITEM_SHA
ITEMS={201009:"HeavenFan",710216:"PurpleEye",710217:"JadeScepter",710218:"Jadeite",710219:"Hairpin",710220:"SnakeMeat",710221:"HeavenFan",710222:"PoemBook",721798:"TangSilk",721799:"Incense",721800:"BambooWine",721801:"PoemSilk",721802:"KunlunLiquid",721803:"PoisonFang",721804:"SacredWater",721806:"BeeKettle"}
def analyze(quest,catalog):
    stages=[]
    for block in re.split(r"(?m)^\[",quest)[1:]:
        row=dict(line.split("=",1) for line in block.splitlines()[1:] if "=" in line)
        if not row.get("MissionId","").isdigit() or not 400<=int(row["MissionId"])<=409:continue
        mid=int(row["MissionId"]);expected=("4","100","120", "" if mid==400 else "0",str(527 if mid==400 else mid-1))
        if tuple(row[k] for k in ("TypeId","Lv_min","Lv_max","Profession","Prequest"))!=expected:raise ValueError("Changed Fan prerequisites")
        if "[item 201009," not in row["Prize"]:raise ValueError("Changed Fan reward")
        stages.append(dict(mission=mid,predecessor=int(row["Prequest"]),begin=row["BeginNpcId"],finish=row["FinishNpcId"],
                           materials=[list(map(int,pair)) for pair in re.findall(r"\[chk_item (\d+),(\d+)\]"," ".join(row.values()))]))
    if [r["mission"] for r in stages]!=list(range(400,410)):raise ValueError("Missing or duplicate Fan mission")
    items={}
    for line in catalog.splitlines():
        fields=line.split("@@")
        if not fields[0].isdigit() or int(fields[0]) not in ITEMS:continue
        item=int(fields[0])
        if item in items or fields[1]!=ITEMS[item]:raise ValueError("Changed Fan item identity")
        items[item]=dict(name=fields[1],required_class=int(fields[2]),level=int(fields[4]))
    if set(items)!=set(ITEMS):raise ValueError("Missing Fan item")
    return dict(source_sha256=dict(quest=QUEST_SHA256,items=ITEM_SHA),stages=stages,items=items,
                prerequisite_conflict=dict(client_mission=527,completed_tower_mission=528,revora_required_tower_stage=10),
                recipe=[dict(inputs=[721803,721802],output=721804),dict(inputs=[721804,721798],output=721801)],
                gem_pillars={"PurpleEye":"Sparrow","JadeScepter":"Dragon","Jadeite":"Tiger","Hairpin":"Leopard"},
                coordinate_conflicts=[dict(subject="Gem pillar tooltips",tooltip=[653,700],journal_targets=[[733,776],[694,721],[694,735],[698,731]]),
                                      dict(subject="Swan Lake",entrance=[471,844],incense_site=[55,109],scene_id_verified=False)],
                scene_candidates=[1788,1789,1790,1791],scene_candidate_identity_verified=False,
                original_drop_odds=None,original_monster_stats=None,implemented=False,native_acceptance=False)
if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    result=analyze(checked(a.client/"ini/Questinfo.ini",QUEST_SHA256).decode("cp1252"),checked(a.client/"ini/itemtype.txt",ITEM_SHA).decode("latin1"))
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Verified ten missions and sixteen item identities; reconstruction/final-claim prerequisite conflict recorded.")
