"""Stage Mystic Ring directions over the Artisan Feather bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = "7526375244fd08edd06d1bece91049f9d52d31c5a4135bf58587a6dcdda7ddaf"
MISSIONS = set(range(1738, 1745))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
MINSTREL = "4500,1011,184,282,Minstrel,PhoenixCastle"
SHEN = "4501,1011,779,477,DealerShen,PhoenixCastle"
JUNIOR = "900071742,1011,920,552,ShenJunior,PhoenixCastle"
STEPS = [
    "Accept Chang's errand, then visit the Minstrel at 184,282 in Phoenix Castle.",
    "Visit Dealer Shen at 779,477 outside Phoenix Castle.",
    "Bring Dealer Shen twelve Stanchers and twelve Agrypnotics.",
    "Defeat twenty Ratlings or FireRatL38. Dealer Shen knows your saved count. Return to him for the quest Castle Gate.",
    "Take the quest Castle Gate to Shen Junior at 920,552, then return his Antique Ring to Dealer Shen.",
    "Bring the Antique Ring to the Minstrel at 184,282, then use the Mystic Ring beside him. Wait for another disguise to end first.",
    "Return to Chang at 473,300 in Twin City. Leave two free places for your rewards.",
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
            raise ValueError("Ambiguous Mystic Ring mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1738
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,MINSTREL,SHEN,SHEN,SHEN,SHEN,MINSTREL][step],
                  "FinishNpcId": [MINSTREL,SHEN,SHEN,SHEN,SHEN,MINSTREL,CHANG][step],
                  "Prize": ("100 Silver. " if step < 5 else "1500 Silver and twenty minutes of experience. " if step == 5 else "")
                  + "Final reward: bound class headgear pack and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Shen Junior waits at 920,552 outside Phoenix Castle. Speak with Chang, Minstrel or either quest Shen to replace a lost quest scroll or ring. Stored belongings cannot be copied; ordinary Castle Gates do not count.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Mystic Ring field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Mystic Ring mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Mystic Ring section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "artisan-feather-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Mystic Ring rewrite is not idempotent")
    manifest = dict(purpose="private-composed-mystic-ring-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the seven Mystic Ring missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "mystic-ring-journal"
    files = [(destination / "originals/ini/Questinfo.ini", installed),
             (destination / "staged/ini/Questinfo.ini", after),
             (destination / "manifest.json", (json.dumps(manifest, indent=2)+"\n").encode())]
    for path, data in files:
        if path.exists() and path.read_bytes() != data:
            raise ValueError("Existing private artifact differs: " + str(path))
    for path, data in files:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    checked(installed_path, INSTALLED)
    checked(source, SOURCE)
    checked(executable, DX9)
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    prepare()
