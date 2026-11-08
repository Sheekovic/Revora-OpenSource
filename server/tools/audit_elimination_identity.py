"""Read-only identity check: similarly named quests do not prove achievement linkage."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from ClientPatches.prepare_shop_buy_hotfix import transform
from audit_warrior_epic import item_rows

QUEST_HASH = "f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14"
ACHIEVEMENT_HASH = "df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc"


def audit(client, runtime):
    quest = (client / "ini/Questinfo.ini").read_bytes()
    achievement = (client / "ini/Achievement.dat").read_bytes()
    if hashlib.sha256(quest).hexdigest() != QUEST_HASH or hashlib.sha256(achievement).hexdigest() != ACHIEVEMENT_HASH:
        raise ValueError("Reviewed client input changed; re-establish the identity evidence")
    plain = transform(achievement)
    if transform(plain, True) != achievement:
        raise ValueError("Achievement codec round trip failed")
    matches = [line.split() for line in plain.splitlines() if len(line.split()) > 1 and line.split()[1] == b"11010"]
    if len(matches) != 1 or matches[0][0] != b"60210" or matches[0][4] != b"10" or matches[0][5] != b"Achievement_PassElimTraitTask":
        raise ValueError("Elimination achievement identity differs")
    candidates = []
    for section in re.split(rb"(?m)^\[\d+\]\s*\r?\n", quest):
        lines = [line.strip().split(b"=", 1) for line in section.splitlines() if b"=" in line]
        fields = dict(lines)
        if fields.get(b"MissionId") not in (b"993", b"1962"):
            continue
        for key in (b"MissionId", b"Name", b"BeginNpcId", b"Lv_min"):
            if sum(name == key for name, value in lines) != 1:
                raise ValueError("Ambiguous quest identity field")
        npc = fields[b"BeginNpcId"].split(b",")
        candidates.append(dict(mission=int(fields[b"MissionId"]), npc=int(npc[0]), map=int(npc[1]),
                               x=int(npc[2]), y=int(npc[3]), minimum_level=int(fields[b"Lv_min"])))
    candidates.sort(key=lambda row: row["mission"])
    if candidates != [dict(mission=993, npc=3713, map=1002, x=436, y=449, minimum_level=40),
                      dict(mission=1962, npc=9043, map=1002, x=443, y=363, minimum_level=70)]:
        raise ValueError("Candidate quest identities differ")
    native=(client / "ini/itemtype.dat").read_bytes()
    server=(runtime / "database/itemtype-new.txt").read_bytes()
    if hashlib.sha256(native).hexdigest() != "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a" or hashlib.sha256(server).hexdigest() != "1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef":
        raise ValueError("Reviewed item catalog changed")
    names={711041:b"FirePill",711042:b"BeautyHerb",723019:b"FancySpice"}
    nr=item_rows(native,True,set(names));sr=item_rows(server,False,set(names))
    for identity,name in names.items():
        if nr[identity][1] != name or sr[identity][1] != name or nr[identity][53] != sr[identity][53]:
            raise ValueError("Expansion item identity or instructions differ")
    if b"12" not in nr[711042][53] or b"10" not in nr[711041][53] or b"(311,256)" not in nr[723019][53]:
        raise ValueError("Expansion requirements differ")
    return dict(quest_sha256=QUEST_HASH, achievement_sha256=ACHIEVEMENT_HASH,
                achievement=11010, achievement_score=10, similarly_named_candidates=candidates,
                candidate_achievement_link_verified=False, expansion_items=sorted(names), enables_rewards=False)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--runtime", type=Path, default=Path(__file__).resolve().parents[1]/"bin/Debug")
    args=parser.parse_args()
    print(json.dumps(audit(args.client,args.runtime), indent=2))
