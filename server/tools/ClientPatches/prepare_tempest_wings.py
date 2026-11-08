"""Compose private Tempest directions and corrected floor over the reviewed Fan package."""
import json,re,tempfile,shutil
from pathlib import Path
from prepare_warrior_journal import ROOT,WORK,INSTALLED,DX9,checked,sha
from prepare_shop_buy_hotfix import transform
MISSIONS={200}
BEGINS=["8796,1004,51,49,TaskmasterChang,JobCenter"]
FINISHES=["10850,1011,212,196,Mr.Wonder,PhoenixCastle"]
STEPS=['Level 100 or reborn, all classes. Speak to Taskmaster Chang in Job Center 51,49, then Mr. Wonder in Phoenix Castle 212,196. Pay five Meteor Scrolls once to enter your private sky. Defeat SkyRoc near 136,103. SkyKeeper at 145,111 returns you to Phoenix Castle. Bring the earned feather to Mr. Wonder with one free space for a bound Elite Tempest Wing and one bound P7 soul. Once per character.']
SOURCES={'client/ini/Questinfo.ini': '4e0614081e987dd01c437eb94cae0ff8ddc490e76a9ad6d98a1ee82102e70192', 'client/ini/itemtype.txt': '771d075f657f2404fb64515495f1988669aa60260c128184e59078746b15b959', 'client/ini/itemtype.dat': 'a5406b3223d575f76dc9cafe509350e3584eee23df4ed133854c433e25a5a2c2', 'client/ini/GameMap.dat': '48f46669af68189f30359ebd56d6db6665e608e4941d0a006b4495ebdcfd5a15', 'server/database/GameMap.dat': 'ae0a92063c4f82ca1face0386cc288b2f89d78b04b635f29ce745f5fafe743fd', 'server/database/maps/1782.map': '9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4', 'server/database/maps/1783.map': '9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4', 'server/database/maps/1784.map': '9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4', 'server/database/maps/1363.map': 'a9a555cf6b1b407f75b5ce19aaaec2109d3d1397cf8b0675b67acfdf7b5a54dd'}
TIPS={3006392:('An~enormous~legendary~bird`s~feather.~Mr.~Wonder~(PhoenixCastle~212,196)~can~refine~it~into~Tempest~Wings.','Bring~your~earned~Roc~feather~to~Mr.~Wonder~at~Phoenix~Castle~212,196.~Only~a~recorded~Tempest~kill~qualifies.~Recover~a~lost~earned~feather~from~him.')}

def rewrite(before):
    edits, seen = [], set()
    for match in re.finditer(rb"^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)", before, re.M):
        original = match.group()
        ids = re.findall(rb"^MissionId=(\d+)\r?$", original, re.M)
        selected = [int(i) for i in ids if int(i) in MISSIONS]
        if not selected:
            continue
        if len(ids) != 1 or selected[0] in seen:
            raise ValueError("Ambiguous Tempest Wings mission")
        mission = selected[0]
        seen.add(mission)
        step = 0
        changed = original
        values = {"Lv_min":"1", "Lv_max":"255", "Map":"0", "Profession":"0", "Prequest":"0", "BeginNpcId":BEGINS[step], "FinishNpcId":FINISHES[step],
                  "Prize":"Bound Elite Tempest Wing and one bound P7 soul. Once per character.",
                  "IntentionDesp":STEPS[step], "Intention1":STEPS[step], "IntentAmount":"1",
                  "Content":STEPS[step]+" Visits last fifteen minutes. Paid reentry is free. A recorded kill survives a full bag. Ask Mr. Wonder to recover a lost earned feather; existing stored feathers cannot be copied. Server eligibility is level100 or any rebirth; client level1 visibility permits reborn characters below100."}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            count=len(re.findall(pattern, changed, re.M))
            if count != 1:
                raise ValueError("Missing or duplicate Tempest Wings field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Tempest Wings mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Tempest Wings section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def tooltip(before):
    lines=before.splitlines(keepends=True);seen=set()
    for n,line in enumerate(lines):
        parts=line.split(b"@@")
        if not parts[0].isdigit() or int(parts[0]) not in TIPS:continue
        identity=int(parts[0]);old,new=TIPS[identity]
        if identity in seen or len(parts)<=53 or parts[53] not in (old.encode(),new.encode()):raise ValueError("Changed Tempest tooltip preimage")
        seen.add(identity);parts[53]=new.encode();lines[n]=b"@@".join(parts)
    if seen!=set(TIPS):raise ValueError("Missing Tempest tooltip")
    return b"".join(lines)


def prepare():
    inputs={};outputs={}
    def read(path,expected):
        data=checked(path,expected);inputs[path]=expected;return data
    read(ROOT/"6711/Env_DX9/Conquer.exe",DX9)
    read(ROOT/"6711/ini/Questinfo.ini",INSTALLED)
    for ext,h in [("txt","52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb"),("dat","ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753")]:read(ROOT/"6711/ini"/("itemtype."+ext),h)
    source=WORK/"attack-talisman-composed-v3"
    for rel,h in SOURCES.items():outputs[rel]=read(source/rel,h)
    outputs["server/database/maps/tempest-1100.map"]=read(WORK/"tempest-scene/server/database/maps/tempest-1100.map","ae99b05f276ccacaff95c3772211a5c9b1f0cc5fe4ea88c2cd9da8c97e346bd6")
    before=outputs["client/ini/Questinfo.ini"];after=rewrite(before)
    if rewrite(after)!=after:raise ValueError("Tempest journal is not idempotent")
    outputs["client/ini/Questinfo.ini"]=after
    for ext in ("txt","dat"):
        rel="client/ini/itemtype."+ext;before=outputs[rel];plain=transform(before) if ext=="dat" else before
        if ext=="dat" and transform(plain,True)!=before:raise ValueError("Catalog round trip failed")
        edited=tooltip(plain)
        if tooltip(edited)!=edited:raise ValueError("Tempest tooltip is not idempotent")
        outputs[rel]=transform(edited,True) if ext=="dat" else edited
    manifest=dict(purpose="Private composed Tempest directions and corrected floor plus all prior Fan/quest/map repairs",inputs=[dict(path=str(p),sha256=h) for p,h in inputs.items()],
        outputs=[dict(path=p,sha256=sha(b),bytes=len(b)) for p,b in outputs.items()],missions=sorted(MISSIONS),tooltips=sorted(TIPS),installed=False,signed=False,published=False,
        pending=["Native DX9/Host acceptance","Signing","Transactional installation with exact preimage backups"],
        note="Changes only mission200 and the feather tooltip over the hash-pinned Fan package; adds a dedicated private floor. Existing1100 client binding is reused. Original public1100 stays unchanged. Proprietary contents stay outside Git.")
    outputs["manifest.json"]=(json.dumps(manifest,indent=2)+"\n").encode()
    destination=WORK/"tempest-wings-composed-v2"
    if destination.exists():
        actual={str(p.relative_to(destination)).replace("\\","/"):p.read_bytes() for p in destination.rglob("*") if p.is_file()}
        if actual!=outputs:raise ValueError("Existing private Tempest bundle differs")
    else:
        temporary=Path(tempfile.mkdtemp(prefix=".tempest-compose-",dir=WORK))
        try:
            for rel,data in outputs.items():
                path=temporary/rel;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
                if path.read_bytes()!=data:raise ValueError("Tempest readback failed")
            for path,expected in inputs.items():checked(path,expected)
            temporary.rename(destination)
        finally:
            if temporary.exists():
                if temporary.resolve().parent!=WORK.resolve() or not temporary.name.startswith(".tempest-compose-"):raise ValueError("Unexpected Tempest cleanup path")
                shutil.rmtree(temporary)
    for path,expected in inputs.items():checked(path,expected)
    print(json.dumps(manifest["outputs"],indent=2));return manifest
if __name__=="__main__":prepare()
