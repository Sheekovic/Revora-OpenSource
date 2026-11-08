"""Stage Soul of the General directions over the Frantic Monkeys bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = "c69b98b3f114b3cd6d011edd2eb9e771cf3980761c7c59517a7d652f08b8c208"
MISSIONS = set(range(1751, 1758))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
AMBER = "4487,1020,865,540,GeneralAmber,ApeCity"
ZHAO = "4488,1002,368,251,GeneralZhao,TwinCity"
GUARD = "4490,1002,372,251,GuardLi,TwinCity"
STEPS = [
    "Speak with General Amber at 865,540 outside Ape City.",
    "Ask General Zhao at 368,251 in Twin City for his hair. Bring it to Amber.",
    "Carry Amber's Silk Pack. Defeat one Snakeman at 219,505 in Ape City, ten RockMonsterL42 at 923,563 in Phoenix Castle and ten GiantApeL53 at 620,367 in Ape City. Return to Amber.",
    "Ask Painter Feng Kang at 556,571 in Ape City for a portrait, then bring it to Amber.",
    "Visit General Zhao at 368,251, then Guard Li at 372,251 in Twin City.",
    "Confront Amber at 865,540. Defeat your Jade Phantom and bring its Paper Figure to General Zhao. Leave one free place for the figure.",
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
            raise ValueError("Ambiguous Soul of the General mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1751
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,AMBER,AMBER,AMBER,AMBER,GUARD,ZHAO][step],
                  "FinishNpcId": [AMBER,AMBER,AMBER,AMBER,GUARD,ZHAO,CHANG][step],
                  "Prize": ("100 Silver. " if step < 5 else "4000 Silver and forty minutes of experience. " if step == 5 else "")
                  + "Final reward: bound level-60 Elite boots pack and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Ask the quest NPCs about lost belongings. Stored items cannot be copied. Each qualifying kill adds one scent. The phantom hunt lasts five minutes, with a one-minute reuse cooldown.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Soul of the General field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Soul of the General mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Soul of the General section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "frantic-monkeys-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Soul of the General rewrite is not idempotent")
    manifest = dict(purpose="private-composed-soul-general-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the seven Soul of the General missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "soul-general-journal"
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
