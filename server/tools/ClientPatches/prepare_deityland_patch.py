"""Prepare combined DeityLand route + Chi/Jiang HUD patch without publishing."""
from pathlib import Path
import hashlib,json,struct,zipfile
import prepare_chi_jiang_hud as hud
ROOT=hud.ROOT
sha=lambda b:hashlib.sha256(b).hexdigest()
def add_route(data,record):
 n=struct.unpack_from('<I',data)[0];o=4;ids=[];existing=[]
 for _ in range(n):
  start=o;mid,l=struct.unpack_from('<II',data,o);ids.append(mid);o+=8+l+4
  if mid==10250:existing.append(data[start:o])
 assert o==len(data)
 if existing:
  assert existing==[record]
  return data
 return struct.pack('<I',n+1)+data[4:]+record
def prepare():
 hud.prepare()
 donor=(ROOT/'6711/ini/GameMap.dat').read_bytes();n=struct.unpack_from('<I',donor)[0];o=4;records=[]
 for _ in range(n):
  start=o;mid,l=struct.unpack_from('<II',donor,o);o+=8;path=donor[o:o+l];o+=l+4
  if mid==10250:
   assert path==b'map/map/island-snail01.7z' and (ROOT/'6711'/path.decode()).is_file()
   records.append(donor[start:o])
 assert len(records)==1
 work=ROOT/'Source/Revora-6711/obj/deityland';work.mkdir(exist_ok=True)
 with zipfile.ZipFile('C:/Revora/Web/wwwroot/download/1.0.20.zip') as z:client=z.read('ini/GameMap.dat')
 server=(ROOT/'Source/Revora-6711/bin/Debug/database/GameMap.dat').read_bytes()
 for name,data in [('client-GameMap.dat',client),('server-GameMap.dat',server)]:
  (work/name).write_bytes(add_route(data,records[0]))
 with zipfile.ZipFile(hud.WORK/'1.0.21.zip','w',zipfile.ZIP_DEFLATED) as z:
  for name,data in [('ini/monster.dat',(hud.WORK/'monster.dat').read_bytes()),('ini/GameMap.dat',(work/'client-GameMap.dat').read_bytes())]:
   info=zipfile.ZipInfo(name,(2026,10,5,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,data)
 with zipfile.ZipFile(hud.WORK/'1.0.21.zip') as z:assert z.testzip() is None
 r=json.loads((hud.WORK/'review.json').read_text());r.update(sha256=sha((hud.WORK/'1.0.21.zip').read_bytes()),size=(hud.WORK/'1.0.21.zip').stat().st_size,files=[dict(path='ini/monster.dat',sha256=sha((hud.WORK/'monster.dat').read_bytes())),dict(path='ini/GameMap.dat',sha256=sha((work/'client-GameMap.dat').read_bytes()))])
 (hud.WORK/'review.json').write_text(json.dumps(r,indent=2));print('Combined patch prepared',r['sha256'])
if __name__=='__main__':prepare()
