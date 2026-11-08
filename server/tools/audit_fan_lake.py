"""Verify Revora owned lake entrance, incense, spirit and combat routes."""
import argparse
from collections import deque
import json
import struct
from pathlib import Path
import audit_defense_talisman_terrain as terrain
POINTS=[(471,844),(473,844),(475,844),(477,840),(479,844),(481,844),(483,844)]
def audit(client,runtime,sevenzip):
    terrain.POINTS={1011:POINTS};r=terrain.audit(client,runtime,sevenzip);row=r["scenes"][0]
    if not row["verified"]:raise ValueError("Cave cells or targets differ")
    raw=(runtime/"database/maps/1011.map").read_bytes();w,h=struct.unpack_from("<II",raw);start=POINTS[0];q=deque([start]);dist={start:0}
    while q:
        x,y=q.popleft()
        for p in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            if 0<p[0]<w-1 and 0<p[1]<h-1 and not (474<=p[0]<=476 and 839<=p[1]<=841) and not raw[8+p[1]*w+p[0]] and p not in dist:dist[p]=dist[(x,y)]+1;q.append(p)
    if any(p not in dist for p in POINTS):raise ValueError("Disconnected cave target")
    row["routes"]=[dict(x=x,y=y,steps=dist[(x,y)]) for x,y in POINTS]
    r["scope"]="Explicit Revora private reuse of map1011 around the verified Swan Lake entrance; not original missing interior restoration. Native/server blocked cells and seven routes agree after reserving Herbalist Chou footprint at475,840. Incense moves from original interior55,109 to475,844. Native rendering/portal acceptance pending."
    return r
if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--runtime",type=Path,default=Path("bin/Debug"));p.add_argument("--sevenzip",type=Path,default=Path("C:/Program Files/7-Zip/7z.exe"));p.add_argument("--output",type=Path,required=True);a=p.parse_args();r=audit(a.client,a.runtime,a.sevenzip);a.output.write_text(json.dumps(r,indent=2)+"\n",encoding="utf-8");print("Verified native/server cells and seven owned lake routes.")
