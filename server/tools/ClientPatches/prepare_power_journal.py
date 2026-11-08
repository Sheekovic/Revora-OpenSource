"""Stage Power Book directions over the Azure bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = "cd9a8666d2bb7d6bd862d7d1f85147856e6407d5f552b2d99f85a91e4c11733d"
MISSIONS = set(range(1840, 1853))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
BEGGAR = "900071841,1002,466,331,OldBeggar,TwinCity"
STEPS = [
    "Take Chang's badge and one Meteor to the Old Beggar at 466,331. Buy the Power Book; return visits are free.",
    "Ask the Old Beggar to enter your private trial. Defeat 20 Slingers.",
    "Lure 10 Gold Ghosts within three paces of 77,23, 102,136 or 146,104, then defeat them.",
    "Defeat 15 Slingers and 15 Gold Ghosts. Read the book at 140,140 to continue.",
    "Defeat two Bladelings within ten seconds.",
    "Hunt Agile Rats for a fang, then use it. A broken fang means another hunt.",
    "Defeat 10 Bladelings and 10 Agile Rats. Read the book at 140,140 to continue.",
    "Defeat two Blue Birds within twenty seconds.",
    "Hunt Fiend Bats for a wing. Use it at 100,66 to roast it, then eat it. Burnt wings must be replaced.",
    "Defeat five Blue Birds and five Fiend Bats. Keep a Blue Bird plume. Read the book at 140,140 to continue.",
    "Defeat 10 Minotaurs. Use the plume within three paces and three seconds of each death. Missing a plume? Re-enter through the Beggar to hunt Blue Birds.",
    "Read the book at 77,27 to leave. Speak to the Old Beggar at 466,331 for silver and experience.",
    "Return to Taskmaster Chang at 473,300 with your badge and book to claim your rewards.",
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
            raise ValueError("Ambiguous Power Book mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1840
        changed = original
        if mission == 1851:
            changed = changed.replace(b"Intention1Talk to [npc OldBeggar,4704,1002,466,331].", b"Intention1=Talk to [npc OldBeggar,4704,1002,466,331].")
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": CHANG if step == 0 else BEGGAR,
                  "FinishNpcId": CHANG if step == 12 else BEGGAR,
                  "Prize": ("100 Silver. " if step < 11 else "6000 Silver and one EXP Ball of experience. " if step == 11 else "")
                  + "Final reward: bound L120 Super class armor pack and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Read your book for directions. Each visit lasts thirty minutes. Read it at 77,27 to leave.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Power Book field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Power Book mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Power Book section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "azure-pill-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Power Book rewrite is not idempotent")
    manifest = dict(purpose="private-composed-power-book-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the thirteen Power Book missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "power-book-journal"
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
