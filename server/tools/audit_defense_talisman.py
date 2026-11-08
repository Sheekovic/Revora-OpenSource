"""Read-only, hash-gated extraction of the 6711 Defense Talisman contract."""
import argparse
import json
import re
from pathlib import Path
from audit_azure_pill import QUEST_SHA256
from audit_family_feud import checked, ITEM_SHA

ITEMS = {
    202008: "StarTower", 721777: "TowerSplinter", 721778: "FreshFish",
    721779: "FishingNet", 721780: "IronPoise", 721781: "Shawn`sSpear",
    721782: "BudHandkerchief", 721784: "GlitterSword", 721785: "StarTower",
    721786: "WeirdInvocation", 721788: "AnnattoBlade", 721789: "WaspSpear",
    721790: "InfernalAxe", 721791: "FireClinker", 721792: "EarthClinker",
    721793: "MetalClinker", 721794: "WoodClinker",
}


def analyze(quest, catalog):
    stages = []
    for block in re.split(r"(?m)^\[", quest)[1:]:
        row = dict(line.split("=", 1) for line in block.splitlines()[1:] if "=" in line)
        if not row.get("MissionId", "").isdigit() or not 520 <= int(row["MissionId"]) <= 528:
            continue
        mid = int(row["MissionId"])
        expected = ("4", "100", "120", "0", str(0 if mid == 520 else mid - 1))
        if tuple(row[k] for k in ("TypeId", "Lv_min", "Lv_max", "Profession", "Prequest")) != expected:
            raise ValueError("Changed Defense Talisman eligibility/prerequisite")
        if "[item 202008," not in row["Prize"]:
            raise ValueError("Changed final reward")
        stages.append(dict(mission=mid, predecessor=int(row["Prequest"]),
                           begin=row["BeginNpcId"], finish=row["FinishNpcId"],
                           materials=[list(map(int, pair)) for pair in re.findall(
                               r"\[chk_item (\d+),(\d+)\]", " ".join(row.values()))]))
    if [r["mission"] for r in stages] != list(range(520, 529)):
        raise ValueError("Incomplete or duplicate Defense Talisman chain")
    items = {}
    for line in catalog.splitlines():
        fields = line.split("@@")
        if not fields[0].isdigit() or int(fields[0]) not in ITEMS:
            continue
        item = int(fields[0])
        if item in items or fields[1] != ITEMS[item]:
            raise ValueError("Changed item identity")
        items[item] = dict(name=fields[1], required_class=int(fields[2]), level=int(fields[4]))
    if set(items) != set(ITEMS):
        raise ValueError("Missing quest item")
    return dict(source_sha256=dict(quest=QUEST_SHA256, items=ITEM_SHA), stages=stages,
                items=items, minimum_level=100, client_maximum=120,
                scene_maps=[1782, 1783, 1784, 1785, 1786, 1787],
                weapon_clinker_pairs=[[721784, 721793], [721788, 721794],
                                      [721789, 721792], [721790, 721791]],
                coordinate_conflicts=[
                    dict(subject="FishingNet use", item_tooltip=[170,190], pc_guide=[310,380]),
                    dict(subject="YeSheng", item_tooltip=[192,212], journal=[324,400]),
                    dict(subject="BloodyShawn", journal_prose=[561,797], journal_link=[400,650]),
                    dict(subject="TimeDoor", journal_522=[340,404], journal_523_begin=[220,220])],
                original_net_price=None, original_drop_odds=None,
                runtime_terrain_verified=False, implemented=False, native_acceptance=False)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = analyze(checked(args.client / "ini/Questinfo.ini", QUEST_SHA256).decode("cp1252"),
                     checked(args.client / "ini/itemtype.txt", ITEM_SHA).decode("latin1"))
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("Verified nine missions, seventeen item identities and four coordinate conflicts; runtime unverified.")
