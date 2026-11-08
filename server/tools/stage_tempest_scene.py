"""Stage a private corrected Tempest floor; never replace the public map."""
import argparse
from collections import deque
import json
from pathlib import Path
import struct
import subprocess
from stage_gloomy_jail import CLIENT_EXE, CLIENT_INDEX, SERVER_INDEX, digest, index_records

ARCHIVE = '7f1250acad39ee4be449ccbaf548c59c7a83f15e856bd8c6454c24f0af280e59'
NATIVE = '1becced83f576eeb01ec371a3121ad5d10631d7114e8274c437af3009f4400b1'
LEGACY = 'c8441206ad582ce7e091170cb97596262c30817fb43bca86896db661e43d3d6b'
ENTRY, ROC, EXIT = (141,111), (136,103), (145,111)


def convert(native):
    if digest(native) != NATIVE:
        raise ValueError('Unreviewed native map')
    w,h = struct.unpack_from('<II', native, 268)
    if (w,h) != (224,224):
        raise ValueError('Changed dimensions')
    end = 276+h*(w*6+4)
    if struct.unpack_from('<IIII', native, end) != (1,190,162,0):
        raise ValueError('Changed inherited portal')
    return struct.pack('<II',w,h)+bytes(
        struct.unpack_from('<H',native,276+y*(w*6+4)+x*6)[0] != 0
        for y in range(h) for x in range(w))


def routes(floor):
    if len(floor) != 8+224*224 or struct.unpack_from('<II',floor) != (224,224):
        raise ValueError('Malformed floor')
    if any(b not in (0,1) for b in floor[8:]):
        raise ValueError('Invalid blocked cell')
    blocked = {(x,y) for y in range(224) for x in range(224) if floor[8+y*224+x]}
    footprint = {(EXIT[0]+dx,EXIT[1]+dy) for dx in (-1,0,1) for dy in (-1,0,1)}
    if footprint & blocked:
        raise ValueError('Blocked exit footprint')
    blocked |= footprint
    targets = [ROC,(143,111)]
    if any(p in blocked for p in [ENTRY]+targets):
        raise ValueError('Blocked entry/combat/exit/portal')
    q,dist = deque([ENTRY]),{ENTRY:0}
    while q:
        x,y = q.popleft()
        for p in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            if 0<p[0]<223 and 0<p[1]<223 and p not in blocked and p not in dist:
                dist[p]=dist[(x,y)]+1
                q.append(p)
    if any(p not in dist for p in targets):
        raise ValueError('Unreachable combat/exit/portal')
    return dict(entry=ENTRY,roc=ROC,exit_npc=EXIT,reserved_exit_radius=1,
                inherited_portal_walk_reachable=(190,162) in dist,
                targets=[dict(point=p,walk_steps=dist[p]) for p in targets])


def stage(client,runtime,destination,sevenzip):
    client,runtime = client.resolve(strict=True),runtime.resolve(strict=True)
    destination = destination.resolve()
    if destination.exists() or any(destination==p or p in destination.parents or destination in p.parents for p in (client,runtime)):
        raise ValueError('Use a new private destination separate from installed inputs')
    expected = {client/'Env_DX9/Conquer.exe':CLIENT_EXE,client/'ini/GameMap.dat':CLIENT_INDEX,
                runtime/'database/GameMap.dat':SERVER_INDEX,client/'map/map/star01.7z':ARCHIVE,
                runtime/'database/maps/1100.map':LEGACY}
    sources = {p:p.read_bytes() for p in expected}
    if any(digest(sources[p])!=sha for p,sha in expected.items()):
        raise ValueError('Changed installed input')
    for p in (client/'ini/GameMap.dat',runtime/'database/GameMap.dat'):
        if [r for r in index_records(sources[p]) if r[0]==1100] != [(1100,b'map/map/star01.7z',256)]:
            raise ValueError('Changed map binding')
    native = subprocess.check_output([str(sevenzip),'e','-so',str(client/'map/map/star01.7z'),'star01.DMap'],timeout=30)
    floor = convert(native)
    layout = routes(floor)
    legacy = sources[runtime/'database/maps/1100.map']
    if len(legacy)!=len(floor) or legacy[:8]!=floor[:8]:
        raise ValueError('Changed legacy dimensions')
    differences = sum(a!=b for a,b in zip(legacy[8:],floor[8:]))
    if differences != 136:
        raise ValueError('Changed discrepancy evidence')
    relative = 'server/database/maps/tempest-1100.map'
    report = dict(purpose='Private Revora combat adaptation; original Rainbow Sky identity unverified',
                  sources=[dict(path=str(p),sha256=s) for p,s in expected.items()],native_sha256=NATIVE,
                  corrected_cells=differences,layout=layout,inherited_portal=dict(x=190,y=162,index=0),
                  output=dict(path=relative,sha256=digest(floor),bytes=len(floor)),
                  installed=False,native_acceptance=False,
                  pending=['Owned lifecycle and explicit portal return handling','Paid permit and tracked encounter','Native/Host acceptance'])
    path=destination/relative
    path.parent.mkdir(parents=True,exist_ok=False)
    path.write_bytes(floor)
    if path.read_bytes()!=floor or any(digest(p.read_bytes())!=s for p,s in expected.items()):
        raise ValueError('Readback or source preservation failed; incomplete private output retained')
    (destination/'manifest.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    return report


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--client',type=Path,required=True)
    p.add_argument('--runtime',type=Path,default=Path('bin/Debug'))
    p.add_argument('--destination',type=Path,required=True)
    p.add_argument('--sevenzip',type=Path,default=Path('C:/Program Files/7-Zip/7z.exe'))
    a=p.parse_args()
    r=stage(a.client,a.runtime,a.destination,a.sevenzip)
    print(json.dumps(r,indent=2))
