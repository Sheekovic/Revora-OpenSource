"""Compose private Heaven Fan directions over the reviewed Tower package; never install."""
import json,re,tempfile,shutil
from pathlib import Path
from prepare_warrior_journal import ROOT,WORK,INSTALLED,DX9,checked,sha
from prepare_shop_buy_hotfix import transform
MISSIONS=set(range(400,410))
SUGAR="4565,1015,685,599,SugarTang,BirdIsland"
CHOU="4582,1011,788,456,VillagerChou,PhoenixCastle"
KUNLUN="4583,1000,490,618,KunlunWanderer,DesertCity"
PLATFORM="9910414,1015,686,723,GardenPlatform,BirdIsland"
BEGINS=[SUGAR,CHOU,CHOU,SUGAR,KUNLUN,KUNLUN,CHOU,CHOU,CHOU,PLATFORM]
FINISHES=[CHOU,CHOU,SUGAR,KUNLUN,KUNLUN,CHOU,CHOU,CHOU,PLATFORM,SUGAR]
STEPS=[
 "Complete the Star Tower quest, then ask Sugar Tang about the Heaven Fan. Bring her Jadeite to Villager Chou at Phoenix Castle 788,456.",
 "Buy Bamboo Wine from Wine Kiddo at 182,280 for 10,000 silver. Hunt Winged Snakes near 338,126 for five pieces of meat, then bring both to Chou at 788,456.",
 "Bring Tang Silk to Sugar Tang at Bird Island 685,599. Choose her Heaven Fan option.",
 "Speak to Kunlun Wanderer at Desert City 490,618.",
 "Buy Kunlun's cave permit for 10,000 silver. Defeat the Viper near 56,50 for Poison Fang, then the Brigand near 60,50 for a Bee Kettle. Use the kettle to return. Ask Kunlun for liquid. Use either ingredient to mix Sacred Water, then use that water with Tang Silk to make Poem Silk. Cave reentry is free after the first permit.",
 "Bring Poem Silk to Villager Chou at Phoenix Castle 788,456.",
 "Defeat five Crimson Claws near 788,460, then speak to Chou. Leave one free space to exchange Poem Silk for Incense and Hairpin.",
 "Speak to Lake Keeper at 469,840 to enter Swan Lake. Use Incense at 475,844 inside. Speak to Herbalist Chou at 475,840, then defeat Crimson Viper near 479,844. Return with Purple Eye to Chou at 788,456.",
 "Ask Chou for your Jade Courtesan location on Bird Island: 733,776; 803,884; 797,513; or 703,653. Defeat her for Jade Scepter. Offer Purple Eye to Sparrow at 680,720; Jade Scepter to Dragon at 694,720; Jadeite to Tiger at 680,734; Hairpin to Leopard at 694,734. Speak to Garden Platform at 686,723. Offerings survive reentry.",
 "Inside the garden, choose a box at 52,50; 58,50; or 64,50. A wrong choice summons a ghost: defeat it before another box opens. Exchange the quest fan with Mr. Loneliness at 60,64 for a Poem Book. Return to Sugar Tang at Bird Island 685,599 with one free space for a bound Super Heaven Fan and one bound P7 soul. Once per character."]
SOURCES={'client/ini/Questinfo.ini': 'a69ac0d6be78134bdd82fa2a43d0fc81dc9079436cd99b6d2919a10429be0a95', 'client/ini/itemtype.txt': '215d2f071ffd7caefbfd13331e5f2c99952953dbf74ce373d9ca807c8c28c5a1', 'client/ini/itemtype.dat': 'b034bafd53b51db365476c1e66073d17a6a3e61e1949d7f53b550f73cbd95b55', 'client/ini/GameMap.dat': '48f46669af68189f30359ebd56d6db6665e608e4941d0a006b4495ebdcfd5a15', 'server/database/GameMap.dat': 'ae0a92063c4f82ca1face0386cc288b2f89d78b04b635f29ce745f5fafe743fd', 'server/database/maps/1782.map': '9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4', 'server/database/maps/1783.map': '9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4', 'server/database/maps/1784.map': '9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4', 'server/database/maps/1363.map': 'a9a555cf6b1b407f75b5ce19aaaec2109d3d1397cf8b0675b67acfdf7b5a54dd'}
TIPS={710216: ('Use~it~to~activate~the~Sparrow~Pillar~in~Bird~Island~(653,700).', 'Offer~Purple~Eye~to~Sparrow~Pillar~at~Bird~Island~680,720.'), 710217: ('Use~it~to~activate~the~Dragon~Pillar~in~Bird~Island~(653,700).', 'Offer~Jade~Scepter~to~Dragon~Pillar~at~Bird~Island~694,720.'), 710218: ('Use~it~to~activate~the~Tiger~Pillar~in~Bird~Island~(653,700).', 'Keep~this~Jadeite~for~Chou~and~the~quest.~Later~offer~it~to~Tiger~Pillar~at~Bird~Island~680,734.'), 710219: ('Use~it~to~activate~the~Leopard~Pillar~in~Bird~Island~(653,700).', 'Offer~Hairpin~to~Leopard~Pillar~at~Bird~Island~694,734.'), 710220: ('Villager~Chou~needs~it~for~lunch.', 'Bring~five~pieces~with~Bamboo~Wine~to~Villager~Chou~at~Phoenix~Castle~788,456.'), 710221: ('Attack~Talisman,~which~increases~the~damage~you~cause.', 'Quest~fan,~not~equipment.~Exchange~it~with~Mr.~Loneliness~at~garden~60,64~for~the~Poem~Book.'), 710222: ('A~book~that~was~written~by~Sugar~Tang`s~grandma~and~Mr.Loneliness.~Take~it~to~Sugar~Tang.', 'Bring~this~book~to~Sugar~Tang~at~Bird~Island~685,599.~Leave~one~free~space~for~bound~Super~Fan~and~bound~P7~soul.'), 721798: ('Kunlun~Wanderer~told~you~that~you~need~to~dye~this~with~Sacred~Water~to~unveil~a~mystery.', 'Show~Sugar~Tang,~then~Kunlun~Wanderer.~Use~Sacred~Water~with~this~silk~to~reveal~Poem~Silk.'), 721799: ('Burn~this~Incense~in~Swan~Lake~(55,109).', 'Enter~through~Lake~Keeper~at~Phoenix~Castle~469,840.~Use~inside~Swan~Lake~at475,844;~then~speak~to~the~spirit~at475,840.'), 721800: ('Villager~Chou~likes~it~so~much.~Take~it~to~him.', 'Bring~this~wine~and~five~Snake~Meat~to~Villager~Chou~at~Phoenix~Castle~788,456.'), 721801: ('Find~Herbalist~Chou`s~grandson~in~Maple~Forest~to~learn~more.', 'Bring~to~Villager~Chou~at~Phoenix~Castle~788,456.~Defeat~five~Crimson~Claws,~then~speak~to~him~again.'), 721802: ('Dissolve~the~Poison~Fang~in~it,~and~you`ll~get~the~Sacred~Water~which~can~be~used~to~dye~the~Tang~Silk.', 'Use~this~liquid~or~Poison~Fang~to~mix~Sacred~Water.~Both~ingredients~are~consumed.'), 721803: ('Dissolve~this~fang~into~the~Kunlun~Liquid,~and~you`ll~get~the~Sacred~Water~which~can~be~used~to~dye~the~Tang~Silk.', 'Return~with~the~Bee~Kettle.~Ask~Kunlun~Wanderer~at~Desert~City490,618~for~liquid,~then~use~either~ingredient.'), 721804: ('You~got~this~water~by~combining~the~Kunlun~Liquid~with~the~Poison~Fang.~Use~it~to~dye~the~Tang~Silk.', 'Use~with~Tang~Silk~to~reveal~Poem~Silk.~Both~ingredients~are~consumed.'), 721806: ('A~kettle~that~will~bring~you~out~of~here.', 'Use~inside~your~Viper~Cave~to~return~to~Desert~City492,618.~The~kettle~is~consumed.')}

def rewrite(before):
    edits, seen = [], set()
    for match in re.finditer(rb"^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)", before, re.M):
        original = match.group()
        ids = re.findall(rb"^MissionId=(\d+)\r?$", original, re.M)
        selected = [int(i) for i in ids if int(i) in MISSIONS]
        if not selected:
            continue
        if len(ids) != 1 or selected[0] in seen:
            raise ValueError("Ambiguous Attack Talisman mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 400
        changed = original
        values = {"Lv_min":"100", "Lv_max":"255", "Map":"0", "Profession":"0", "Prequest":str(528 if step==0 else 399+step), "BeginNpcId":BEGINS[step], "FinishNpcId":FINISHES[step],
                  "Prize":("100 Silver. " if step<9 else "")+"Final reward: bound Super Heaven Fan and one bound P7 soul. Once per character.",
                  "IntentionDesp":STEPS[step], "Intention1":STEPS[step], "IntentAmount":"1",
                  "Content":STEPS[step]+" Private visits last ten minutes. Earned materials, offerings and box choices persist. Use NPC lost-item options for missing items; stored or consumed items cannot be copied. Rewards are once per character, not per rebirth."}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            count=len(re.findall(pattern, changed, re.M))
            if count != 1:
                raise ValueError("Missing or duplicate Attack Talisman field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Attack Talisman mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Attack Talisman section")
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
        if identity in seen or len(parts)<=53 or parts[53] not in (old.encode(),new.encode()):raise ValueError("Changed Fan tooltip preimage")
        seen.add(identity);parts[53]=new.encode();lines[n]=b"@@".join(parts)
    if seen!=set(TIPS):raise ValueError("Missing Fan tooltip")
    return b"".join(lines)


def prepare():
    inputs={};outputs={}
    def read(path,expected):
        data=checked(path,expected);inputs[path]=expected;return data
    read(ROOT/"6711/Env_DX9/Conquer.exe",DX9)
    read(ROOT/"6711/ini/Questinfo.ini",INSTALLED)
    for ext,h in [("txt","52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb"),("dat","ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753")]:read(ROOT/"6711/ini"/("itemtype."+ext),h)
    source=WORK/"defense-talisman-composed-v2"
    for rel,h in SOURCES.items():outputs[rel]=read(source/rel,h)
    before=outputs["client/ini/Questinfo.ini"];after=rewrite(before)
    if rewrite(after)!=after:raise ValueError("Fan journal is not idempotent")
    outputs["client/ini/Questinfo.ini"]=after
    for ext in ("txt","dat"):
        rel="client/ini/itemtype."+ext;before=outputs[rel];plain=transform(before) if ext=="dat" else before
        if ext=="dat" and transform(plain,True)!=before:raise ValueError("Catalog round trip failed")
        edited=tooltip(plain)
        if tooltip(edited)!=edited:raise ValueError("Fan tooltip is not idempotent")
        outputs[rel]=transform(edited,True) if ext=="dat" else edited
    manifest=dict(purpose="Private composed Heaven Fan directions plus all prior Tower/quest/map repairs",inputs=[dict(path=str(p),sha256=h) for p,h in inputs.items()],
        outputs=[dict(path=p,sha256=sha(b),bytes=len(b)) for p,b in outputs.items()],missions=sorted(MISSIONS),tooltips=sorted(TIPS),installed=False,signed=False,published=False,
        pending=["Native DX9/Host acceptance","Signing","Transactional installation with exact preimage backups"],
        note="Changes only Fan journal sections and tooltip fields over the hash-pinned Tower package. Fan scenes use existing1025/1011/3937 bindings; no new map alias. Proprietary contents stay outside Git.")
    outputs["manifest.json"]=(json.dumps(manifest,indent=2)+"\n").encode()
    destination=WORK/"attack-talisman-composed-v3"
    if destination.exists():
        actual={str(p.relative_to(destination)).replace("\\","/"):p.read_bytes() for p in destination.rglob("*") if p.is_file()}
        if actual!=outputs:raise ValueError("Existing private Fan bundle differs")
    else:
        temporary=Path(tempfile.mkdtemp(prefix=".fan-compose-",dir=WORK))
        try:
            for rel,data in outputs.items():
                path=temporary/rel;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
                if path.read_bytes()!=data:raise ValueError("Fan readback failed")
            for path,expected in inputs.items():checked(path,expected)
            temporary.rename(destination)
        finally:
            if temporary.exists():
                if temporary.resolve().parent!=WORK.resolve() or not temporary.name.startswith(".fan-compose-"):raise ValueError("Unexpected Fan cleanup path")
                shutil.rmtree(temporary)
    for path,expected in inputs.items():checked(path,expected)
    print(json.dumps(manifest["outputs"],indent=2));return manifest
if __name__=="__main__":prepare()
