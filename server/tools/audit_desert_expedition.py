"""Validate installed Desert Expedition evidence without runtime mutations."""
import argparse
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256
from audit_family_feud import checked, ITEM_SHA
ITEMS={710259:"BurntBullhorn",710260:"BullHeart",710261:"BullGhost",721920:"FerventPill",721921:"GhostMirror",728250:"L110SuperRingPack",150219:"DragonRing",152229:"DiamondBracelet"}
def analyze(quest,catalog):
    rows=[]
    for block in re.split(r"(?m)^\[",quest)[1:]:
        pairs=[line.split("=",1) for line in block.splitlines()[1:] if "=" in line];row=dict(pairs)
        if not row.get("MissionId","").isdigit() or not 1835<=int(row["MissionId"])<=1839:continue
        mid=int(row["MissionId"])
        if len(pairs)!=len(row) or tuple(row[k] for k in ("TypeId","Lv_min","Lv_max","Profession","Prequest"))!=("4","105","125","0",str(0 if mid==1835 else mid-1)):raise ValueError("Changed quest contract")
        begin=list(map(int,row["BeginNpcId"].split(",")[:4]));finish=list(map(int,row["FinishNpcId"].split(",")[:4]))
        chang=[8796,1002,473,300];momo=[4702,1000,520,589]
        if begin!=(chang if mid==1835 else momo) or finish!=(chang if mid==1839 else momo) or "[item 728250," not in row["Prize"]:raise ValueError("Changed NPC/reward")
        materials=[list(map(int,m)) for m in re.findall(r"\[chk_item (\d+),(\d+)\]"," ".join(row.values()))]
        if materials!={1836:[[710259,10]],1837:[[710261,3]],1838:[[710260,1]]}.get(mid,[]):raise ValueError("Changed materials")
        rows.append(dict(mission=mid,predecessor=int(row["Prequest"]),begin=begin,finish=finish,materials=materials))
    if [r["mission"] for r in rows]!=list(range(1835,1840)):raise ValueError("Incomplete or duplicate chain")
    found={}
    for line in catalog.splitlines():
        f=line.split("@@")
        if not f[0].isdigit() or int(f[0]) not in ITEMS:continue
        key=int(f[0])
        if key in found or f[1]!=ITEMS[key]:raise ValueError("Changed item identity")
        found[key]=dict(name=f[1],required_class=int(f[2]),level=int(f[4]))
        if key==721920 and "5~minutes" not in f[53]:raise ValueError("Pill tooltip evidence changed")
    if set(found)!=set(ITEMS) or (found[150219]["level"],found[150219]["required_class"],found[152229]["level"],found[152229]["required_class"])!=(110,0,115,190):raise ValueError("Changed equipment")
    return dict(stages=rows,items=found,sha256=dict(quest=QUEST_SHA256,items=ITEM_SHA),minimum_level=105,client_maximum=125,
        trap_hints=[[172,399],[102,292],[109,369]],bull_master_hint=[186,359],
        pill_duration_conflict=dict(client_tooltip_seconds=300,official_guide_seconds=60),
        original_drop_odds=None,original_trap_duration=None,implemented=False,native_acceptance=False)
if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    result=analyze(checked(a.client/"ini/Questinfo.ini",QUEST_SHA256).decode("cp1252"),checked(a.client/"ini/itemtype.txt",ITEM_SHA).decode("latin1"))
    a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Verified five stages, five materials, pack and two equipment candidates.")
