"""Stage Tomboy Marriage directions over the Ghost City bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha
from prepare_shop_buy_hotfix import transform

SOURCE = "c9d5f361b9edfa1946a8b7b3014d894b32679375eebded85da01d1235b77c3d5"
MISSIONS = set(range(1795, 1800))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
LYNN = "4718,1000,461,661,FarmerLynn,DesertCity"
CAROLYN = "4719,1000,460,667,Carolyn,DesertCity"
STEPS = [
    "Visit Farmer Lynn at 461,661 in Desert City.",
    "Hunt Rock Monsters near 356,358 in Desert City for ten earned Marbles. Bring them to Lynn.",
    "Hunt RockMonsterL78 near 265,264 for Leaf, Maple and Ocean Stones. Click Leaf with Maple for Rune Jade, Maple with Ocean for Moon Jade, and Ocean with Leaf for Sun Jade. Bring all three jades to Lynn.",
    "Speak with Carolyn at 460,667. Remember which stone you clicked with Maple to make Rune Jade.",
    "Return to Chang at 473,300 in Twin City. Leave three free places for the bound level-82 Elite Necklace/Bag pack, EXP Ball (E), and P7 soul.",
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
            raise ValueError("Ambiguous Tomboy Marriage mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1795
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,LYNN,LYNN,LYNN,CAROLYN][step],
                  "FinishNpcId": [LYNN,LYNN,LYNN,CAROLYN,CHANG][step],
                  "Prize": ("100 Silver. " if step < 3 else "3000 Silver and half an EXP ball of experience. " if step == 3 else "")
                  + "Final reward: bound level-82 Elite Necklace/Bag pack, one bound EXP Ball (E) and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Revora: drops and fusion each have a 50% chance. Failed fusion consumes both stones. Ask Lynn about lost materials; banked items cannot be copied. Lost raw materials must be hunted again, but earned jade can be restored. Rewards are once per rebirth.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Tomboy Marriage field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Tomboy Marriage mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Tomboy Marriage section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def tooltip(before):
    old = b"Open~it~to~receive~an~Elite~bracelet~or~necklace~(around~Level~82)~and~an~ExpBall(E)."
    new = b"Open~for~a~level~82~Elite~Necklace~or~Taoist~Bag.~Your~quest~EXP~Ball(E)~is~awarded~separately."
    lines = before.splitlines(keepends=True)
    found = 0
    for index, line in enumerate(lines):
        if line.startswith(b"728241@@"):
            found += 1
            parts = line.split(b"@@")
            if parts[53] not in (old,new):
                raise ValueError("Tomboy tooltip preimage changed")
            parts[53] = new
            lines[index] = b"@@".join(parts)
    if found != 1:
        raise ValueError("Missing or duplicate Tomboy pack")
    return b"".join(lines)


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "ghost-city-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Tomboy Marriage rewrite is not idempotent")
    manifest = dict(purpose="private-composed-tomboy-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the five Tomboy Marriage missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "tomboy-journal"
    files = [(destination / "originals/ini/Questinfo.ini", installed),
             (destination / "staged/ini/Questinfo.ini", after),
             (destination / "manifest.json", (json.dumps(manifest, indent=2)+"\n").encode())]
    tip_inputs = [("ini/itemtype.txt","52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb",False),
                  ("ini/itemtype.dat","ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753",True)]
    tip_manifest = []
    for relative, expected, encrypted in tip_inputs:
        original = checked(ROOT / "6711" / relative, expected)
        plain = transform(original) if encrypted else original
        if encrypted and transform(plain, True) != original:
            raise ValueError("Item catalog transform failed round trip")
        edited = tooltip(plain)
        if tooltip(edited) != edited:
            raise ValueError("Tooltip rewrite is not idempotent")
        output = transform(edited, True) if encrypted else edited
        files.append((destination / "staged" / relative, output))
        tip_manifest.append(dict(relative=relative,before=expected,after=sha(output)))
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
    for relative, expected, _ in tip_inputs:
        checked(ROOT / "6711" / relative, expected)
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    prepare()
