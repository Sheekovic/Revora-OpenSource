"""Prepare private Ancient Devil display data; never modify the active client."""
from pathlib import Path
import hashlib
import json
import re
from prepare_shop_buy_hotfix import transform
from prepare_abyss_boss_profiles import section, field

ROOT=Path(__file__).resolve().parents[4]
WORK=ROOT/'Source/Revora-6711/obj/chi-refill-validation/ancient-boss-hud'
PREIMAGE='99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585'
DX9='3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60'
def sha(data): return hashlib.sha256(data).hexdigest()
def replace(row,key,old,new):
    if field(row,key)!=old: raise ValueError('Unexpected field: '+key)
    result,count=re.subn(rb'^('+key.encode()+rb'=)'+str(old).encode()+rb'(\r?)$',
                        lambda m:m[1]+str(new).encode()+m[2],row,flags=re.M)
    if count!=1: raise ValueError('Ambiguous field: '+key)
    return result

def patch(plain):
    if re.search(rb'^\[DevilGuard\]\r?$',plain,re.M): raise ValueError('Guard profile already exists')
    original=section(plain,'AncientDevil');template=section(plain,'WarriorDevil')
    if field(original,'TypeID')!=2407 or field(original,'MaxLife')!=65535 or field(original,'Level')!=120:
        raise ValueError('Ancient Devil template changed')
    if field(template,'Boss')!=0: raise ValueError('Guard template boss flag changed')
    boss=replace(original,'Boss',0,1)
    guard=template.replace(b'[WarriorDevil]',b'[DevilGuard]',1)
    for key,old,new in [('TypeID',7001,7000),('MaxLife',23588,6590),('Level',70,100)]: guard=replace(guard,key,old,new)
    updated=plain.replace(original,boss,1)+b'\r\n'+guard
    if section(updated,'WarriorDevil')!=template or section(updated,'AncientDevil')!=boss or section(updated,'DevilGuard')!=guard:
        raise ValueError('Profile preservation failed')
    return updated

def prepare():
    before=(ROOT/'6711/ini/monster.dat').read_bytes()
    if sha(before)!=PREIMAGE or sha((ROOT/'6711/Env_DX9/Conquer.exe').read_bytes())!=DX9:
        raise ValueError('Approved client preimage changed')
    plain=transform(before)
    if transform(plain,True)!=before: raise ValueError('Input codec round trip failed')
    updated=patch(plain);after=transform(updated,True)
    if transform(after)!=updated: raise ValueError('Output codec round trip failed')
    for folder,content in [('originals',before),('staged',after)]:
        path=WORK/folder/'ini/monster.dat';path.parent.mkdir(parents=True,exist_ok=True)
        if path.exists() and path.read_bytes()!=content: raise ValueError('Private artifact differs: '+str(path))
        path.write_bytes(content)
    manifest=dict(purpose='private-ancient-display-validation',relative='ini/monster.dat',before=sha(before),after=sha(after),
                  approved_dx9=DX9,installed=False,published=False,requires_restart=True,
                  profiles=[dict(name='AncientDevil',type_id=2407,max_life=65535,boss=1),dict(name='DevilGuard',type_id=7000,max_life=6590,boss=0)],
                  rendering_acceptance='pending',merge_with_other_staged_profiles_required=True)
    (WORK/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(manifest,indent=2))
if __name__=='__main__': prepare()
