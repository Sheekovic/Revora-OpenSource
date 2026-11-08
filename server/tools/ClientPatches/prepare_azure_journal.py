"""Stage Azure Pill navigation over the reviewed quest bundle; never install it."""
import json
import re
from prepare_warrior_journal import ROOT, WORK, INSTALLED, DX9, checked, sha

SOURCE = "160a4d649f5120216cba33f813c5612f16c291715a296bb200f4795b4e2387de"
ALIASES = {8796: 900071727, 4482: 900071728, 4483: 900071729, 4484: 900071730}
MISSIONS = set(range(1727, 1733))


def rewrite(before):
    edits = []
    seen = set()
    for match in re.finditer(rb"^\[\d+\]\r?$[\s\S]*?(?=^\[|\Z)", before, re.M):
        original = match.group()
        ids = re.findall(rb"^MissionId=(\d+)\r?$", original, re.M)
        selected = [int(i) for i in ids if int(i) in MISSIONS]
        if not selected:
            continue
        if len(ids) != 1 or selected[0] in seen:
            raise ValueError("Ambiguous Azure mission")
        mission = selected[0]
        seen.add(mission)
        changed = original
        for old, new in ALIASES.items():
            changed = re.sub(rb"(?<=[,=])" + str(old).encode() + rb"(?=,)", str(new).encode(), changed)
        changed = changed.replace(b",TaskmasterChang,JobCenter", b",TaskmasterChang,TwinCity")
        values = {"Lv_max": "255", "Prize":
                  {1727: "100 Silver. ", 1728: "100 Silver. ", 1729: "100 Silver. ",
                   1730: "100 Silver. ", 1731: "1500 Silver and one-third EXP Ball. ", 1732: ""}[mission]
                  + "Final reward: bound Unique Ring Pack and one bound P7 soul. Once per rebirth."}
        if mission == 1729:
            values["Content"] = ("Hunt level-38 Fire Rats in Phoenix Castle for one eye and three cuts of meat. "
                                 "Bian Qing in Twin City (425,331) supplies two herb packs. Take all six materials to Mu Mu.")
        for key, value in values.items():
            pattern = rb"^" + key.encode() + rb"=([^\r\n]*)(\r?)(?=\n|\Z)"
            if len(re.findall(pattern, changed, re.M)) != 1:
                raise ValueError("Missing or duplicate Azure field: " + key)
            changed = re.sub(pattern, lambda m: key.encode()+b"="+value.encode("ascii")+m[2], changed, flags=re.M)
        edits.append((match.start(), match.end(), original, changed))
    if seen != MISSIONS:
        raise ValueError("Missing Azure mission")
    after = before
    for start, end, _, changed in reversed(edits):
        after = after[:start] + changed + after[end:]
    restored = after
    for _, _, original, changed in edits:
        if restored.count(changed) != 1:
            raise ValueError("Ambiguous rewritten Azure section")
        restored = restored.replace(changed, original, 1)
    if restored != before:
        raise ValueError("Unrelated journal bytes changed")
    return after


def prepare():
    installed_path = ROOT / "6711/ini/Questinfo.ini"
    executable = ROOT / "6711/Env_DX9/Conquer.exe"
    source = WORK / "warrior-max-reward-journal/staged/ini/Questinfo.ini"
    installed = checked(installed_path, INSTALLED)
    checked(executable, DX9)
    before = checked(source, SOURCE)
    after = rewrite(before)
    if rewrite(after) != after:
        raise ValueError("Azure rewrite is not idempotent")
    manifest = dict(purpose="private-composed-azure-pill-journal-review", relative="ini/Questinfo.ini",
                    before=INSTALLED, source=SOURCE, after=sha(after), approved_dx9=DX9, architecture="x86",
                    missions=sorted(MISSIONS), aliases=ALIASES, installed=False, published=False, signed=False,
                    native_acceptance="pending", preserved="All sections outside the six Azure missions are byte-identical to source.",
                    rollback="Matching server required. Future installation must verify and back up the live preimage transactionally.")
    destination = WORK / "azure-pill-journal"
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
