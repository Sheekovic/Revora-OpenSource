"""Read-only Flame Lit catalog and stone-approach audit; never installs assets."""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
from audit_warrior_epic import item_rows,require
from ClientPatches.prepare_shop_buy_hotfix import transform

HASHES={
 'client_items':'d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a',
 'server_items':'1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef',
 'achievement':'df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc',
 '1002':'9f75cc638480a0485e6b17bdf9f9f76886a9ab7449317bebe18e72ef6ad34a10',
 '1038':'1707d73636d727ac471673736dc3965596aee43e09818da4b67c0d1c8bd4b2fd'}
SITES=[(1002,350,327),(1038,317,270),(1038,236,291),(1038,194,168),(1038,115,53),
       (1038,316,378),(1038,136,182),(1038,38,94),(1002,350,321),(1038,62,59)]
NAMES=['JadeRune']+['GoldRune']*3+['SpiritRune']*3+['HeavenRune']*3+['DragonRune']
def sha(data):return hashlib.sha256(data).hexdigest()
def approaches(data,points):
    require(len(data)>=8,'Truncated terrain')
    w,h=struct.unpack_from('<ii',data)
    require(0<w<=4096 and 0<h<=4096 and len(data)==8+w*h,'Invalid terrain dimensions')
    grid=data[8:];require(all(v in (0,1) for v in grid),'Unknown terrain flags')
    result=[]
    for x,y in points:
        require(0<=x<w and 0<=y<h,'Stone outside terrain')
        choices=[(a,b) for a in range(max(0,x-6),min(w,x+7)) for b in range(max(0,y-6),min(h,y+7))
                 if max(abs(a-x),abs(b-y))>1 and grid[a+b*w]==0]
        require(choices,'No walkable stone approach')
        result.append(min(choices,key=lambda p:(abs(p[0]-x)+abs(p[1]-y),p)))
    return result

def audit(client,runtime):
    inputs={'client_items':(client/'ini/itemtype.dat').read_bytes(),'server_items':(runtime/'database/itemtype-new.txt').read_bytes(),
            'achievement':(client/'ini/Achievement.dat').read_bytes()}
    for mapid in (1002,1038):inputs[str(mapid)]=(runtime/f'database/maps/{mapid}.map').read_bytes()
    for key,data in inputs.items():require(sha(data)==HASHES[key],'Reviewed input changed: '+key)
    ids=set(range(729960,729971));native=item_rows(inputs['client_items'],True,ids);server=item_rows(inputs['server_items'],False,ids)
    for i,name in enumerate(NAMES):
        n,s=native[729960+i],server[729960+i]
        require(len(n)>53 and len(s)>53 and n[1]==s[1]==name.encode() and n[53]==s[53],'Rune catalog mismatch')
        if i<10:
            coords=re.findall(rb'\((\d+),(\d+)\)',n[53]);require(len(coords)==1 and tuple(map(int,coords[0]))==SITES[i][1:],'Rune stone coordinate changed')
    plain=transform(inputs['achievement']);require(transform(plain,True)==inputs['achievement'],'Achievement codec mismatch')
    rows=[line.split() for line in plain.splitlines() if len(line.split())>1 and line.split()[1]==b'11016']
    require(len(rows)==1 and len(rows[0])==7 and rows[0][0]==b'60216' and rows[0][4]==b'10','Flame achievement identity changed')
    routes={str(m):approaches(inputs[str(m)],[(x,y) for mapid,x,y in SITES if mapid==m]) for m in (1002,1038)}
    return dict(hashes=HASHES,first_item=729960,last_item=729970,ordered_sites=SITES,achievement=11016,achievement_score=10,
                approaches=routes,proves_gate_crossing=False,proves_native_acceptance=False)
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--client',type=Path,required=True);p.add_argument('--runtime',type=Path,required=True)
    a=p.parse_args();print(json.dumps(audit(a.client,a.runtime),indent=2))
