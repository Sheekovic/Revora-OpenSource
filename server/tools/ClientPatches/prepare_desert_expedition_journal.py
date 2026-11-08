"""Stage Desert Expedition directions over the Desert Treasure bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha
from prepare_shop_buy_hotfix import transform

SOURCE = "ecffa24751eaaa6b13e6f756c1738e7dc7e9880ea474be0983b3942dbcc7e8fc"
MISSIONS = set(range(1835, 1840))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
MOMO = "4702,1000,520,589,MasterMoMo,DesertCity"
STEPS = [
    "Visit Master Mo Mo at 520,589 in Desert City for the Fervent Pill.",
    "Use the pill in Mystic Castle and hunt Bull Monsters near 173,329 for ten Burnt Bullhorns. The fever lasts one minute and takes ten percent of current HP every ten seconds. After the fever is paid, bring the horns and pill to Mo Mo.",
    "Use the Ghost Mirror at 172,399, 102,292 and 109,369. Lure your Mad Bull into each five-minute trap and kill it within one cell of the trap center. Bring three Bull Ghosts and the mirror to Mo Mo.",
    "Approach 186,359 in Mystic Castle to face your Bull Master. Defeat it and bring its heart to Mo Mo.",
    "Visit Taskmaster Chang with three free bag slots for the bound Super Ring Pack, EXP Ball (E) and P7 soul. Taoists receive a level115 Super bracelet from the pack; other supported classes receive a level110 Super ring.",
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
            raise ValueError("Ambiguous Desert Expedition mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1835
        changed = original
        values = {"Lv_min": "105", "Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,MOMO,MOMO,MOMO,MOMO][step],
                  "FinishNpcId": [MOMO,MOMO,MOMO,MOMO,CHANG][step],
                  "Prize": ("100 Silver. " if 0 <= step <= 2 else "6000 Silver and one EXP ball of experience. " if step == 3 else "")
                  + "Final reward: bound class-aware Super Ring Pack, one bound EXP Ball (E) and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Revora: horns have a 50% chance per qualifying kill. The fever leaves at least one HP; reconnecting cannot erase unpaid ticks. Each trap site grants one bound ghost per rebirth. Spirit stones mark the sites. Keep a free bag slot for earned materials. Mo Mo replaces truly lost quest items; stored items cannot be copied. Rewards are once per rebirth.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Desert Expedition field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Desert Expedition mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Desert Expedition section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def tooltip(before):
    replacements = {721920: (b'Use~it~and~kill~Bull~Monsters~in~5~minutes.', b'Use~in~Mystic~Castle.~Lasts~one~minute;~costs~10%~of~current~HP~each~ten~seconds.~Hunt~Bull~Monsters~for~ten~Burnt~Bullhorns.'), 721921: (b'Right~click~to~use~it~in~Heaven~Well~and~herd~Mad~Bulls~into~it~and~kill~it.', b'Use~at~172,399;~102,292;~109,369~in~Mystic~Castle.~Lure~your~Mad~Bull~into~each~five-minute~trap,~then~kill~it.'), 728250: (b'Open~it~to~receive~a~Level~110~Super~ring~and~an~ExpBall(E).', b'Open~for~a~level110~Super~ring~or~level115~Super~Taoist~bracelet.~Quest~EXP~Ball(E)~is~awarded~separately.')}
    lines = before.splitlines(keepends=True)
    seen = set()
    for index,line in enumerate(lines):
        parts = line.split(b"@@")
        if not parts[0].isdigit() or int(parts[0]) not in replacements:
            continue
        identity = int(parts[0])
        old,new = replacements[identity]
        if identity in seen or parts[53] not in (old,new):
            raise ValueError("Desert Expedition tooltip preimage changed")
        seen.add(identity)
        parts[53] = new
        lines[index] = b"@@".join(parts)
    if seen != set(replacements):
        raise ValueError("Missing Desert Expedition tooltip")
    return b"".join(lines)


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "desert-treasure-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Desert Expedition rewrite is not idempotent")
    manifest = dict(purpose="private-composed-desert-expedition-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the five Desert Expedition missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "desert-expedition-journal"
    files = [(destination / "originals/ini/Questinfo.ini", installed),
             (destination / "staged/ini/Questinfo.ini", after),
             (destination / "manifest.json", (json.dumps(manifest, indent=2)+"\n").encode())]
    tip_inputs = [("ini/itemtype.txt","52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb","59c431acd932266d53f8a318768b6339be01849e573b145f211a0e21040bee51",False),
                  ("ini/itemtype.dat","ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753","3c3d3a5f597990a3121121ad63ea15f552aa11165a5dfa2f1624f5ddb02d0c3d",True)]
    tip_manifest = []
    for relative, expected, source_hash, encrypted in tip_inputs:
        checked(ROOT / "6711" / relative, expected)
        original = checked(WORK / "desert-treasure-journal/staged" / relative, source_hash)
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
