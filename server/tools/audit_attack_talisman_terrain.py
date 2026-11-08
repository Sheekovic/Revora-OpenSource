"""Read-only Fan public targets and candidate scene inventory; identities remain unverified."""
import argparse
import json
from pathlib import Path
import audit_defense_talisman_terrain as terrain
POINTS={1011:[(788,456),(182,280),(338,126),(471,844)],1000:[(490,618)],1015:[(685,599),(733,776),(803,884),(797,513),(703,653),(686,723),(694,721),(694,735),(698,731)],1788:[],1789:[],1790:[],1791:[(59,61),(59,56),(59,51),(55,55)]}
if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--runtime",type=Path,default=Path("bin/Debug"));p.add_argument("--sevenzip",type=Path,default=Path("C:/Program Files/7-Zip/7z.exe"));p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    terrain.POINTS=POINTS;r=terrain.audit(a.client,a.runtime,a.sevenzip);r["candidate_scene_identity_verified"]=False
    a.output.write_text(json.dumps(r,indent=2)+"\n",encoding="utf-8")
    for row in r["scenes"]:print(row["map"],"verified cells/targets" if row["verified"] else row.get("problem","Terrain/target discrepancy"))
