"""Stage Desert Blood directions over the Tomboy bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha
from prepare_shop_buy_hotfix import transform

SOURCE = "6fcd7b896089388e4a0778e2a2ab1f444e6fc4bd325eb371b49c17c5f1606600"
MISSIONS = set(range(1800, 1808))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
HARVEY = "4621,1000,495,579,Harvey,DesertCity"
COLUMN = "4622,1000,386,103,StoneColumn,DesertCity"
PEACH = "4633,1015,765,602,AuntPeach,BirdIsland"
STEPS = [
    "Visit Harvey at 495,579 in Desert City.",
    "Hunt BladeGhostL83 near 165,276 for a Stone Spell. Read it and visit the column at 386,103. Speak to the Crypt warrior.",
    "Defeat 200 Blade Ghosts and bring the earned Blunt Sword to the Crypt warrior. Keep your Stone Spell for entry.",
    "Bring five earned BladeGhostL83 fangs to the Crypt warrior.",
    "Return to Harvey at 495,579 for his letter.",
    "Take Harvey's letter to the Crypt warrior for the Peach Bag.",
    "Bring the Peach Bag to Aunt Peach at 765,602 in Bird Island.",
    "Read Aunt Peach's Note and speak to her again. Return to Chang with three free places for the bound Elite Ring/Bracelet pack, EXP Ball (E) and P7 soul.",
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
            raise ValueError("Ambiguous Desert Blood mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1800
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,HARVEY,COLUMN,COLUMN,COLUMN,HARVEY,COLUMN,PEACH][step],
                  "FinishNpcId": [HARVEY,COLUMN,COLUMN,COLUMN,HARVEY,COLUMN,PEACH,CHANG][step],
                  "Prize": ("100 Silver. " if step < 6 else "6000 Silver and one EXP ball of experience. " if step == 7 else "")
                  + "Final reward: bound Elite level-90 Ring/level-85 Bracelet pack, one bound EXP Ball (E) and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Revora: Spell and fang drops have a 50% chance; the sword is earned at 200 kills. Ask about lost belongings; stored items cannot be copied. Lost fangs must be hunted again. The Crypt column grants one-twelfth EXP ball once per rebirth. Visits last 20 minutes; the column returns you to Desert City. Final rewards are once per rebirth.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Desert Blood field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Desert Blood mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Desert Blood section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def tooltip(before):
    old = b"Right~click~to~get~1~Elite~Ring~or~Bracelet~(around~level~90)~and~an~ExpBall(E)."
    new = b"Open~for~a~level~90~Elite~Ring~or~level~85~Taoist~Bracelet.~Your~quest~EXP~Ball(E)~is~awarded~separately."
    lines = before.splitlines(keepends=True)
    found = 0
    for index, line in enumerate(lines):
        if line.startswith(b"728242@@"):
            found += 1
            parts = line.split(b"@@")
            if parts[53] not in (old,new):
                raise ValueError("Desert Blood tooltip preimage changed")
            parts[53] = new
            lines[index] = b"@@".join(parts)
    if found != 1:
        raise ValueError("Missing or duplicate Desert Blood pack")
    return b"".join(lines)


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "tomboy-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Desert Blood rewrite is not idempotent")
    manifest = dict(purpose="private-composed-desert-blood-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the eight Desert Blood missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "desert-blood-journal"
    files = [(destination / "originals/ini/Questinfo.ini", installed),
             (destination / "staged/ini/Questinfo.ini", after),
             (destination / "manifest.json", (json.dumps(manifest, indent=2)+"\n").encode())]
    tip_inputs = [("ini/itemtype.txt","52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb","961bae394dc908663e080560aba5e20ba5f46417e6c1eeb156d8687b7686abd5",False),
                  ("ini/itemtype.dat","ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753","a7e2752ee6c7cf0b215423c4f090d077c68ce5674b087bf6680f2f32efa77712",True)]
    tip_manifest = []
    for relative, expected, source_hash, encrypted in tip_inputs:
        checked(ROOT / "6711" / relative, expected)
        original = checked(WORK / "tomboy-journal/staged" / relative, source_hash)
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
