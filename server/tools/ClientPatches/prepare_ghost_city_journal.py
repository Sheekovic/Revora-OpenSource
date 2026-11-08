"""Stage Ghost City directions over the Mad Man bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = "26a73fb04a1d2770b31cba6ea6c3094283ebaf0140c77b646a8a167ce26fb37e"
MISSIONS = set(range(1787, 1795))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
AUNT = "4503,1002,454,540,AuntZhang,TwinCity"
WANG = "4504,1002,468,309,JerkWang,TwinCity"
SHINE = "4505,1002,440,334,TaoistShine,TwinCity"
MOMO = "4526,1780,145,134,MoMo,GhostCity"
STEPS = [
    "Visit Aunt Zhang at 454,540 in Twin City; approach from the north.",
    "Speak with Jerk Wang at 468,309 in Twin City.",
    "Use MagicFirework at 440,308, then 443,344, then 465,356. Report to Wang.",
    "Ask Taoist Shine at 440,334 for a Charm.",
    "Use Charm at a firework spot. Speak with MoMo, inspect the pillar, then bring Powder to Shine. Each visit lasts twenty minutes.",
    "Use Charm at a firework spot with inspected Powder. Slay 100 KidCatchers or KidRippers combined, then KidHunter and KidSiezer. Your Powder becomes a bound Bezoar. Optional children each give one-sixth EXP ball once per rebirth. Feed MoMo last to return home.",
    "Tell Aunt Zhang at 454,540 that her child is safe.",
    "Return to Chang at 473,300. Leave three free places for the bound armor pack, EXP Ball (E) and P7 soul.",
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
            raise ValueError("Ambiguous Ghost City mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1787
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,AUNT,WANG,WANG,SHINE,SHINE,MOMO,AUNT][step],
                  "FinishNpcId": [AUNT,WANG,WANG,SHINE,SHINE,MOMO,AUNT,CHANG][step],
                  "Prize": ("100 Silver. " if step < 6 else "6000 Silver and one EXP ball of experience. " if step == 6 else "")
                  + "Final reward: bound level-87 Elite armor pack, one bound EXP Ball (E) and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Ask quest NPCs to replace lost belongings; stored items cannot be copied. Interrupted visits return safely and retain combat progress. Rewards are once per rebirth.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Ghost City field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Ghost City mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Ghost City section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "mad-man-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Ghost City rewrite is not idempotent")
    manifest = dict(purpose="private-composed-ghost-city-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the eight Ghost City missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "ghost-city-journal"
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
