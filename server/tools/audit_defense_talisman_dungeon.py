"""Verify reviewed dungeon floors and candidate walking routes; no runtime writes."""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import struct
from audit_defense_talisman_terrain import audit

ROUTES = {
    1785: ((96,124), [(96,120),(70,60),(70,56)], []),
    1786: ((113,126), [(113,122),(62,81),(62,77)], []),
    1787: ((56,68), [(56,64),(48,39)], [(48,37)]),
}


def verify(client, runtime, sevenzip):
    reviewed=json.loads((Path(__file__).resolve().parents[1]/"docs/research/DEFENSE_TALISMAN_TERRAIN.json").read_text(encoding="utf-8"))
    hashes={row["map"]:row["sha256"] for row in reviewed["scenes"] if row["map"] in ROUTES}
    current={row["map"]:row for row in audit(client,runtime,sevenzip)["scenes"]}
    results=[]
    for mid,(start,targets,npcs) in ROUTES.items():
        row=current[mid]
        if not row["verified"] or row["sha256"]!=hashes[mid]:
            raise ValueError("Unreviewed dungeon input")
        raw=(runtime/f"database/maps/{mid}.map").read_bytes()
        if hashlib.sha256(raw).hexdigest()!=hashes[mid]["server"]:
            raise ValueError("Floor changed during audit")
        w,h=struct.unpack_from("<II",raw)
        blocked={(x,y) for y in range(h) for x in range(w) if raw[8+y*w+x]}
        for x,y in npcs:
            if (x,y) in blocked:raise ValueError("Blocked NPC center")
            blocked.update((x+dx,y+dy) for dx in (-1,0,1) for dy in (-1,0,1))
        if any(p in blocked for p in [start]+targets):raise ValueError("Blocked entrance or target")
        q,dist=deque([start]),{start:0}
        while q:
            x,y=q.popleft()
            for p in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                if 0<p[0]<w-1 and 0<p[1]<h-1 and p not in blocked and p not in dist:
                    dist[p]=dist[(x,y)]+1;q.append(p)
        if any(p not in dist for p in targets):raise ValueError("Disconnected dungeon target")
        results.append(dict(map=mid,entrance=start,npc_centers=npcs,sha256=hashes[mid],
                            targets=[dict(x=x,y=y,walk_steps=dist[(x,y)]) for x,y in targets]))
    return dict(scope="Shared native/server four-direction routes and planned Cauldron footprint. Inter-floor transitions, live actors and native rendering are not verified.",floors=results,native_acceptance=False)


if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--client",type=Path,required=True)
    p.add_argument("--runtime",type=Path,default=Path("bin/Debug"))
    p.add_argument("--sevenzip",type=Path,default=Path("C:/Program Files/7-Zip/7z.exe"))
    p.add_argument("--output",type=Path,required=True)
    a=p.parse_args();r=verify(a.client,a.runtime,a.sevenzip)
    a.output.write_text(json.dumps(r,indent=2)+"\n",encoding="utf-8")
    print("Verified all three reviewed dungeon floors and eight entrance-to-objective routes.")
