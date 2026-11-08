"""Stage Family Feud directions over the Hero Wings bundle; never install or publish."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha
from prepare_shop_buy_hotfix import transform

SOURCE = "6be1c0c45d4e6c5c76f24b541d9cc4d3e6793fb28e15c0f65ba80c2b1893d536"
MISSIONS = set(range(1819, 1828))
CHANG = "900071727,1002,473,300,TaskmasterChang,TwinCity"
FELIX = "4705,1015,686,547,Felix,BirdIsland"
GUIDE = "4706,1015,669,780,GuideZhang,BirdIsland"
KERRY = "4707,1801,56,55,CommanderKerry,BanditChamber"
STEPS = [
    "Visit Felix at 686,547 in Bird Island.",
    "Defeat 50 Bandits L97 near 362,469. Defeat your summoned leader nearby and bring Felix the Red Token.",
    "Hunt Bandits L98 near 362,469 for an Indigo Token, then return to Felix.",
    "Defeat ten Banditti near 525,345. Defeat your summoned courier there and bring Felix the Black Token.",
    "Defeat ten of your Bandit Deliverers near 525,345, then bring the Pitch Token to Felix.",
    "Hunt your Senior Bandits near 570,416 for the Scarlet Token, then return to Felix.",
    "Take the Golden Token to Guide Zhang at 669,780 within 90 seconds. In the chamber, speak to Kerry to begin. Jump to blue 50,60; green 54,64; red 58,64; yellow 60,60; black 58,60 within ten seconds. Return to Kerry. If your disguise expires, ask Felix to renew it.",
    "Tell Felix what Kerry revealed.",
    "Return to Chang with three free bag slots for the bound L97 Super Necklace Pack, EXP Ball (E) and P7 soul.",
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
            raise ValueError("Ambiguous Family Feud mission")
        mission = selected[0]
        seen.add(mission)
        step = mission - 1819
        changed = original
        values = {"Lv_max": "255", "Map": "0", "BeginNpcId": [CHANG,FELIX,FELIX,FELIX,FELIX,FELIX,FELIX,GUIDE,FELIX][step],
                  "FinishNpcId": [FELIX,FELIX,FELIX,FELIX,FELIX,FELIX,KERRY,FELIX,CHANG][step],
                  "Prize": ("100 Silver. " if 0 <= step <= 6 else "6000 Silver and one EXP ball of experience. " if step == 7 else "")
                  + "Final reward: bound L97 Super Necklace Pack, one bound EXP Ball (E) and one bound P7 soul. Once per rebirth.",
                  "IntentionDesp": STEPS[step], "Content": STEPS[step]
                  + " Revora: Indigo and Scarlet have a 50% drop chance. Quest encounters belong to their summoner. The chamber is a Revora adaptation. Ask Felix about lost tokens; stored tokens cannot be copied. Rewards are once per rebirth.",
                  "IntentAmount": "1", "Intention1": STEPS[step]}
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Family Feud field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        changed = re.sub(rb"^Intention(?:[2-9]|[1-9][0-9]+)=[^\r\n]*\r?\n", b"", changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Family Feud mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Family Feud section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def tooltip(before):
    replacements = {
        728248: (b"Open~it~to~receive~a~Level~97~Super~necklace~and~an~ExpBall(E).",
                 b"Open~for~a~level~97~Super~necklace~or~Taoist~bag.~The~quest~EXP~Ball(E)~is~awarded~separately."),
        710567: (b"Give~it~to~Guide~Zhang~on~Bird~Island~(641,660).",
                 b"Take~this~earned~token~to~Guide~Zhang~at~669,780~within~90~seconds.~Felix~at~686,547~can~renew~an~expired~disguise.")}
    lines = before.splitlines(keepends=True)
    seen = set()
    for index,line in enumerate(lines):
        parts = line.split(b"@@")
        if not parts[0].isdigit() or int(parts[0]) not in replacements:
            continue
        identity = int(parts[0])
        old,new = replacements[identity]
        if identity in seen or parts[53] not in (old,new):
            raise ValueError("Family Feud tooltip preimage changed")
        seen.add(identity)
        parts[53] = new
        lines[index] = b"@@".join(parts)
    if seen != set(replacements):
        raise ValueError("Missing Family Feud tooltip")
    return b"".join(lines)


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "hero-wings-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Family Feud rewrite is not idempotent")
    manifest = dict(purpose="private-composed-family-feud-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the nine Family Feud missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "family-feud-journal"
    files = [(destination / "originals/ini/Questinfo.ini", installed),
             (destination / "staged/ini/Questinfo.ini", after),
             (destination / "manifest.json", (json.dumps(manifest, indent=2)+"\n").encode())]
    tip_inputs = [("ini/itemtype.txt","52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb","212404aefe8f72515f60b12d380621560f32fe77ef85028ac2d832ea4d2ef07d",False),
                  ("ini/itemtype.dat","ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753","3e2da1a4e3d0f9bcb3c0f8fe482284e29cc1b03308b07b4632fb8bb269e7bc27",True)]
    tip_manifest = []
    for relative, expected, source_hash, encrypted in tip_inputs:
        checked(ROOT / "6711" / relative, expected)
        original = checked(WORK / "hero-wings-journal/staged" / relative, source_hash)
        plain = transform(original) if encrypted else original
        if encrypted and transform(plain, True) != original:
            raise ValueError("Item catalog transform failed round trip")
        edited = tooltip(plain)
        if tooltip(edited) != edited:
            raise ValueError("Tooltip rewrite is not idempotent")
        output = transform(edited, True) if encrypted else edited
        files.append((destination / "staged" / relative, output))
        tip_manifest.append(dict(relative=relative,before=expected,source=source_hash,after=sha(output)))
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
    for relative, expected, source_hash, _ in tip_inputs:
        checked(ROOT / "6711" / relative, expected)
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    prepare()
