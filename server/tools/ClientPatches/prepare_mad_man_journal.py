"""Stage The Mad Man directions over the Soul of the General bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = "c89ba95366e9a78c14f6cf98c0364cbe5a8e83078c242b40b13603e67e600105"
MISSIONS = set(range(1781, 1787))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
DU = "4468,1020,552,601,DuSan,ApeCity"
JUDD = "1611,1002,345,251,GeneralJudd,TwinCity"
DONG = "4470,1002,431,331,PharmacistDong,TwinCity"
STEPS = [
    "Visit Du San at 552,601 in Ape City.",
    "Speak with General Judd at 345,251 in Twin City.",
    "Hunt Snakemen west of Ape City near 255,504 for a Venomous Fang. Show it to Judd.",
    "Take the fang to Pharmacist Dong at 431,331 in Twin City. Leave two free places.",
    "Use Blue Toxin, then Green, beside Dong. Touch all seven stones in your private mountain trial within five minutes, then use one powder to combine them. Bring the Rainbow Powder to Du San.",
    "Return to Chang at 473,300 in Twin City. Leave three free places for your rewards.",
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
            raise ValueError("Ambiguous The Mad Man mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1781
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,DU,JUDD,JUDD,DONG,DU][step],
                  "FinishNpcId": [DU,JUDD,JUDD,DONG,DU,CHANG][step],
                  "Prize": ("100 Silver. " if step < 4 else "5000 Silver and fifty minutes of experience. " if step == 4 else "")
                  + "Final reward: bound Elite headgear pack, one bound EXP Ball (E) and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Ask quest NPCs about lost belongings; stored items cannot be copied. Other mixtures have a twenty-second window. Afterward, hunt a fresh fang for Dong to replace spent toxins. Failed mountain trips return you safely; clear your bag and ask about recovery.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate The Mad Man field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing The Mad Man mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten The Mad Man section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "soul-general-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("The Mad Man rewrite is not idempotent")
    manifest = dict(purpose="private-composed-mad-man-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the six The Mad Man missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "mad-man-journal"
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
