"""Prepare exact-name profiles for daily world bosses and the five Boss Rush bosses."""
from pathlib import Path
import hashlib
import json
import re
import zipfile
from prepare_shop_buy_hotfix import transform

ROOT = Path(__file__).resolve().parents[4]
PROJECT = ROOT / 'Source/Revora-6711'
WORK = PROJECT / 'obj/chi-refill-validation/daily-bosses'
EXPECTED = '0740673b772cc70c8466e5776c14153fb7f06a751a0b2a4f00644136d3bd50bb'
PROFILES = [
    ('Moonfang King',39,100000000),('Dawnfire King',7486,100000000),
    ('Sunscar Tyrant',4220,100000000),('Nightveil King',2719,100000000),
    ('Misery Monk',7484,1000000),('Awakened Flame',7486,3000000),
    ('Thrilling Spook',4212,6000000),('Furious Flame',7487,9000000),
    ('Tyrant Nemesis',4220,12000000)]
sha = lambda b: hashlib.sha256(b).hexdigest()


def prepare():
    daily=(PROJECT/'Game/Features/DailyWorldBosses.cs').read_text()
    rush=(PROJECT/'Game/Features/BossRushSystem.cs').read_text()
    for name,type_id,hp in PROFILES[:4]:
        assert re.search(r'new Definition\(\d+,'+str(type_id)+r',"'+re.escape(name)+r'"',daily)
        assert 'Health = 100000000' in daily
    for name,type_id,hp in PROFILES[4:]:
        assert re.search(r'new RushBoss\('+str(type_id)+r', "'+re.escape(name)+r'", '+str(hp)+r',',rush)
    source=ROOT/'6711/ini/monster.dat'
    before=source.read_bytes()
    assert sha(before)==EXPECTED, 'Unexpected client monster catalog'
    exe_hash=sha((ROOT/'6711/Env_DX9/Conquer.exe').read_bytes())
    assert exe_hash=='3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60'
    plain=transform(before)
    assert transform(plain,True)==before
    sections=list(re.finditer(rb'^\[([^\r\n]+)\]\r?$[\s\S]*?(?=^\[|\Z)',plain,re.M))
    records=[]
    updated=plain
    for name,type_id,hp in PROFILES:
        assert ('['+name+']').encode() not in plain, name
        candidates=[s for s in sections if re.search(rb'^TypeID=0*'+str(type_id).encode()+rb'\r?$',s.group(),re.M)]
        assert candidates, ('Missing template',type_id,name)
        # Family 2719 has the original CreepySpook and the 1.0.11 name alias; either shares visuals.
        template=candidates[0]
        alias=template.group().replace(b'['+template.group(1)+b']',('['+name+']').encode(),1)
        for key,value in [('MaxLife',hp),('Boss',1),('Level',140)]:
            if value is None: continue
            alias,count=re.subn(rb'^'+key.encode()+rb'=\d+\r?$',key.encode()+b'='+str(value).encode()+b'\r',alias,count=1,flags=re.M)
            assert count==1,(name,key)
        updated+=b'\r\n'+alias
        records.append(dict(name=name,type_id=type_id,hp=hp,template=template.group(1).decode('latin1')))
    assert updated[:len(plain)]==plain
    for name,_,hp in PROFILES:
        rows=list(re.finditer(rb'^\['+re.escape(name.encode())+rb'\]\r?$[\s\S]*?(?=^\[|\Z)',updated,re.M))
        assert len(rows)==1
        assert re.search(rb'^Boss=1\r?$',rows[0].group(),re.M)
        assert re.search(rb'^Level=140\r?$',rows[0].group(),re.M)
        assert re.search(rb'^MaxLife='+str(hp).encode()+rb'\r?$',rows[0].group(),re.M)
    after=transform(updated,True)
    assert transform(after)==updated
    original=WORK/'originals/ini/monster.dat'
    staged=WORK/'staged/ini/monster.dat'
    original.parent.mkdir(parents=True,exist_ok=True);staged.parent.mkdir(parents=True,exist_ok=True)
    if original.exists(): assert original.read_bytes()==before
    original.write_bytes(before);staged.write_bytes(after)
    archive=WORK/'1.0.12.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        info=zipfile.ZipInfo('ini/monster.dat',(2026,9,14,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED
        z.writestr(info,after)
    with zipfile.ZipFile(archive) as z:
        assert z.namelist()==['ini/monster.dat'] and z.testzip() is None and z.read('ini/monster.dat')==after
    manifest=dict(version='1.0.12',before=sha(before),after=sha(after),zip_sha256=sha(archive.read_bytes()),zip_size=archive.stat().st_size,approved_dx9=exe_hash,profiles=records)
    (WORK/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print(json.dumps(manifest,indent=2))


if __name__=='__main__': prepare()
