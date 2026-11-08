from pathlib import Path
import re, json, hashlib, zipfile

ROOT = Path(__file__).resolve().parents[4]
WORK = ROOT / 'Source/Revora-6711/obj/chi-refill-validation/subclass-shop'
CLIENT = ROOT / '6711'
RUNTIME = ROOT / 'Source/Revora-6711/bin/Debug'
SUBCLASS = {721259:30,721261:10,711188:150,723087:5,1088001:3,711679:150,723903:3,723340:1,723341:4,728883:10,723342:99}
INNER = {3005365:100,3005366:100,3005395:100,3005396:100,3007113:200,3007114:200,3005397:200,3005398:200,3007115:250,3007116:250,3007117:250,3005399:250,3005400:250,3005401:250,3007230:300,3007231:300,3007232:300}
PRICES = {**SUBCLASS, **INNER}
WORK.mkdir(parents=True, exist_ok=True)
records=[]
def sha(b): return hashlib.sha256(b).hexdigest()
def stage(source, relative, content, group):
    before=source.read_bytes()
    backup=WORK/'originals'/group/relative
    backup.parent.mkdir(parents=True,exist_ok=True)
    if backup.exists(): assert backup.read_bytes()==before, 'Source changed since backup'
    else: backup.write_bytes(before)
    dest=WORK/'staged'/group/relative
    dest.parent.mkdir(parents=True,exist_ok=True)
    dest.write_bytes(content)
    records.append(dict(source=str(source),relative=relative,group=group,before=sha(before),after=sha(content)))

# Byte-preserving inverse transforms, derived from the locally possessed CypterPLm
# data-format evidence. Verify the inverse against the entire original catalog.
seed=0x2537
key=[]
for _ in range(128):
    seed=(seed*214013+2531011)&0xffffffff
    key.append(((seed>>16)&0x7fff)&255)
def decrypt(b):
    out=bytearray(len(b))
    for i,x in enumerate(b):
        v=x^key[i%128]; n=i%8
        out[i]=((v>>n)|(v<<(8-n)))&255
    return bytes(out)
def encrypt(b):
    out=bytearray(len(b))
    for i,x in enumerate(b):
        n=i%8
        out[i]=(((x<<n)|(x>>(8-n)))&255)^key[i%128]
    return bytes(out)
def prices(b):
    found={}
    lines=b.splitlines(keepends=True)
    for index,line in enumerate(lines):
        if not re.match(rb'^\d+(?:@@| )',line): continue
        sep=b'@@' if b'@@' in line else b' '
        fields=line.split(sep)
        if not fields[0].isdigit(): continue
        item=int(fields[0])
        if item not in PRICES: continue
        assert item not in found and len(fields)>46
        found[item]=(fields[1].decode('ascii'),int(fields[36]),int(fields[46]))
        fields[36]=str(PRICES[item]).encode('ascii')
        lines[index]=sep.join(fields)
    assert set(found)==set(PRICES), found
    return b''.join(lines),found
encrypted=(CLIENT/'ini/itemtype.dat').read_bytes()
assert sha(encrypted)=='58784821002aa73173903a9a7d61d782ee6d5d25fa0e2873cd1558294735f976'
plain=decrypt(encrypted)
assert encrypt(plain)==encrypted
newplain,client_rows=prices(plain)
stage(CLIENT/'ini/itemtype.dat','ini/itemtype.dat',encrypt(newplain),'client')
source=RUNTIME/'database/itemtype-new.txt'
newserver,server_rows=prices(source.read_bytes())
stage(source,'database/itemtype-new.txt',newserver,'server')
assert all(client_rows[i][0]==server_rows[i][0] for i in PRICES)

def add_shop(source,group,relative):
    b=source.read_bytes()
    assert not re.search(rb'^ID=9100[89]\s*$',b,re.M)
    n=max(int(i) for i in re.findall(rb'^\[Shop(\d+)\]',b,re.M))+1
    b,count=re.subn(rb'^Amount=\d+',b'Amount='+str(n+2).encode(),b,count=1,flags=re.M)
    assert count==1
    section=''
    for offset,(uid,name,items) in enumerate([(91008,'SubclassSupplies',SUBCLASS),(91009,'InnerPowerShop',INNER)]):
        section+='\r\n[Shop%d]\r\nID=%d\r\nName=%s\r\nType=1\r\nMoneyType=1\r\nItemAmount=%d\r\n'%(n+offset,uid,name,len(items))
        section+=''.join('Item%d=%d\r\n'%(i,item) for i,item in enumerate(items))
    stage(source,relative,b+section.encode('ascii'),group)
add_shop(CLIENT/'ini/Shop.dat','client','ini/Shop.dat')
add_shop(RUNTIME/'database/shops/Shop.dat','server','database/shops/Shop.dat')
source=CLIENT/'ini/npc.ini'
b=source.read_bytes()
assert b'[NpcType4607]' not in b and b'[NpcType4608]' not in b
template=re.search(rb'\[NpcType1\]\r?\n.*?(?=\r?\n\[)',b,re.S).group()
for mesh,name,note in [(4607,b'SubclassSupplies',b'Subclass~Supplies'),(4608,b'InnerPowerShop',b'Inner~Power~Supplies')]:
    entry=template.replace(b'[NpcType1]',('[NpcType%d]'%mesh).encode()).replace(b'Name=Storekeeper',b'Name='+name).replace(b'Note=[Store]',b'Note=['+note+b']')
    b+=b'\r\n'+entry+b'\r\n'
stage(source,'ini/npc.ini',b,'client')
manifest=dict(version='1.0.8',npcs=[dict(id=91008,map=1002,x=424,y=339,mesh=46070,type=1),dict(id=91009,map=1002,x=428,y=343,mesh=46080,type=1)],files=records,items=[dict(id=i,name=server_rows[i][0],cps=p,old_cps=server_rows[i][1],stack=server_rows[i][2],shop=91008 if i in SUBCLASS else 91009) for i,p in PRICES.items()])
(WORK/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
with zipfile.ZipFile(WORK/'1.0.8.zip','w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for f in sorted((WORK/'staged/client').rglob('*')):
        if f.is_file():
            info=zipfile.ZipInfo(f.relative_to(WORK/'staged/client').as_posix(),(2026,9,13,0,0,0))
            info.compress_type=zipfile.ZIP_DEFLATED
            z.writestr(info,f.read_bytes())
print(json.dumps(dict(items=manifest['items'],zip_sha256=sha((WORK/'1.0.8.zip').read_bytes()),zip_size=(WORK/'1.0.8.zip').stat().st_size),indent=2))
