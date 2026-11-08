"""Stage Connector directions over the Power Book bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = "65e897ecdd50bcbfa69bb0f72cf81c4f858f7dfaa4384f0a46876118fe87335c"
MISSIONS = set(range(1718, 1723))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
WONG = "900071718,1002,440,339,ThiefWong,TwinCity"
CHEN = "4437,1011,33,398,ThiefChen,PhoenixCastle"
STEPS = [
    "Accept Chang's errand, then show your badge to Thief Wong at 440,339 in Twin City.",
    "Find Thief Chen at 33,398 in Phoenix Castle. Hear his request before hunting.",
    "Defeat five Castle Bandits in Phoenix Castle, then return to Chen for the Connector Letter.",
    "Take your Connector Letter to Wong at 440,339. He will reward you with silver and experience.",
    "Return to Chang at 473,300 with your badge. Leave two free places for your rewards.",
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
            raise ValueError("Ambiguous Connector mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1718
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,WONG,CHEN,CHEN,WONG][step],
                  "FinishNpcId": [WONG,CHEN,CHEN,WONG,CHANG][step],
                  "Prize": ("100 Silver. " if step < 3 else "1000 Silver and ten minutes of experience. " if step == 3 else "")
                  + "Final reward: bound Refined Necklace Pack, bound EXP Ball and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Speak with Chang, Wong or Chen if you lose your badge or earned letter.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Connector field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Connector mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Connector section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "power-book-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Connector rewrite is not idempotent")
    manifest = dict(purpose="private-composed-connector-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the five Connector missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "connector-journal"
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
