"""Stage Frantic Monkeys directions over the Mystic Ring bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = "ff5096676959b02251eb8ab84870bfcd3e8bd48bf0942d8bd69c32d309f4561f"
MISSIONS = set(range(1745, 1751))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
BAO = "4485,1020,553,584,Bao,ApeCity"
DOCTOR = "4486,1002,440,343,DoctorLi,TwinCity"
STEPS = [
    "Speak with Bao at 553,584 outside Ape City.",
    "Hunt Macaques for a Decayed Heart, then show it to Bao.",
    "Take the Decayed Heart to Doctor Li at 440,343 in Twin City.",
    "Use Doctor Li's poison beside him. Wait one minute in the trial, then defeat five monkeys and return for your vial.",
    "Use the vial at 795,471 outside Ape City. Defeat two Venomous Apes and their leader. Bring the Devilish Sword to Bao.",
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
            raise ValueError("Ambiguous Frantic Monkeys mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1745
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,BAO,BAO,DOCTOR,DOCTOR,BAO][step],
                  "FinishNpcId": [BAO,BAO,DOCTOR,DOCTOR,BAO,CHANG][step],
                  "Prize": ("100 Silver. " if step < 4 else "3000 Silver and thirty minutes of experience. " if step == 4 else "")
                  + "Final reward: bound class weapon pack and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Ask Chang, Bao or Doctor Li about a lost quest item. Stored belongings cannot be copied. A failed vial hunt can be retried after its one-minute cooldown.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Frantic Monkeys field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Frantic Monkeys mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Frantic Monkeys section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "mystic-ring-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Frantic Monkeys rewrite is not idempotent")
    manifest = dict(purpose="private-composed-frantic-monkeys-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the six Frantic Monkeys missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "frantic-monkeys-journal"
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
