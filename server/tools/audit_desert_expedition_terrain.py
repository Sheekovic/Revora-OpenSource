"""Hash-gated static Desert Expedition terrain checks; no live mutations."""
import argparse
import json
from pathlib import Path
import audit_desert_blood_exterior as terrain


def audit(client,runtime,sevenzip):
    old=terrain.PROFILES.copy()
    try:
        d=list(old[1000]);d[5]=[(520,589)];d[6]=(518,589);d[7]=[(516,589)];terrain.PROFILES[1000]=tuple(d)
        terrain.PROFILES[1001]=("d_antre01",908,
            "89bdcb2d247add85d8d7b888c8f90d7d4d5127e51499479ca988a0c9884624fc",
            "61db100d22f061f9ab8317cb752c0ccfa56a76f48cb86cb1f30c4c36775294ed",
            "0d3357ffbb1517c870a637a447bac47d871a0fb379b59687c53690309793e509",
            [(172,401),(102,294),(109,371)],(173,329),[(x+dx,y) for x,y in ((172,399),(102,292),(109,369),(186,359)) for dx in range(-3,9)])
        results=[terrain.audit(client,runtime,sevenzip,m) for m in (1000,1001)]
        for result in results:
            result["scope"]="Static shared-grid walking with proposed NPC footprints; live population, interaction effects and inter-map travel are not validated."
        return results
    finally:
        terrain.PROFILES.clear();terrain.PROFILES.update(old)

if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True)
    p.add_argument("--runtime",type=Path,default=Path("bin/Debug"));p.add_argument("--output",type=Path,required=True)
    p.add_argument("--sevenzip",type=Path,default=Path("C:/Program Files/7-Zip/7z.exe"));a=p.parse_args()
    result=audit(a.client,a.runtime,a.sevenzip);a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print(json.dumps(result))
