"""Validate the installed beach grid for Revora's private Pirate hub choice."""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import struct
import subprocess
from stage_gloomy_jail import index_records


def inspect(client, floor, sevenzip):
    expected=(10271,b'map/map/beach.7z',128)
    if [r for r in index_records((client/'ini/GameMap.dat').read_bytes()) if r[0]==10271]!=[expected]:
        raise ValueError('Hub index identity changed')
    archive=client/'map/map/beach.7z'
    if hashlib.sha256(archive.read_bytes()).hexdigest()!='c3d4e6de96e942364b60afdeaf38e9e98816a4af1f214c07669ef7ffe7bb000f':
        raise ValueError('Unreviewed hub archive')
    native=subprocess.check_output([str(sevenzip),'e','-so',str(archive)],timeout=15)
    if hashlib.sha256(native).hexdigest()!='3b78387ad9d9cd521addd4f44af6f61929d547a57e633546e502857071729837':
        raise ValueError('Unreviewed hub native map')
    if struct.unpack_from('<II',native)!=(1004,0) or struct.unpack_from('<II',native,268)!=(214,214):
        raise ValueError('Unexpected hub header')
    w=h=214
    flags=[struct.unpack_from('<H',native,276+y*(w*6+4)+x*6)[0] for y in range(h) for x in range(w)]
    if not set(flags)<={0,1}:raise ValueError('Unexpected access flag')
    derived=struct.pack('<II',w,h)+bytes(flags)
    if floor.read_bytes()!=derived:raise ValueError('Server grid differs from reviewed native access grid')
    offset=276+h*(w*6+4)
    if struct.unpack_from('<I',native,offset)[0]!=2:raise ValueError('Unreviewed portal count')
    offset+=28
    if struct.unpack_from('<I',native,offset)[0]!=56:raise ValueError('Unreviewed object count')
    offset+=4
    for _ in range(56):
        if struct.unpack_from('<I',native,offset)[0]!=4:raise ValueError('Unreviewed object kind')
        offset+=420
    sites=[(116,191),(128,187),(107,178)]
    occupied={(x+dx,y+dy) for x,y in sites for dx in (-1,0,1) for dy in (-1,0,1)}
    landing=(114,188);seen={landing};queue=deque([landing])
    if flags[landing[1]*w+landing[0]] or landing in occupied:raise ValueError('Landing blocked')
    while queue:
        x,y=queue.popleft()
        for point in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
            a,b=point
            if 0<=a<w and 0<=b<h and flags[b*w+a]==0 and point not in occupied and point not in seen:
                seen.add(point);queue.append(point)
    approaches=[]
    for x,y in sites:
        points=sorted((a,b) for a,b in seen if max(abs(a-x),abs(b-y))==2)
        if not points:raise ValueError('NPC has no cardinal approach')
        approaches.append(dict(npc=[x,y],approach=points[0]))
    return dict(map=10271,dimensions=[w,h],floor_sha256=hashlib.sha256(derived).hexdigest(),
                landing=landing,reachable_cells=len(seen),approaches=approaches,
                portal_records=2,kind4_objects=56,opaque_trailer_bytes=len(native)-offset,
                original_hub_identity_verified=False,native_acceptance=False,
                scope='Revora hub choice; matches existing version1004 server base-floor model; native/trailer semantics not certified')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--client',type=Path,required=True);p.add_argument('--floor',type=Path,required=True)
    p.add_argument('--sevenzip',type=Path,default=Path('C:/Program Files/7-Zip/7z.exe'))
    p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    a.output.write_text(json.dumps(inspect(a.client,a.floor,a.sevenzip),indent=2)+'\n',encoding='utf-8')
    print('Private hub grid and three NPC approaches verified; native acceptance pending.')
