"""Read-only route audit across both installed terrain grids; never assert parity."""
import argparse
from array import array
from collections import deque
import hashlib
import json
from pathlib import Path
import struct
import subprocess
from stage_gloomy_jail import index_records

MAPS = [
    (1002,"newplain_new",128,"9f75cc638480a0485e6b17bdf9f9f76886a9ab7449317bebe18e72ef6ad34a10","27f89ae6329344ca2694be2028df274258f2961569040f65a5634ad34d716d14",[(473,300),(440,343),(440,345)]),
    (1020,"canyon",256,"bdb39e79fd6a7701da0ea10a12c0753e8cd6f4187ff8719b05608699bc903b95","816cf5004f2b3224e06dec9cb96bf9beaceedd0dee4e2e1c7f7e53ff940a2618",[(553,584),(668,673),(795,471),(794,470),(796,470),(794,472),(796,472)])]
ARCHIVE = "d741f4f15030ee1b0effc46963684b2ba4344fac50d930c71868aacf358a86e0"


def audit(client,runtime,sevenzip):
    reports=[]
    for mid,name,extra,server_hash,native_hash,points in MAPS:
        expected=(mid, ("map/map/"+name+".7z").encode(),extra)
        for path in [client/"ini/GameMap.dat",runtime/"database/GameMap.dat"]:
            if [r for r in index_records(path.read_bytes()) if r[0]==mid]!=[expected]:raise ValueError("Map binding changed")
        server=(runtime/"database/maps"/(str(mid)+".map")).read_bytes()
        if mid==1002:
            archive=client/"map/map/newplain_new.7z"
            if hashlib.sha256(archive.read_bytes()).hexdigest()!=ARCHIVE:raise ValueError("Unreviewed archive")
            native=subprocess.check_output([str(sevenzip),"e","-so",str(archive),"newplain_new.DMap"],timeout=30)
            if len(native)!=4670056:raise ValueError("Unexpected archive member length")
        else:native=(client/"map/map"/(name+".DMap")).read_bytes()
        if hashlib.sha256(server).hexdigest()!=server_hash or hashlib.sha256(native).hexdigest()!=native_hash:raise ValueError("Unreviewed terrain")
        sw,sh=struct.unpack_from("<II",server);nw,nh=struct.unpack_from("<II",native,268)
        w,h=min(sw,nw),min(sh,nh)
        native_cells=[struct.unpack_from("<H",native,276+y*(nw*6+4)+x*6)[0] for y in range(h) for x in range(w)]
        server_cells=[server[8+y*sw+x] for y in range(h) for x in range(w)]
        if not set(server_cells)<={0,1} or not set(native_cells)<={0,1,4,5}:raise ValueError("Unexpected cell flag")
        # Conservatively traverse only native flag 0. Values 4/5 in this newer
        # Twin City donor are recorded, not inferred to be traversable.
        cells=bytes(int(a!=0 or b!=0) for a,b in zip(native_cells,server_cells));start=points[0][1]*w+points[0][0]
        if cells[start]:raise ValueError("Route origin blocked")
        distance=array('i',[-1])*(w*h);distance[start]=0;queue=deque([start])
        while queue:
            pos=queue.popleft();x,y=pos%w,pos//w
            for nx,ny in ((x+1,y),(x-1,y),(x,y+1),(x,y-1)):
                if not (0<=nx<w and 0<=ny<h):continue
                nxt=ny*w+nx
                if cells[nxt]==0 and distance[nxt]<0:distance[nxt]=distance[pos]+1;queue.append(nxt)
        routes=[dict(x=x,y=y,steps=distance[y*w+x]) for x,y in points]
        mismatch=sum(a!=b for a,b in zip(native_cells,server_cells))
        reports.append(dict(map=mid,server_sha256=server_hash,native_sha256=native_hash,archive_sha256=ARCHIVE if mid==1002 else None,
                            server_dimensions=[sw,sh],native_dimensions=[nw,nh],native_flags=sorted(set(native_cells)),server_flags=sorted(set(server_cells)),overlap_mismatches=mismatch,
                            terrain_equal=(sw,sh)==(nw,nh) and mismatch==0,routes=routes))
    return dict(native_acceptance=False,terrain_only=True,all_landmarks_shared_cardinal_paths=all(r["steps"]>=0 for m in reports for r in m["routes"]),policy="cardinal paths using flag 0 in both grids within overlapping dimensions; no jump or portal inference",maps=reports)


if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("--client",type=Path,required=True);p.add_argument("--runtime",type=Path,default=Path("bin/Debug"));p.add_argument("--sevenzip",type=Path,default=Path("C:/Program Files/7-Zip/7z.exe"));p.add_argument("--output",type=Path,required=True);a=p.parse_args()
    result=audit(a.client,a.runtime,a.sevenzip);a.output.write_text(json.dumps(result,indent=2)+"\n")
    print("Frantic shared cardinal route acceptance:",result["all_landmarks_shared_cardinal_paths"])
    if not result["all_landmarks_shared_cardinal_paths"]:raise SystemExit(2)
