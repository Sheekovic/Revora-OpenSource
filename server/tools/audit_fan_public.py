"""Audit public Fan NPC approach routes."""
import argparse,json,struct
from collections import deque
from pathlib import Path
import audit_defense_talisman_terrain as terrain
NPCS={1011:[(788,456),(182,280),(469,840)],1000:[(490,618)],1015:[(685,599)]}
ROUTES={1011:[(790,456),(184,280),(471,840),(471,844)],1000:[(492,618)],1015:[(687,599)]}
def audit(client,runtime,sevenzip):
    terrain.POINTS={mid:NPCS[mid]+ROUTES[mid] for mid in NPCS};r=terrain.audit(client,runtime,sevenzip)
    for row in r['scenes']:
        mid=row['map']
        if not row['verified']:raise ValueError('Garden layout cells differ')
        raw=(runtime/f'database/maps/{mid}.map').read_bytes();w,h=struct.unpack_from('<II',raw);start=ROUTES[mid][0];q=deque([start]);dist={start:0}
        occupied={(x+dx,y+dy) for x,y in NPCS[mid] for dx in (-1,0,1) for dy in (-1,0,1)}
        while q and not all(pos in dist for pos in ROUTES[mid]):
            x,y=q.popleft()
            for pos in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                if 0<pos[0]<w-1 and 0<pos[1]<h-1 and pos not in occupied and not raw[8+pos[1]*w+pos[0]] and pos not in dist:dist[pos]=dist[(x,y)]+1;q.append(pos)
        if not all(pos in dist for pos in ROUTES[mid]):raise ValueError('Disconnected garden route')
        row['routes']=[dict(x=x,y=y,steps=dist[(x,y)]) for x,y in ROUTES[mid]]
    r['scope']='Public Fan NPC coordinates and connected approach routes, reserving their footprints. Lake Keeper is a Revora addition at469,840; entry approach471,844 remains reachable. Native rendering pending.'
    return r
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--client',type=Path,required=True);p.add_argument('--runtime',type=Path,default=Path('bin/Debug'));p.add_argument('--sevenzip',type=Path,default=Path('C:/Program Files/7-Zip/7z.exe'));p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(audit(a.client,a.runtime,a.sevenzip),indent=2)+'\n',encoding='utf-8');print('Verified public Fan NPC cells and approach routes.')
