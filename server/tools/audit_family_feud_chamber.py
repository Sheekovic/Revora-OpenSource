"""Verify the explicit Revora chamber adaptation, not original map 1818."""
import argparse
import json
from pathlib import Path
import audit_desert_blood_terrain as terrain

ACTORS = [(56,55),(48,60),(52,64),(58,66),(62,60),(58,58)]
PADS = [(50,60),(54,64),(58,64),(60,60),(58,60)]
NAMES = ["CommanderKerry","BlueEye","GreenEye","RedEye","YellowEye","BlackEye"]


def audit(client,runtime,sevenzip):
    original = terrain.ENTRY,terrain.ACTORS,terrain.APPROACHES
    terrain.ENTRY,terrain.ACTORS,terrain.APPROACHES = (50,55),ACTORS,[(54,55)]+PADS
    try:
        result = terrain.audit(client,runtime,sevenzip)
    finally:
        terrain.ENTRY,terrain.ACTORS,terrain.APPROACHES = original
    result["actors"] = [dict(name=n,position=p,placement="Revora adaptation") for n,p in zip(NAMES,ACTORS)]
    result["scope"] = "Exact native/server static terrain with six 3x3 NPC footprints; not native rendering, travel speed or movement-packet acceptance."
    result["adaptation"] = "Owned dynamic copy of installed map 1801; missing original map 1818 is not reconstructed or claimed equivalent."
    result["pads"] = [dict(color=n,position=p) for n,p in zip(["blue","green","red","yellow","black"],PADS)]
    result["seconds"] = 10
    result["requires_final_kerry_report"] = True
    return result


if __name__ == "__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client",type=Path,required=True)
    parser.add_argument("--runtime",type=Path,default=Path("bin/Debug"))
    parser.add_argument("--sevenzip",type=Path,default=Path("C:/Program Files/7-Zip/7z.exe"))
    parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args()
    result=audit(args.client,args.runtime,args.sevenzip)
    args.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print("Verified adapted chamber entry, Kerry approach and five pads with six NPC footprints.")
