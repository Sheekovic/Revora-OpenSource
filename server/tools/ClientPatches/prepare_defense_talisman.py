"""Compose private Tower journal/tooltips and both pending map repairs; never install."""
import json
import re
import sys
import tempfile
import shutil
from pathlib import Path
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha
from prepare_shop_buy_hotfix import transform
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from stage_defense_talisman_scenes import add_scenes, FLOOR, ARCHIVE as HOUSE_ARCHIVE
from stage_gloomy_jail import add_map, CLIENT_INDEX, SERVER_INDEX, ARCHIVE as JAIL_ARCHIVE
SOURCE="a9bb11d3b44be6208b3e01abb011ef42704f756a7e5703f36bc862fd8266b4c2"
MISSIONS=set(range(520,529))
SUGAR="4565,1015,685,599,SugarTang,BirdIsland"
YANG="4566,1015,732,516,YangYun,BirdIsland"
HOUSE="4567,1782,51,70,YangYun,TreasureHouse"
RUAN="4568,1784,55,55,RuanBrother,Hut"
PLUM="4572,1000,472,578,Plum,DesertCity"
DREAM="4577,1015,362,397,DreamDoor,BirdIsland"
GHOUL="4573,1000,85,313,GhoulKong,DesertCity"
CAULDRON="4578,1787,48,37,Cauldron,Dungeon3F"
BEGINS=[SUGAR,YANG,"4579,1015,325,371,NetPeddler,BirdIsland",DREAM,RUAN,PLUM,YANG,GHOUL,SUGAR]
FINISHES=[HOUSE,YANG,DREAM,RUAN,PLUM,YANG,"4574,1783,52,50,InvocationStone,TreasureHouse",CAULDRON,SUGAR]
STEPS=[
 "Ask Sugar Tang to enter the house. Defeat five bandits near 40,55, then report to Yang Yun at 51,70.",
 "Speak to Yang Yun on Bird Island at 732,516 about the lost splinters.",
 "Buy nets for 30,000 silver at 325,371. Use each at 310,380 and defeat your Trout Elf. Trade three fish to Ye Sheng at 324,400 for a poise. Open Time at 340,404, Season at 354,402 and Dream at 362,397 in that order.",
 "Enter the Hut through a door and ask Ruan Brother at 55,55 about the spear. Leave, then defeat Bloody Shawn near 400,650. He appears every ten minutes. Return through a door with the spear.",
 "Bring news of the splinter to Plum at 472,578 in Desert City.",
 "At 166,226 in the desert, defeat the Condor and then Caprice Leader. Your splinter is replaced by a handkerchief. Show it to Yang Yun at 732,516.",
 "Ask Yang Yun to enter his house again. Defeat five rats near 40,55. Collect the invocation at 52,50, then visit Ghoul Kong at 85,313 in the desert.",
 "Enter through Ghoul Kong. Defeat generals at 96,120 and 70,60 on floor one; use the invocation near 70,56 to descend. On floor two defeat generals at 113,122 and 62,81, then use it near 62,77. Elsewhere in a dungeon, the invocation returns you safely. Defeat Yang Feng near 56,64 on floor three with three free slots. At the Cauldron at 48,37, smelt all four weapons and restore the tower with three splinters, four clinkers and the invocation.",
 "Bring the restored quest tower to Sugar Tang at 685,599 with one free bag slot. Receive a bound Elite Star Tower and one bound P7 soul, once per character."]
TIPS={721778: ('Trade~3~Fresh~Fish~with~Ye~Sheng~(192,212)~for~an~Iron~Poise.', 'Trade~three~Fresh~Fish~with~Ye~Sheng~at~Bird~Island~324,400~for~an~Iron~Poise.'), 721779: ('Go~to~Bird~Island~(170,190)~and~use~this~to~catch~Fresh~Fish~there.', 'Use~at~Bird~Island~310,380.~Defeat~your~Trout~Elf;~each~earned~fish~consumes~one~net.'), 721780: ('You~need~this~to~open~three~stone~doors.', 'Open~Time~340,404,~Season~354,402,~then~Dream~362,397~on~Bird~Island.'), 721786: ('You~need~to~visit~Ghoul~Kong~(85,313)~at~the~entrance~to~the~Dungeon~to~read~this.', 'Show~Ghoul~Kong~at~Desert~City~85,313.~In~the~dungeon~use~near~floor-one~70,56~or~floor-two~62,77~to~descend;~elsewhere~it~returns~you~safely.'), 721785: ('Extracted~from~gold~by~monks,~the~Star~Tower~contains~a~precious~Buddhist~bone~relic~to~ensure~defense.', 'Bring~this~quest~tower~to~Sugar~Tang~at~Bird~Island~685,599~with~one~free~bag~slot.~It~is~not~equipment.')}
JAIL_FLOOR='a9a555cf6b1b407f75b5ce19aaaec2109d3d1397cf8b0675b67acfdf7b5a54dd'

def rewrite(before):
    edits, seen = [], set()
    for match in re.finditer(rb"^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)", before, re.M):
        original = match.group()
        ids = re.findall(rb"^MissionId=(\d+)\r?$", original, re.M)
        selected = [int(i) for i in ids if int(i) in MISSIONS]
        if not selected:
            continue
        if len(ids) != 1 or selected[0] in seen:
            raise ValueError("Ambiguous Defense Talisman mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 520
        changed = original
        values = {"Lv_min":"100", "Lv_max":"255", "Map":"0", "BeginNpcId":BEGINS[step], "FinishNpcId":FINISHES[step],
                  "Prize":("100 Silver. " if step<7 else "")+"Final reward: bound Elite Star Tower and one bound P7 soul. Once per character.",
                  "IntentionDesp":STEPS[step], "Intention1":STEPS[step], "IntentAmount":"1",
                  "Content":STEPS[step]+" Revora: private visits last ten minutes. Unfinished house kills reset on reentry; earned materials and dungeon floors persist. Use NPC lost-item options for truly missing items. Stored or consumed items cannot be copied. Rewards are once per character, not per rebirth."}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            count=len(re.findall(pattern, changed, re.M))
            if count==0 and key=="Content" and mission==520:
                changed+=b"Content="+value.encode("ascii")+b"\r\n"
                continue
            if count != 1:
                raise ValueError("Missing or duplicate Defense Talisman field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Defense Talisman mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Defense Talisman section")
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
        if identity in seen or len(parts)<=53 or parts[53] not in (old.encode(),new.encode()):raise ValueError("Changed Tower tooltip preimage")
        seen.add(identity);parts[53]=new.encode();lines[n]=b"@@".join(parts)
    if seen!=set(TIPS):raise ValueError("Missing Tower tooltip")
    return b"".join(lines)


def prepare():
    inputs={};outputs={}
    def read(path,expected):
        data=checked(path,expected);inputs[path]=expected;return data
    read(ROOT/"6711/Env_DX9/Conquer.exe",DX9)
    read(ROOT/"6711/ini/Questinfo.ini",INSTALLED)
    before=read(WORK/"desert-expedition-journal/staged/ini/Questinfo.ini",SOURCE)
    after=rewrite(before)
    if rewrite(after)!=after:raise ValueError("Tower journal is not idempotent")
    outputs["client/ini/Questinfo.ini"]=after
    catalog=[("txt","52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb","945d0c61f987f1bde6f051f832dc61a59f3127799dd5a454bf88e08f95d73f58"),
             ("dat","ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753","d6298cf31f3d2b4ce75d2f53c9ca9dd2a7ba3df3f0efc76fe9dd5824756f1a19")]
    for ext,installed,source in catalog:
        rel="ini/itemtype."+ext;read(ROOT/"6711"/rel,installed);original=read(WORK/"desert-expedition-journal/staged"/rel,source)
        plain=transform(original) if ext=="dat" else original
        if ext=="dat" and transform(plain,True)!=original:raise ValueError("Catalog round trip failed")
        edited=tooltip(plain)
        if tooltip(edited)!=edited:raise ValueError("Tooltip is not idempotent")
        outputs["client/"+rel]=transform(edited,True) if ext=="dat" else edited
    for side,path,expected in [("client",ROOT/"6711/ini/GameMap.dat",CLIENT_INDEX),("server",ROOT/"Source/Revora-6711/bin/Debug/database/GameMap.dat",SERVER_INDEX)]:
        outputs[side+("/ini/GameMap.dat" if side=="client" else "/database/GameMap.dat")]=add_scenes(add_map(read(path,expected)))
    read(ROOT/"6711/map/map/house03.7z",HOUSE_ARCHIVE);read(ROOT/"6711/map/map/maidan-D.7z",JAIL_ARCHIVE)
    for mid in (1782,1783,1784):outputs[f"server/database/maps/{mid}.map"]=read(WORK/f"defense-talisman-scenes/server/database/maps/{mid}.map",FLOOR)
    outputs["server/database/maps/1363.map"]=read(WORK/"gloomy-jail-stage/server/database/maps/1363.map",JAIL_FLOOR)
    manifest=dict(purpose="Private composed quest journal/tooltips, Gloomy Jail and Tower scene repairs",inputs=[dict(path=str(p),sha256=h) for p,h in inputs.items()],
                  outputs=[dict(path=p,sha256=sha(b),bytes=len(b)) for p,b in outputs.items()],missions=sorted(MISSIONS),installed=False,signed=False,published=False,
                  pending=["Native DX9/Host acceptance","Signing","Transactional installation with exact preimage backups"],
                  note="Includes prior quest journal/tooltips unchanged outside Tower sections and both pending map repairs. Proprietary contents stay outside Git.")
    outputs["manifest.json"]=(json.dumps(manifest,indent=2)+"\n").encode()
    destination=WORK/"defense-talisman-composed-v2"
    if destination.exists():
        actual={str(p.relative_to(destination)).replace("\\","/"):p.read_bytes() for p in destination.rglob("*") if p.is_file()}
        if actual!=outputs:raise ValueError("Existing private bundle differs")
    else:
        temporary=Path(tempfile.mkdtemp(prefix=".tower-compose-",dir=WORK))
        try:
            for rel,data in outputs.items():
                path=temporary/rel;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
                if path.read_bytes()!=data:raise ValueError("Readback failed")
            for path,expected in inputs.items():checked(path,expected)
            temporary.rename(destination)
        finally:
            if temporary.exists():
                if temporary.resolve().parent!=WORK.resolve() or not temporary.name.startswith(".tower-compose-"):raise ValueError("Unexpected cleanup path")
                shutil.rmtree(temporary)
    for path,expected in inputs.items():checked(path,expected)
    print(json.dumps(manifest["outputs"],indent=2))
    return manifest


if __name__=="__main__":prepare()
