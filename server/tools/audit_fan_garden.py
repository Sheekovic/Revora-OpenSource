"""Audit Revora pillar layout and private garden terrain adaptation."""
import argparse,json,struct
from collections import deque
from pathlib import Path
import audit_defense_talisman_terrain as terrain
NPCS={1015:[(680,720),(694,720),(680,734),(694,734),(686,723)],3937:[(52,50),(58,50),(64,50),(60,64)]}
ROUTES={1015:[(688,723),(682,720),(696,720),(682,734),(696,734)],3937:[(48,60),(52,53),(58,53),(64,53),(60,61),(54,58),(56,58),(58,58)]}
def audit(client,runtime,sevenzip):
    terrain.POINTS={mid:NPCS[mid]+ROUTES[mid] for mid in NPCS};r=terrain.audit(client,runtime,sevenzip)
    for row in r['scenes']:
        mid=row['map']
        if not row['verified']:raise ValueError('Garden layout cells differ')
        raw=(runtime/f'database/maps/{mid}.map').read_bytes();w,h=struct.unpack_from('<II',raw);start=ROUTES[mid][0];q=deque([start]);dist={start:0}
        occupied={(x+dx,y+dy) for x,y in NPCS[mid] for dx in (-1,0,1) for dy in (-1,0,1)}
        if any(raw[8+y*w+x] for x,y in occupied):raise ValueError('Blocked NPC footprint')
        while q and not set(ROUTES[mid]).issubset(dist):
            x,y=q.popleft()
            for pos in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                if 0<pos[0]<w-1 and 0<pos[1]<h-1 and pos not in occupied and not raw[8+pos[1]*w+pos[0]] and pos not in dist:dist[pos]=dist[(x,y)]+1;q.append(pos)
        if not set(ROUTES[mid]).issubset(dist):raise ValueError('Disconnected garden route')
        row['routes']=[dict(x=x,y=y,steps=dist[(x,y)]) for x,y in ROUTES[mid]]
    r['scope']='Explicit Revora pillar layout and private reuse of3937 skygarden; not restored original Rose Garden. Native/server cells, reserved NPC footprints and routes verified; native rendering and portal acceptance pending.'
    return r
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--client',type=Path,required=True);p.add_argument('--runtime',type=Path,default=Path('bin/Debug'));p.add_argument('--sevenzip',type=Path,default=Path('C:/Program Files/7-Zip/7z.exe'));p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(audit(a.client,a.runtime,a.sevenzip),indent=2)+'\n',encoding='utf-8');print('Verified pillar/garden cells, footprints and routes.')
