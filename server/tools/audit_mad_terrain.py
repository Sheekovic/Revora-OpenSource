"""Audit Revora's private Mad Man layout, including the seven NPC footprints."""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import struct
import audit_frantic_terrain as terrain

ENTRY=(553,584)
PREFERRED=[(555,560),(578,560),(600,602),(570,630),(549,624),(580,600),(600,575)]

def layout(client,runtime):
    server=(runtime/"database/maps/1020.map").read_bytes()
    native=(client/"map/map/canyon.DMap").read_bytes()
    if hashlib.sha256(server).hexdigest()!=terrain.MAPS[1][3] or hashlib.sha256(native).hexdigest()!=terrain.MAPS[1][4]:raise ValueError("Unreviewed canyon terrain")
    w,h=struct.unpack_from("<II",server);nw,nh=struct.unpack_from("<II",native,268)
    if (w,h)!=(nw,nh) or (w,h)!=(932,932):raise ValueError("Unexpected canyon dimensions")
    cells=bytearray(int(server[8+y*w+x]!=0 or struct.unpack_from("<H",native,276+y*(w*6+4)+x*6)[0]!=0) for y in range(h) for x in range(w))
    def free(x,y):return 1<=x<w-1 and 1<=y<h-1 and not cells[y*w+x]
    entry=next((x,y) for _,x,y in sorted((abs(x-ENTRY[0])+abs(y-ENTRY[1]),x,y) for x in range(ENTRY[0]-10,ENTRY[0]+11) for y in range(ENTRY[1]-10,ENTRY[1]+11)) if free(x,y))
    reachable={entry};queue=deque([entry])
    while queue:
        x,y=queue.popleft()
        for nx,ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            if free(nx,ny) and (nx,ny) not in reachable:reachable.add((nx,ny));queue.append((nx,ny))
    stones=[]
    for px,py in PREFERRED:
        candidates=sorted(((abs(x-px)+abs(y-py),x,y) for x in range(px-25,px+26) for y in range(py-25,py+26)))
        for _,x,y in candidates:
            if max(abs(x-entry[0]),abs(y-entry[1]))>=8 and all(max(abs(x-sx),abs(y-sy))>=8 for sx,sy in stones) and (x-2,y) in reachable and all(free(x+dx,y+dy) for dx in (-2,-1,0,1,2) for dy in (-2,-1,0,1,2)):
                stones.append((x,y));break
        else:raise ValueError("No reviewed landing near stone: "+str((px,py)))
    for x,y in stones:
        for dx in (-1,0,1):
            for dy in (-1,0,1):cells[(y+dy)*w+x+dx]=1
    def distance(a,b):
        if not free(*a) or not free(*b):raise ValueError("Blocked stand point")
        start=a[1]*w+a[0];end=b[1]*w+b[0];q=deque([start]);d={start:0}
        while q:
            p=q.popleft()
            if p==end:return d[p]
            x,y=p%w,p//w
            for nx,ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                z=ny*w+nx
                if free(nx,ny) and z not in d:d[z]=d[p]+1;q.append(z)
        raise ValueError("Stone route disconnected")
    points=[entry]+[(x-2,y) for x,y in stones]+[entry]
    legs=[distance(a,b) for a,b in zip(points,points[1:])]
    if not free(552,599):raise ValueError("Return point blocked")
    if sum(legs)>600:raise ValueError("Collection circuit exceeds the reviewed walk budget")
    return dict(return_point=dict(map=1020,x=552,y=599,outside_du_san_footprint=True),base_map=1020,entry=entry,stones=[dict(index=i,model=555+i,x=x,y=y,stand=[x-2,y]) for i,(x,y) in enumerate(stones)],walk_steps=legs,total_walk_steps=sum(legs),npc_footprints_included=True,original_kunlun_map_verified=False,native_acceptance=False,policy="Explicit Revora layout on a private canyon copy; one five-minute trip, seven distinct stones.",server_sha256=hashlib.sha256(server).hexdigest(),native_sha256=hashlib.sha256(native).hexdigest())

if __name__=="__main__":
    p=argparse.ArgumentParser();p.add_argument("--client",type=Path,required=True);p.add_argument("--runtime",type=Path,default=Path("bin/Debug"));p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    result=layout(a.client,a.runtime);a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print(json.dumps(result))
