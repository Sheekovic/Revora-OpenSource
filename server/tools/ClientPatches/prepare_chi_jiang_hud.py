"""Stage Chi/Jiang boss profiles on the exact released 1.0.20 catalog."""
from pathlib import Path
import hashlib,json,re,zipfile
from prepare_shop_buy_hotfix import transform
ROOT=Path(__file__).resolve().parents[4]
WORK=ROOT/'Source/Revora-6711/obj/chi-jiang-hud'
sha=lambda b:hashlib.sha256(b).hexdigest()
def prepare():
    with zipfile.ZipFile('C:/Revora/Web/wwwroot/download/1.0.20.zip') as z: before=z.read('ini/monster.dat')
    assert sha(before)=='df6431be254159ce83a812532b6f24330df799bfec66e1cfc2ba6436b5d6970a'
    plain=transform(before);assert transform(plain,True)==before
    updated=plain
    for name,typeid in [('Chi~Beast',7902),('Jiang~Hu~Beast',2648)]:
        assert ('['+name+']').encode() not in plain
        sections=re.findall(rb'^\[[^\r\n]+\][\s\S]*?(?=^\[|\Z)',plain,re.M)
        templates=[s for s in sections if re.search(rb'^TypeID=0*'+str(typeid).encode()+rb'\r*$',s,re.M)]
        assert len(templates)==1
        alias=re.sub(rb'^\[[^\r\n]+\]',('['+name+']').encode(),templates[0],count=1)
        for key,value in [('MaxLife',5000000),('Boss',1),('Level',140),('ZoomPercent',200)]:
            alias,count=re.subn(rb'^'+key.encode()+rb'=\d+(\r*)$',lambda m:key.encode()+b'='+str(value).encode()+m[1],alias,count=1,flags=re.M)
            assert count==1
        updated+=b'\r\n'+alias
    assert updated[:len(plain)]==plain
    after=transform(updated,True);assert transform(after)==updated
    WORK.mkdir(parents=True,exist_ok=True)
    (WORK/'monster.dat').write_bytes(after)
    with zipfile.ZipFile(WORK/'1.0.21.zip','w',zipfile.ZIP_DEFLATED) as z:z.writestr('ini/monster.dat',after)
    with zipfile.ZipFile(WORK/'1.0.21.zip') as z:assert z.testzip() is None and z.read('ini/monster.dat')==after
    report=dict(version='1.0.21',before=sha(before),after=sha(after),sha256=sha((WORK/'1.0.21.zip').read_bytes()),size=(WORK/'1.0.21.zip').stat().st_size,health=5000000,profiles=['Chi~Beast','Jiang~Hu~Beast'])
    (WORK/'review.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report))
if __name__=='__main__':prepare()
