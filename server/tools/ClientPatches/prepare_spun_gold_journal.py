"""Stage Spun Gold directions over the Connector bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = "a4aaa9ac6da9453170fca6de42322e482ec7cc6f44d8ee584b310cc67b25b90e"
MISSIONS = set(range(1723, 1727))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
HONG = "4464,1011,194,260,VeteranHong,PhoenixCastle"
STEPS = [
    "Accept Chang's request, then speak to Veteran Hong at 194,260 in Phoenix Castle.",
    "Hunt Bandits in Phoenix Castle for an Invitation. Exchange it with Hong for an Offering.",
    "Ask Zhao Jian at 200,325 for a five-minute disguise. Use the Offering near 100,430, defeat your summoned Cloud, open his Wooden Box and return the armor to Hong.",
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
            raise ValueError("Ambiguous Spun Gold mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1723
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,HONG,HONG,HONG][step],
                  "FinishNpcId": [HONG,HONG,HONG,CHANG][step],
                  "Prize": ("100 Silver. " if step < 2 else "1000 Silver and ten minutes of experience. " if step == 2 else "")
                  + "Final reward: bound class armor pack and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Speak with Chang, Hong or Zhao if you lose quest belongings. Bandit invitations must be earned again; stored items cannot be copied.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Spun Gold field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Spun Gold mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Spun Gold section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "connector-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Spun Gold rewrite is not idempotent")
    manifest = dict(purpose="private-composed-spun-gold-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the four Spun Gold missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "spun-gold-journal"
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
