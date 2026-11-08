"""Stage Artisan Feather directions over the Spun Gold bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = "ea389442bc611fa747d68084dac807716d4aa23c60182c0f9d1458e8a1ad9e4f"
MISSIONS = set(range(1733, 1738))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
APPRENTICE = "4466,1002,459,331,ApprenticeLuo,TwinCity"
ARTISAN = "4467,1002,342,640,ArtisanLuo,TwinCity"
STEPS = [
    "Accept Chang's errand, then speak to Apprentice Luo at 459,331 in Twin City.",
    "Find Artisan Luo at 342,640 in Wind Plain.",
    "Collect 100 feather credits from Pheasants, Turtledoves or Robins in Wind Plain. Artisan Luo knows your saved count.",
    "Hunt Vultures near 713,678 or 866,552 in Ape City for a Vulture Feather. Return it to Artisan Luo.",
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
            raise ValueError("Ambiguous Artisan Feather mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1733
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,APPRENTICE,ARTISAN,ARTISAN,ARTISAN][step],
                  "FinishNpcId": [APPRENTICE,ARTISAN,ARTISAN,ARTISAN,CHANG][step],
                  "Prize": ("100 Silver. " if step < 3 else "1500 Silver and twenty minutes of experience. " if step == 3 else "")
                  + "Final reward: bound class weapon pack and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Speak with Chang or either Luo if you lose your Vulture Feather. The 100-feather hunt stays saved; a lost Vulture Feather must be hunted again. Stored items cannot be copied.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Artisan Feather field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Artisan Feather mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Artisan Feather section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "spun-gold-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Artisan Feather rewrite is not idempotent")
    manifest = dict(purpose="private-composed-artisan-feather-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the five Artisan Feather missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "artisan-feather-journal"
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
