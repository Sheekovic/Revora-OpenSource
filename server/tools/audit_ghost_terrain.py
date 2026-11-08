"""Audit the Ghost City investigation route in both terrain grids, with NPC footprints."""
import argparse,hashlib,json,struct,subprocess
from collections import deque
from pathlib import Path
from stage_gloomy_jail import index_records

def audit(client,runtime,sevenzip):
    server=(runtime/"database/maps/1780.map").read_bytes();native=(client/"map/map/slpk.DMap").read_bytes()
    sh=hashlib.sha256(server).hexdigest();nh=hashlib.sha256(native).hexdigest()
    if sh!="a8e54974e3127c3235a788269df3f9f662b3c40be93130638469022266fd532b" or nh!="6bf114e344b4b550200f654620da8e024013c440b60ba94005e42defb8c0870c":raise ValueError("Unreviewed Ghost City terrain")
    for path in [client/"ini/GameMap.dat",runtime/"database/GameMap.dat"]:
        if [r for r in index_records(path.read_bytes()) if r[0]==1780]!=[(1780,b"map/map/slpk.7z",256)]:raise ValueError("Ghost City binding changed")
    w,h=struct.unpack_from("<II",server);nw,nhh=struct.unpack_from("<II",native,268)
    if (w,h)!=(nw,nhh) or (w,h)!=(320,320):raise ValueError("Ghost City dimensions changed")
    blocked=bytearray(int(server[8+y*w+x]!=0 or struct.unpack_from("<H",native,276+y*(w*6+4)+x*6)[0]!=0) for y in range(h) for x in range(w))
    def free(x,y):return 0<x<w-1 and 0<y<h-1 and not blocked[y*w+x]
    npcs=[(145,134),(162,217)]
    for x,y in npcs:
        if not all(free(x+dx,y+dy) for dx in range(-2,3) for dy in range(-2,3)):raise ValueError("NPC landing/approach blocked")
        for dx in (-1,0,1):
            for dy in (-1,0,1):blocked[(y+dy)*w+x+dx]=1
    def distance(a,b):
        if not free(*a) or not free(*b):raise ValueError("Blocked Ghost City stand point")
        q=deque([a]);dist={a:0}
        while q:
            x,y=q.popleft()
            if (x,y)==b:return dist[b]
            for cell in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
                if free(*cell) and cell not in dist:dist[cell]=dist[x,y]+1;q.append(cell)
        raise ValueError("Ghost City route disconnected")
    entry=(150,150);points=[entry,(143,134),(160,217),entry];legs=[distance(a,b) for a,b in zip(points,points[1:])]
    town=(runtime/"database/maps/1002.map").read_bytes();archive=client/"map/map/newplain_new.7z"
    if hashlib.sha256(town).hexdigest()!="9f75cc638480a0485e6b17bdf9f9f76886a9ab7449317bebe18e72ef6ad34a10" or hashlib.sha256(archive.read_bytes()).hexdigest()!="d741f4f15030ee1b0effc46963684b2ba4344fac50d930c71868aacf358a86e0":raise ValueError("Unreviewed return terrain")
    draw=subprocess.check_output([str(sevenzip),"e","-so",str(archive),"newplain_new.DMap"],timeout=30)
    if hashlib.sha256(draw).hexdigest()!="27f89ae6329344ca2694be2028df274258f2961569040f65a5634ad34d716d14":raise ValueError("Native return terrain changed")
    sw,sw_h=struct.unpack_from("<II",town);dw,dh=struct.unpack_from("<II",draw,268);return_point=(438,334)
    x,y=return_point
    if town[8+y*sw+x]!=0 or struct.unpack_from("<H",draw,276+y*(dw*6+4)+x*6)[0]!=0:raise ValueError("Return to Shine is blocked")
    town_npcs=[(473,300),(454,540),(468,309),(440,334)]
    town_blocked=bytearray(int(town[8+ty*sw+tx]!=0 or struct.unpack_from("<H",draw,276+ty*(dw*6+4)+tx*6)[0]!=0) for ty in range(sw_h) for tx in range(sw))
    if (sw,sw_h)!=(824,824) or (dw,dh)!=(852,852):raise ValueError("Town terrain dimensions changed")
    for tx,ty in town_npcs:
        if town_blocked[ty*sw+tx]:raise ValueError("Ghost quest NPC center blocked")
        for dx in (-1,0,1):
            for dy in (-1,0,1):town_blocked[(ty+dy)*sw+tx+dx]=1
    start=(471,300);targets=[(454,538),(466,309),(438,334),(440,308),(443,344),(465,356)]
    if town_blocked[start[1]*sw+start[0]]:raise ValueError("Chang approach blocked")
    queue=deque([start]);town_dist={start:0}
    while queue:
        tx,ty=queue.popleft()
        for cell in ((tx-1,ty),(tx+1,ty),(tx,ty-1),(tx,ty+1)):
            cx,cy=cell
            if 0<cx<sw-1 and 0<cy<sw_h-1 and not town_blocked[cy*sw+cx] and cell not in town_dist:
                town_dist[cell]=town_dist[(tx,ty)]+1;queue.append(cell)
    unreachable=[cell for cell in targets if cell not in town_dist]
    town_routes=dict(walk_route_verified=not unreachable,unreachable=unreachable,server_dimensions=[sw,sw_h],native_dimensions=[dw,dh],search_bounds=[sw,sw_h],start=start,npc_footprints=town_npcs,targets=[dict(x=tx,y=ty,walk_steps=town_dist.get((tx,ty))) for tx,ty in targets],scope="Quest NPC footprints only; four-direction shared-grid walking. Northern Aunt stand is free, but disconnected from Chang in this audit. Jump travel/native acceptance is unverified. Western Aunt stand is blocked.")
    return dict(town_routes=town_routes,base_map=1780,entry=entry,npcs=[dict(role="MoMo",uid=4526,model=565,x=145,y=134,stand=[143,134]),dict(role="Pillar",uid=9917911,model=577,x=162,y=217,stand=[160,217],model_policy="Revora FiendAltar candidate")],walk_steps=legs,npc_footprints_included=True,return_point=dict(map=1002,x=x,y=y,outside_shine_footprint=True),server_sha256=sh,native_sha256=nh,native_acceptance=False)

if __name__=="__main__":
    p=argparse.ArgumentParser();p.add_argument("--client",type=Path,required=True);p.add_argument("--runtime",type=Path,default=Path("bin/Debug"));p.add_argument("--sevenzip",type=Path,default=Path("C:/Program Files/7-Zip/7z.exe"));p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    result=audit(a.client,a.runtime,a.sevenzip);a.output.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8");print(json.dumps(result))
