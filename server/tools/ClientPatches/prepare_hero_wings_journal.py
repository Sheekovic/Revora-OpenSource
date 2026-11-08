"""Stage Hero Wings directions over the Desert Blood bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha
from prepare_shop_buy_hotfix import transform

SOURCE = "669faa70c47812a82884df64ff22f2aa166f1e2b1fd6aa0bdf680a0238419503"
MISSIONS = set(range(1808, 1819))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
XIAO = "4700,1015,725,541,PoorXiao,BirdIsland"
DOCTOR = "4701,1015,701,595,DoctorKnowitAll,BirdIsland"
STEPS = [
    "Visit Poor Xiao at 725,541 in Bird Island.",
    "Visit Doctor Know-it-All at 701,595. Approach from the south.",
    "Tell Poor Xiao the doctor needs a Meteor.",
    "Bring one usable Meteor to Doctor Know-it-All.",
    "Hunt Birdman or BirdmanL88 near 800,675 for ten earned Bird Blood samples. Bring them to the doctor.",
    "Read your Ginseng Map for your saved location. Hunt HawKing near 451,676 for a Gallbladder. Use it within two cells of the marked place, then take the Bitter Ginseng to the doctor.",
    "Hunt HawkL93 near 451,676. Two distinct kills within 60 seconds earn one claw. Bring five claws to the doctor.",
    "Bring the Peel Pill to Poor Xiao.",
    "Tell Doctor Know-it-All what happened to Xiao.",
    "Tell Poor Xiao what the doctor discovered.",
    "Return to Chang in Twin City with three free places for the bound Super Headgear Pack, EXP Ball (E), and P7 soul.",
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
            raise ValueError("Ambiguous Hero Wings mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1808
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,XIAO,DOCTOR,XIAO,DOCTOR,DOCTOR,DOCTOR,DOCTOR,XIAO,DOCTOR,XIAO][step],
                  "FinishNpcId": [XIAO,DOCTOR,XIAO,DOCTOR,DOCTOR,DOCTOR,DOCTOR,XIAO,DOCTOR,XIAO,CHANG][step],
                  "Prize": ("100 Silver. " if 1 <= step <= 8 else "6000 Silver and one EXP ball of experience. " if step == 9 else "")
                  + "Final reward: bound class-specific Super Headgear Pack, one bound EXP Ball (E) and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Revora: Blood and Gallbladder drops have a 50% chance. Your Ginseng site is fixed for this life. Hawk pairs do not overlap, and the 60-second timer continues while offline. Lost raw ingredients must be hunted again; ask about lost prepared belongings. Stored items cannot be copied. Final rewards are once per rebirth.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Hero Wings field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Hero Wings mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Hero Wings section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def tooltip(before):
    old = b"Open~it~to~receive~a~Super~headgear~and~an~ExpBall(E)."
    new = b"Open~for~class-specific~Super~headgear:~level~92~Archer~hat~or~level~97~for~other~classes.~Your~quest~EXP~Ball(E)~is~awarded~separately."
    lines = before.splitlines(keepends=True)
    found = 0
    for index, line in enumerate(lines):
        if line.startswith(b"728247@@"):
            found += 1
            parts = line.split(b"@@")
            if parts[53] not in (old,new):
                raise ValueError("Hero Wings tooltip preimage changed")
            parts[53] = new
            lines[index] = b"@@".join(parts)
    if found != 1:
        raise ValueError("Missing or duplicate Hero Wings pack")
    return b"".join(lines)


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "desert-blood-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Hero Wings rewrite is not idempotent")
    manifest = dict(purpose="private-composed-hero-wings-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the eleven Hero Wings missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "hero-wings-journal"
    files = [(destination / "originals/ini/Questinfo.ini", installed),
             (destination / "staged/ini/Questinfo.ini", after),
             (destination / "manifest.json", (json.dumps(manifest, indent=2)+"\n").encode())]
    tip_inputs = [("ini/itemtype.txt","52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb","7b9b491978dd86c35f51cc5ed6a11fda4d88cdf87594d5cd924282f070e64629",False),
                  ("ini/itemtype.dat","ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753","3eb56222a5fc0a7cb7bfe624ebb8d151ad3537a8ec62c589a1e469a8564d1631",True)]
    tip_manifest = []
    for relative, expected, source_hash, encrypted in tip_inputs:
        checked(ROOT / "6711" / relative, expected)
        original = checked(WORK / "desert-blood-journal/staged" / relative, source_hash)
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
