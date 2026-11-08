"""Verify Mystic Ring landmarks against shared walkable cells in installed client/server terrain."""
import argparse
from array import array
from collections import deque
import hashlib
import json
from pathlib import Path
import struct
from stage_gloomy_jail import index_records

SERVER_SHA = "7e8f7a8262f8ae93a314699af6d276f0324fff6c32bcc6061bffc2f336f1e817"
NATIVE_SHA = "48003374aa509f63a4e2b63a70cbcf6ab89b0db3f5fedc506ba50ca410238ba8"
LANDMARKS = {"minstrel":(184,282),"shen":(779,477),"junior":(920,552)}


def audit(client,runtime):
    expected=(1011,b"map/map/woods.7z",256)
    for path in [client/"ini/GameMap.dat",runtime/"database/GameMap.dat"]:
        rows=[r for r in index_records(path.read_bytes()) if r[0]==1011]
        if rows!=[expected]:raise ValueError("Ambiguous or changed Phoenix binding")
    terrain=(runtime/"database/maps/1011.map").read_bytes()
    native=(client/"map/map/woods.DMap").read_bytes()
    if hashlib.sha256(terrain).hexdigest()!=SERVER_SHA:raise ValueError("Unreviewed server terrain")
    if hashlib.sha256(native).hexdigest()!=NATIVE_SHA:raise ValueError("Unreviewed client terrain")
    w,h=struct.unpack_from("<II",native,268)
    if (w,h)!=(944,944):raise ValueError("Phoenix dimensions changed")
    cells=bytes(struct.unpack_from("<H",native,276+y*(w*6+4)+x*6)[0] for y in range(h) for x in range(w))
    server=terrain[8:]
    if struct.unpack_from("<II",terrain)!=(w,h) or len(server)!=w*h or not set(cells)<={0,1} or not set(server)<={0,1}:raise ValueError("Invalid terrain shape or cells")
    mismatch=sum(a!=b for a,b in zip(cells,server))
    # Existing catalogs differ: validate paths accepted by BOTH, never claim equality.
    cells=bytes(a|b for a,b in zip(cells,server))
    start=LANDMARKS["minstrel"];origin=start[1]*w+start[0]
    if cells[origin]:raise ValueError("Minstrel terrain is blocked")
    distances=array('i',[-1])*(w*h);distances[origin]=0;pending=deque([origin])
    while pending:
        index=pending.popleft();x,y=index%w,index//w
        for nx,ny in ((x+1,y),(x-1,y),(x,y+1),(x,y-1)):
            if not (0<=nx<w and 0<=ny<h):continue
            next_index=ny*w+nx
            if cells[next_index]==0 and distances[next_index]<0:
                distances[next_index]=distances[index]+1;pending.append(next_index)
    landmarks=[]
    for name,(x,y) in LANDMARKS.items():
        if distances[y*w+x]<0:raise ValueError("Unreachable Mystic Ring landmark: "+name)
        landmarks.append(dict(name=name,x=x,y=y,walking_steps_from_minstrel=distances[y*w+x]))
    return dict(map=1011,dimensions=[w,h],server_sha256=SERVER_SHA,native_sha256=hashlib.sha256(native).hexdigest(),
                landmarks=landmarks,mismatched_cells=mismatch,terrain_equal=mismatch==0,route_policy="walkable in both inputs",terrain_only=True,native_acceptance=False,limits="Shared cardinal walk routes do not establish live NPC occupancy or native appearance.")


if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--runtime",type=Path,default=Path("bin/Debug"));p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    result=audit(a.client,a.runtime);a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print("Mystic Ring: all three NPC landmarks have shared client/server walk routes; terrain differences recorded")
