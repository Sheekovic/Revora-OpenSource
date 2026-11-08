"""Verify four documented Courtesan hunt sites and local approach/fallback routes."""
import argparse,json,struct
from collections import deque
from pathlib import Path
import audit_defense_talisman_terrain as terrain
SITES=[(733,776),(803,884),(797,513),(703,653)]
def audit(client,runtime,sevenzip):
    terrain.POINTS={1015:[(x+dx,y) for x,y in SITES for dx in (-2,0,2,4)]}
    result=terrain.audit(client,runtime,sevenzip);row=result['scenes'][0]
    if not row['verified']:raise ValueError('Courtesan native/server targets differ')
    raw=(runtime/'database/maps/1015.map').read_bytes();w,h=struct.unpack_from('<II',raw)
    routes=[]
    for x,y in SITES:
        start=(x-2,y);q=deque([start]);dist={start:0};targets={(x+dx,y) for dx in (0,2,4)}
        while q and not targets.issubset(dist):
            a,b=q.popleft()
            for pos in ((a-1,b),(a+1,b),(a,b-1),(a,b+1)):
                if 0<pos[0]<w-1 and 0<pos[1]<h-1 and not raw[8+pos[1]*w+pos[0]] and pos not in dist:
                    dist[pos]=dist[(a,b)]+1;q.append(pos)
        if not targets.issubset(dist):raise ValueError('Disconnected Courtesan approach')
        routes.append(dict(site=[x,y],approach=list(start),steps=[dist[(x+dx,y)] for dx in (0,2,4)]))
    row['routes']=routes
    result['scope']='Four official Courtesan sites; verified native/server cells and local approaches/fallbacks. Owned appearance, tuning and durable random site are Revora adaptations. Native acceptance pending.'
    return result
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--client',type=Path,required=True);p.add_argument('--runtime',type=Path,default=Path('bin/Debug'));p.add_argument('--sevenzip',type=Path,default=Path('C:/Program Files/7-Zip/7z.exe'));p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(audit(a.client,a.runtime,a.sevenzip),indent=2)+'\n',encoding='utf-8');print('Verified four Courtesan sites and approach/fallback routes.')
