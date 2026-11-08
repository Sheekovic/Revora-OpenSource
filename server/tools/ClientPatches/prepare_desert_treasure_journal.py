"""Stage Desert Treasure directions over the Family Feud bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha
from prepare_shop_buy_hotfix import transform

SOURCE = "ee7e50b04038c2959f8dc1047fe3f2551c3a6862224ab0dd9cc47d5e6035d1e2"
MISSIONS = set(range(1828, 1835))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
LOUIS = "4651,1000,466,658,Louis,DesertCity"
SHARK = "4652,1001,310,631,Shark,MysticCastle"
STEPS = [
    "Visit Louis at 466,658 in Desert City for the Reveal Vial.",
    "Hunt Tomb Bats near 333,612 for a Box Key. Use the Reveal Vial near 91,283, walk or jump onto 91,283, then click ScriptureVault at 94,283 and turn the key. Open the box and bring the Scripture to Louis.",
    "Visit Shark at 310,631. Bid 30,000 silver (30%), 60,000 (60%) or 99,999 (certain). Refused offers cost nothing; wait five seconds before retrying.",
    "Examine the blank portrait, then ask Shark about the treasures.",
    "Hunt Bloody Bats, Bloody Bats L108 or Vampire Bats for five Bat Essences. Near Louis, use the portrait to reveal it, then give it to him within one minute. If it fades, gather five more essences to reveal it again.",
    "With the Pure Bottle, approach 728,676 in Mystic Castle. Defeat your Jinx Tomb Bat and use the bottle within three cells of its corpse within three seconds. Bring the Buddha Relic to Louis.",
    "Visit Chang with three free bag slots for the bound L100 Super Boots Pack, EXP Ball (E) and P7 soul. Louis also offers an optional memory quiz for half an EXP ball of experience, once per rebirth.",
]


def rewrite(before):
    edits, seen = [], set()
    for match in re.finditer(rb"^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)", before, re.M):
        original = match.group()
        ids = re.findall(rb"^MissionId=(\d+)\r?$", original, re.M)
        selected = [int(i) for i in ids if int(i) in MISSIONS]
        if not selected:
            continue
        if len(ids) != 1 or selected[0] in seen:
            raise ValueError("Ambiguous Desert Treasure mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1828
        changed = original
        values = {"Lv_min": "98", "Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,LOUIS,LOUIS,SHARK,SHARK,LOUIS,LOUIS][step],
                  "FinishNpcId": [LOUIS,LOUIS,SHARK,SHARK,LOUIS,LOUIS,CHANG][step],
                  "Prize": ("100 Silver. " if 0 <= step <= 4 else "6000 Silver and one EXP ball of experience. " if step == 5 else "")
                  + "Final reward: bound L100 Super Boots Pack, one bound EXP Ball (E) and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Revora: keys, essences and compartment attempts have a 50% success chance. Failed compartment attempts consume a key; use the vial again to retry. The revealed vault lasts 60 seconds. Louis or Shark can replace truly lost quest items; stored items cannot be copied. The clickable vault stone and quiz questions are Revora adaptations. Rewards are once per rebirth.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Desert Treasure field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Desert Treasure mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Desert Treasure section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def tooltip(before):
    replacements = {728249: (b'Open~it~to~receive~a~pair~of~Level~100~Super~boots~and~an~ExpBall(E).', b'Open~for~level~100~Super~KylinBoots.~The~quest~EXP~Ball(E)~is~awarded~separately.'), 721873: (b'Right~click~it~in~Scripture~Vault~in~Ancient~City~(91,283)~to~reveal~the~compartment.', b'Use~near~91,283~in~Mystic~Castle.~Walk~or~jump~onto~91,283,~then~click~ScriptureVault~at~94,283~within~60~seconds.'), 721874: (b'Right~click~it~to~get~the~Buddha~Relic~from~the~dead~body~of~the~Jinx~Tomb~Bat.', b'Defeat~your~JinxTombBat~near~728,676.~Use~within~three~cells~of~its~corpse~within~three~seconds.'), 721871: (b'Louis~is~looking~for~it.~Right~click~to~use~it.', b'Use~five~earned~BatEssences~to~reveal~an~expired~portrait.~Give~the~revealed~portrait~to~Louis~within~one~minute.'), 721878: (b'Right~click~to~use~it.', b'Examine~it,~then~ask~Shark~about~the~treasures.~Use~five~earned~BatEssences~to~reveal~it~near~Louis.')}
    lines = before.splitlines(keepends=True)
    seen = set()
    for index,line in enumerate(lines):
        parts = line.split(b"@@")
        if not parts[0].isdigit() or int(parts[0]) not in replacements:
            continue
        identity = int(parts[0])
        old,new = replacements[identity]
        if identity in seen or parts[53] not in (old,new):
            raise ValueError("Desert Treasure tooltip preimage changed")
        seen.add(identity)
        parts[53] = new
        lines[index] = b"@@".join(parts)
    if seen != set(replacements):
        raise ValueError("Missing Desert Treasure tooltip")
    return b"".join(lines)


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "family-feud-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Desert Treasure rewrite is not idempotent")
    manifest = dict(purpose="private-composed-desert-treasure-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the seven Desert Treasure missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "desert-treasure-journal"
    files = [(destination / "originals/ini/Questinfo.ini", installed),
             (destination / "staged/ini/Questinfo.ini", after),
             (destination / "manifest.json", (json.dumps(manifest, indent=2)+"\n").encode())]
    tip_inputs = [("ini/itemtype.txt","52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb","b0893b6d5121bb9d7753b73cf165d2de26dc94c5e0025f660d92967b94334c00",False),
                  ("ini/itemtype.dat","ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753","bc8093973a603800d86b2f3acd6e475afc75adaf7d827b4550e4eb367ede230b",True)]
    tip_manifest = []
    for relative, expected, source_hash, encrypted in tip_inputs:
        checked(ROOT / "6711" / relative, expected)
        original = checked(WORK / "family-feud-journal/staged" / relative, source_hash)
        plain = transform(original) if encrypted else original
        if encrypted and transform(plain, True) != original:
            raise ValueError("Item catalog transform failed round trip")
        edited = tooltip(plain)
        if tooltip(edited) != edited:
            raise ValueError("Tooltip rewrite is not idempotent")
        output = transform(edited, True) if encrypted else edited
        files.append((destination / "staged" / relative, output))
        tip_manifest.append(dict(relative=relative,before=expected,source=source_hash,after=sha(output)))
    files.append((destination / "tooltip-manifest.json", (json.dumps(dict(files=tip_manifest,installed=False,signed=False,published=False),indent=2)+"\n").encode()))
    for path, data in files:
        if path.exists() and path.read_bytes() != data:
            raise ValueError("Existing private artifact differs: " + str(path))
    for path, data in files:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    checked(installed_path, INSTALLED)
    checked(source, SOURCE)
    checked(executable, DX9)
    for relative, expected, source_hash, _ in tip_inputs:
        checked(ROOT / "6711" / relative, expected)
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    prepare()
