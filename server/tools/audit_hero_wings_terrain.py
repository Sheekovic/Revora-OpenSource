"""Audit Hero Wings NPC approaches, Ginseng sites and hunting hints."""
import argparse
import json
from pathlib import Path
import audit_desert_blood_exterior as terrain


def audit(client,runtime,sevenzip):
    original=terrain.PROFILES[1015]
    profile=list(original)
    profile[5]=[(725,541),(701,595)]
    profile[6]=(723,541)
    profile[7]=[(701,597),(450,720),(296,290),(255,228),(243,193),(229,142),
                (800,675),(451,676),(409,568)]
    terrain.PROFILES[1015]=tuple(profile)
    try:
        result=terrain.audit(client,runtime,sevenzip,1015)
    finally:
        terrain.PROFILES[1015]=original
    result["scope"]="Shared four-direction static terrain including quest NPC footprints. No other live objects, client rendering or inter-city arrival acceptance."
    result["doctor_approach"]={"blocked_west":[699,595],"verified_south":[701,597]}
    result["spawn_hint_caution"]="Journal Hawk hint 409,568 is walkable but outside verified HawkL93 spawn rectangles. Use 451,676 hunting region after checking actual cells."
    if not result["walk_route_verified"]:
        raise ValueError("Hero Wings route disconnected")
    return result


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client",type=Path,required=True)
    parser.add_argument("--runtime",type=Path,default=Path("bin/Debug"))
    parser.add_argument("--sevenzip",type=Path,default=Path("C:/Program Files/7-Zip/7z.exe"))
    parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args()
    result=audit(args.client,args.runtime,args.sevenzip)
    args.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(result))
