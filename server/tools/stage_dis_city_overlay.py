"""Stage reviewed Dis City quest text and item tips in a separate private directory."""
import argparse
import json
import os
from pathlib import Path
import re
import tempfile
from stage_summer_overlay import digest, transform, revise as revise_summer
from stage_labyrinth_overlay import revise as revise_labyrinth

ITEM_SHA256 = "d2a2014fb2acea3af4e15388eb2bec8ee32299a667478006306c94dc8037422a"
QUEST_SHA256 = "f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14"
COMBINED_ITEM_SHA256 = "ed2936c1f1a83aea39093a58f1b6f7f04fa277b665e982a13a4bf5f69bd3f190"
COMBINED_QUEST_SHA256 = "c3d232473ff879c057fa3a5725ac013e84118f3fa68aa747201b5e7d7218f5d8"
DESCRIPTIONS = {
    790001: "Earn one bound Dark Horn with your weekly victory over Pluto. Bring your earned horn to Solar Saint in Twin City (327,250) for 300 minutes of direct experience. Keep it until after rebirth if you have reached maximum level.",
    723088: "Pluto has a 20% chance to leave this sword with your weekly victory spoils. Bring your earned sword to Solar Saint in Twin City (327,250) for a permanent bound Phoenix, Elegant or Celestial Dress. These level-100 outfits occupy the armor slot.",
    723085: "Earn five bound Soul Stones from your Hell Gate demons. Collect any undelivered stones from Solar Saint, then submit all five to pass the gate.",
    3006981: "Use inside your Dis City trial to return to Solar Saint and learn your next objective. Resume an unfinished journey with Solar Saint in Twin City (327,250). Your chosen Standard, Veteran or Nightmare trial remains fixed until completion.",
    3006982: "Earn one key per weekly Dis City victory. Ask Solar Saint in Twin City (327,250) for passage, then use it beside your chest. Regular prizes: 50% Super pack, 35% Sacred pack, 15% Yin Jade. Every fifth opening gives two Sacred packs and a Yin Jade.",
    3006983: "Earn four bound scraps from the Syren in your Battle Formation. Right click one of your four issued scraps to unite them into a Pluto Token.",
    3006984: "Use the token issued for your own Battle Formation to summon Ultimate Pluto. Defeat him and return to Solar Saint for your victory reward.",
    3008057: "Open for one Super Refinery material, chosen equally from 32 kinds. A bound pack gives a bound material. Apply it for a seven-day refinement, extended by VIP benefits.",
    3008058: "Open for one Sacred Refinery material, chosen equally from 32 kinds. A bound pack gives a bound material. Apply it for a seven-day refinement, extended by VIP benefits.",
    3008059: "Use below 8,000 Chi for 300 points. If you also carry a usable Yang Jade, both are consumed for 1,200 points instead. Earn Yin Jade by completing Revora's four Dis City trials.",
    3008060: "Use below 8,000 Chi for 300 points. If you also carry a usable Yin Jade, both are consumed for 1,200 points instead. The later Labyrinth earning route is not yet open in Revora.",
}
OBJECTIVES = {
    6472: "Earn five Soul Stones from your Hell Gate demons. Collect all five, then use your Mirror of Hell to return to Solar Saint and submit them.",
    6473: "Defeat your Hell Hall demons: 400 at entry, 600 for Archers or 300 for Taoists. Your entry class fixes the quota. Use your Mirror of Hell to check progress and return to Solar Saint.",
    6474: "Defeat the appointed Hell Wraith, then seal its crystal. Repeat in the nine-crystal order shown by your Mirror of Hell. Return to Solar Saint when all nine are sealed.",
    6475: "Earn four Syren scraps and unite them. Use your issued Pluto Token to summon Ultimate Pluto, defeat him, then use your Mirror of Hell to return to Solar Saint.",
}
CONTENT = ("Solar Saint awaits in Twin City (327,250). Heroes of level 110 or above may begin on Monday, Wednesday or Friday, Pacific time. "
           "Choose Standard, Veteran or Nightmare. Veteran demons have twice the HP and 25% stronger attacks; Nightmare has three times the HP and 50% stronger attacks. "
           "Rewards are equal. Your trial stays fixed on resume. All difficulties share one weekly victory. Unfinished journeys resume any day. "
           "Each victory grants a treasury key and a Dark Horn. Exchange your horn with Solar Saint for 300 minutes of experience, or ask for passage to the hoard. "
           "The ten fastest Pluto defeats in each difficulty and combat edition each Pacific day earn separate rank prizes from the following day: 3 Sacred Refinery Packs for first, 2 for second, and 1 for third through tenth. Unclaimed prizes wait with Solar Saint.")


def revise_items(plain):
    result, changes, seen = [], [], set()
    for row in plain.splitlines(keepends=True):
        fields = row.split(b"@@")
        try:
            item = int(fields[0])
        except ValueError:
            result.append(row)
            continue
        if item not in DESCRIPTIONS:
            result.append(row)
            continue
        if item in seen or len(fields) != 66:
            raise ValueError("Unexpected or repeated Dis City item")
        seen.add(item)
        fields[53] = DESCRIPTIONS[item].replace(" ", "~").encode("ascii")
        output = b"@@".join(fields)
        result.append(output)
        changes.append({"item": item, "fields": [53], "before_sha256": digest(row), "after_sha256": digest(output)})
    if seen != set(DESCRIPTIONS):
        raise ValueError("Dis City item catalog incomplete")
    return b"".join(result), changes


def quest_fields(mission):
    prize = ("One bound Super Refinery Pack." if mission <= 6473 else
             "One bound Sacred Refinery Pack." if mission == 6474 else
             "One bound Sacred Refinery Pack, one bound Yin Jade, one bound Pluto's Key, one bound Dark Horn, and a 20% chance of one bound Star Sword. Newly admitted expeditions also give one random bound P7 soul.")
    return {
        "BeginNpcId": "3215,1002,327,250,SolarSaint,TwinCity",
        "FinishNpcId": "",
        "ActivityBeginTime": "0", "ActivityEndTime": "0",
        "Prize": prize,
        "Intention1": OBJECTIVES[mission] + "[chk_quest " + str(mission) + ",1,1,1]",
        "Content": CONTENT,
    }


def revise_quests(plain):
    # Work on bytes so every unrelated record, encoding and line ending survives.
    pieces = re.split(rb"(?m)(?=^\[[0-9]+\]\r?$)", plain)
    seen, changes, result = set(), [], []
    for piece in pieces:
        identities = re.findall(rb"(?m)^MissionId=([0-9]+)\r?$", piece)
        targets = [int(i) for i in identities if int(i) in OBJECTIVES]
        if not targets:
            result.append(piece)
            continue
        if len(identities) != 1 or len(targets) != 1 or targets[0] in seen:
            raise ValueError("Ambiguous or repeated Dis City mission")
        mission = targets[0]
        seen.add(mission)
        output, fields = piece, quest_fields(mission)
        for key, value in fields.items():
            pattern = rb"(?m)^" + key.encode("ascii") + rb"=([^\r\n]*)(\r?)(?=\n|$)"
            replacement = key.encode("ascii") + b"=" + value.encode("ascii")
            output, count = re.subn(pattern, lambda match: replacement + match[2], output)
            if count != 1:
                raise ValueError("Missing or repeated mission field: " + key)
        result.append(output)
        changes.append({"mission": mission, "fields": list(fields), "before_sha256": digest(piece), "after_sha256": digest(output)})
    if seen != set(OBJECTIVES):
        raise ValueError("Dis City mission catalog incomplete")
    return b"".join(result), changes


def stage(item_source, quest_source, destination, profile="baseline"):
    if profile not in ("baseline", "combined"):
        raise ValueError("Unknown reviewed source profile")
    item_hash, quest_hash = ((ITEM_SHA256, QUEST_SHA256) if profile == "baseline" else
                             (COMBINED_ITEM_SHA256, COMBINED_QUEST_SHA256))
    item_source, quest_source = item_source.resolve(strict=True), quest_source.resolve(strict=True)
    destination = destination.resolve()
    for source, name in [(item_source, "itemtype.dat"), (quest_source, "questinfo.ini")]:
        if source.name.lower() != name or source.parent.name.lower() != "ini":
            raise ValueError("Unexpected source profile path")
        root = source.parent.parent
        if destination == root or root in destination.parents or destination in root.parents:
            raise ValueError("Staging destination must not overlap either source tree")
    if destination.exists():
        raise ValueError("Use a new private staging directory")
    item_original, quest_original = item_source.read_bytes(), quest_source.read_bytes()
    if digest(item_original) != item_hash or digest(quest_original) != quest_hash:
        raise ValueError("Source differs from the reviewed profile")
    item_plain = transform(item_original)
    inherited_changes = []
    if profile == "combined":
        item_plain, summer_changes = revise_summer(item_plain)
        item_plain, labyrinth_changes = revise_labyrinth(item_plain)
        inherited_changes = summer_changes + labyrinth_changes
    item_plain, item_changes = revise_items(item_plain)
    item_changes = inherited_changes + item_changes
    item_output = transform(item_plain, True)
    if transform(item_output) != item_plain:
        raise ValueError("Item overlay round trip failed")
    quest_output, quest_changes = revise_quests(quest_original)
    manifest = {
        "purpose": "Unsigned private Dis City difficulty, reward and ranking content stage",
        "source_profile": profile, "signed": False, "installed": False, "published": False,
        "client_acceptance": "pending", "rollback": "No installed client changed; omit this stage to retain originals.",
        "files": [
            {"destination": "ini/itemtype.dat", "source": str(item_source), "source_sha256": item_hash, "output_sha256": digest(item_output), "changes": item_changes},
            {"destination": "ini/Questinfo.ini", "source": str(quest_source), "source_sha256": quest_hash, "output_sha256": digest(quest_output), "changes": quest_changes},
        ],
    }
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="dis-city-stage-", dir=destination.parent) as temporary:
        work = Path(temporary)
        (work / "ini").mkdir()
        (work / "ini/itemtype.dat").write_bytes(item_output)
        (work / "ini/Questinfo.ini").write_bytes(quest_output)
        (work / "review.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        if digest(item_source.read_bytes()) != item_hash or digest(quest_source.read_bytes()) != quest_hash:
            raise ValueError("A source changed during staging")
        if destination.exists():
            raise ValueError("Staging destination appeared during preparation")
        os.rename(work, destination)
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("items", type=Path)
    parser.add_argument("quests", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--profile", choices=("baseline", "combined"), default="baseline")
    args = parser.parse_args()
    result = stage(args.items, args.quests, args.destination, args.profile)
    for entry in result["files"]:
        print(entry["destination"] + ": " + entry["output_sha256"])
