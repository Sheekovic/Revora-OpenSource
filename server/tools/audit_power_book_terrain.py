"""Verify Revora Power Book cave adaptation against installed client/server terrain."""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import re
import struct
from stage_gloomy_jail import index_records

NATIVE = ["e736bac3be542f0cf0a4d7a9def07626632bc7366f8104dc51f79bddcb8bc070",
          "f4b1640826e38519f627fedfbf115a921da9500d34a88a8b90f2804b1d883fa8",
          "d04771987ab954188fab3df2d6fb23e569fca4777c2568c14878617829938a6e",
          "7f4aad7f17680ba9d5b43ee29a578b62a90c30a39bd15890350868761206ba2d"]


def audit(client,runtime,source):
    code=source.read_text(encoding="utf-8-sig")
    hashes=re.findall(r'"([0-9a-f]{64})"',code)
    if len(hashes)!=4:raise ValueError("Four reviewed terrain hashes required")
    def constant(name):return int(re.search(r"\b"+name+r"=(\d+)",code)[1])
    start=(constant("EntryX"),constant("EntryY"))
    targets=[(constant(a+"X"),constant(a+"Y")) for a in ("Exit","Gate","Fire")]
    targets += [tuple(map(int,p)) for p in re.findall(r"\{(\d+),(\d+)\}",re.search(r"HerdPoints=\{(.*?)\};",code,re.S)[1])]
    bindings=[set(index_records(p.read_bytes())) for p in (client/"ini/GameMap.dat",runtime/"database/GameMap.dat")]
    result=[]
    for floor in range(4):
        identity=1500+floor;name="mine-"+chr(ord("a")+floor)
        expected=(identity,("map/map/"+name+".7z").encode(),256)
        if any(expected not in b for b in bindings):raise ValueError("Cave binding changed")
        native=(client/("map/map/"+name+".DMap")).read_bytes()
        terrain=(runtime/("database/maps/"+str(identity)+".map")).read_bytes()
        if hashlib.sha256(native).hexdigest()!=NATIVE[floor] or hashlib.sha256(terrain).hexdigest()!=hashes[floor]:raise ValueError("Terrain hash changed")
        w,h=struct.unpack_from("<II",native,268)
        if (w,h)!=(184,184):raise ValueError("Cave dimensions changed")
        cells=bytes(struct.unpack_from("<H",native,276+y*(w*6+4)+x*6)[0] for y in range(h) for x in range(w))
        if struct.pack("<II",w,h)+cells!=terrain or not set(cells)<={0,1}:raise ValueError("Client/server terrain mismatch")
        if cells[start[1]*w+start[0]]:raise ValueError("Blocked entry")
        distances={start:0};queue=deque([start])
        while queue:
            x,y=queue.popleft()
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                point=(x+dx,y+dy)
                if 0<=point[0]<w and 0<=point[1]<h and cells[point[1]*w+point[0]]==0 and point not in distances:
                    distances[point]=distances[(x,y)]+1;queue.append(point)
        if any(p not in distances for p in targets):raise ValueError("Unreachable trial landmark")
        result.append(dict(base=identity,terrain_sha256=hashes[floor],native_sha256=NATIVE[floor],entry=start,
                           routes=[dict(target=p,walking_steps=distances[p]) for p in targets]))
    return dict(adaptation=True,original_trial_identity_verified=False,native_acceptance=False,floors=result)


if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--runtime",type=Path,default=Path("bin/Debug"));p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    result=audit(a.client,a.runtime,Path("Game/Features/PowerBookScenes.cs"));a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print("Four cave layouts agree with client terrain; all 24 landmark routes are reachable")
