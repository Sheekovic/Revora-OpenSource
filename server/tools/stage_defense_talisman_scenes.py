"""Stage private Star Tower scene aliases from exact installed inputs; never install."""
import argparse
from collections import deque
import json
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile
from stage_gloomy_jail import CLIENT_EXE, CLIENT_INDEX, SERVER_INDEX, digest, index_records

ARCHIVE = "569019c1fb6d15454a1d53df5008b3c547b77bc2d3e0b06b851950814aae0c32"
NATIVE = "42f2d23a952b8b84e73b2e621c4ee79c498ab9a3343dfcfd2e3652970ed91f23"
FLOOR = "9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4"
RECORDS = [(mid, b"map/map/house03.7z", 256) for mid in (1782, 1783, 1784)]
SCENES = {1782: [(51,70)], 1783: [(52,50)], 1784: [(55,55),(61,68),(61,30)]}
START = (45,60)
SPAWNS = [(40,55),(42,55),(44,55),(46,55),(48,55)]


def add_scenes(data):
    rows = index_records(data)
    if (1099, b"map/map/house03.7z", 256) not in rows:
        raise ValueError("Missing reviewed source binding")
    if any(r[0] in SCENES for r in rows):
        raise ValueError("Target scene already registered")
    result = struct.pack("<I", len(rows)+3) + data[4:]
    for mid, path, flags in RECORDS:
        result += struct.pack("<II",mid,len(path))+path+struct.pack("<I",flags)
    if index_records(result) != rows+RECORDS:
        raise ValueError("Unrelated map records changed")
    return result


def routes(floor, mid):
    w,h = struct.unpack_from("<II",floor)
    blocked = {(x,y) for y in range(h) for x in range(w) if floor[8+y*w+x]}
    for x,y in SCENES[mid]:
        if (x,y) in blocked:
            raise ValueError("Blocked NPC center")
        blocked.update((x+dx,y+dy) for dx in (-1,0,1) for dy in (-1,0,1))
    targets = [(x-2,y) for x,y in SCENES[mid]]+SPAWNS
    if any(p in blocked for p in [START]+targets):
        raise ValueError("Blocked admission or target")
    q,dist = deque([START]),{START:0}
    while q:
        x,y = q.popleft()
        for p in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            if 0<p[0]<w-1 and 0<p[1]<h-1 and p not in blocked and p not in dist:
                dist[p]=dist[(x,y)]+1;q.append(p)
    if any(p not in dist for p in targets):
        raise ValueError("Unreachable scene target")
    return dict(map=mid, entrance=START, npc_centers=SCENES[mid],
                targets=[dict(x=x,y=y,walk_steps=dist[(x,y)]) for x,y in targets])


def stage(client, runtime, destination, sevenzip):
    client,runtime = client.resolve(strict=True),runtime.resolve(strict=True)
    destination = destination.resolve()
    if destination.exists() or any(destination==p or p in destination.parents or destination in p.parents for p in (client,runtime)):
        raise ValueError("Use a new private destination separate from installed inputs")
    expected = {client/"Env_DX9/Conquer.exe":CLIENT_EXE,
                client/"ini/GameMap.dat":CLIENT_INDEX,
                runtime/"database/GameMap.dat":SERVER_INDEX,
                client/"map/map/house03.7z":ARCHIVE,
                runtime/"database/maps/1099.map":FLOOR}
    sources = {}
    for path,sha in expected.items():
        raw=path.read_bytes()
        if digest(raw)!=sha: raise ValueError("Changed input: "+str(path))
        sources[path]=raw
    native=subprocess.check_output([str(sevenzip),"e","-so",str(client/"map/map/house03.7z"),"house03.DMap"],timeout=30)
    if digest(native)!=NATIVE: raise ValueError("Changed native interior")
    floor=sources[runtime/"database/maps/1099.map"]
    if struct.unpack_from("<II",floor)!=(92,92) or len(floor)!=8472:
        raise ValueError("Changed floor dimensions")
    if any((floor[8+y*92+x]!=0)!=(struct.unpack_from("<H",native,276+y*(92*6+4)+x*6)[0]!=0) for y in range(92) for x in range(92)):
        raise ValueError("Native and server cells differ")
    if struct.unpack_from("<IIII",native,276+92*(92*6+4))!=(1,53,86,0):
        raise ValueError("Changed inherited portal")
    checks=[routes(floor,mid) for mid in SCENES]
    outputs={"client/ini/GameMap.dat":add_scenes(sources[client/"ini/GameMap.dat"]),
             "server/database/GameMap.dat":add_scenes(sources[runtime/"database/GameMap.dat"])}
    outputs.update({f"server/database/maps/{mid}.map":floor for mid in SCENES})
    manifest=dict(purpose="Private unsigned Revora scene adaptation, not original map restoration",
                  inputs=[dict(path=str(p),sha256=h) for p,h in expected.items()], native_sha256=NATIVE,
                  outputs=[dict(path=p,sha256=digest(b),bytes=len(b)) for p,b in outputs.items()],
                  routes=checks, inherited_portal=dict(x=53,y=86,index=0),
                  pending=["Owned scene lifecycle", "Explicit inherited-portal handling", "Composition with other staged map repairs", "Native and Host acceptance", "Signing and installation"],
                  installed=False,native_acceptance=False)
    destination.parent.mkdir(parents=True,exist_ok=True)
    temporary=Path(tempfile.mkdtemp(prefix=".tower-stage-",dir=destination.parent))
    try:
        for relative,data in outputs.items():
            path=temporary/relative;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
            if path.read_bytes()!=data: raise ValueError("Staged output readback failed")
        (temporary/"manifest.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
        # Recheck input preimages before publishing the private staged directory.
        if any(digest(p.read_bytes())!=h for p,h in expected.items()):
            raise ValueError("Installed input changed during staging")
        temporary.rename(destination)
    finally:
        if temporary.exists():
            resolved=temporary.resolve()
            if resolved.parent!=destination.parent or not resolved.name.startswith(".tower-stage-"):
                raise ValueError("Unexpected temporary cleanup target")
            shutil.rmtree(resolved)
    return manifest


if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--client",type=Path,required=True)
    p.add_argument("--runtime",type=Path,default=Path("bin/Debug"))
    p.add_argument("--destination",type=Path,required=True)
    p.add_argument("--sevenzip",type=Path,default=Path("C:/Program Files/7-Zip/7z.exe"))
    a=p.parse_args();r=stage(a.client,a.runtime,a.destination,a.sevenzip)
    print("Privately staged three scene aliases; all admission/NPC/spawn routes pass. Native acceptance pending.")
